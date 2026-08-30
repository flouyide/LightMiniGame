using LightMiniGame.Relic;
using LightMiniGame.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 敌人能力：序章弹窗（Prologue Popup）。
    ///
    /// 用途：序章敌人用来逐回合向玩家介绍游戏机制的“教学弹窗”。
    /// 每个玩家回合开始时弹出一个配置好的弹窗，弹窗带关闭按钮，
    /// 在玩家点关闭之前锁定玩家输入（无法出牌 / 切换角色 / 结束回合），即“只有关闭弹窗才能继续游戏”。
    ///
    /// 在 RelicData 资产（Inspector）中配置（把本脚本拖到 Effect Script 字段，
    /// effectScriptName 会自动填为 "LightMiniGame.RelicEffects.ProloguePopupEffect"），
    /// 然后把该 RelicData 加到序章敌人的 EnemyConfig → abilities 条目里。
    ///
    /// 可配置项（选中 RelicData → Inspector）：
    ///   Object 参数[0] = 弹窗预制体（GameObject）。大小、样式、布局完全由你在预制体里调，
    ///                     只需保证预制体里有一个 Button（建议命名为 CloseButton）和一个文本组件
    ///                     （建议命名为 Content / Text / Message，使用 TextMeshProUGUI 或 UGUI Text）。
    ///   String 参数[N] = 第 N+1 回合的弹窗文案（索引从 0 开始：effectStringParams[0] = 第1回合文案）。
    ///   Effect Params[0] = 一共要出现弹窗的回合数（整数）。留 0 时自动取 String 参数的条数。
    ///
    /// 注意：本能力作为“敌人能力”挂载（EnemyConfig.abilities），不是玩家遗物。
    /// </summary>
    public class ProloguePopupEffect : RelicEffectBase
    {
        private const string InputLockKey = "ProloguePopup";

        private BattleManager _battle;
        private RelicData _relic;
        private GameObject _popupPrefab;
        private GameObject _popupInstance;

        private int _totalTurns;
        private int _nextTurnIndex;   // 下一个要显示的回合文案索引（0 = 第1回合）
        private bool _popupOpen;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx?.battle;
            _relic = ctx?.relic;
            if (_battle == null || _relic == null) return;

            _popupPrefab = GetEffectObjectParam<GameObject>(_relic, 0, null);
            if (_popupPrefab == null)
            {
                Debug.LogError("[ProloguePopup] 未在 RelicData 的 Object 参数[0] 配置弹窗预制体，序章弹窗将不显示。");
                return;
            }

            int paramTurns = Mathf.RoundToInt(GetEffectParam(_relic, 0, 0f));
            int textCount = _relic.effectStringParams != null ? _relic.effectStringParams.Count : 0;
            _totalTurns = paramTurns > 0 ? paramTurns : textCount;
            if (_totalTurns <= 0)
            {
                Debug.LogWarning("[ProloguePopup] 未配置弹窗回合数（Effect Params[0]）且无弹窗文案，序章弹窗将不显示。");
                return;
            }

            _nextTurnIndex = 0;
            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;

            // 首回合的 OnPlayerTurnStarted 不会派发（首回合在 StartBattle 内预启动），
            // 因此在这里补一次首回合弹窗。
            ShowNextPopup();
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach();

        private void OnPlayerTurnStarted() => ShowNextPopup();

        private void ShowNextPopup()
        {
            if (_battle == null || _popupOpen) return;
            if (_nextTurnIndex >= _totalTurns) return;

            string text = (_relic.effectStringParams != null && _nextTurnIndex < _relic.effectStringParams.Count)
                ? _relic.effectStringParams[_nextTurnIndex]
                : $"序章提示（第 {_nextTurnIndex + 1}/{_totalTurns} 回合）";

            var parent = GetPopupParent();
            _popupInstance = parent != null
                ? Object.Instantiate(_popupPrefab, parent, false)
                : Object.Instantiate(_popupPrefab);

            var content = FindContentText(_popupInstance);
            if (content != null) content.text = text;
            else Debug.LogWarning("[ProloguePopup] 弹窗预制体未找到文本组件，无法设置文案。");

            var closeBtn = FindCloseButton(_popupInstance);
            if (closeBtn == null)
            {
                // 没有关闭按钮会导致无法继续游戏，直接销毁以免卡死，并给出明确提示让设计修正预制体。
                Debug.LogError("[ProloguePopup] 弹窗预制体未找到关闭按钮（Button），已跳过本次弹窗以免卡死。请放置一个 Button（建议命名 CloseButton）。");
                Object.Destroy(_popupInstance);
                _popupInstance = null;
                return;
            }

            closeBtn.onClick.AddListener(ClosePopup);

            _popupOpen = true;
            _battle.SetPlayerInputLocked(InputLockKey, true);   // 锁定输入，直到关闭
            _nextTurnIndex++;

            Debug.Log($"[ProloguePopup] 显示序章弹窗（第 {_nextTurnIndex}/{_totalTurns} 回合）");
        }

        private void ClosePopup()
        {
            if (_battle != null)
                _battle.SetPlayerInputLocked(InputLockKey, false);   // 解锁输入，游戏继续

            if (_popupInstance != null)
            {
                Object.Destroy(_popupInstance);
                _popupInstance = null;
            }
            _popupOpen = false;
        }

        private void Detach()
        {
            if (_battle != null)
            {
                _battle.OnPlayerTurnStarted -= OnPlayerTurnStarted;
                if (_popupOpen) _battle.SetPlayerInputLocked(InputLockKey, false);
            }
            if (_popupInstance != null)
            {
                Object.Destroy(_popupInstance);
                _popupInstance = null;
            }
            _popupOpen = false;
            _battle = null;
            _relic = null;
            _popupPrefab = null;
            _nextTurnIndex = 0;
            _totalTurns = 0;
        }

        // ===== 辅助：定位 UI 父节点 / 预制体内的按钮与文本 =====

        private static Transform GetPopupParent()
        {
            var go = GameObject.Find("BattleCanvas");
            if (go != null)
            {
                var canvas = go.GetComponent<Canvas>() ?? go.GetComponentInChildren<Canvas>();
                if (canvas != null) return canvas.transform;
                return go.transform;
            }
            var anyCanvas = Object.FindObjectOfType<Canvas>();
            return anyCanvas != null ? anyCanvas.transform : null;
        }

        private static Button FindCloseButton(GameObject root)
        {
            foreach (var name in new[] { "CloseButton", "CloseBtn", "Close", "OK", "Confirm", "ConfirmButton" })
            {
                var child = root.transform.Find(name);
                if (child != null)
                {
                    var btn = child.GetComponent<Button>();
                    if (btn != null) return btn;
                }
            }
            return root.GetComponentInChildren<Button>();
        }

        private static TMP_Text FindContentText(GameObject root)
        {
            foreach (var name in new[] { "Content", "Text", "Message", "Desc", "Description", "Body" })
            {
                var child = root.transform.Find(name);
                if (child != null)
                {
                    var tmp = child.GetComponent<TMP_Text>();
                    if (tmp != null) return tmp;
                }
            }
            return root.GetComponentInChildren<TMP_Text>();
        }
    }
}