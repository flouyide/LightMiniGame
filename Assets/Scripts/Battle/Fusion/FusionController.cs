using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 融合（Fusion）核心控制器 —— 由 BattleManager.BeginBattle() 自动创建并挂载。
///
/// UI 流程（数字加粗变紫版，无高亮方块）：
///  1) 常驻“融合”按钮（左上角），每回合可用一次。
///  2) 点击进入融合状态：立即扣 4 理智 + 全屏暗色蒙版，
///     场上所有可融合【数字本身】加粗变紫（#800080）：
///       - 手牌/敌人意图卡描述与费用数字：原位富文本着色（紫、加粗）+ 透明命中层（精准点击）；
///       - 玩家能量/护甲/血量、敌人护甲/血量等战场数字：直接改对应 TMP 的颜色与字重；
///       - 货币：无原位文本，蒙层左下生成一个同等样式的紫字（承载点击）。
///  3) 依次点选两个紫色数字 → FusionManager 自动结算：Sum=A+B → R∈[0,Sum] → A=R、B=Sum-R，
///     写回卡牌/战场数据并刷新显示，本回合融合结束（每回合可用一次）。
///  4) 蒙层右下“退出”按钮取消融合（恢复颜色、销毁命中层）。
/// 全部 UI 由代码在运行时创建，不依赖场景手工摆放。
/// </summary>
public class FusionController : MonoBehaviour
{
    private const int SanityCost = 4;

    private BattleManager _battle;
    private bool _initialized;
    private List<FusableValue> _candidates = new();
    private readonly List<GameObject> _hitBlocks = new();     // 战场数值（非卡面）透明命中层
    private TextMeshProUGUI _goldNode;                         // 货币的紫色数字节点
    private int _goldCandidateIndex = -1;

    // === 运行时构建的 UI ===
    private GameObject _entryButtonGO;
    private Image _entryButtonImage;
    private GameObject _panelRoot;
    private TextMeshProUGUI _statusText;
    private string _lastFusionInfo = "";

    private bool PanelActive => _panelRoot != null;

    /// <summary>融合面板是否激活（供 BattleManager 融合期间拦截出牌等）。</summary>
    public bool IsPanelActive => PanelActive;

    /// <summary>融合是否激活（静态，供 HandCardLayout 等禁用手牌 hover 放大避免块与点击错位）。</summary>
    public static bool IsFusionActive;

    private void OnDestroy()
    {
        FusionManager.OnFusionResolved -= OnPairResolved;
        FusionManager.CustomApply = null;
        if (_entryButtonGO != null) Destroy(_entryButtonGO);
        if (_panelRoot != null) Destroy(_panelRoot);
    }

    /// <summary>由 BattleManager 调用以初始化（幂等）。</summary>
    public void Setup(BattleManager battle)
    {
        _battle = battle;
        if (_initialized) return;
        _initialized = true;

        FusionManager.OnFusionResolved += OnPairResolved;
        FusionManager.CustomApply = OnFusionCustomApply;

        BuildEntryButton();
    }

    // ========================================================================
    // 常驻入口按钮
    // ========================================================================

