using System.Text;
using LightMiniGame.Card;
using UnityEngine;

namespace LightMiniGame.Card
{
    /// <summary>
    /// 牌库测试工具：运行时手动给某个角色的 CharacterCardLibrary 增删卡，
    /// 并在 Console 打印「总牌数」与「cards 列表」。
    ///
    /// 用法：
    ///   1. 把本脚本挂到场景任意 GameObject（建议挂在 GlobalCardLibrary 或 GameManager 上）。
    ///   2. Inspector 里指定 targetCharacter（留空则自动取第一个已注册角色）；
    ///      在 cardPresets 列表里拖入若干 CardData 作为「可添加的卡」，运行时用 ◀ ▶ 切换。
    ///   3. 进入 Play 模式，按 ~ 键（toggleKey）开关面板；或在 Inspector 里右键本组件用 ContextMenu 触发。
    ///
    /// 注意：本工具只用 UnityEngine 自带 API，可安全打进正式包（不含 UnityEditor 依赖）。
    /// </summary>
    public class CardLibraryTestTool : MonoBehaviour
    {
        [Header("测试目标")]
        [Tooltip("要操作的角色；留空则自动取 GlobalCardLibrary 中第一个已注册角色")]
        public CharacterData targetCharacter;

        [Header("可添加的卡（运行时用 ◀ ▶ 选择）")]
        public CardData[] cardPresets = new CardData[0];

        [Header("GUI")]
        public bool showPanel = false;   // 默认不显示，按 ~ 键（toggleKey）开关
        public KeyCode toggleKey = KeyCode.BackQuote;   // 数字 1 左边那个 ~ 键

        private CharacterCardLibrary _lib;
        private int _presetIndex;
        private string _removeId = "";
        private Vector2 _scroll;

        private void Start() => ResolveLibrary();

        private void ResolveLibrary()
        {
            var g = GlobalCardLibrary.Instance ?? GlobalCardLibrary.EnsureInstance();
            if (targetCharacter != null)
                _lib = g.GetLibrary(targetCharacter);
            else if (g.AllLibraries.Count > 0)
                _lib = g.AllLibraries[0];
            else
                _lib = null;

            if (_lib == null)
                Debug.LogWarning("[CardLibraryTestTool] 未找到目标牌库：请先注册角色（GlobalCardLibrary.BuildFromStartingLibrary 或 RegisterCharacter）。");
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) showPanel = !showPanel;
        }

