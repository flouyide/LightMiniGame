using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 融合（Fusion）核心控制器 —— 由 BattleManager.BeginBattle() 自动创建并挂载。
/// UI 流程（原位高亮版）：
///  1) 常驻“融合”按钮（左上角），每回合可用一次。
///  2) 点击进入融合状态：立即扣 4 理智 + 全屏暗色蒙版 + 在每个可融合数值的“原位”
///     生成半透明高亮片（贴合对应数字，不遮字）。手牌描述内数字（攻击/护甲/增益/抽牌/回费）
///     用卡面文本精确字位定位；费用/能量/护甲/敌人护甲/意图用对应 UI 文本 rect 定位。
///     紫/金=未选、红=选中、理智锁定灰显；点击高亮片切换选中。
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
    private List<int> _lastPreviewSplit = new();   // 最近一次预览的随机拆分（确认时使用，所见即所得）

    // === 运行时构建的 UI ===
    private GameObject _entryButtonGO;
    private Image _entryButtonImage;
    private GameObject _panelRoot;
    private TextMeshProUGUI _statusText;
    private readonly List<GameObject> _highlights = new();   // 原位高亮片（与候选一一对应）

    // === Selected 选中动画（覆盖在被选中的数字上，5 倍于高亮块）===
    private static Sprite[] _selectFrames;                    // Selected.anim 的 4 帧
    private readonly List<GameObject> _selectAnims = new();  // 当前播放中的动画层（应只有1个）
    private const string SelectFrameA = "Assets/Art/Animation/未命名作品-1.png";
    private const string SelectFrameB = "Assets/Art/Animation/未命名作品-2.png";
    private const string SelectFrameC = "Assets/Art/Animation/未命名作品-3.png";
    private const string SelectFrameD = "Assets/Art/Animation/未命名作品-4.png";
    private const float SelectFrameInterval = 0.2f;

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
        // 高亮定位依赖 TMP 文本网格（低理智切形态后需一帧重建），延迟一帧构建避免错位/fallback 大块
        StartCoroutine(BuildHighlightsNextFrame());
    }

    private System.Collections.IEnumerator BuildHighlightsNextFrame()
    {
        yield return null;   // 等一帧：TMP mesh 重建（低理智切形态后文本已变）
        // 手牌强制摆到目标布局，避免读 lerp 动画中途坐标造成高亮错位
        _battle.SnapHandToTarget();
        yield return null;   // 再等一帧应用 layout 约束
        BuildHighlights();
        RefreshHighlights();
    }

    /// <summary>枚举当前要高亮的融合数值。
    /// 玩家=能量/当前护甲/货币；敌人=护甲/出招意图卡牌数值；手牌=费用/攻击/护甲/增益/抽牌/回费。</summary>
    private List<FusableValue> CollectCandidates()
    {
        var list = new List<FusableValue>();

        // —— 玩家数值 ——
        list.Add(new FusableValue("player:energy", "能量", _battle.ActionPoints, false,
            v => _battle.SetActionPoints(v)));
        // 护甲为 0 时不生成高亮（UI 中无护甲数字显示，避免在原位出现多余的“0”块）
        if (_battle.PlayerArmor > 0)
            list.Add(new FusableValue("player:armor", "当前护甲", _battle.PlayerArmor, false,
                v => _battle.SetPlayerArmor(v)));
        list.Add(new FusableValue("player:gold", "货币", _battle.PlayerGold, false,
            v => _battle.SetPlayerGold(v)));

        // —— 进阶2：玩家血量 / 血量上限（血量项始终可融合可显示；低理智也正常参与计算）——
        if (_battle.IncludeHPInFusion)
        {
            list.Add(new FusableValue("player:hp", "玩家血量", _battle.PlayerHP, false,
                v => _battle.SetPlayerHP(v)));
            list.Add(new FusableValue("player:maxhp", "玩家血量上限", _battle.PlayerMaxHP, false,
                v => _battle.SetPlayerMaxHP(v)));
        }

        // —— 敌人：护甲 + 意图牌库内卡面数值 ——
        // 注意：敌人“意图”文本本身（含字样）不做高亮，意图数值由下方牌库小卡的 token 覆盖。
        for (int i = 0; i < _battle.EnemySlotCount; i++)
        {
            if (!_battle.FusionIsEnemyAlive(i)) continue;
            int enemySlot = i;   // 闭包安全：旧编译器下 for 循环变量在 lambda 中共享，必须复制
            if (_battle.FusionEnemyArmor(i) > 0)
                list.Add(new FusableValue($"enemy:{i}:armor", $"敌人{i + 1}护甲", _battle.FusionEnemyArmor(i), false,
                    v => _battle.FusionSetEnemyArmor(enemySlot, v)));

            // —— 进阶2：敌人血量 / 血量上限（始终可融合）——
            if (_battle.IncludeHPInFusion)
            {
                list.Add(new FusableValue($"enemy:{i}:hp", $"敌人{i + 1}当前血量", _battle.FusionEnemyHP(i), false,
                    v => _battle.FusionSetEnemyHP(enemySlot, v)));
                list.Add(new FusableValue($"enemy:{i}:maxhp", $"敌人{i + 1}血量上限", _battle.FusionEnemyMaxHP(i), false,
                    v => _battle.FusionSetEnemyMaxHP(enemySlot, v)));
            }

            // 意图牌库：敌人下方列出的小卡，其中的数值（含费用）也参与融合高亮（把敌人意图伤害覆盖为融合值）
            int deckCard = 0;
            foreach (var deckView in _battle.GetEnemyIntentDeckDisplays(i))
            {
                if (deckView == null) continue;
                int slot = enemySlot;
                int cardN = deckCard;

                // —— 卡牌费用：在卡面费用气泡处高亮（可选中，融合后作为该意图卡的费用/伤害显示）——
                var costRT = deckView.GetCostRectTransform();
                if (costRT != null)
                {
                    int costVal = deckView.GetDisplayCost();
                    list.Add(new FusableValue(
                        $"enemy:{i}:ideck:{cardN}:cost",
                        $"敌人{i + 1}意图牌{cardN + 1}费用",
                        costVal, false,
                        v => _battle.FusionSetEnemyIntentDamage(slot, v))
                    {
                        cardView = deckView,
                        hasExactRect = true,
                        exactCenter = costRT.position,
                        exactSize = costRT.rect.size,
                    });
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
                    {
                        cardView = deckView,
                        hasExactRect = true,
                        exactCenter = tok.center,
                        exactSize = tok.size,
                    });
                    tokenN++;
                }
                deckCard++;
            }
        }

        // —— 手牌：费用 + 数值（攻击/护甲/增益/抽牌/回费） ——
        int handN = _battle.HandCount;
        for (int i = 0; i < handN; i++)
        {
            var card = _battle.GetHandCardData(i);
            if (card == null) continue;
            int handIdx = i;   // 闭包安全：UniT 旧编译器下 for 循环变量在 lambda 中共享，必须复制
            var hview = _battle.GetHandCardDisplay(i);   // 用于卡面内数字的精确定位
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

    // ========================================================================
    // 面板构建：全屏蒙层 + 原位高亮 + 状态/确认/退出
    // ========================================================================

    private void BuildPanel()
    {
        if (_panelRoot != null) Destroy(_panelRoot);
        _highlights.Clear();

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

        // 原位高亮（作为蒙层的子物体，渲染在蒙层之上；由 EnterFusion 延迟一帧构建，保证 TMP mesh 就绪）
        // BuildHighlights();   // 已移到 BuildHighlightsNextFrame

        // 右下：确认 + 重掷 + 退出
        CreateButton(_panelRoot.transform, "ConfirmButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-90, 80), new Vector2(200, 56),
            "确认融合", 20, () => ConfirmFusion(), new Color(0.55f, 0.28f, 0.75f, 0.95f));
        CreateButton(_panelRoot.transform, "RerollButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-90, 12), new Vector2(160, 44),
            "重新随机", 16, () => UpdateStatus(), new Color(0.45f, 0.4f, 0.55f, 0.9f));
        CreateButton(_panelRoot.transform, "CancelButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-90, -44), new Vector2(160, 40),
            "退出", 16, ExitFusion, new Color(0.4f, 0.4f, 0.4f, 0.9f));

        UpdateStatus();
    }

    /// <summary>为每个候选在原位生成一个半透明高亮片（蒙层之上，不遮字，可点击）。</summary>
    private void BuildHighlights()
    {
        var panelRT = _panelRoot.transform as RectTransform;
        if (panelRT == null) return;
        // 用面板所属 Canvas 的真实 worldCamera（World Space / ScreenSpaceCamera 时必须有相机，Overlay 为 null）
        var rootCanvas = _panelRoot.GetComponentInParent<Canvas>()?.rootCanvas;
        Camera cam = rootCanvas != null ? rootCanvas.worldCamera : null;

        for (int i = 0; i < _candidates.Count; i++)
        {
            var fv = _candidates[i];
            var (center, size) = ResolveCandidateLayout(fv);
            var go = new GameObject($"FusionHL_{i}");
            go.transform.SetParent(_panelRoot.transform, false);
            var rt = go.AddComponent<RectTransform>();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, center);
            Vector2 local;
            // 屏幕→面板局部（Overlay 用 null，World 用相机，取决于 Canvas renderMode）
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRT, screen, rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam, out local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = ok ? local : new Vector2(120, 140);
            // 半透明深紫高亮片：贴合数字（限最大尺寸，避免整槽/整卡被盖），不遮字、可点
            float w = Mathf.Clamp(size.x + 10f, 30f, 72f);
            float h = Mathf.Clamp(size.y + 6f, 24f, 48f);
            rt.sizeDelta = new Vector2(w, h);

            var img = go.AddComponent<Image>();
            img.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.interactable = !fv.lockedBySanity;
            int capturedIndex = i;   // 闭包绑定当前迭代索引（避免循环变量捕获错误）
            btn.onClick.AddListener(() => OnHighlightClick(capturedIndex));
            ApplyHighlightColor(img, fv);

            // 高亮片内显示当前数值（白色加粗带阴影，数字明显；血量上限加"上"前缀区分）
            var numGo = new GameObject("Num");
            numGo.transform.SetParent(go.transform, false);
            var numRT = numGo.AddComponent<RectTransform>();
            numRT.anchorMin = numRT.anchorMax = new Vector2(0.5f, 0.5f);
            numRT.sizeDelta = new Vector2(w - 4f, h - 4f);
            var num = numGo.AddComponent<TextMeshProUGUI>();
            num.text = fv.lockedBySanity ? "" : fv.current.ToString();
            num.fontSize = 20;
            num.fontStyle = TMPro.FontStyles.Bold;
            num.alignment = TextAlignmentOptions.Center;
            num.color = FusionPurpleBright;   // 高饱和紫
            num.outlineWidth = 0.2f;
            num.outlineColor = new Color(0f, 0f, 0f, 0.85f);
            num.enableWordWrapping = false;
            num.raycastTarget = false;

            _highlights.Add(go);
        }
    }

    /// <summary>计算候选“原位数字”的世界中心/尺寸。
    /// 手牌描述数字→卡面字符精确位置；费用/能量/护甲/敌人→对应 UI 文本 rect；货币固定左下。</summary>
    private (Vector2 center, Vector2 size) ResolveCandidateLayout(FusableValue fv)
    {
        // —— 预计算精确位置（意图牌库 token 等）—— 最高优先级 ——
        if (fv.hasExactRect)
            return (fv.exactCenter, fv.exactSize);

        // —— 手牌描述内数字（不含费用）：TryGetNumberRects 精确定位 ——
        if (fv.cardView != null && !fv.id.EndsWith(":cost"))
        {
            var vals = new List<int> { fv.current };
            if (fv.cardView.TryGetNumberRects(vals, out var centers, out var sizes)
                && sizes.Count > 0 && sizes[0] != Vector2.zero)
                return (centers[0], sizes[0]);
            // 解析失败：退回卡面中心附近（至少高亮在该卡上，可点击）
            var crt = fv.cardView.GetComponent<RectTransform>();
            if (crt != null) return (crt.position + new Vector3(0f, -40f, 0f), crt.rect.size * 0.35f);
        }
        // —— 手牌费用：卡面费用文本 rect ——
        if (fv.id.EndsWith(":cost") && fv.cardView != null)
        {
            var costRT = fv.cardView.GetCostRectTransform();
            if (costRT != null) return (costRT.position, costRT.rect.size);
        }
        // —— 敌人护甲：敌人视图对应文本 rect ——
        if (fv.id.StartsWith("enemy:") && fv.id.EndsWith(":armor"))
        {
            int slot = ParseEnemySlot(fv.id);
            if (slot >= 0)
            {
                var r = _battle.GetEnemyArmorAnchor(slot);
                if (r != null) return (r.position + new Vector3(0f, -r.rect.height * 0.5f, 0f), new Vector2(28f, 30f));
            }
        }
        // —— 玩家能量/护甲：对应文本 rect（但只取其中心、用小方块，避免整槽长条被高亮）——
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
        // —— 进阶2：玩家血量 / 血量上限（精确对齐到 HP 文本中数字字符）——
        if (fv.id == "player:hp" || fv.id == "player:maxhp")
        {
            bool isMax = fv.id == "player:maxhp";
            if (_battle.TryGetPlayerHPNumberRect(isMax, out var hc, out var hs))
                return (hc, hs);
            var r = _battle.HPAnchor;
            if (r != null) return (r.position, new Vector2(48f, 30f));
        }
        // —— 进阶2：敌人血量 / 血量上限（敌人 HP 文本内数字精确定位）——
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
        // —— 兜底（货币等无锚点）：固定左下角 ——
        var rootRT = _panelRoot.transform as RectTransform;
        return ((Vector2)rootRT.position + new Vector2(-560f, -420f), new Vector2(24, 24));
    }

    private int ParseEnemySlot(string id)
    {
        // "enemy:0:armor" → 0
        var parts = id.Split(':');
        int slot;
        return parts.Length >= 3 && int.TryParse(parts[1], out slot) ? slot : -1;
    }

    private void ApplyHighlightColor(Image img, FusableValue fv)
    {
        if (img == null) return;
        // 高亮方块透明度恒为 0：方块不可见，只作点击命中层；视觉由数字加粗紫承担
        img.color = new Color(1f, 1f, 1f, 0f);
    }

    // === 高饱和紫（供数字着色） ===
    private static readonly Color FusionPurpleBright = new Color(0.75f, 0.29f, 1f, 1f);   // #BF4AFF 高饱和紫
    private static readonly Color FusionSelectedRed  = new Color(1f, 0.23f, 0.36f, 1f);   // #FF3B5C 选中红

    private void OnHighlightClick(int index)
    {
        if (index < 0 || index >= _candidates.Count) return;
        var fv = _candidates[index];
        if (fv.lockedBySanity) return;

        if (_selected.Contains(fv)) { _selected.Remove(fv); }
        else { _selected.Add(fv); }

        RefreshHighlights();
        UpdateStatus();
    }

    private void RefreshHighlights()
    {
        // 1) 清除上一轮的选中动画层
        ClearSelectAnims();

        for (int i = 0; i < _highlights.Count && i < _candidates.Count; i++)
        {
            var go = _highlights[i];
            if (go == null) continue;
            // 方块保持全透明（透明度 0）
            Image img = go.GetComponent<Image>();
            if (img != null) ApplyHighlightColor(img, _candidates[i]);
            // 数字文字：加粗高饱和紫（未选）/ 红（已选）
            var num = go.transform.Find("Num")?.GetComponent<TextMeshProUGUI>();
            if (num != null)
                num.color = _selected.Contains(_candidates[i])
                    ? FusionSelectedRed
                    : FusionPurpleBright;
            // 2) 被选中的数字上播放 Selected 动画（5 倍覆盖）
            if (_selected.Contains(_candidates[i]))
            {
                var rt = go.GetComponent<RectTransform>();
                var anim = PlaySelectAnimAt(rt);
                if (anim != null) _selectAnims.Add(anim);
            }
        }
    }

    private void ClearSelectAnims()
    {
        foreach (var a in _selectAnims)
            if (a != null) Destroy(a);
        _selectAnims.Clear();
    }

    private static Sprite[] EnsureSelectFrames()
    {
        if (_selectFrames != null && _selectFrames.Length == 4) return _selectFrames;
#if UNITY_EDITOR
        var paths = new string[] { SelectFrameA, SelectFrameB, SelectFrameC, SelectFrameD };
        _selectFrames = new Sprite[4];
        for (int i = 0; i < paths.Length; i++)
            _selectFrames[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
#else
        _selectFrames = null;
#endif
        return _selectFrames;
    }

    /// <summary>在指定高亮块上播放 Selected 动画覆盖层（5 倍大，居中于块），返回 GameObject。</summary>
    private GameObject PlaySelectAnimAt(RectTransform blockRT)
    {
        if (blockRT == null || _panelRoot == null) return null;
        var frames = EnsureSelectFrames();
        if (frames == null || frames.Length == 0) return null;

        var go = new GameObject("SelectAnim");
        go.transform.SetParent(_panelRoot.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = blockRT.anchoredPosition + new Vector2(-blockRT.sizeDelta.x * 0.5f, 0f);   // 居中于数字，略左移贴合数字
        rt.sizeDelta = blockRT.sizeDelta * 5f;            // 5 倍于高亮块
        var img = go.AddComponent<Image>();
        img.sprite = frames[0];
        img.raycastTarget = false;                        // 不拦截点击
        StartCoroutine(AnimateSelectOverlay(img, frames));
        return go;
    }

    private System.Collections.IEnumerator AnimateSelectOverlay(Image img, Sprite[] frames)
    {
        int idx = 0;
        while (img != null && img.gameObject != null && PanelActive)
        {
            img.sprite = frames[idx];
            idx = (idx + 1) % frames.Length;
            yield return new WaitForSeconds(SelectFrameInterval);
        }
        if (img != null && img.gameObject != null) Destroy(img.gameObject);
    }

    private void UpdateStatus()
    {
        if (_statusText == null) return;
        if (_selected.Count < 2)
        {
            _statusText.text = $"已选 {_selected.Count} 项（至少 2 项）  总和: {SumSelected()}";
            return;
        }
        // 每次刷新状态都重新随机拆分，预览“这次确认将如何分配”
        int total = SumSelected();
        int parts = _selected.Count;
        int minEach = total >= parts ? 1 : 0;
        _lastPreviewSplit = FusionSplitAlgorithm.Split(total, parts, minEach: minEach);
        _statusText.text = $"已选 {_selected.Count} 项  总和: {total}\n随机分配: [{string.Join(" , ", _lastPreviewSplit)}]  （点“重新随机”再掷）";
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
        // 使用预览过的拆分（所见即所得）；无预览或数量不符则现拆
        var split = (_lastPreviewSplit != null && _lastPreviewSplit.Count == parts)
            ? _lastPreviewSplit
            : FusionSplitAlgorithm.Split(total, parts, minEach: total >= parts ? 1 : 0);

        // 血量对（current+max）同时被选需原子回填，避免 SetHP/SetMaxHP 相互钳制
        // —— 玩家：player:hp + player:maxhp ——
        bool hpPairDone = false;
        FusableValue hp = null, maxHp = null;
        int hpVal = 0, maxVal = 0;
        foreach (var fv in _selected)
        {
            if (fv.id == "player:hp") { hp = fv; hpVal = split[_selected.IndexOf(fv)]; }
            else if (fv.id == "player:maxhp") { maxHp = fv; maxVal = split[_selected.IndexOf(fv)]; }
        }
        if (hp != null && maxHp != null)
        {
            // 小的给当前血量、大的给上限（保证 当前 ≤ 上限，数值守恒）
            int cur = Mathf.Min(hpVal, maxVal);
            int mx = Mathf.Max(hpVal, maxVal);
            _battle.SetPlayerHPAndMax(cur, mx);
            hpPairDone = true;
            Debug.Log($"[Fusion] 玩家血量融合 {total} → hp={cur} max={mx}");
        }

        for (int i = 0; i < _selected.Count; i++)
        {
            var fv = _selected[i];
            // 独立的玩家血量/上限：若成对原子处理过则跳过；否则单值回填
            if (hpPairDone && (fv.id == "player:hp" || fv.id == "player:maxhp")) continue;
            if (fv.id == "player:hp" || fv.id == "player:maxhp")
            {
                fv.apply?.Invoke(split[i]);
                continue;
            }
            if (fv.id.StartsWith("enemy:") && (fv.id.EndsWith(":hp") || fv.id.EndsWith(":maxhp"))) continue;   // 敌人血量对稍后原子处理
            fv.apply?.Invoke(split[i]);
        }

        // —— 敌人血量对（enemy:i:hp + enemy:i:maxhp）原子回填；单选时单值回填 ——
        for (int i = 0; i < _battle.EnemySlotCount; i++)
        {
            FusableValue ehp = null, emax = null;
            int ehpVal = 0, emaxVal = 0;
            foreach (var fv in _selected)
            {
                if (fv.id == $"enemy:{i}:hp") { ehp = fv; ehpVal = split[_selected.IndexOf(fv)]; }
                else if (fv.id == $"enemy:{i}:maxhp") { emax = fv; emaxVal = split[_selected.IndexOf(fv)]; }
            }
            if (ehp != null && emax != null)
            {
                int cur = Mathf.Min(ehpVal, emaxVal);
                int mx = Mathf.Max(ehpVal, emaxVal);
                _battle.FusionSetEnemyHPAndMax(i, cur, mx);
                Debug.Log($"[Fusion] 敌人{i + 1}血量 {cur + mx}→{cur}/{mx}");
            }
            else if (ehp != null)
            {
                ehp.apply?.Invoke(ehpVal);
                Debug.Log($"[Fusion] 敌人{i + 1}当前血量 → {ehpVal}");
            }
            else if (emax != null)
            {
                emax.apply?.Invoke(emaxVal);
                Debug.Log($"[Fusion] 敌人{i + 1}血量上限 → {emaxVal}");
            }
        }

        Debug.Log($"[Fusion] 融合 {total} → [{string.Join(",", split)}]");
        _battle.MarkFusionUsed();
        _battle.SetDirtyUI();
        ExitFusion();
    }

    private void ExitFusion()
    {
        ClearSelectAnims();   // 清除选中动画层
        if (_panelRoot != null) Destroy(_panelRoot);
        _panelRoot = null;
        _highlights.Clear();
        _selected.Clear();
        _candidates.Clear();
        _lastPreviewSplit.Clear();
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