    private void BuildEntryButton()
    {
        var parent = FindParentCanvas();
        if (parent == null)
        {
            Debug.LogWarning("[FusionController] 未找到 Canvas，无法创建入口按钮");
            return;
        }

        _entryButtonGO = new GameObject("FusionEntryButton");
        _entryButtonGO.transform.SetParent(parent.transform, false);
        var rt = _entryButtonGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(56, 56);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(330f, -30f);

        _entryButtonImage = _entryButtonGO.AddComponent<Image>();
        var btn = _entryButtonGO.AddComponent<Button>();
        btn.onClick.AddListener(OnEntryClicked);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(_entryButtonGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = labelRT.anchorMax = new Vector2(0.5f, 0.5f);
        labelRT.sizeDelta = new Vector2(50, 50);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = "∮";
        label.fontSize = 28;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        UpdateEntryInteractable();
    }

    private Canvas FindParentCanvas()
    {
        var t = transform;
        while (t != null)
        {
            var c = t.GetComponent<Canvas>();
            if (c != null) return c;
            t = t.parent;
        }
        return FindObjectOfType<Canvas>();
    }

    private void OnEntryClicked()
    {
        if (_battle == null) return;

        // 已在融合状态：再次点击入口 = 退出（恢复原样）
        if (PanelActive)
        {
            ExitFusion();
            return;
        }
        if (!_battle.IsPlayerTurn || _battle.FusionUsedThisTurn) return;
        EnterFusion();
    }

    /// <summary>刷新入口按钮可用态（被 BattleManager.UpdateUI 调用）。</summary>
    public void UpdateEntryInteractable()
    {
        if (_entryButtonImage == null) return;
        bool canEnter = _battle != null && _battle.IsPlayerTurn && !_battle.FusionUsedThisTurn;
        _entryButtonImage.color = canEnter
            ? new Color(0.55f, 0.28f, 0.75f, 0.95f)
            : new Color(0.35f, 0.28f, 0.45f, 0.5f);
    }

    // ========================================================================
    // 进入融合
    // ========================================================================

    private void EnterFusion()
    {
        // 立即扣 4 理智（代价而非条件，不足也可进，clamp>=0）
        _battle.DeductSanityAsCost(SanityCost);

        IsFusionActive = true;
        FusionManager.ClearPairSelection();
        FusionManager.BeginFusion();
        _candidates = CollectCandidates();
        // 战场数字（能量/护甲/血量等）原位加粗变紫；低理智锁定的血量项不染
        _battle.ApplyFusionNumberTint(true);
        BuildPanel();
        UpdateEntryInteractable();
        _lastFusionInfo = "";
        // 高亮定位依赖 TMP 文本网格，延迟一帧构建避免错位/fallback 大块
        StartCoroutine(BuildHighlightsNextFrame());
    }

    private System.Collections.IEnumerator BuildHighlightsNextFrame()
    {
        yield return null;   // 等一帧：TMP mesh 重建（低理智切形态后文本已变）
        if (!IsFusionActive || _panelRoot == null) yield break;   // 期间已退出
        _battle.SnapHandToTarget();
        for (int attempt = 0; attempt < 8; attempt++)
        {
            yield return null;
            if (!IsFusionActive || _panelRoot == null) yield break;
            _battle.SnapHandToTarget();
            try
            {
                BuildCardHighlights();
                BuildHitBlocks();
                if (AnyHighlightReady()) break;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Fusion] 构建高亮异常：{e.Message}");
            }
        }
        UpdateStatus();
        Debug.Log($"[Fusion] 高亮构建完成：命中层={_hitBlocks.Count} 卡面命中={CardAnyHighlight()}");
    }

    private bool AnyHighlightReady() => _hitBlocks.Count > 0 || CardAnyHighlight();

    private bool CardAnyHighlight()
    {
        foreach (var c in _candidates)
            if (c.cardView != null && c.cardView.HasNumberHighlights()) return true;
        return false;
    }

