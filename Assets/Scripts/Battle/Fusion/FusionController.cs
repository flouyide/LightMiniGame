using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 融合（Fusion）核心控制器 —— 由 BattleManager.BeginBattle() 自动创建并挂载。
/// UI 流程（原位高亮版）：
///  1) 常驻“融合”按钮（左上角魔方），每回合可用一次。
///  2) 点击进入融合状态：立即扣 4 理智 + 全屏暗色蒙版 + 在每个可融合数值的“原位”
///     生成半透明高亮片（贴合对应数字，不遮字）。
///  3) 已选 ≥2 个数值时再点魔方 = 随机融合并回填，本回合禁用。
///  4) 未选满（0 或 1 个）时再点魔方 = 退出融合状态。
/// 全部 UI 由代码在运行时创建，不依赖场景手工摆放。
/// 重分配总值 = 选中数字之和 + 当前福报值 + 当前激活角色的遗物加成。
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
    private readonly List<GameObject> _highlights = new();   // 原位高亮片（与候选一一对应）

    // === Selected 选中动画（覆盖在被选中的数字上，5 倍于高亮块）===
    private static Sprite[] _selectFrames;                    // Selected.anim 的 4 帧
    private readonly List<GameObject> _selectAnims = new();  // 当前播放中的动画层（应只有1个）
    private const string SelectFrameA = "Assets/Art/Animation/未命名作品-1.png";
    private const string SelectFrameB = "Assets/Art/Animation/未命名作品-2.png";
    private const string SelectFrameC = "Assets/Art/Animation/未命名作品-3.png";
    private const string SelectFrameD = "Assets/Art/Animation/未命名作品-4.png";
    private const float SelectFrameInterval = 0.2f;

    // === 融合入口按钮图标（魔方） ===
    private const float EntryButtonSize = 60f;
    [SerializeField]
    [Tooltip("融合魔方按钮的 Y 偏移（Inspector 可调）。正值向上（更贴近顶栏），负值向下。")]
    private float entryButtonOffset = -90f;

    // 未加偏移时的基准 Y（= -TopBar高度 + 36），供偏移重算使用
    private float _entryButtonBaseY;
    private const string CubeClosedPath = "Assets/Art/局内/魔方关.png";
    private const string CubeOpenPath = "Assets/Art/局内/魔方开.png";
    private static Sprite _cubeClosedSprite;
    private static Sprite _cubeOpenSprite;

    private bool PanelActive => _panelRoot != null;

    /// <summary>融合面板是否打开（供意图牌层在融合期间保持在蒙层之上）。</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>当前融合蒙层面板（供意图牌层锚定兄弟顺序）。</summary>
    public static RectTransform PanelTransform { get; private set; }

    private void OnDestroy()
    {
        IsOpen = false;
        PanelTransform = null;
        CardDisplay.FusionHighlightActive = false;
        _battle?.SetHandLayoutFrozen(false);
        if (_battle != null)
        {
            for (int i = 0; i < _battle.HandCount; i++)
                _battle.GetHandCardDisplay(i)?.RestoreDescOverflow();
        }
        for (int i = 0; i < _highlights.Count; i++)
        {
            if (_highlights[i] != null)
                Destroy(_highlights[i]);
        }
        _highlights.Clear();
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
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        // TopBar 约占屏幕顶部 12%；略叠进栏下沿，战斗中栏背景已关 raycast
        const float liftIntoBar = 36f;
        float barH = ResolveTopBarHeight(parent.transform as RectTransform);
        _entryButtonBaseY = -barH + liftIntoBar;
        rt.anchoredPosition = new Vector2(78f, _entryButtonBaseY + entryButtonOffset);
        rt.sizeDelta = new Vector2(EntryButtonSize, EntryButtonSize);

        _entryButtonImage = _entryButtonGO.AddComponent<Image>();
        _entryButtonImage.preserveAspect = false;
        _entryButtonImage.maskable = false;
        _entryButtonImage.raycastTarget = true;
        _entryButtonImage.sprite = EnsureCubeSprite(false);
        FitEntryButtonToSprite();

        // 独立排序，避免意图牌全屏层盖住魔方导致点不进去
        var hudCanvas = _entryButtonGO.AddComponent<Canvas>();
        hudCanvas.overrideSorting = true;
        hudCanvas.sortingOrder = 500;
        _entryButtonGO.AddComponent<GraphicRaycaster>();

        var btn = _entryButtonGO.AddComponent<Button>();
        btn.targetGraphic = _entryButtonImage;
        btn.onClick.AddListener(OnEntryClicked);

        RaiseEntryButton();
        UpdateEntryInteractable();
    }

    private static float ResolveTopBarHeight(RectTransform canvasRT)
    {
        var topBar = FindTopBar();
        if (topBar != null)
        {
            Canvas.ForceUpdateCanvases();
            if (topBar.rect.height > 1f)
                return topBar.rect.height;
        }
        if (canvasRT != null && canvasRT.rect.height > 1f)
            return canvasRT.rect.height * (1f - 0.877f);
        return 140f;
    }

    private static RectTransform FindTopBar()
    {
        var canvases = FindObjectsOfType<Canvas>();
        foreach (var c in canvases)
        {
            if (c == null || c.name != "BookCanvas") continue;
            var tb = c.transform.Find("TopBar") as RectTransform;
            if (tb != null) return tb;
        }
        return null;
    }

    private void RaiseEntryButton()
    {
        if (_entryButtonGO != null)
            _entryButtonGO.transform.SetAsLastSibling();
    }
    /// <summary>重新套用入口按钮的 Y 偏移；运行模式下在 Inspector 改数值时即时生效。</summary>
    private void ApplyEntryButtonOffset()
    {
        if (_entryButtonGO == null) return;
        var rt = _entryButtonGO.transform as RectTransform;
        if (rt == null) return;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, _entryButtonBaseY + entryButtonOffset);
    }