        private void OnGUI()
        {
            if (!showPanel) return;

            GUILayout.BeginArea(new Rect(10, 10, 380, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("牌库测试工具  (~ 开关)", GUI.skin.box);

            string libName = _lib != null ? (_lib.owner != null ? _lib.owner.Label : "?") : "（无）";
            int count = _lib != null ? _lib.Count : 0;
            GUILayout.Label($"目标角色: {libName}    总牌数: {count}");

            // —— 选择要添加的卡 ——
            if (cardPresets != null && cardPresets.Length > 0)
            {
                _presetIndex = Mathf.Clamp(_presetIndex, 0, cardPresets.Length - 1);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◀", GUILayout.Width(28))) _presetIndex = (_presetIndex - 1 + cardPresets.Length) % cardPresets.Length;
                string nm = cardPresets[_presetIndex] != null ? cardPresets[_presetIndex].cardName : "(空)";
                GUILayout.Label($"[{_presetIndex}] {nm}", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("▶", GUILayout.Width(28))) _presetIndex = (_presetIndex + 1) % cardPresets.Length;
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("➕ 添加选中卡")) AddSelected();

            // —— 按 ID 删除 ——
            GUILayout.BeginHorizontal();
            _removeId = GUILayout.TextField(_removeId, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("🗑 按ID删除", GUILayout.Width(90))) RemoveById(_removeId);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("删除最后一张")) RemoveLast();
            if (GUILayout.Button("清空牌库")) ClearLib();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("🖨 打印牌库到 Console")) PrintLibrary();

            GUILayout.Space(6);
            GUILayout.Label("当前牌库预览：");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(220));
            if (_lib != null)
            {
                for (int i = 0; i < _lib.cards.Count; i++)
                {
                    var c = _lib.cards[i];
                    string t = c.template != null ? CardData.GetCardTypeName(c.template.cardType) : "孤儿";
                    GUILayout.Label($"[{i}] {c.EffectiveName} | {t} | 费{c.EffectiveCost} | {ShortId(c.instanceId)}");
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        // ===== 操作（也可在 Inspector 右键本组件 → ContextMenu 调用）=====

        [ContextMenu("添加选中卡")]
        public void AddSelected()
        {
            ResolveLibrary();
            if (_lib == null) { Debug.LogWarning("[CardLibraryTestTool] 无目标牌库，无法添加"); return; }
            CardData tpl = (cardPresets != null && cardPresets.Length > 0)
                ? cardPresets[Mathf.Clamp(_presetIndex, 0, cardPresets.Length - 1)]
                : null;
            if (tpl == null) { Debug.LogWarning("[CardLibraryTestTool] 未选择要添加的卡（请在 cardPresets 里拖一张 CardData）"); return; }
            _lib.Add(tpl);
            Debug.Log($"[CardLibraryTestTool] 已添加「{tpl.cardName}」，当前总牌数 = {_lib.Count}");
        }

        [ContextMenu("删除最后一张")]
        public void RemoveLast()
        {
            ResolveLibrary();
            if (_lib == null || _lib.cards.Count == 0) { Debug.LogWarning("[CardLibraryTestTool] 牌库为空，无可删除"); return; }
            var last = _lib.cards[_lib.cards.Count - 1];
            _lib.Remove(last.instanceId);
            Debug.Log($"[CardLibraryTestTool] 已删除最后一张「{last.EffectiveName}」，当前总牌数 = {_lib.Count}");
        }

        public void RemoveById(string id)
        {
            ResolveLibrary();
            if (_lib == null || string.IsNullOrEmpty(id)) return;
            _lib.Remove(id);
            Debug.Log($"[CardLibraryTestTool] 已按 ID 删除 {ShortId(id)}，当前总牌数 = {_lib.Count}");
        }

        [ContextMenu("清空牌库")]
        public void ClearLib()
        {
            ResolveLibrary();
            if (_lib == null) return;
            _lib.Clear();
            Debug.Log("[CardLibraryTestTool] 已清空牌库");
        }

        [ContextMenu("打印牌库到 Console")]
        public void PrintLibrary()
        {
            ResolveLibrary();
            if (_lib == null) { Debug.LogWarning("[CardLibraryTestTool] 无目标牌库"); return; }

            var sb = new StringBuilder();
            sb.AppendLine("════════ 牌库内容 ════════");
            sb.AppendLine($"角色 : {(_lib.owner != null ? _lib.owner.Label : "(无)")}");
            sb.AppendLine($"总牌数: {_lib.Count}");
            sb.AppendLine("────────────────────────────");
            if (_lib.Count == 0)
                sb.AppendLine("（空）");
            for (int i = 0; i < _lib.cards.Count; i++)
            {
                var c = _lib.cards[i];
                string t = c.template != null ? CardData.GetCardTypeName(c.template.cardType) : "孤儿(无模板)";
                sb.AppendLine($"[{i}] id={ShortId(c.instanceId)} | 名={c.EffectiveName} | 类型={t} | 费={c.EffectiveCost} | 模板={(c.template != null ? c.template.name : "∅")}");
            }
            sb.AppendLine("═══════════════════════════");
            Debug.Log(sb.ToString());
        }

        private static string ShortId(string id)
            => string.IsNullOrEmpty(id) ? "?" : (id.Length > 8 ? id.Substring(0, 8) : id);
    }
}