    /// <summary>枚举当前要参与融合的数值。
    /// 玩家=能量/当前护甲/货币；敌人=护甲/出招意图卡牌数值；手牌=费用/攻击/护甲/增益/抽牌/回费。
    /// 血量类（玩家/敌人当前血量+上限）低理智时 locked=true（不可选、不染色）。</summary>
    private List<FusableValue> CollectCandidates()
    {
        var list = new List<FusableValue>();
        bool lowSanity = _battle.IsLowSanityForFusion;   // 低理智 → 血量类锁定

        // —— 玩家数值 ——
        list.Add(new FusableValue("player:energy", "能量", _battle.ActionPoints, false,
            v => _battle.SetActionPoints(v)));
        if (_battle.PlayerArmor > 0)
            list.Add(new FusableValue("player:armor", "当前护甲", _battle.PlayerArmor, false,
                v => _battle.SetPlayerArmor(v)));
        list.Add(new FusableValue("player:gold", "货币", _battle.PlayerGold, false,
            v => _battle.SetPlayerGold(v)));

        // 玩家血量 / 血量上限（始终可融合；低理智下锁定不可修改）
        list.Add(new FusableValue("player:hp", "玩家血量", _battle.PlayerHP, lowSanity,
            v => _battle.SetPlayerHP(v)));
        list.Add(new FusableValue("player:maxhp", "玩家血量上限", _battle.PlayerMaxHP, lowSanity,
            v => _battle.SetPlayerMaxHP(v)));

        // —— 敌人：护甲 + 意图牌库内卡面数值 ——
        for (int i = 0; i < _battle.EnemySlotCount; i++)
        {
            if (!_battle.FusionIsEnemyAlive(i)) continue;
            int enemySlot = i;   // 闭包安全
            if (_battle.FusionEnemyArmor(i) > 0)
                list.Add(new FusableValue($"enemy:{i}:armor", $"敌人{i + 1}护甲", _battle.FusionEnemyArmor(i), false,
                    v => _battle.FusionSetEnemyArmor(enemySlot, v)));

            // 敌人血量 / 血量上限（低理智下锁定不可修改）
            list.Add(new FusableValue($"enemy:{i}:hp", $"敌人{i + 1}当前血量", _battle.FusionEnemyHP(i), lowSanity,
                v => _battle.FusionSetEnemyHP(enemySlot, v)));
            list.Add(new FusableValue($"enemy:{i}:maxhp", $"敌人{i + 1}血量上限", _battle.FusionEnemyMaxHP(i), lowSanity,
                v => _battle.FusionSetEnemyMaxHP(enemySlot, v)));

            // 意图牌库：敌人下方列出的小卡，其中的数值（含费用）也参与融合
            int deckCard = 0;
            foreach (var deckView in _battle.GetEnemyIntentDeckDisplays(i))
            {
                if (deckView == null) continue;
                int slot = enemySlot;
                int cardN = deckCard;

                if (deckView.GetCostRectTransform() != null)
                {
                    int costVal = deckView.GetDisplayCost();
                    list.Add(new FusableValue(
                        $"enemy:{i}:ideck:{cardN}:cost",
                        $"敌人{i + 1}意图牌{cardN + 1}费用",
                        costVal, false,
                        v => _battle.FusionSetEnemyIntentDamage(slot, v))
                    { cardView = deckView });
                }

                int tokenN = 0;
                foreach (var tok in deckView.EnumerateNumberTokens())
                {
                    int tIdx = tokenN;
                    list.Add(new FusableValue(
                        $"enemy:{i}:ideck:{cardN}:t{tIdx}",
                        $"敌人{i + 1}意图牌{cardN + 1}·{tok.value}",
                        tok.value, false,
                        v => _battle.FusionSetEnemyIntentDamage(slot, v))
                    { cardView = deckView });
                    tokenN++;
                }
                deckCard++;
            }
        }

        // —— 手牌：费用 + 数值 ——
        int handN = _battle.HandCount;
        for (int i = 0; i < handN; i++)
        {
            var card = _battle.GetHandCardData(i);
            if (card == null) continue;
            int handIdx = i;   // 闭包安全
            var hview = _battle.GetHandCardDisplay(i);
            list.Add(new FusableValue($"hand:{i}:cost", $"手牌{i + 1}费用", card.GetEffectiveCost(), false,
                v => _battle.SetHandCardCost(handIdx, v))
            { cardView = hview });

            if (_battle.HandCardHasAttack(card) || card.attackValue > 0)
                list.Add(new FusableValue($"hand:{i}:atk", $"手牌{i + 1}攻击",
                    hview != null ? hview.GetDisplayNumberForField(LightMiniGame.CardEditor.EffectOperation.DealDamage, card.EffectiveAttack) : card.EffectiveAttack, false,
                    v => _battle.SetHandCardAttack(handIdx, v))
                { cardView = hview });

            if (_battle.HandCardHasArmor(card) || card.armorValue > 0)
                list.Add(new FusableValue($"hand:{i}:armor", $"手牌{i + 1}护甲",
                    hview != null ? hview.GetDisplayNumberForField(LightMiniGame.CardEditor.EffectOperation.GainBlock, card.EffectiveArmor) : card.EffectiveArmor, false,
                    v => _battle.SetHandCardArmor(handIdx, v))
                { cardView = hview });

            if (card.EffectiveBuffValue > 0)
                list.Add(new FusableValue($"hand:{i}:buff", $"手牌{i + 1}增益",
                    hview != null ? hview.GetDisplayNumberForField(LightMiniGame.CardEditor.EffectOperation.ModifyAttribute, card.EffectiveBuffValue) : card.EffectiveBuffValue, false,
                    v => _battle.SetHandCardBuff(handIdx, v))
                { cardView = hview });

            if (card.EffectiveDraw > 0)
                list.Add(new FusableValue($"hand:{i}:draw", $"手牌{i + 1}抽牌",
                    hview != null ? hview.GetDisplayNumberForField(LightMiniGame.CardEditor.EffectOperation.DrawCards, card.EffectiveDraw) : card.EffectiveDraw, false,
                    v => _battle.SetHandCardDraw(handIdx, v))
                { cardView = hview });

            if (card.EffectiveRestoreAP > 0)
                list.Add(new FusableValue($"hand:{i}:restore", $"手牌{i + 1}回费",
                    hview != null ? hview.GetDisplayNumberForField(LightMiniGame.CardEditor.EffectOperation.RestoreActionPoints, card.EffectiveRestoreAP) : card.EffectiveRestoreAP, false,
                    v => _battle.SetHandCardRestore(handIdx, v))
                { cardView = hview });
        }

        return list;
    }