#if UNITY_EDITOR
// 仅编辑器下调用：Inspector 里改 entryButtonOffset 时立刻刷新按钮位置，方便边看边调。
private void OnValidate() => ApplyEntryButtonOffset();
#endif

    private Canvas FindParentCanvas()
    {
        // 优先找名为 "BattleCanvas" 的 Canvas（避免找到 BookCanvas2）
        var canvases = FindObjectsOfType<Canvas>();
        foreach (var c in canvases)
        {
            if (c.name == "BattleCanvas" && c.gameObject.activeSelf)
                return c;
        }
        // 回退：从自身向上找
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

        // 已在融合状态：选满 2 项及以上 → 随机融合；否则退出
        if (PanelActive)
        {
            if (_selected.Count >= 2)
                ConfirmFusion();
            else
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

        if (PanelActive)
        {
            // 正在融合 → 魔方开
            _entryButtonImage.sprite = EnsureCubeSprite(true);
            _entryButtonImage.color = Color.white;
        }
        else if (canEnter)
        {
            // 可以融合 → 魔方关
            _entryButtonImage.sprite = EnsureCubeSprite(false);
            _entryButtonImage.color = Color.white;
        }
        else
        {
            // 不可融合 → 魔方关（灰显）
            _entryButtonImage.sprite = EnsureCubeSprite(false);
            _entryButtonImage.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        }
        FitEntryButtonToSprite();
    }

    /// <summary>按当前魔方图的宽高比设置按钮 Rect，点击范围与贴图一致。</summary>
    private void FitEntryButtonToSprite()
    {
        if (_entryButtonGO == null || _entryButtonImage == null || _entryButtonImage.sprite == null)
            return;
        var rt = _entryButtonGO.transform as RectTransform;
        if (rt == null) return;
        var sprite = _entryButtonImage.sprite;
        float w = sprite.rect.width;
        float h = sprite.rect.height;
        if (w < 1f || h < 1f) return;
        float scale = EntryButtonSize / Mathf.Max(w, h);
        rt.sizeDelta = new Vector2(w * scale, h * scale);
        _entryButtonImage.preserveAspect = false;
        _entryButtonImage.raycastTarget = true;
    }

    // ========================================================================
    // 进入融合
    // ========================================================================

    private void EnterFusion()
    {
        // 立即扣 4 理智（代价而非条件，不足也可进，clamp≥0）
        _battle.DeductSanityAsCost(SanityCost);
        _battle.MarkFusionUsed();

        _candidates.Clear();
        _selected.Clear();
        _battle.SetHandLayoutFrozen(true);
        BuildPanel();
        CardDisplay.FusionHighlightActive = true;
        RaiseEntryButton();
        UpdateEntryInteractable();
        // 手牌描述数字必须等 TMP 网格重建后再收集，否则会因矩形为 0 被全部跳过
        StartCoroutine(BuildHighlightsNextFrame());
    }

    private System.Collections.IEnumerator BuildHighlightsNextFrame()
    {
        yield return null;   // 等一帧：TMP mesh 重建（低理智切形态后文本已变）
        if (_panelRoot == null) yield break;
        // 手牌强制摆到目标布局，避免读 lerp 动画中途坐标造成高亮错位
        _battle.SnapHandToTarget();
        for (int i = 0; i < _battle.HandCount; i++)
            _battle.GetHandCardDisplay(i)?.PrepareFusionNumberMesh();
        yield return null;   // 再等一帧应用 layout 约束
        if (_panelRoot == null) yield break;
        try
        {
            _candidates = CollectCandidates();
            BuildHighlights();
            RefreshHighlights();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FusionController] 构建融合高亮失败: {e}");
        }
        SyncIntentDeckAbovePanel();
        RaiseEntryButton();
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

            // 意图牌库：敌人下方列出的小卡，按效果槽回写到该卡自己的融合覆盖（与手牌一致）
            foreach (var deckView in _battle.GetEnemyIntentDeckDisplays(i))
            {
                if (deckView == null) continue;
                int slot = enemySlot;
                int cardN = deckView.IntentSkillIndex >= 0 ? deckView.IntentSkillIndex : 0;

                var costRT = deckView.GetCostRectTransform();
                if (costRT != null)
                {
                    int costVal = deckView.GetDisplayCost();
                    list.Add(new FusableValue(
                        $"enemy:{i}:ideck:{cardN}:cost",
                        $"敌人{i + 1}意图牌{cardN + 1}费用",
                        costVal, false,
                        v => _battle.SetEnemyIntentCardCost(slot, cardN, v))
                    {
                        cardView = deckView,
                        hasExactRect = true,
                        exactCenter = costRT.position,
                        exactSize = costRT.rect.size,
                    });
                }

                AddEnemyDescriptionSlots(list, deckView, i, slot, cardN);
            }
        }

        // —— 手牌：费用 + 描述内每个可融合数字（多段伤害/次数/破甲等） ——
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

            AddHandDescriptionSlots(list, card, hview, i, handIdx);
        }

        return list;
    }

    /// <summary>
    /// 把手牌描述里的每个效果数字绑到卡面 token 上。
    /// 只绑定文案里真实出现的数字；效果有值但卡面没写出来的槽位不参与融合。
    /// </summary>
    private void AddHandDescriptionSlots(List<FusableValue> list, CardData card, CardDisplay hview, int handI, int handIdx)
    {
        if (hview != null && hview.HidesDescriptionText)
            return;

        var slots = hview != null
            ? hview.EnumerateFusionSlots()
            : CardFusionSlots.Collect(
                card.sourceEntry != null ? card.sourceEntry.GetEffectNodes(card.isLowSanityForm) : null,
                0, 0, false, card.fusion);

        var tokens = hview != null ? hview.EnumerateNumberTokens() : null;
        var used = tokens != null && tokens.Count > 0 ? new bool[tokens.Count] : null;

        if (slots.Count == 0)
        {
            var nodes = card.sourceEntry != null ? card.sourceEntry.GetEffectNodes(card.isLowSanityForm) : null;
            if (CardFusionSlots.ShouldSkipLegacyNumericFusion(nodes))
                return;
            AddLegacyHandSlots(list, card, hview, handI, handIdx);
            return;
        }

        for (int s = 0; s < slots.Count; s++)
        {
            var slot = slots[s];
            int tok = FindMatchingToken(tokens, used, slot.displayValue);
            if (tok < 0) continue;

            used[tok] = true;
            int nodeIdx = slot.nodeIndex;
            var kind = slot.kind;
            list.Add(new FusableValue(
                $"hand:{handI}:slot:{s}",
                $"手牌{handI + 1}·{slot.label}",
                tokens[tok].value, false,
                v => _battle.SetHandCardFusionSlot(handIdx, nodeIdx, kind, v))
            {
                cardView = hview,
                hasExactRect = true,
                exactCenter = tokens[tok].center,
                exactSize = tokens[tok].size,
            });
        }
    }

    /// <summary>敌人意图卡描述数字：按效果槽绑定，融合后写回该卡 FusionCardDelta。</summary>
    private void AddEnemyDescriptionSlots(List<FusableValue> list, CardDisplay deckView, int enemyI, int enemySlot, int skillIndex)
    {
        if (deckView != null && deckView.HidesDescriptionText)
            return;

        var slots = deckView.EnumerateFusionSlots();
        var tokens = deckView.EnumerateNumberTokens();
        var used = tokens != null && tokens.Count > 0 ? new bool[tokens.Count] : null;

        if (slots == null || slots.Count == 0)
        {
            if (tokens == null) return;
            if (deckView != null && CardFusionSlots.ShouldSkipLegacyNumericFusion(deckView.GetFusionEffectNodes()))
                return;
            for (int tIdx = 0; tIdx < tokens.Count; tIdx++)
            {
                if (tokens[tIdx].size == Vector2.zero) continue;
                int captured = tIdx;
                list.Add(new FusableValue(
                    $"enemy:{enemyI}:ideck:{skillIndex}:t{tIdx}",
                    $"敌人{enemyI + 1}意图牌{skillIndex + 1}·{tokens[tIdx].value}",
                    tokens[tIdx].value, false,
                    v => _battle.SetEnemyIntentCardFusionSlot(enemySlot, skillIndex, 0, FusionSlotKind.Damage, v))
                {
                    cardView = deckView,
                    hasExactRect = true,
                    exactCenter = tokens[captured].center,
                    exactSize = tokens[captured].size,
                });
            }
            return;
        }

        for (int s = 0; s < slots.Count; s++)
        {
            var slot = slots[s];
            int tok = FindMatchingToken(tokens, used, slot.displayValue);
            if (tok < 0) continue;

            used[tok] = true;
            int nodeIdx = slot.nodeIndex;
            var kind = slot.kind;
            list.Add(new FusableValue(
                $"enemy:{enemyI}:ideck:{skillIndex}:slot:{s}",
                $"敌人{enemyI + 1}意图牌{skillIndex + 1}·{slot.label}",
                tokens[tok].value, false,
                v => _battle.SetEnemyIntentCardFusionSlot(enemySlot, skillIndex, nodeIdx, kind, v))
            {
                cardView = deckView,
                hasExactRect = true,
                exactCenter = tokens[tok].center,
                exactSize = tokens[tok].size,
            });
        }
    }

    /// <summary>只绑定卡面上真实存在的数字；效果有值但文案没写出来的槽位不参与融合。</summary>
    private static int FindMatchingToken(List<(int value, Vector2 center, Vector2 size)> tokens, bool[] used, int displayValue)
    {
        if (tokens == null || used == null) return -1;
        for (int k = 0; k < tokens.Count; k++)
        {
            if (used[k] || tokens[k].size == Vector2.zero) continue;
            if (tokens[k].value == displayValue) return k;
        }
        return -1;
    }

    /// <summary>效果槽枚举失败时，退回按类型各高亮一个数字（保证卡面数字仍可融合）。</summary>
    private void AddLegacyHandSlots(List<FusableValue> list, CardData card, CardDisplay hview, int handI, int handIdx)
    {
        if (_battle.HandCardHasAttack(card) || card.attackValue > 0)
            list.Add(new FusableValue($"hand:{handI}:atk", $"手牌{handI + 1}攻击",
                hview != null ? hview.GetDisplayNumberForField(LightMiniGame.CardEditor.EffectOperation.DealDamage, card.EffectiveAttack) : card.EffectiveAttack, false,
                v => _battle.SetHandCardAttack(handIdx, v))
            { cardView = hview });

        if (_battle.HandCardHasArmor(card) || card.armorValue > 0)
            list.Add(new FusableValue($"hand:{handI}:armor", $"手牌{handI + 1}护甲",
                hview != null ? hview.GetDisplayNumberForField(LightMiniGame.CardEditor.EffectOperation.GainBlock, card.EffectiveArmor) : card.EffectiveArmor, false,
                v => _battle.SetHandCardArmor(handIdx, v))
            { cardView = hview });

        if (card.EffectiveBuffValue > 0)
            list.Add(new FusableValue($"hand:{handI}:buff", $"手牌{handI + 1}增益",
                hview != null ? hview.GetDisplayNumberForField(LightMiniGame.CardEditor.EffectOperation.ModifyAttribute, card.EffectiveBuffValue) : card.EffectiveBuffValue, false,
                v => _battle.SetHandCardBuff(handIdx, v))
            { cardView = hview });

        if (card.EffectiveDraw > 0)
            list.Add(new FusableValue($"hand:{handI}:draw", $"手牌{handI + 1}抽牌",
                hview != null ? hview.GetDisplayNumberForField(LightMiniGame.CardEditor.EffectOperation.DrawCards, card.EffectiveDraw) : card.EffectiveDraw, false,
                v => _battle.SetHandCardDraw(handIdx, v))
            { cardView = hview });

        if (card.EffectiveRestoreAP > 0)
            list.Add(new FusableValue($"hand:{handI}:restore", $"手牌{handI + 1}回费",
                hview != null ? hview.GetDisplayNumberForField(LightMiniGame.CardEditor.EffectOperation.RestoreActionPoints, card.EffectiveRestoreAP) : card.EffectiveRestoreAP, false,
                v => _battle.SetHandCardRestore(handIdx, v))
            { cardView = hview });
    }

    // ========================================================================
    // 面板构建：全屏蒙层 + 原位高亮
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

        IsOpen = true;
        PanelTransform = rootRT;
        SyncIntentDeckAbovePanel();
    }

    /// <summary>
    /// 把敌人意图牌层抬到融合蒙层之上：卡牌可悬停放大，空白处仍点到蒙层（不能误出牌）。
    /// </summary>
    private void SyncIntentDeckAbovePanel()
    {
        if (_panelRoot == null) return;
        var canvas = FindParentCanvas();
        if (canvas == null) return;
        var overlay = canvas.transform.Find("IntentDeckOverlay");
        if (overlay == null) return;
        int want = _panelRoot.transform.GetSiblingIndex() + 1;
        if (overlay.GetSiblingIndex() != want)
            overlay.SetSiblingIndex(want);
        RaiseEntryButton();
    }

    private void RestoreIntentDeckOverlay()
    {
        var canvas = FindParentCanvas();
        if (canvas == null) return;
        var overlay = canvas.transform.Find("IntentDeckOverlay");
        if (overlay == null) return;
        var container = canvas.transform.Find("EnemyContainer");
        if (container != null)
        {
            int afterContainer = container.GetSiblingIndex() + 1;
            if (overlay.GetSiblingIndex() != afterContainer)
                overlay.SetSiblingIndex(afterContainer);
        }
        RaiseEntryButton();
    }

    /// <summary>为每个候选在原位生成一个半透明高亮片（蒙层之上，不遮字，可点击）。</summary>
    private void BuildHighlights()
    {
        var panelRT = _panelRoot.transform as RectTransform;
        if (panelRT == null) return;
        // 用面板所属 Canvas 的真实 worldCamera（World Space / ScreenSpaceCamera 时必须有相机，Overlay 为 null）
        var rootCanvas = _panelRoot.GetComponentInParent<Canvas>()?.rootCanvas;
        Camera cam = rootCanvas != null ? rootCanvas.worldCamera : null;

        Camera eventCam = rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;

        for (int i = 0; i < _candidates.Count; i++)
        {
            var fv = _candidates[i];
            var (center, size) = ResolveCandidateLayout(fv);
            var go = new GameObject($"FusionHL_{i}");
            bool onIntentCard = IsEnemyIntentCard(fv);
            RectTransform spaceRT = onIntentCard ? fv.cardView.transform as RectTransform : panelRT;
            if (spaceRT == null) spaceRT = panelRT;
            go.transform.SetParent(spaceRT, false);
            if (onIntentCard)
                go.transform.SetAsLastSibling();

            var rt = go.AddComponent<RectTransform>();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, center);
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(spaceRT, screen, eventCam, out Vector2 local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = ok ? local : new Vector2(120, 140);

            float w;
            float h;
            float fontSize;
            if (onIntentCard)
            {
                // 卡面本地尺寸：随卡牌 localScale 一起变，放大倍率与卡牌相同，位置钉在数字上。
                // 不要按世界像素把字撑大，否则未悬停就很大，悬停后其它高亮也会显得被放大。
                float lsx = Mathf.Abs(spaceRT.lossyScale.x);
                float lsy = Mathf.Abs(spaceRT.lossyScale.y);
                if (lsx < 0.0001f) lsx = 1f;
                if (lsy < 0.0001f) lsy = 1f;
                Vector2 worldSize = IntentWorldSize(fv, size, lsx, lsy);
                w = Mathf.Max(worldSize.x / lsx + 6f, 12f);
                h = Mathf.Max(worldSize.y / lsy + 4f, 12f);
                bool isCost = fv.id != null && fv.id.EndsWith(":cost");
                fontSize = isCost ? fv.cardView.GetCostFontSize() : fv.cardView.GetDescFontSize();
            }
            else
            {
                w = Mathf.Clamp(size.x + 10f, 30f, 72f);
                h = Mathf.Clamp(size.y + 6f, 24f, 48f);
                fontSize = 20f;
            }
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
            ApplyHighlightFont(num, onIntentCard ? fv.cardView : null);
            num.text = fv.lockedBySanity ? "" : fv.current.ToString();
            num.fontSize = fontSize;
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

    private static bool IsEnemyIntentCard(FusableValue fv)
    {
        return fv != null && fv.cardView != null && fv.id != null && fv.id.IndexOf(":ideck:") >= 0;
    }

    /// <summary>
    /// 意图牌数字的世界尺寸。描述 token 已是世界坐标；费用 exactSize 是文本本地尺寸，需乘 lossyScale。
    /// </summary>
    private static Vector2 IntentWorldSize(FusableValue fv, Vector2 resolvedSize, float cardLsX, float cardLsY)
    {
        if (fv != null && fv.hasExactRect && fv.id != null && fv.id.EndsWith(":cost"))
        {
            var costRT = fv.cardView != null ? fv.cardView.GetCostRectTransform() : null;
            if (costRT != null)
            {
                float cx = Mathf.Abs(costRT.lossyScale.x);
                float cy = Mathf.Abs(costRT.lossyScale.y);
                if (cx < 0.0001f) cx = cardLsX;
                if (cy < 0.0001f) cy = cardLsY;
                return new Vector2(resolvedSize.x * cx, resolvedSize.y * cy);
            }
        }
        return resolvedSize;
    }

    private static TMP_FontAsset _highlightFont;

    /// <summary>
    /// 运行时 TMP 必须用场上已有字体。默认 TMP_Settings 字体常缺 atlas，会抛 m_AtlasTextures 未赋值并中断融合。
    /// </summary>
    private static void ApplyHighlightFont(TextMeshProUGUI tmp, CardDisplay card)
    {
        if (tmp == null) return;
        TMP_FontAsset font = null;
        if (card != null)
        {
            var existing = card.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && existing[i] != tmp && existing[i].font != null)
                {
                    font = existing[i].font;
                    break;
                }
            }
        }
        if (font == null)
        {
            if (_highlightFont == null)
            {
                var any = Object.FindObjectOfType<TextMeshProUGUI>();
                if (any != null) _highlightFont = any.font;
            }
            font = _highlightFont;
        }
        else
            _highlightFont = font;
        if (font != null)
            tmp.font = font;
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
        var paths = new string[] { SelectFrameA, SelectFrameB, SelectFrameC, SelectFrameD };
        _selectFrames = new Sprite[4];
        int n = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            _selectFrames[i] = RuntimeArt.LoadSprite(paths[i]);
            if (_selectFrames[i] != null) n++;
        }
        if (n == 0) _selectFrames = null;
        return _selectFrames;
    }

    private static Sprite EnsureCubeSprite(bool isOpen)
    {
        if (isOpen)
        {
            if (_cubeOpenSprite == null)
                _cubeOpenSprite = RuntimeArt.LoadSprite(CubeOpenPath);
            return _cubeOpenSprite;
        }

        if (_cubeClosedSprite == null)
            _cubeClosedSprite = RuntimeArt.LoadSprite(CubeClosedPath);
        return _cubeClosedSprite;
    }

    /// <summary>在指定高亮块上播放 Selected 动画覆盖层（5 倍大，居中于块），返回 GameObject。</summary>
    private GameObject PlaySelectAnimAt(RectTransform blockRT)
    {
        if (blockRT == null || _panelRoot == null) return null;
        var frames = EnsureSelectFrames();
        if (frames == null || frames.Length == 0) return null;

        var go = new GameObject("SelectAnim");
        // 挂到高亮块上：意图卡悬停放大时选中动画一起缩放
        go.transform.SetParent(blockRT, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-blockRT.sizeDelta.x * 0.5f, 0f);   // 居中于数字，略左移贴合数字
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

    private int SumSelected()
    {
        int s = 0;
        foreach (var v in _selected) s += v.current;
        return s;
    }

    /// <summary>当前福报值（未接入战斗时视为 0）。</summary>
    private int CurrentFortune => _battle != null ? Mathf.Max(0, _battle.PlayerFortune) : 0;

    /// <summary>融合重分配总值 = 选中数字之和 + 当前福报值 + 当前激活角色的遗物加成。</summary>
    private int FusionPoolTotal()
        => SumSelected() + CurrentFortune + (_battle != null ? _battle.GetActiveCharacterFusionPoolBonus() : 0);

    /// <summary>
    /// 股神：只要本次融合有股神参与，非股神槽为 0，股神拿走全部（多名均分）。
    /// 无股神时：韭菜为 0，剩余守恒分给其它槽。
    /// </summary>
    private void ApplyFusionKeywordBias(List<int> split)
    {
        if (split == null || split.Count != _selected.Count) return;

        var god = new List<int>();
        var leek = new List<int>();
        var rest = new List<int>();
        for (int i = 0; i < _selected.Count; i++)
        {
            var kw = _selected[i].cardView != null ? _selected[i].cardView.keywords : KeywordType.None;
            if (CardKeywords.Has(kw, KeywordType.StockGod)) god.Add(i);
            else if (CardKeywords.Has(kw, KeywordType.Leek)) leek.Add(i);
            else rest.Add(i);
        }

        int total = 0;
        for (int i = 0; i < split.Count; i++) total += split[i];

        if (god.Count > 0)
        {
            for (int i = 0; i < split.Count; i++) split[i] = 0;
            int share = total / god.Count;
            int rem = total % god.Count;
            for (int k = 0; k < god.Count; k++)
                split[god[k]] = share + (k < rem ? 1 : 0);
            return;
        }

        if (leek.Count == 0) return;

        foreach (int i in leek) split[i] = 0;
        if (rest.Count == 0)
        {
            if (leek.Count > 0) split[leek[0]] = total;
            return;
        }

        var sub = FusionSplitAlgorithm.Split(total, rest.Count, minEach: total >= rest.Count ? 1 : 0);
        for (int p = 0; p < rest.Count; p++)
            split[rest[p]] = sub[p];
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

        int total = FusionPoolTotal();
        int parts = _selected.Count;
        var split = FusionSplitAlgorithm.Split(total, parts, minEach: total >= parts ? 1 : 0);
        ApplyFusionKeywordBias(split);

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
        ExitFusion();
        _battle.SetDirtyUI();
    }

    private void ExitFusion()
    {
        ClearSelectAnims();   // 清除选中动画层
        for (int i = 0; i < _highlights.Count; i++)
        {
            if (_highlights[i] != null)
                Destroy(_highlights[i]);
        }
        _highlights.Clear();
        if (_panelRoot != null) Destroy(_panelRoot);
        _panelRoot = null;
        IsOpen = false;
        PanelTransform = null;
        CardDisplay.FusionHighlightActive = false;
        RestoreIntentDeckOverlay();
        _selected.Clear();
        _candidates.Clear();
        _battle.SetHandLayoutFrozen(false);
        for (int i = 0; i < _battle.HandCount; i++)
            _battle.GetHandCardDisplay(i)?.RestoreDescOverflow();
        UpdateEntryInteractable();
    }
}