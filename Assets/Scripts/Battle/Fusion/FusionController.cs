using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 融合（Fusion）核心控制器 —— 由 BattleManager.BeginBattle() 自动创建并挂载。
/// UI 流程（改版：原位高亮，而非左侧列表）：
///  1) 常驻“融合”按钮（左上角），每回合可用一次。
///  2) 点击进入融合状态：立即扣 4 理智 + 全屏暗色蒙版 + 在场上所有可融合数值处
///     “原位”生成可点击高亮徽章（紫=未选、红=选中、再点取消；理智锁定的项灰显不可点）。
///     手牌数值（攻击/护甲/增益/抽牌/回费）会按卡面描述里对应数字的原文位置精确高亮。
///  3) 再次点击入口按钮 = 确认融合 → 随机拆分回填 → 蒙版消失，本回合禁用。
///  4) 蒙版右下提供“退出”按钮取消。
/// 全部 UI 由代码在运行时创建，不依赖场景手工摆放。
/// </summary>
public class FusionController : MonoBehaviour
{
    private const int SanityCost = 4;

    private BattleManager _battle;
    private bool _initialized;
    private List<FusableValue> _candidates = new();
    private readonly List<FusableValue> _selected = new();

    // === 运行时构建的 UI ===
    private GameObject _entryButtonGO;
    private Image _entryButtonImage;
    private GameObject _panelRoot;
    private TextMeshProUGUI _statusText;
    private readonly List<GameObject> _badges = new();   // 原位徽章（用于改色）

    private bool PanelActive => _panelRoot != null;

    private void OnDestroy()
    {
        if (_entryButtonGO != null) Destroy(_entryButtonGO);
        if (_panelRoot != null) Destroy(_panelRoot);
    }