    private static FusionNumberType TypeFromId(string id)
    {
        if (id.Contains(":ideck:")) return FusionNumberType.Intent;
        if (id.Contains(":cost")) return FusionNumberType.Cost;
        if (id.Contains(":atk")) return FusionNumberType.Attack;
        if (id.Contains(":armor")) return FusionNumberType.Armor;
        if (id.Contains(":buff")) return FusionNumberType.Buff;
        if (id.Contains(":draw")) return FusionNumberType.Draw;
        if (id.Contains(":restore")) return FusionNumberType.RestoreAP;
        if (id.StartsWith("player:")) return FusionNumberType.PlayerStat;
        if (id.StartsWith("enemy:")) return FusionNumberType.EnemyStat;
        return FusionNumberType.Other;
    }

    private FusionTarget ToTarget(FusableValue fv)
        => new FusionTarget(fv.id, fv.label, TypeFromId(fv.id), fv.current, fv.lockedBySanity, fv.apply);

    private bool IsSelected(FusableValue fv)
    {
        if (FusionManager.FirstTarget == null) return false;
        return string.Equals(FusionManager.FirstTarget.id, fv.id);
    }

    // ========================================================================
    // 面板构建：全屏蒙层 + 状态 + 退出
    // ========================================================================

    private void BuildPanel()
    {
        if (_panelRoot != null) Destroy(_panelRoot);
        ClearHitBlocks();
        if (_goldNode != null) { Destroy(_goldNode.gameObject); _goldNode = null; }

        var canvas = FindParentCanvas();
        if (canvas == null) return;

        _panelRoot = new GameObject("FusionPanel");
        _panelRoot.transform.SetParent(canvas.transform, false);
        var rootRT = _panelRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // 暗色蒙层（不拦截指针；0.6 保持明显灰底，紫字加粗仍可读）
        var maskImg = _panelRoot.AddComponent<Image>();
        maskImg.color = new Color(0f, 0f, 0f, 0.6f);
        maskImg.raycastTarget = false;

        // 顶部状态
        _statusText = CreateText(_panelRoot.transform, "StatusText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(700, 64),
            "融合模式：点选两个紫色数字自动融合", 22, new Color(0.95f, 0.9f, 0.5f, 1f));

        // 右下：退出
        CreateButton(_panelRoot.transform, "CancelButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-90, 30), new Vector2(160, 44),
            "退出", 16, ExitFusion, new Color(0.4f, 0.4f, 0.4f, 0.9f));

        UpdateStatus();
    }

    // ========================================================================
    // 高亮构建：卡面数字原位着色 + 战场透明命中层 + 货币节点
    // ========================================================================

    /// <summary>卡面（手牌+敌人意图卡）描述数字/费用数字：原位加粗变紫 + 透明命中层。</summary>
    private void BuildCardHighlights()
    {
        // 按卡分组：卡 → 候选索引序列（描述数字；费用候选单独处理）
        var byCard = new Dictionary<CardDisplay, List<int>>();
        for (int i = 0; i < _candidates.Count; i++)
        {
            var fv = _candidates[i];
            if (fv.cardView == null) continue;
            if (fv.lockedBySanity) continue;               // 锁定数字不高亮、不可点
            if (fv.id.EndsWith(":cost")) continue;          // 费用候选走 SetCostHighlight
            if (!byCard.TryGetValue(fv.cardView, out var list))
            {
                list = new List<int>();
                byCard[fv.cardView] = list;
            }
            list.Add(i);
        }
        foreach (var kv in byCard)
        {
            var candIdx = kv.Value;
            var vals = new List<int>(candIdx.Count);
            for (int k = 0; k < candIdx.Count; k++) vals.Add(_candidates[candIdx[k]].current);
            kv.Key.SetCardNumberHighlights(vals,
                idx => (idx >= 0 && idx < candIdx.Count) && IsSelected(_candidates[candIdx[idx]]),
                idx => { if (idx >= 0 && idx < candIdx.Count) OnCardNumberClicked(candIdx[idx]); },
                true,
                clickable: true);
        }
        // 费用候选：卡面费用数字原位着色 + 点击
        for (int i = 0; i < _candidates.Count; i++)
        {
            var fv = _candidates[i];
            if (fv.cardView == null || fv.lockedBySanity) continue;
            if (fv.id.EndsWith(":cost"))
            {
                int captured = i;
                fv.cardView.SetCostHighlight(IsSelected(fv),
                    () => OnCardNumberClicked(captured));
            }
        }
    }

    /// <summary>卡面数字点击：转换为目标交给 FusionManager 参与融合，并刷新选中着色。</summary>
    private void OnCardNumberClicked(int index)
    {
        if (index < 0 || index >= _candidates.Count) return;
        var fv = _candidates[index];
        FusionManager.OnTargetClick(ToTarget(fv));
        RefreshHighlightColors();
        UpdateStatus();
    }

    /// <summary>刷新全部高亮的选中态着色（第一个选中数字变红）。</summary>
    private void RefreshHighlightColors()
    {
        if (!PanelActive) return;
        // 卡面仅重刷着色；战场命中层选中无额外视觉（数字已由富文本/原文本承担）
        BuildCardHighlights();
    }