    /// <summary>由 BattleManager 调用以初始化（幂等）。</summary>
    public void Setup(BattleManager battle)
    {
        _battle = battle;
        if (_initialized) return;
        _initialized = true;
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

        // 已在融合状态：再次点击入口 = 确认融合
        if (PanelActive)
        {
            ConfirmFusion();
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
        // 立即扣 4 理智（代价而非条件，不足也可进，clamp≥0）
        _battle.DeductSanityAsCost(SanityCost);

        _candidates = CollectCandidates();
        _selected.Clear();
        BuildPanel();
        UpdateEntryInteractable();
    }

    /// <summary>枚举当前要高亮的融合数值（原位锚点）。
    /// 范围（按需精简）：玩家=能量/当前护甲/货币；敌人=护甲/出招意图卡牌数值；手牌=费用/攻击/护甲/增益/抽牌/回费。
    /// 血量槽位不参与融合高亮。</summary>
    private List<FusableValue> CollectCandidates()
    {
        var list = new List<FusableValue>();

        // —— 玩家数值 ——
        list.Add(new FusableValue("player:energy", "能量", _battle.ActionPoints, false,
            v => _battle.SetActionPoints(v))
        { anchor = _battle.ActionPointAnchor, anchorOffset = new Vector2(0, 36) });
        list.Add(new FusableValue("player:armor", "当前护甲", _battle.PlayerArmor, false,
            v => _battle.SetPlayerArmor(v))
        { anchor = _battle.ArmorAnchor, anchorOffset = new Vector2(0, 0) });
        list.Add(new FusableValue("player:gold", "货币", _battle.PlayerGold, false,
            v => _battle.SetPlayerGold(v)));   // 货币无实时锚点，在徽章构建时放指定位置

        // —— 敌人：护甲 + 出招意图的卡牌数值 ——
        for (int i = 0; i < _battle.EnemySlotCount; i++)
        {
            if (!_battle.FusionIsEnemyAlive(i)) continue;
            var eanch = _battle.GetEnemyAnchor(i);
            list.Add(new FusableValue($"enemy:{i}:armor", $"敌人{i + 1}护甲", _battle.FusionEnemyArmor(i), false,
                v => _battle.FusionSetEnemyArmor(i, v))
            { anchor = eanch, anchorOffset = new Vector2(-24, 24) });
            list.Add(new FusableValue($"enemy:{i}:intent", $"敌人{i + 1}意图", _battle.FusionEnemyIntentDamage(i), false,
                v => _battle.FusionSetEnemyIntentDamage(i, v))
            { anchor = eanch, anchorOffset = new Vector2(-24, 52) });
        }

        // —— 手牌：费用 + 数值（攻击/护甲/增益/抽牌/回费） ——
        int handN = _battle.HandCount;
        for (int i = 0; i < handN; i++)
        {
            var card = _battle.GetHandCardData(i);
            if (card == null) continue;
            var hanchor = _battle.GetHandCardAnchor(i);
            var hview = _battle.GetHandCardDisplay(i);   // 用于卡面内数字的精确定位
            list.Add(new FusableValue($"hand:{i}:cost", $"手牌{i + 1}费用", card.GetEffectiveCost(), false,
                v => _battle.SetHandCardCost(i, v))
            { anchor = hanchor, cardView = hview, anchorOffset = new Vector2(-28, -28) });

            if (_battle.HandCardHasAttack(card) || card.attackValue > 0)
                list.Add(new FusableValue($"hand:{i}:atk", $"手牌{i + 1}攻击", card.EffectiveAttack, false,
                    v => _battle.SetHandCardAttack(i, v))
                { anchor = hanchor, cardView = hview, anchorOffset = new Vector2(0, 8) });

            if (_battle.HandCardHasArmor(card) || card.armorValue > 0)
                list.Add(new FusableValue($"hand:{i}:armor", $"手牌{i + 1}护甲", card.EffectiveArmor, false,
                    v => _battle.SetHandCardArmor(i, v))
                { anchor = hanchor, cardView = hview, anchorOffset = new Vector2(0, 36) });

            if (card.EffectiveBuffValue > 0)
                list.Add(new FusableValue($"hand:{i}:buff", $"手牌{i + 1}增益", card.EffectiveBuffValue, false,
                    v => _battle.SetHandCardBuff(i, v))
                { anchor = hanchor, cardView = hview, anchorOffset = new Vector2(0, 44) });

            if (card.EffectiveDraw > 0)
                list.Add(new FusableValue($"hand:{i}:draw", $"手牌{i + 1}抽牌", card.EffectiveDraw, false,
                    v => _battle.SetHandCardDraw(i, v))
                { anchor = hanchor, cardView = hview, anchorOffset = new Vector2(0, 56) });

            if (card.EffectiveRestoreAP > 0)
                list.Add(new FusableValue($"hand:{i}:restore", $"手牌{i + 1}回费", card.EffectiveRestoreAP, false,
                    v => _battle.SetHandCardRestore(i, v))
                { anchor = hanchor, cardView = hview, anchorOffset = new Vector2(0, 68) });
        }

        return list;
    }

    // ========================================================================
    // 面板构建：全屏蒙层 + 原位徽章 + 状态/确认/退出
    // ========================================================================

    private void BuildPanel()
    {
        if (_panelRoot != null) Destroy(_panelRoot);
        _badges.Clear();

        var canvas = FindParentCanvas();
        if (canvas == null) return;

        _panelRoot = new GameObject("FusionPanel");
        _panelRoot.transform.SetParent(canvas.transform, false);
        var rootRT = _panelRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // 暗色蒙层
        var maskImg = _panelRoot.AddComponent<Image>();
        maskImg.color = new Color(0f, 0f, 0f, 0.6f);
        var blocker = _panelRoot.AddComponent<Button>();
        blocker.transition = Selectable.Transition.None;
        blocker.image = maskImg;
        blocker.image.raycastTarget = true;

        // 顶部状态
        _statusText = CreateText(_panelRoot.transform, "StatusText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(700, 54),
            "点选要融合的数值（至少 2 项）", 24, new Color(0.95f, 0.9f, 0.5f, 1f));

        // 原位徽章（作为蒙层的子物体，渲染在蒙层之上）
        BuildBadges();

        // 右下：确认 + 退出
        CreateButton(_panelRoot.transform, "ConfirmButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-90, 80), new Vector2(200, 56),
            "确认融合", 20, () => ConfirmFusion(), new Color(0.55f, 0.28f, 0.75f, 0.95f));
        CreateButton(_panelRoot.transform, "CancelButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-90, 12), new Vector2(200, 48),
            "退出", 18, ExitFusion, new Color(0.4f, 0.4f, 0.4f, 0.9f));

        UpdateStatus();
    }

    /// <summary>为每个候选生成原位徽章（有锚点的按锚点屏幕坐标定位；货币固定在左下区）。</summary>
    private void BuildBadges()
    {
        // 先按“同一张卡”分组解析描述内数字的精确矩形（攻击/护甲/增益/抽牌/回费），
        // 避免逐槽位各自找第一个相同数字导致的错位。
        var precise = new Dictionary<int, (Vector2 center, Vector2 size)>();
        var byCard = new Dictionary<CardDisplay, List<(int idx, FusableValue fv)>>();
        for (int i = 0; i < _candidates.Count; i++)
        {
            var fv = _candidates[i];
            if (fv.cardView == null) continue;
            if (fv.id.EndsWith(":cost")) continue;   // 费用在卡面费用徽章上，不走描述数字
            if (!byCard.TryGetValue(fv.cardView, out var list))
            {
                list = new List<(int idx, FusableValue fv)>();
                byCard[fv.cardView] = list;
            }
            list.Add((i, fv));
        }

        foreach (var kv in byCard)
        {
            var cardView = kv.Key;
            var list = kv.Value;
            var values = new List<int>(list.Count);
            for (int k = 0; k < list.Count; k++) values.Add(list[k].fv.current);
            if (!cardView.TryGetNumberRects(values, out var centers, out var sizes)) continue;
            for (int k = 0; k < list.Count; k++)
                if (centers[k] != Vector2.zero)
                    precise[list[k].idx] = (centers[k], sizes[k]);
        }

        for (int i = 0; i < _candidates.Count; i++)
        {
            var fv = _candidates[i];
            RectTransform anchorRT = fv.anchor;
            Vector2 useOffset = fv.anchorOffset;

            // 货币：无锚点，固定到面板左下某一固定点
            if (anchorRT == null)
            {
                if (!fv.id.StartsWith("player:gold")) continue;
                anchorRT = _panelRoot.transform as RectTransform;
                useOffset = new Vector2(120, 140);
            }

            bool preciseHere = precise.TryGetValue(i, out var wp);
            var go = CreateBadge(anchorRT, fv, i, useOffset, preciseHere, wp.center, wp.size);
            if (go != null) _badges.Add(go);
        }
    }

    /// <summary>以锚点世界坐标定位徽章到面板上（徽章为 _panelRoot 的子物体，故在蒙层之上）。</summary>
    private GameObject CreateBadge(RectTransform anchorRT, FusableValue fv, int idx, Vector2 offset,
        bool precise, Vector2 wCenter, Vector2 wSize)
    {
        var go = new GameObject($"Badge_{idx}_{fv.id}");
        go.transform.SetParent(_panelRoot.transform, false);
        var rt = go.AddComponent<RectTransform>();
        // 优先用卡面描述内对应数字的精确矩形定位（已由分组解析），否则退回锚点位置
        bool placedPrecise = false;
        if (precise && wSize != Vector2.zero)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, wCenter);
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panelRoot.transform as RectTransform, screen, null, out local))
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = local;
                rt.sizeDelta = new Vector2(Mathf.Max(44, wSize.x + 18), wSize.y + 6);
                placedPrecise = true;
            }
        }
        if (!placedPrecise)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector3 worldPos = anchorRT != null ? anchorRT.position : Vector3.zero;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, worldPos);
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panelRoot.transform as RectTransform, screen, null, out local))
            {
                rt.anchoredPosition = local + offset;
            }
            else
            {
                rt.anchoredPosition = offset;
            }
            rt.sizeDelta = new Vector2(66, 34);
        }

        var img = go.AddComponent<Image>();
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => OnBadgeClick(idx));

        var txt = CreateText(go.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 28),
            $"{fv.current}", /* 徽章只显示数值本身，不带 label 描述 */ 15, Color.white);
        txt.enableWordWrapping = false;

        ApplyBadgeColor(img, fv);
        btn.interactable = !fv.lockedBySanity;
        return go;
    }

    private void ApplyBadgeColor(Image img, FusableValue fv)
    {
        if (img == null) return;
        if (fv.lockedBySanity)
            img.color = new Color(0.35f, 0.35f, 0.35f, 0.5f);
        else if (_selected.Contains(fv))
            img.color = new Color(0.9f, 0.2f, 0.2f, 0.9f);      // 红=已选
        else
            img.color = new Color(0.55f, 0.28f, 0.75f, 0.85f);  // 紫=未选
    }

    private void OnBadgeClick(int index)
    {
        if (index < 0 || index >= _candidates.Count) return;
        var fv = _candidates[index];
        if (fv.lockedBySanity) return;

        if (_selected.Contains(fv)) { _selected.Remove(fv); }
        else { _selected.Add(fv); }

        RefreshBadges();
        UpdateStatus();
    }

    private void RefreshBadges()
    {
        for (int i = 0; i < _candidates.Count; i++)
        {
            Image img = _badges[i]?.GetComponent<Image>();
            if (img != null) ApplyBadgeColor(img, _candidates[i]);
        }
    }

    private void UpdateStatus()
    {
        if (_statusText != null)
            _statusText.text = $"已选 {_selected.Count} 项（至少 2 项）  总和: {SumSelected()}";
    }

    private int SumSelected()
    {
        int s = 0;
        foreach (var v in _selected) s += v.current;
        return s;
    }

    // ========================================================================
    // 确认融合
    // ========================================================================

    private void ConfirmFusion()
    {
        if (_candidates == null || _selected.Count < 2)
        {
            Debug.Log("[Fusion] 至少选择 2 个数值才能融合");
            return;
        }
        if (_panelRoot == null) return;

        int total = SumSelected();
        int parts = _selected.Count;
        int minEach = total >= parts ? 1 : 0;
        var split = FusionSplitAlgorithm.Split(total, parts, minEach: minEach);

        for (int i = 0; i < _selected.Count; i++)
            _selected[i].apply?.Invoke(split[i]);

        Debug.Log($"[Fusion] 融合 {total} → [{string.Join(",", split)}]");
        _battle.MarkFusionUsed();
        _battle.SetDirtyUI();
        ExitFusion();
    }

    private void ExitFusion()
    {
        if (_panelRoot != null) Destroy(_panelRoot);
        _panelRoot = null;
        _badges.Clear();
        _selected.Clear();
        _candidates.Clear();
        UpdateEntryInteractable();
    }

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