    /// <summary>战场上（非卡面）可融合数值：原位透明命中层 + FusionNumberTarget（数字视觉静置）。</summary>
    private void BuildHitBlocks()
    {
        ClearHitBlocks();
        _hitBlocks.Clear();
        if (_panelRoot == null) return;
        var panelRT = _panelRoot.transform as RectTransform;
        var rootCanvas = _panelRoot.GetComponentInParent<Canvas>()?.rootCanvas;
        Camera cam = rootCanvas != null ? rootCanvas.worldCamera : null;

        for (int i = 0; i < _candidates.Count; i++)
        {
            var fv = _candidates[i];
            if (fv.cardView != null) continue;      // 卡内数字由卡面命中层处理
            if (fv.lockedBySanity) continue;         // 锁定项（低理智血量）：不生成
            if (fv.id == "player:gold") continue;    // 货币由 _goldNode 承担
            var (center, size) = ResolveCandidateLayout(fv);
            var go = new GameObject($"FusionHit_{i}");
            go.transform.SetParent(_panelRoot.transform, false);
            var rt = go.AddComponent<RectTransform>();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, center);
            Vector2 local;
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRT, screen,
                rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam, out local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = ok ? local : new Vector2(120, 140);
            float w = Mathf.Clamp(size.x + 10f, 30f, 72f);
            float h = Mathf.Clamp(size.y + 6f, 24f, 48f);
            rt.sizeDelta = new Vector2(w, h);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);   // 完全透明：数字视觉由原位染色承担
            img.raycastTarget = true;
            var fnt = go.AddComponent<FusionNumberTarget>();
            fnt.target = ToTarget(fv);
            _hitBlocks.Add(go);
        }

        // —— 货币：无原位文本，蒙层左下角一个紫色数字节点（承载点击）——
        for (int i = 0; i < _candidates.Count; i++)
        {
            if (_candidates[i].id != "player:gold") continue;
            _goldCandidateIndex = i;
            var gold = _candidates[i];
            var go = new GameObject("FusionGold");
            go.transform.SetParent(_panelRoot.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(30f, 30f);
            rt.sizeDelta = new Vector2(170f, 40f);

            _goldNode = go.AddComponent<TextMeshProUGUI>();
            _goldNode.text = $"货币: {gold.current}";
            _goldNode.fontSize = 26;
            _goldNode.alignment = TextAlignmentOptions.Center;
            _goldNode.color = new Color(0.5f, 0f, 0.5f, 1f);
            _goldNode.fontStyle = FontStyles.Bold;
            var goldTarget = go.AddComponent<FusionNumberTarget>();
            goldTarget.target = ToTarget(gold);
            break;
        }
    }

    private void ClearHitBlocks()
    {
        foreach (var go in _hitBlocks)
            if (go != null) Destroy(go);
        _hitBlocks.Clear();
    }

    /// <summary>计算候选“原位数字”的世界中心/尺寸。
    /// 手牌描述数字→卡面字符精确位置；费用/能量/护甲/敌人→对应 UI 文本 rect；货币固定左下。</summary>
    private (Vector2 center, Vector2 size) ResolveCandidateLayout(FusableValue fv)
    {
        if (fv.hasExactRect)
            return (fv.exactCenter, fv.exactSize);

        if (fv.cardView != null && !fv.id.EndsWith(":cost"))
        {
            var vals = new List<int> { fv.current };
            if (fv.cardView.TryGetNumberRects(vals, out var centers, out var sizes)
                && sizes.Count > 0 && sizes[0] != Vector2.zero)
                return (centers[0], sizes[0]);
            var crt = fv.cardView.GetComponent<RectTransform>();
            if (crt != null) return (crt.position + new Vector3(0f, -40f, 0f), crt.rect.size * 0.35f);
        }
        if (fv.id.EndsWith(":cost") && fv.cardView != null)
        {
            var costRT = fv.cardView.GetCostRectTransform();
            if (costRT != null) return (costRT.position, costRT.rect.size);
        }
        if (fv.id.StartsWith("enemy:") && fv.id.EndsWith(":armor"))
        {
            int slot = ParseEnemySlot(fv.id);
            if (slot >= 0)
            {
                var r = _battle.GetEnemyArmorAnchor(slot);
                if (r != null) return (r.position + new Vector3(0f, -r.rect.height * 0.5f, 0f), new Vector2(28f, 30f));
            }
        }
        if (fv.id == "player:energy")
        {
            var r = _battle.ActionPointAnchor;
            if (r != null) return (r.position, new Vector2(40f, 30f));
        }
        if (fv.id == "player:armor")
        {
            var r = _battle.ArmorAnchor;
            if (r != null) return (r.position, new Vector2(40f, 30f));
        }
        if (fv.id == "player:hp" || fv.id == "player:maxhp")
        {
            bool isMax = fv.id == "player:maxhp";
            if (_battle.TryGetPlayerHPNumberRect(isMax, out var hc, out var hs))
                return (hc, hs);
            var r = _battle.HPAnchor;
            if (r != null) return (r.position, new Vector2(48f, 30f));
        }
        if (fv.id.StartsWith("enemy:") && (fv.id.EndsWith(":hp") || fv.id.EndsWith(":maxhp")))
        {
            int slot = ParseEnemySlot(fv.id);
            if (slot >= 0)
            {
                bool isMax = fv.id.EndsWith(":maxhp");
                if (_battle.TryGetEnemyHPNumberRect(slot, isMax, out var hc, out var hs))
                    return (hc, hs);
                var r = _battle.GetEnemyHPAnchor(slot);
                if (r != null) return (r.position, new Vector2(48f, 30f));
            }
        }
        var rootRT = _panelRoot != null ? _panelRoot.transform as RectTransform : null;
        return ((Vector2)(rootRT != null ? rootRT.position : Vector3.zero) + new Vector2(-560f, -420f), new Vector2(24, 24));
    }

    private int ParseEnemySlot(string id)
    {
        var parts = id.Split(':');
        int slot;
        return parts.Length >= 3 && int.TryParse(parts[1], out slot) ? slot : -1;
    }

    // ========================================================================
    // 融合结算（FusionManager 回调）
    // ========================================================================

    /// <summary>一对数值融合完成：记录信息、标记本回合融合已用、刷新 UI 并退出融合。</summary>
    private void OnPairResolved(FusionTarget a, FusionTarget b, int ra, int rb)
    {
        _lastFusionInfo = $"{LabelOf(a)} {a.value} + {LabelOf(b)} {b.value} = {a.value + b.value} → [{ra}, {rb}]";
        Debug.Log($"[Fusion] 融合完成：{_lastFusionInfo}");
        _battle.MarkFusionUsed();   // 本回合融合已消耗
        _battle.SetDirtyUI();       // 刷新手牌/面板数字显示
        ExitFusion();
    }

    /// <summary>
    /// 特殊槽位（血量对）原子回填钩子：同时选中的 当前血量+上限 直接按“小/大”分配，
    /// 避免 SetHP/SetMaxHP 相互钳制。其余返回 false 走默认逐个回填。
    /// </summary>
    private bool OnFusionCustomApply(FusionTarget a, FusionTarget b, int ra, int rb)
    {
        bool aHp = a.id == "player:hp", bHp = b.id == "player:hp";
        bool aMax = a.id == "player:maxhp", bMax = b.id == "player:maxhp";
        if ((aHp && bMax) || (aMax && bHp))
        {
            int cur = Mathf.Min(ra, rb);
            int mx = Mathf.Max(ra, rb);
            _battle.SetPlayerHPAndMax(cur, mx);
            return true;
        }
        for (int i = 0; i < _battle.EnemySlotCount; i++)
        {
            bool aEhp = a.id == $"enemy:{i}:hp", bEhp = b.id == $"enemy:{i}:hp";
            bool aEmax = a.id == $"enemy:{i}:maxhp", bEmax = b.id == $"enemy:{i}:maxhp";
            if ((aEhp && bEmax) || (aEmax && bEhp))
            {
                int cur = Mathf.Min(ra, rb);
                int mx = Mathf.Max(ra, rb);
                _battle.FusionSetEnemyHPAndMax(i, cur, mx);
                return true;
            }
        }
        return false;
    }

    private void UpdateStatus()
    {
        if (_statusText == null) return;
        string sel = "";
        if (FusionManager.FirstTarget != null)
            sel = $"\n已选: {FusionManager.FirstTarget.label} = {FusionManager.FirstTarget.value}";
        string info = _lastFusionInfo.Length > 0
            ? $"\n最近融合: {_lastFusionInfo}"
            : "";
        _statusText.text = $"融合模式：点选两个紫色数字自动融合（第一个选中变红）{info}{sel}";
    }

    private static string LabelOf(FusionTarget t)
        => string.IsNullOrEmpty(t.label) ? t.id : t.label;

    // ========================================================================
    // 退出融合 / 兼容接口
    // ========================================================================

    private void ExitFusion()
    {
        IsFusionActive = false;
        FusionManager.EndFusionMode();   // 清空选中、关闭监听
        _battle.ApplyFusionNumberTint(false);   // 恢复战场数字原色
        foreach (var c in _candidates)
            if (c.cardView != null) c.cardView.ClearHighlights();   // 卡面去紫/去命中层
        ClearHitBlocks();
        if (_goldNode != null) { Destroy(_goldNode.gameObject); _goldNode = null; }
        _goldCandidateIndex = -1;
        if (_panelRoot != null) Destroy(_panelRoot);
        _panelRoot = null;
        _statusText = null;
        _candidates.Clear();
        _lastFusionInfo = "";
        UpdateEntryInteractable();
    }

    /// <summary>整卡切换选中（兼容旧接口）：仅用于没有独立数字命中的兜底情形。
    /// 新流程下卡面每个数字都有独立命中层，EnemyView 不再调用本方法。</summary>
    public void ToggleCard(CardDisplay view)
    {
        if (view == null || !FusionManager.IsFusionActive) return;
        // 兜底：把该卡第一个可交互候选作为第一个选中
        foreach (var fv in _candidates)
            if (fv.cardView == view && !fv.lockedBySanity)
            {
                FusionManager.OnTargetClick(ToTarget(fv));
                RefreshHighlightColors();
                UpdateStatus();
                return;
            }
    }

    /// <summary>当前融合面板是否激活（供敌人卡点击选择判断）。</summary>
    public bool IsActiveForInput => IsFusionActive;

    // ========================================================================
    // UI 辅助
    // ========================================================================

    private TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 pos, Vector2 size, string text, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = fontSize; txt.color = color;
        txt.alignment = TextAlignmentOptions.Center; txt.enableWordWrapping = true;
        return txt;
    }

    private Button CreateButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 pos, Vector2 size,
        string text, int fontSize, UnityEngine.Events.UnityAction onClick, Color bg)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        CreateText(go.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, size, text, fontSize, Color.white);
        return btn;
    }
}