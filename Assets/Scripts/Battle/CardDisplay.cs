using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightMiniGame.CardEditor;

/// <summary>
/// 卡牌显示+数据组件 —— 挂载在卡牌Prefab上。
/// 策划工作流：复制BattleCard.prefab → 改名 → 在Inspector直接配置卡牌属性
/// </summary>
public class CardDisplay : MonoBehaviour
{
    // ========================================================================
    // 区域1: 卡牌数据（策划在Inspector中配置）
    // ========================================================================

    [Header("基础信息")]
    public string cardName = "新卡牌";
    [TextArea(2, 4)] public string description;
    public CardType cardType = CardType.Attack;
    public Sprite cardArt;

    [Header("导出预制体")]
    [Tooltip("关联的卡牌编辑器数据（导出预制体后保留）；实例化时若未手动填充数据则自动从它刷新显示与功能")]
    public LightMiniGame.CardEditor.CardEntry cardEntry;

    [Header("通用属性")]
    [Tooltip("商店价值")] public int value = 10;
    [Tooltip("品级")] public CardGrade grade = CardGrade.Bronze;
    [Tooltip("需要消耗的行动点")] public int actionPointCost = 1;
    [Tooltip("消耗类型")] public ConsumeType consumeType = ConsumeType.None;
    [Tooltip("词条（不同词条具有不同效果）")] public KeywordType keywords = KeywordType.None;

    // === 攻击牌属性 ===
    [Header("攻击属性")]
    [Tooltip("攻击次数")] public int attackCount = 1;
    [Tooltip("攻击数值计算方式")] public ValueType attackValueType = ValueType.Fixed;
    [Tooltip("基础攻击数值")] public int attackValue = 5;
    [Tooltip("当attackValueType为AttributeBased时，附加的玩家属性")] public PlayerAttributeType attackAttribute = PlayerAttributeType.Strength;
    [Tooltip("攻击是否无视敌人护甲")] public bool ignoreArmor = false;

    // === 护甲牌属性 ===
    [Header("护甲属性")]
    [Tooltip("护甲值计算方式")] public ValueType armorValueType = ValueType.Fixed;
    [Tooltip("基础护甲值")] public int armorValue = 5;
    [Tooltip("当armorValueType为AttributeBased时，附加的玩家属性")] public PlayerAttributeType armorAttribute = PlayerAttributeType.Dexterity;

    // === 增益牌属性 ===
    [Header("增益属性")]
    [Tooltip("增益时效")] public BuffDurationType buffDuration = BuffDurationType.BattlePermanent;
    [Tooltip("当buffDuration为BattleXTurns时生效的回合数")] public int buffDurationTurns = 3;
    [Tooltip("增益层数")] public int buffStacks = 1;
    [Tooltip("增益效果列表")] public List<BuffEffect> buffEffects = new List<BuffEffect>();

    // ========================================================================
    // 区域2: UI引用（Prefab内部，不需要手动拖）
    // ========================================================================

    [Header("UI引用")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI keywordText;
    [SerializeField] private TextMeshProUGUI gradeText;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image artImage;

    [Tooltip("中层：描述框（卡名与描述所在框体 Image，卡面下层之上）")]
    [SerializeField] private Image descBoxImage;
    [SerializeField] private Image typeBadgeImage;
    [SerializeField] private Image costBadgeImage;

    [Header("词条图标")]
    [Tooltip("右侧签上的图标容器（VerticalLayoutGroup）。空则按名字 KeyWordsDeck/KeywordIcons 查找或运行时创建")]
    [SerializeField] private RectTransform keywordIconContainer;
    [Tooltip("词条图标库；空则尝试 Resources/CardEditor/KeywordIconLibrary")]
    [SerializeField] private KeywordIconLibrary keywordIconLibrary;
    [Tooltip("按位顺序备用图标：股神、韭菜、回流、配件、查阅、内部价、贿赂、摸鱼、监控目标")]
    [SerializeField] private Sprite[] keywordIconSprites;
    [Tooltip("单个词条图标宽高（像素）")]
    [SerializeField] private float keywordIconSize = 16f;
    [Tooltip("词条图标之间的垂直间距（像素）")]
    [SerializeField] private float keywordIconSpacing = 2f;

    [Header("词条悬浮提示")]
    [Tooltip("悬浮时显示的词条说明面板（Image + 子 TextMeshProUGUI），需挂在卡牌上方")]
    [SerializeField] private GameObject keywordTooltip;
    [Tooltip("词条说明文本组件（keywordTooltip 的子对象）")]
    [SerializeField] private TextMeshProUGUI tooltipText;
    [Tooltip("词条提示背景颜色")]
    [SerializeField] private Color tooltipBgColor = new Color(0.15f, 0.1f, 0.2f, 0.9f);
    [Tooltip("词条提示文字颜色")]
    [SerializeField] private Color tooltipTextColor = new Color(0.85f, 0.8f, 0.95f, 1f);

    [Header("类型颜色")]
    [SerializeField] private Color attackColor = new Color(0.75f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color armorColor = new Color(0.22f, 0.45f, 0.78f, 1f);
    [SerializeField] private Color buffColor = new Color(0.22f, 0.68f, 0.35f, 1f);

    [Header("品级颜色（边框）")]
    [SerializeField] private Color bronzeColor = new Color(0.72f, 0.48f, 0.34f, 1f);   // 铜
    [SerializeField] private Color silverColor = new Color(0.78f, 0.80f, 0.84f, 1f);   // 银
    [SerializeField] private Color goldColor = new Color(1.00f, 0.78f, 0.25f, 1f);   // 金

    [Header("不可打出状态")]
    [SerializeField] private Color unplayableColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

    private bool _playable = true;
    private FusionCardDelta _fusion;  // 融合覆盖层（从 CardData.fusion 读入，显示时优先覆盖）

    // —— 属性上下文：用于卡面描述实时解析力量/敏捷 ——
    /// <summary>玩家力量提供者（由 BattleManager 在战斗初始化时设置）</summary>
    public static System.Func<int> PlayerStrengthProvider;
    /// <summary>玩家敏捷提供者（由 BattleManager 在战斗初始化时设置）</summary>
    public static System.Func<int> PlayerDexterityProvider;
    /// <summary>意图牌在敌人本回合技能列表中的下标（融合回写用；非意图卡为 -1）</summary>
    public int IntentSkillIndex { get; private set; } = -1;

    public void SetIntentSkillIndex(int index) => IntentSkillIndex = index;

    /// <summary>把融合覆盖套到当前卡面（敌人意图卡没有 CardData 时走这条）。</summary>
    public void ApplyFusionOverlay(FusionCardDelta fusion)
    {
        _fusion = fusion;
        if (_data != null) _data.fusion = fusion;
        UpdateDisplay();
    }

    /// <summary>是否为敌人卡牌（意图预览用）</summary>
    private bool _isEnemyCard;
    /// <summary>敌人力量（意图预览用）</summary>
    private int _enemyStrength;
    /// <summary>敌人敏捷（意图预览用）</summary>
    private int _enemyDexterity;
    /// <summary>当前显示低理智形态（ApplyCardEntry 的 upgraded 参数）</summary>
    private bool _displayLowSanity;
    /// <summary>当前 CardEntry 是否要求仅隐藏 DescText（运行时复制牌专用）。</summary>
    private bool _hideDescriptionText;

    /// <summary>实时融合覆盖层：优先读 CardData.fusion（融合可能新建覆盖层使旧引用失效），否则用缓存。</summary>
    private FusionCardDelta LiveFusion => _data != null && _data.fusion != null ? _data.fusion : _fusion;

    private CardData _data;  // 源 CardData（融合精确数字定位用）
    private LightMiniGame.CardEditor.CardEntry _entry;   // 源 CardEntry（融合感知描述用）
    private Sprite _descBoxSprite;  // 从 CardData/CardEntry 读入的中层描述框
    private Sprite _typeBoxSprite;  // 从 CardData/CardEntry 读入的顶层类型框
    private List<KeywordType> _keywordDisplayOrder;
    private RectTransform _resolvedKeywordIconContainer;
    private Transform _resolvedKeywordTab;
    private readonly List<GameObject> _keywordIconPool = new List<GameObject>();
    private static Sprite _fallbackIconSprite;

    // ========================================================================
    // 公共方法
    // ========================================================================

    private void Awake()
    {
        // 导出预制体自带 CardEntry：实例化后若未被战斗/牌库填充数据，则自动刷新为该卡
        if (cardEntry != null && _entry == null)
            ApplyCardEntry(cardEntry, false);
    }

    /// <summary>
    /// 生成这张卡的运行时 CardData（若挂有 CardEntry 就用它，否则用本组件字段）。
    /// 供导出预制体在场景中直接接入战斗/牌库使用：拿到 CardData 即可入战斗牌堆。
    /// </summary>
    public CardData ToCardData()
    {
        if (cardEntry != null)
            return CardEntryAdapter.ConvertSingle(cardEntry);

        var cd = ScriptableObject.CreateInstance<CardData>();
        cd.cardName = cardName;
        cd.description = description;
        cd.cardType = cardType;
        cd.cardArt = cardArt;
        cd.value = value;
        cd.grade = grade;
        cd.actionPointCost = actionPointCost;
        cd.consumeType = consumeType;
        cd.keywords = keywords;
        if (_keywordDisplayOrder != null)
            cd.keywordOrder = new List<KeywordType>(_keywordDisplayOrder);
        cd.attackCount = attackCount;
        cd.attackValueType = attackValueType;
        cd.attackValue = attackValue;
        cd.attackAttribute = attackAttribute;
        cd.ignoreArmor = ignoreArmor;
        cd.armorValueType = armorValueType;
        cd.armorValue = armorValue;
        cd.armorAttribute = armorAttribute;
        cd.buffDuration = buffDuration;
        cd.buffDurationTurns = buffDurationTurns;
        cd.buffStacks = buffStacks;
        cd.buffEffects = buffEffects != null ? new List<BuffEffect>(buffEffects) : new List<BuffEffect>();
        return cd;
    }

    /// <summary>
    /// 设置是否可打出（行动点不足时灰显）
    /// </summary>
    public void SetPlayable(bool playable)
    {
        _playable = playable;
        UpdateDisplay();
    }

    /// <summary>悬浮时显示词条提示，移开时隐藏</summary>
    public void ShowKeywordTooltip(bool show)
    {
        if (!show)
        {
            if (keywordTooltip != null)
                keywordTooltip.SetActive(false);
            else
                KeywordTooltipOverlay.Hide(transform as RectTransform);
            return;
        }

        var desc = GetKeywordTooltipText();
        if (string.IsNullOrEmpty(desc))
        {
            if (keywordTooltip != null)
                keywordTooltip.SetActive(false);
            else
                KeywordTooltipOverlay.Hide(transform as RectTransform);
            return;
        }

        if (keywordTooltip != null)
        {
            if (tooltipText != null)
            {
                tooltipText.text = desc;
                tooltipText.color = tooltipTextColor;
            }
            keywordTooltip.SetActive(true);
        }
        else
        {
            KeywordTooltipOverlay.Show(transform as RectTransform, desc, tooltipBgColor, tooltipTextColor, ResolveFont());
        }
    }

    private TMP_FontAsset ResolveFont()
    {
        if (nameText != null && nameText.font != null) return nameText.font;
        if (descText != null && descText.font != null) return descText.font;
        if (costText != null && costText.font != null) return costText.font;
        return null;
    }

    private void OnDisable()
    {
        KeywordTooltipOverlay.Hide(transform as RectTransform);
    }

    private void LateUpdate()
    {
        KeywordTooltipOverlay.Reposition();
    }

    /// <summary>生成词条提示文本（含锁定原因/缠结提示）</summary>
    private string GetKeywordTooltipText()
    {
        var kw = CardKeywords.GetTooltip(keywords);
        var extra = new System.Text.StringBuilder();
        var lockReason = _data != null ? _data.lockReason : null;
        if (!string.IsNullOrEmpty(lockReason))
            extra.AppendLine(lockReason);
        if (_data != null && _data.statusCostBonus > 0)
            extra.AppendLine($"缠结：费用+{_data.statusCostBonus}");
        var extraText = extra.ToString().TrimEnd();
        if (string.IsNullOrEmpty(extraText)) return kw;
        return string.IsNullOrEmpty(kw) ? extraText : kw + "\n" + extraText;
    }

    /// <summary>设置词条展示顺序（加入先后）。null 则按枚举位顺序。</summary>
    public void SetKeywordDisplayOrder(IList<KeywordType> order)
    {
        if (order == null || order.Count == 0)
        {
            _keywordDisplayOrder = null;
            return;
        }
        _keywordDisplayOrder = new List<KeywordType>(order);
    }

    private void RefreshKeywordIcons()
    {
        var container = EnsureKeywordIconContainer();
        if (container == null) return;

        if (!CanMutateHierarchy(this))
            return;

        ApplyKeywordIconLayout(container);

        for (int i = _keywordIconPool.Count - 1; i >= 0; i--)
        {
            if (_keywordIconPool[i] == null)
                _keywordIconPool.RemoveAt(i);
        }

        if (_keywordIconPool.Count == 0)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (child == null) continue;
                // KeywordIcons 容器本身也以 KeywordIcon 开头，不能当图标回收。
                if (child.name == "KeywordIcons") continue;
                if (!child.name.StartsWith("KeywordIcon")) continue;
                var childGo = child.gameObject;
                if (childGo == null) continue;
                _keywordIconPool.Add(childGo);
            }
        }

        var ordered = CardKeywords.GetOrderedFlags(keywords, _keywordDisplayOrder);
        if (_resolvedKeywordTab != null)
            _resolvedKeywordTab.gameObject.SetActive(ordered.Count > 0);

        while (_keywordIconPool.Count < ordered.Count)
        {
            var created = CreateKeywordIcon(container);
            if (created == null) break;
            _keywordIconPool.Add(created);
        }

        for (int i = 0; i < _keywordIconPool.Count; i++)
        {
            var iconGo = _keywordIconPool[i];
            if (iconGo == null) continue;
            bool on = i < ordered.Count;
            iconGo.SetActive(on);
            if (!on) continue;
            ApplyKeywordIcon(iconGo, ordered[i]);
            ApplyKeywordIconSize(iconGo);
        }
    }

    private float ResolvedKeywordIconSize => keywordIconSize > 0f ? keywordIconSize : 16f;

    private void ApplyKeywordIconLayout(RectTransform container)
    {
        if (container == null) return;
        var vlg = container.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
            vlg.spacing = keywordIconSpacing;
    }

    private void ApplyKeywordIconSize(GameObject iconGo)
    {
        if (iconGo == null) return;
        float size = ResolvedKeywordIconSize;
        var rt = iconGo.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(size, size);
        var le = iconGo.GetComponent<LayoutElement>();
        if (le != null)
        {
            le.preferredWidth = size;
            le.preferredHeight = size;
            le.minWidth = size;
            le.minHeight = size;
        }
        var label = iconGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.fontSize = Mathf.Max(8f, size * 0.55f);
    }

    private RectTransform EnsureKeywordIconContainer()
    {
        if (_resolvedKeywordIconContainer != null) return _resolvedKeywordIconContainer;
        if (keywordIconContainer != null)
        {
            _resolvedKeywordIconContainer = keywordIconContainer;
            _resolvedKeywordTab = keywordIconContainer.parent;
            return _resolvedKeywordIconContainer;
        }

        var tab = transform.Find("KeyWordsDeck");
        if (tab == null)
        {
            tab = CreateKeywordTab();
            if (tab == null) return null;
        }
        else
            UpgradeKeywordTabIfNeeded(tab);

        var icons = tab.Find("KeywordIcons");
        if (icons == null)
        {
            if (!CanMutateHierarchy(this)) return null;
            icons = CreateKeywordIconsChild(tab);
        }

        _resolvedKeywordTab = tab;
        _resolvedKeywordIconContainer = icons as RectTransform;
        if (_resolvedKeywordIconContainer == null)
            _resolvedKeywordIconContainer = icons.GetComponent<RectTransform>();
        return _resolvedKeywordIconContainer;
    }

    private static bool CanMutateHierarchy(CardDisplay owner)
    {
        if (owner == null) return false;
        var go = owner.gameObject;
        if (go == null) return false;
#if UNITY_EDITOR
        // Play 模式也会对 Prefab 资源触发 OnValidate，绝不能因为 isPlaying 就放行 SetParent
        if (UnityEditor.EditorUtility.IsPersistent(go))
            return false;
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(go))
            return false;
        if (!go.scene.IsValid() || !go.scene.isLoaded)
            return false;

        // Project 窗口里的 Prefab：有资产类型、但不是场景实例、也不在 Prefab Stage
        var assetType = UnityEditor.PrefabUtility.GetPrefabAssetType(go);
        var instanceStatus = UnityEditor.PrefabUtility.GetPrefabInstanceStatus(go);
        if (assetType != UnityEditor.PrefabAssetType.NotAPrefab &&
            instanceStatus == UnityEditor.PrefabInstanceStatus.NotAPrefab)
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(go);
            if (stage == null)
                return false;
        }
        return true;
#else
        return Application.isPlaying;
#endif
    }

    private Transform CreateKeywordTab()
    {
        if (!CanMutateHierarchy(this)) return null;
        var tabGo = new GameObject("KeyWordsDeck", typeof(RectTransform));
        tabGo.transform.SetParent(transform, false);
        tabGo.transform.SetAsFirstSibling();
        var tabRt = tabGo.GetComponent<RectTransform>();
        tabRt.anchorMin = tabRt.anchorMax = new Vector2(0.5f, 0.5f);
        tabRt.pivot = new Vector2(0.5f, 0.5f);
        tabRt.anchoredPosition = new Vector2(102.5f, 74f);
        tabRt.sizeDelta = new Vector2(24f, 86f);

        var visualGo = new GameObject("TabVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        visualGo.transform.SetParent(tabRt, false);
        var visualRt = visualGo.GetComponent<RectTransform>();
        visualRt.anchorMin = visualRt.anchorMax = new Vector2(0.5f, 0.5f);
        visualRt.pivot = new Vector2(0.5f, 0.5f);
        visualRt.anchoredPosition = Vector2.zero;
        visualRt.sizeDelta = new Vector2(86f, 24f);
        visualRt.localRotation = Quaternion.Euler(0f, 0f, -90f);
        var visualImg = visualGo.GetComponent<Image>();
        visualImg.raycastTarget = false;
        visualImg.preserveAspect = true;
        if (typeBadgeImage != null && typeBadgeImage.sprite != null)
            visualImg.sprite = typeBadgeImage.sprite;
        visualImg.color = Color.white;

        CreateKeywordIconsChild(tabRt);
        return tabRt;
    }

    private Transform CreateKeywordIconsChild(Transform tab)
    {
        var go = new GameObject("KeywordIcons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        go.transform.SetParent(tab, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(2, 2, 8, 8);
        vlg.spacing = keywordIconSpacing;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        return rt;
    }

    private void UpgradeKeywordTabIfNeeded(Transform tab)
    {
        var sr = tab.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (!CanMutateHierarchy(this)) return;

        var sprite = sr.sprite;
        if (Application.isPlaying) Destroy(sr);
        else DestroyImmediate(sr);

        var rt = tab as RectTransform;
        if (rt == null) rt = tab.gameObject.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.anchoredPosition = new Vector2(102.5f, 74f);
        rt.sizeDelta = new Vector2(24f, 86f);

        if (tab.Find("TabVisual") == null)
        {
            var visualGo = new GameObject("TabVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            visualGo.transform.SetParent(rt, false);
            visualGo.transform.SetAsFirstSibling();
            var visualRt = visualGo.GetComponent<RectTransform>();
            visualRt.anchorMin = visualRt.anchorMax = new Vector2(0.5f, 0.5f);
            visualRt.pivot = new Vector2(0.5f, 0.5f);
            visualRt.anchoredPosition = Vector2.zero;
            visualRt.sizeDelta = new Vector2(86f, 24f);
            visualRt.localRotation = Quaternion.Euler(0f, 0f, -90f);
            var visualImg = visualGo.GetComponent<Image>();
            visualImg.raycastTarget = false;
            visualImg.preserveAspect = true;
            visualImg.sprite = sprite;
            visualImg.color = Color.white;
        }
    }

    private GameObject CreateKeywordIcon(RectTransform parent)
    {
        float size = ResolvedKeywordIconSize;
        var go = new GameObject("KeywordIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;
        le.minWidth = size;
        le.minHeight = size;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = Mathf.Max(8f, size * 0.55f);
        tmp.color = Color.white;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (nameText != null && nameText.font != null)
            tmp.font = nameText.font;
        else if (costText != null && costText.font != null)
            tmp.font = costText.font;
        return go;
    }

    private void ApplyKeywordIcon(GameObject iconGo, KeywordType kw)
    {
        if (iconGo == null) return;
        var img = iconGo.GetComponent<Image>();
        var label = iconGo.GetComponentInChildren<TextMeshProUGUI>(true);
        var sprite = ResolveKeywordSprite(kw);
        var name = CardKeywords.GetName(kw);

        if (img != null)
        {
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                img.sprite = FallbackIconSprite();
                img.color = KeywordFallbackColor(kw);
            }
        }

        if (label != null)
        {
            bool showLetter = sprite == null && !string.IsNullOrEmpty(name);
            label.gameObject.SetActive(showLetter);
            if (showLetter) label.text = name.Substring(0, 1);
        }
    }

    private Sprite ResolveKeywordSprite(KeywordType kw)
    {
        var lib = keywordIconLibrary != null ? keywordIconLibrary : KeywordIconLibrary.Load();
        var fromLib = lib != null ? lib.GetIcon(kw) : null;
        if (fromLib != null) return fromLib;

        int idx = -1;
        for (int i = 0; i < CardKeywords.AllFlags.Length; i++)
        {
            if (CardKeywords.AllFlags[i] == kw) { idx = i; break; }
        }
        if (idx >= 0 && keywordIconSprites != null && idx < keywordIconSprites.Length)
            return keywordIconSprites[idx];
        return null;
    }

    private static Sprite FallbackIconSprite()
    {
        if (_fallbackIconSprite != null) return _fallbackIconSprite;
        var tex = Texture2D.whiteTexture;
        _fallbackIconSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
        return _fallbackIconSprite;
    }

    private static Color KeywordFallbackColor(KeywordType kw)
    {
        if (kw == KeywordType.StockGod) return new Color(0.85f, 0.65f, 0.15f, 1f);
        if (kw == KeywordType.Leek) return new Color(0.35f, 0.72f, 0.32f, 1f);
        if (kw == KeywordType.Recycle) return new Color(0.25f, 0.70f, 0.75f, 1f);
        if (kw == KeywordType.Accessory) return new Color(0.55f, 0.55f, 0.60f, 1f);
        if (kw == KeywordType.Consult) return new Color(0.30f, 0.50f, 0.85f, 1f);
        if (kw == KeywordType.InternalPrice) return new Color(0.90f, 0.75f, 0.20f, 1f);
        if (kw == KeywordType.Bribe) return new Color(0.65f, 0.35f, 0.80f, 1f);
        if (kw == KeywordType.Slack) return new Color(0.90f, 0.50f, 0.20f, 1f);
        if (kw == KeywordType.WatchTarget) return new Color(0.80f, 0.25f, 0.30f, 1f);
        return new Color(0.45f, 0.45f, 0.50f, 1f);
    }

    /// <summary>
    /// 全局标记：融合面板激活时避免 UpdateDisplay 清掉原位高亮（由 FusionController 置位/复位）。
    /// </summary>
    public static bool FusionHighlightActive;

    /// <summary>
    /// 刷新卡牌UI
    /// </summary>
    public void UpdateDisplay()
    {
        // 非融合状态不显示数字高亮（数据变更时清掉；融合面板会按需重新 Set）
        if (!FusionHighlightActive) ClearNumberHighlights();
        // 基础文本
        if (nameText) nameText.text = cardName;
        // 费用：融合覆盖优先于原始费用
        if (costText)
            costText.text = GetDisplayCost().ToString();
        if (typeText) typeText.text = CardData.GetCardTypeName(cardType);
        if (gradeText) gradeText.text = CardData.GetGradeName(grade);
        if (descText)
        {
            // 运行时复制牌可保留描述数据，但不在卡面实例展示 DescText。
            // 每次刷新都显式恢复普通卡牌的文本节点，避免对象复用后错误沿用隐藏状态。
            descText.gameObject.SetActive(!_hideDescriptionText);
            descText.text = GetFusionAwareDescription();
        }

        // 词条文本（有侧签图标时不再在卡面叠一层文字）
        if (keywordText)
        {
            var kwNames = CardData.GetKeywordNames(keywords);
            bool useIcons = EnsureKeywordIconContainer() != null;
            if (useIcons)
            {
                keywordText.gameObject.SetActive(false);
            }
            else
            {
                keywordText.text = kwNames.Count > 0 ? string.Join("  ", kwNames) : "";
                keywordText.gameObject.SetActive(kwNames.Count > 0);
            }
        }

        RefreshKeywordIcons();

        Color typeColor = GetCardTypeColor();
        if (costBadgeImage) costBadgeImage.color = _playable ? typeColor : new Color(0.5f, 0.5f, 0.5f, 1f);

        // 中层描述框（卡名+描述所在框体，卡面之上、类型框之下）
        if (descBoxImage)
        {
            if (_descBoxSprite != null)
            {
                descBoxImage.sprite = _descBoxSprite;
                descBoxImage.color = Color.white;
                descBoxImage.gameObject.SetActive(true);
            }
            else
            {
                descBoxImage.gameObject.SetActive(false);
            }
        }

        // 顶层类型框（配置了类型框美术时替换为美术；未配置则保留模板自带样式，不做任何改动）
        if (typeBadgeImage && _typeBoxSprite != null)
        {
            typeBadgeImage.sprite = _typeBoxSprite;
            typeBadgeImage.color = Color.white;
        }

        // 背景颜色（按类型微调）
        if (backgroundImage)
        {
            Color bgColor = typeColor;
            bgColor.r = Mathf.Min(bgColor.r * 0.3f + 0.08f, 1f);
            bgColor.g = Mathf.Min(bgColor.g * 0.3f + 0.08f, 1f);
            bgColor.b = Mathf.Min(bgColor.b * 0.3f + 0.08f, 1f);
            bgColor.a = 0.95f;
            if (!_playable)
            {
                bgColor.r *= 0.5f;
                bgColor.g *= 0.5f;
                bgColor.b *= 0.5f;
            }
            backgroundImage.color = bgColor;
        }

        // 品级边框颜色
        if (frameImage)
        {
            frameImage.color = _playable ? GetCardGradeColor() : new Color(0.4f, 0.4f, 0.4f, 1f);
        }

        // 卡牌插图
        if (artImage)
        {
            if (cardArt != null)
            {
                artImage.sprite = cardArt;
                artImage.color = Color.white;
            }
            else
            {
                Color placeholder = typeColor;
                placeholder.a = 0.15f;
                artImage.color = placeholder;
            }
        }

        // 恢复文本颜色
        Color normalTextColor = Color.white;
        if (nameText) nameText.color = normalTextColor;
        if (descText) descText.color = normalTextColor;
        if (costText) costText.color = normalTextColor;
        if (typeText) typeText.color = normalTextColor;
        if (gradeText) gradeText.color = normalTextColor;
        if (keywordText) keywordText.color = normalTextColor;
    }

    /// <summary>
    /// 从 CardData ScriptableObject 复制全部字段到本组件并刷新显示
    /// </summary>
    public void ApplyCardData(CardData data)
    {
        if (data == null) return;

        // 融合覆盖层：卡片数值被融合修改后优先显示覆盖值
        _data = data;
        _fusion = data.fusion;
        _hideDescriptionText = false;

        // 如果有关联的 CardEntry，优先从 CardEntry 读取显示数据
        if (data.sourceEntry != null)
        {
            ApplyCardEntry(data.sourceEntry, data.isLowSanityForm);
            // 运行时字段覆盖（费用可能被修改过，keywords 可能被理智转阶段改过）
            actionPointCost = data.GetEffectiveCost();
            keywords = data.keywords;
            SetKeywordDisplayOrder(data.keywordOrder);
            UpdateDisplay();

            // CardData 自身的三层 sprite 优先；老数据字段为空时回退 sourceEntry。
            ApplyPrefabLayerSprites(
                data.cardArt != null ? data.cardArt : data.sourceEntry.cardArt,
                data.descBoxSprite != null ? data.descBoxSprite : data.sourceEntry.descBoxSprite,
                data.typeBoxSprite != null ? data.typeBoxSprite : data.sourceEntry.typeBoxSprite);
            return;
        }

        cardName = data.cardName;
        description = data.description;
        cardType = data.cardType;
        cardArt = data.cardArt;
        _descBoxSprite = data.descBoxSprite;
        _typeBoxSprite = data.typeBoxSprite;
        value = data.value;
        grade = data.grade;
        actionPointCost = data.actionPointCost;
        consumeType = data.consumeType;
        keywords = data.keywords;
        SetKeywordDisplayOrder(data.keywordOrder);
        attackCount = data.attackCount;
        attackValueType = data.attackValueType;
        attackValue = data.attackValue;
        attackAttribute = data.attackAttribute;
        ignoreArmor = data.ignoreArmor;
        armorValueType = data.armorValueType;
        armorValue = data.armorValue;
        armorAttribute = data.armorAttribute;
        buffDuration = data.buffDuration;
        buffDurationTurns = data.buffDurationTurns;
        buffStacks = data.buffStacks;
        buffEffects = data.buffEffects != null
            ? new List<BuffEffect>(data.buffEffects)
            : new List<BuffEffect>();
        UpdateDisplay();

        // 局内战斗手牌：直接替换卡牌 prefab 已有的三层 Image，不创建任何 GameObject。
        // ArtImage ← cardArt；CardFrame ← descBoxSprite；CardType ← typeBoxSprite。
        ApplyPrefabLayerSprites(data.cardArt, data.descBoxSprite, data.typeBoxSprite);
    }

    /// <summary>
    /// 从 CardEntry（卡牌编辑器数据）读取显示信息并刷新。
    /// </summary>
    public void ApplyCardEntry(CardEntry entry, bool upgraded = false)
    {
        if (entry == null) return;
        _entry = entry;
        _displayLowSanity = upgraded;
        _hideDescriptionText = entry.hideDescriptionText;

        cardName = entry.cardName;
        description = entry.GetDescription(upgraded);
        cardArt = entry.cardArt;
        actionPointCost = entry.GetCost(upgraded);

        // 映射品级
        grade = entry.grade switch
        {
            LightMiniGame.CardEditor.CardGrade.Bronze => CardGrade.Bronze,
            LightMiniGame.CardEditor.CardGrade.Silver => CardGrade.Silver,
            LightMiniGame.CardEditor.CardGrade.Gold => CardGrade.Gold,
            _ => CardGrade.Bronze
        };

        // 映射卡牌类型（与编辑器统一）
        cardType = entry.cardType switch
        {
            LightMiniGame.CardEditor.CardType.Attack => CardType.Attack,
            LightMiniGame.CardEditor.CardType.Skill => CardType.Skill,
            LightMiniGame.CardEditor.CardType.Ability => CardType.Ability,
            _ => CardType.Attack
        };

        // 三层卡面美术（底层卡面放 cardArt；中层描述框 / 顶层类型框）
        _descBoxSprite = entry.descBoxSprite;
        _typeBoxSprite = entry.typeBoxSprite;

        // 描述框位置（卡牌编辑器配置，导出预制体/运行时统一应用）
        ApplyDescBoxLayoutPosition(entry);

        // 词条映射（默认无词条；编辑器一次勾选按枚举位顺序）
        keywords = CardKeywords.FromEditor(entry.keyword);
        SetKeywordDisplayOrder(null);
        if (CardKeywords.Has(keywords, KeywordType.InternalPrice))
            actionPointCost = Mathf.Max(0, actionPointCost - 1);

        UpdateDisplay();

        // CardEntry 路径同样直接替换模板节点，避免 prefab 的旧字段接线导致描述框/类型框不刷新。
        ApplyPrefabLayerSprites(entry.cardArt, entry.descBoxSprite, entry.typeBoxSprite);
    }

    /// <summary>
    /// 直接替换当前卡牌 prefab 已有节点的三层 Image.sprite，不创建或附加任何 GameObject/组件。
    /// 节点约定：ArtImage ← cardArt；CardFrame ← descBoxSprite；CardType ← typeBoxSprite。
    /// </summary>
    private void ApplyPrefabLayerSprites(Sprite art, Sprite descBox, Sprite typeBox)
    {
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            if (image == null) continue;
            switch (image.gameObject.name)
            {
                case "ArtImage":
                    if (art != null)
                    {
                        image.sprite = art;
                        image.color = Color.white;
                    }
                    break;
                case "CardFrame":
                    if (descBox != null)
                    {
                        image.sprite = descBox;
                        image.color = Color.white;
                    }
                    break;
                case "CardType":
                    if (typeBox != null)
                    {
                        image.sprite = typeBox;
                        image.color = Color.white;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// 应用卡牌编辑器配置的描述框位置到 DescBox 背景框（若配置了 descBoxImage）。
    /// 语义：descBoxOffsetX/Y 非 0 时才移动；descBoxHeight 非 0 时才改高度；
    /// descBoxInset 非 0 时才改左右内缩。全为 0/默认时保持模板原有布局（不覆盖）。
    /// 描述文字跟随框：水平居中、垂直位于框内偏上。
    /// </summary>
    private void ApplyDescBoxLayoutPosition(LightMiniGame.CardEditor.CardEntry entry)
    {
        if (entry == null || descBoxImage == null) return;
        var rt = descBoxImage.rectTransform;

        if (entry.descBoxOffsetX != 0f || entry.descBoxOffsetY != 0f)
            rt.anchoredPosition = new Vector2(entry.descBoxOffsetX, entry.descBoxOffsetY);
        if (entry.descBoxHeight > 0f)
        {
            var sd = rt.sizeDelta;
            rt.sizeDelta = new Vector2(sd.x, entry.descBoxHeight);
        }
        if (entry.descBoxInset != 0f)
        {
            var sd = rt.sizeDelta;
            rt.sizeDelta = new Vector2(-entry.descBoxInset * 2f, sd.y);
        }

        // 描述文字跟随框：保持模板锚点，仅在用户设置了偏移/内缩时对齐框内
        if (descText != null && (entry.descBoxOffsetX != 0f || entry.descBoxOffsetY != 0f || entry.descBoxInset != 0f))
        {
            var trt = descText.rectTransform;
            float textH = trt.sizeDelta.y;
            var boxPos = rt.anchoredPosition;
            trt.anchoredPosition = new Vector2(boxPos.x, boxPos.y + rt.sizeDelta.y * 0.5f - textH * 0.5f);
        }
    }

    // ========================================================================
    // 数字字符定位（融合原位精确高亮用）
    // ========================================================================

    /// <summary>
    /// 把“每个可融合数值”映射到该卡描述文本中对应数字字符的包围盒（世界坐标），
    /// 按值的匹配 + 文档顺序依次配对，返回与传入 values 一一对应的中心点/尺寸数组。
    /// 避免旧 TryGetNumberRect 只找“第一个出现”导致的错位（如“理智 -1，抽 1 张牌”里
    /// 抽牌数 1 会被误配到 -1 的 1）。找不到对应数字的槽位返回 false；找到部分则以其计。
    /// </summary>
    public bool TryGetNumberRects(List<int> values, out List<Vector2> centers, out List<Vector2> sizes)
    {
        centers = new List<Vector2>();
        sizes = new List<Vector2>();
        if (values == null || values.Count == 0 || descText == null) return false;

        EnsureDescMesh();
        var info = descText.textInfo;
        if (info == null || info.characterInfo == null || info.characterCount == 0) return false;

        // 1) 解析描述文本中所有“有符号整数 token”（按文档顺序），记录其字符跨度。
        var tokens = new List<Token>();
        string text = descText.text;
        int n = text.Length;
        for (int idx = 0; idx < n; idx++)
        {
            if (!char.IsDigit(text[idx])) continue;

            // 向左吸收可选的 '-'（形成负整数，如 “理智 -1”），只吸收紧邻的单个负号
            int start = idx;
            if (idx > 0 && text[idx - 1] == '-') start = idx - 1;

            // 向右吸收连续数字
            int end = idx;
            while (end + 1 < n && char.IsDigit(text[end + 1])) end++;
            if (!int.TryParse(text.Substring(start, end - start + 1), out int val)) continue;

            tokens.Add(new Token { startChar = start, endChar = end, value = val });
            idx = end; // 跳过已处理的数字
        }
        if (tokens.Count == 0) return false;

        // 2) 为每个 token 计算世界包围盒
        var tokenRects = new List<Vector4>(tokens.Count); // x=minX,y=minY,z=maxX,w=maxY
        foreach (var t in tokens)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
            bool found = false;
            for (int ci = 0; ci < info.characterCount; ci++)
            {
                var ch = info.characterInfo[ci];
                if (ch.index < t.StartChar || ch.index > t.EndChar) continue;
                if (!ch.isVisible) continue;
                Vector3 tl = descText.transform.TransformPoint(ch.topLeft);
                Vector3 tr = descText.transform.TransformPoint(ch.topRight);
                Vector3 bl = descText.transform.TransformPoint(ch.bottomLeft);
                Vector3 br = descText.transform.TransformPoint(ch.bottomRight);
                min = Vector3.Min(min, Vector3.Min(Vector3.Min(tl, bl), Vector3.Min(tr, br)));
                max = Vector3.Max(max, Vector3.Max(Vector3.Max(tl, bl), Vector3.Max(tr, br)));
                found = true;
            }
            tokenRects.Add(found
                ? new Vector4(min.x, min.y, max.x, max.y)
                : new Vector4(0, 0, 0, 0));
        }

        // 3) 依序为每个可融合数值找“值相等且未被占用”的 token
        var used = new bool[tokens.Count];
        bool anySuccess = false;
        for (int i = 0; i < values.Count; i++)
        {
            int target = values[i];
            bool placed = false;
            for (int t = 0; t < tokens.Count; t++)
            {
                if (used[t] || tokens[t].value != target) continue;
                used[t] = true;
                var r = tokenRects[t];
                centers.Add(new Vector2((r.x + r.z) * 0.5f, (r.y + r.w) * 0.5f));
                sizes.Add(new Vector2(r.z - r.x, r.w - r.y));
                placed = true;
                anySuccess = true;
                break;
            }
            if (!placed)
            {
                centers.Add(Vector2.zero);
                sizes.Add(Vector2.zero);
            }
        }
        return anySuccess;
    }

    // ========================================================================
    // 原位数字高亮
    // ========================================================================

    private readonly List<Image> _numberHighlights = new();   // 复用矩形 Image

    /// <summary>
    /// 设置需要高亮的卡牌数字（融合时调用）。每个数字对应描述文本中该数值的字符矩形，
    /// 在文本底下（descText 的前兄弟节点）生成半透明高亮片，数字透在其上，位于原位。
    /// </summary>
    /// <param name="isSelected">可选回调：给定 values 内的索引 i，返回该数字槽位是否已被选中（高亮加深）。</param>
    public void SetNumberHighlights(List<int> values, bool clearExisting = true, System.Func<int, bool> isSelected = null)
    {
        if (clearExisting) ClearNumberHighlights();
        if (values == null || values.Count == 0 || descText == null) return;

        if (!TryGetNumberRects(values, out var centers, out var sizes)) return;

        RectTransform descParent = descText.transform.parent as RectTransform;
        if (descParent == null) return;

        for (int i = 0; i < values.Count; i++)
        {
            if (sizes[i] == Vector2.zero) continue;
            var img = GetOrCreateNumberHighlight(i);
            var rt = img.GetComponent<RectTransform>();
            // 数字中心世界坐标 → 该文本父节点本地坐标（与高亮片同级）
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, centers[i]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(descParent, screen, null, out Vector2 local))
                continue;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local;
            float scale = descParent.lossyScale.x != 0 ? descParent.lossyScale.x : 1f;
            rt.sizeDelta = new Vector2(sizes[i].x / scale, sizes[i].y / scale);
            bool sel = isSelected != null && isSelected(i);
            bool fused = LiveFusion != null && LiveFusion.HasAny;
            //  高亮：选中→红色实底；已融合→更实；否则淡金色衬底
            img.color = sel
                ? new Color(0.9f, 0.2f, 0.2f, 0.85f)
                : fused
                    ? new Color(0.95f, 0.72f, 0.30f, 0.85f)
                    : new Color(0.95f, 0.85f, 0.35f, 0.30f);
            img.raycastTarget = false;
            img.gameObject.SetActive(true);
        }
    }

    private Image GetOrCreateNumberHighlight(int index)
    {
        if (index < _numberHighlights.Count)
            return _numberHighlights[index];

        var go = new GameObject($"NumberHighlight_{index}");
        if (descText != null)
            go.transform.SetParent(descText.transform.parent, false);   // 与描述文本同父
        else
            go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = new Color(0.95f, 0.85f, 0.35f, 0.25f);
        img.raycastTarget = false;
        //  关键：插到 descText 前面，让它在文字底下渲染
        if (descText != null)
            go.transform.SetSiblingIndex(descText.transform.GetSiblingIndex());
        _numberHighlights.Add(img);
        return img;
    }

    private void ClearNumberHighlights()
    {
        foreach (var img in _numberHighlights)
            if (img != null && img.gameObject != null)
                Destroy(img.gameObject);
        _numberHighlights.Clear();
    }

    /// <summary>清除本卡所有原位数字高亮（融合退出时调用）。</summary>
    public void ClearHighlights()
    {
        ClearNumberHighlights();
        ClearCostHighlight();
    }

    // --- 费用原位高亮 ---

    private Image _costHighlight;

    /// <summary>在费用文字（costText）底下生成原位高亮片（不遮字）。融合时调用。</summary>
    public void SetCostHighlight(bool selected)
    {
        if (costText == null) return;
        if (_costHighlight == null)
        {
            var go = new GameObject("CostHighlight");
            go.transform.SetParent(costText.transform.parent, false);
            _costHighlight = go.AddComponent<Image>();
            _costHighlight.raycastTarget = false;
            go.transform.SetSiblingIndex(costText.transform.GetSiblingIndex());
        }

        var rt = _costHighlight.GetComponent<RectTransform>();
        var costRT = costText.GetComponent<RectTransform>();
        // 直接复用费用文本的矩形（锚点/枢轴/位置一致，仅略放大）
        rt.anchorMin = costRT.anchorMin;
        rt.anchorMax = costRT.anchorMax;
        rt.pivot = costRT.pivot;
        rt.anchoredPosition = costRT.anchoredPosition;
        rt.sizeDelta = costRT.rect.size + new Vector2(10f, 6f);
        _costHighlight.color = selected
            ? new Color(0.9f, 0.2f, 0.2f, 0.85f)
            : new Color(0.95f, 0.85f, 0.35f, 0.30f);
        _costHighlight.gameObject.SetActive(true);
    }

    private void ClearCostHighlight()
    {
        if (_costHighlight != null && _costHighlight.gameObject != null)
            Destroy(_costHighlight.gameObject);
        _costHighlight = null;
    }

    /// <summary>返回费用文本的 RectTransform（供融合点击层定位）。</summary>
    public RectTransform GetCostRectTransform() => costText != null ? costText.rectTransform : null;

    public float GetCostFontSize() => costText != null ? costText.fontSize : 16f;

    public float GetDescFontSize() => descText != null ? descText.fontSize : 16f;

    /// <summary>当前显示的费用（融合覆盖优先；否则按入口当前形态费用）。</summary>
    public int GetDisplayCost()
    {
        var f = LiveFusion;
        if (f != null && f.overrideCost) return f.cost;
        return actionPointCost;
    }

    /// <summary>
    /// 确保 descText 的 TMP 网格已针对当前文本重建（低理智切形态后文本变了但网格可能未刷新，
    /// 直接 ForceMeshUpdate 拿不到字符；先设脏 + 强制 Canvas 更新）。
    /// </summary>
    private void EnsureDescMesh()
    {
        if (descText == null) return;
        descText.SetAllDirty();
        UnityEngine.Canvas.ForceUpdateCanvases();
        // forceTextReparsing=true：强制 TMP 重新解析文本并重建字符布局（低理智切形态后文本已变）
        descText.ForceMeshUpdate(true, true);
    }

    /// <summary>
    /// 枚举该卡描述文本中的每个“整数 token”（值 + 世界中心/尺寸）。
    /// 供“意图牌库”等非手牌卡面在融合时按 token 逐个精确高亮。
    /// </summary>
    public List<(int value, Vector2 center, Vector2 size)> EnumerateNumberTokens()
    {
        var result = new List<(int, Vector2, Vector2)>();
        if (descText == null) return result;

        EnsureDescMesh();
        var info = descText.textInfo;
        if (info == null || info.characterInfo == null || info.characterCount == 0)
        {
            // mesh 未就绪（低理智切形态同帧）：回退纯文本解析，仅返回数值（矩形为零，供数值读取）
            string t = descText.text;
            for (int i = 0; i < t.Length; i++)
            {
                if (!char.IsDigit(t[i])) continue;
                int s = i;
                if (i > 0 && t[i - 1] == '-') s = i - 1;
                int e = i;
                while (e + 1 < t.Length && char.IsDigit(t[e + 1])) e++;
                if (int.TryParse(t.Substring(s, e - s + 1), out int tv))
                    result.Add((tv, Vector2.zero, Vector2.zero));
                i = e;
            }
            return result;
        }

        string text = descText.text;
        int n = text.Length;
        for (int idx = 0; idx < n; idx++)
        {
            if (!char.IsDigit(text[idx])) continue;
            int start = idx;
            if (idx > 0 && text[idx - 1] == '-') start = idx - 1;
            int end = idx;
            while (end + 1 < n && char.IsDigit(text[end + 1])) end++;
            if (!int.TryParse(text.Substring(start, end - start + 1), out int val))
            {
                idx = end;
                continue;
            }

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
            for (int ci = 0; ci < info.characterCount; ci++)
            {
                var ch = info.characterInfo[ci];
                if (ch.index < start || ch.index > end) continue;
                if (!ch.isVisible) continue;
                Vector3 tl = descText.transform.TransformPoint(ch.topLeft);
                Vector3 tr = descText.transform.TransformPoint(ch.topRight);
                Vector3 bl = descText.transform.TransformPoint(ch.bottomLeft);
                Vector3 br = descText.transform.TransformPoint(ch.bottomRight);
                min = Vector3.Min(min, Vector3.Min(Vector3.Min(tl, bl), Vector3.Min(tr, br)));
                max = Vector3.Max(max, Vector3.Max(Vector3.Max(tl, bl), Vector3.Max(tr, br)));
            }
            if (max.x <= min.x || max.y <= min.y)
            {
                idx = end;
                continue;
            }
            result.Add((val, (Vector2)((min + max) * 0.5f), new Vector2(max.x - min.x, max.y - min.y)));
            idx = end;
        }
        return result;
    }

    // ========================================================================
    // 数字字符定位（单数值，保留给旧调用）
    // ========================================================================

    /// <summary>
    /// 返回该卡描述文本中“targetNumber”这一串字符的包围盒（世界坐标）。
    /// 用于融合时在卡面数字位置上精确定位高亮。找不到返回 false。
    /// </summary>
    public bool TryGetNumberRect(string targetNumber, out Vector2 worldCenter, out Vector2 worldSize)
    {
        worldCenter = Vector2.zero;
        worldSize = Vector2.zero;
        if (descText == null || string.IsNullOrEmpty(targetNumber)) return false;

        EnsureDescMesh();
        var info = descText.textInfo;
        if (info == null || info.characterInfo == null || info.characterCount == 0) return false;

        string text = descText.text;
        int startIdx = text.IndexOf(targetNumber, System.StringComparison.Ordinal);
        if (startIdx < 0) return false;
        int endIdx = startIdx + targetNumber.Length - 1;

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
        bool found = false;

        for (int i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            if (ch.index < startIdx || ch.index > endIdx) continue;
            if (!ch.isVisible) continue;

            Vector3 tl = descText.transform.TransformPoint(ch.topLeft);
            Vector3 tr = descText.transform.TransformPoint(ch.topRight);
            Vector3 bl = descText.transform.TransformPoint(ch.bottomLeft);
            Vector3 br = descText.transform.TransformPoint(ch.bottomRight);

            min = Vector3.Min(min, Vector3.Min(Vector3.Min(tl, bl), Vector3.Min(tr, br)));
            max = Vector3.Max(max, Vector3.Max(Vector3.Max(tl, bl), Vector3.Max(tr, br)));
            found = true;
        }

        if (!found) return false;
        worldCenter = (Vector2)((min + max) * 0.5f);
        worldSize = new Vector2(max.x - min.x, max.y - min.y);
        return true;
    }

    // ========================================================================
    // 描述生成（复用CardData的静态方法）
    // ========================================================================

    /// <summary>
    /// 按效果节点顺序列出当前卡面可融合的数字槽（伤害/次数/破甲/护甲/增益等），含已有融合覆盖后的显示值。
    /// </summary>
    public List<CardFusionSlotInfo> EnumerateFusionSlots()
    {
        bool low = _data != null ? _data.isLowSanityForm : _displayLowSanity;
        var entry = _entry != null ? _entry : (_data != null ? _data.sourceEntry : null);
        var nodes = entry != null ? entry.GetEffectNodes(low) : null;
        return CardFusionSlots.Collect(nodes, ContextStrength, ContextDexterity, _isEnemyCard, LiveFusion);
    }

    /// <summary>当前卡面正在用的效果节点（与融合槽/描述形态一致）。</summary>
    public List<EffectNode> GetFusionEffectNodes()
    {
        bool low = _data != null ? _data.isLowSanityForm : _displayLowSanity;
        var entry = _entry != null ? _entry : (_data != null ? _data.sourceEntry : null);
        return entry != null ? entry.GetEffectNodes(low) : null;
    }

    /// <summary>
    /// 融合感知描述：若存在融合覆盖，按效果数字槽顺序把文案中对应数字替换为融合后的值。
    /// 无融合覆盖或无法解析时回退原文案。
    /// </summary>
    public string GetFusionAwareDescription()
    {
        if (LiveFusion == null || !LiveFusion.HasAny || _entry == null)
            return GetDisplayDescription();

        // 使用属性解析后的描述作为基础文本（力量/敏捷已替换为实际数值）
        var desc = GetDisplayDescription();
        if (string.IsNullOrEmpty(desc)) return desc;

        bool low = _data != null ? _data.isLowSanityForm : _displayLowSanity;
        var slots = CardFusionSlots.Collect(
            _entry.GetEffectNodes(low), ContextStrength, ContextDexterity, _isEnemyCard, LiveFusion);
        if (slots.Count == 0) return desc;

        var replacements = new List<(int start, int len, string newVal)>();
        int searchFrom = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            int pos = FindNumberToken(desc, slot.baseValue, searchFrom);
            if (pos < 0) continue;
            int start = pos;
            int len = 0;
            if (start > 0 && desc[start - 1] == '-') { start--; len++; }
            while (start + len < desc.Length && char.IsDigit(desc[start + len])) len++;
            searchFrom = start + len;
            if (slot.displayValue == slot.baseValue) continue;
            replacements.Add((start, len, slot.displayValue.ToString()));
        }
        for (int i = replacements.Count - 1; i >= 0; i--)
        {
            var (start, len, nv) = replacements[i];
            desc = desc.Substring(0, start) + nv + desc.Substring(start + len);
        }
        return desc;
    }

    /// <summary>找 desc 中从 startIndex 开始的第一个等于数值 val 的数字 token，返回其起始下标；无则 -1。</summary>
    private static int FindNumberToken(string desc, int val, int startIndex)
    {
        if (string.IsNullOrEmpty(desc)) return -1;
        int i = Mathf.Max(0, startIndex);
        while (i < desc.Length)
        {
            if (char.IsDigit(desc[i]))
            {
                int s = i;
                int e = i;
                while (e + 1 < desc.Length && char.IsDigit(desc[e + 1])) e++;
                int neg = 0;
                if (s > 0 && desc[s - 1] == '-') neg = -1;
                int tokenVal;
                if (int.TryParse(desc.Substring(s, e - s + 1), out tokenVal) && tokenVal + (neg == -1 ? 0 : 0) == val)
                    return s;
                i = e + 1;
            }
            else i++;
        }
        return -1;
    }

    /// <summary>
    /// 取卡面当前显示的、对应指定效果字段的数值（与显示文本一致，含低理智形态）。
    /// 遍历 CardEntry 可融合效果节点顺序，与 EnumerateNumberTokens 的 token 序列按序对齐。
    /// 找不到返回 fallback。
    /// </summary>
    public int GetDisplayNumberForField(LightMiniGame.CardEditor.EffectOperation op, int fallback)
    {
        bool low = _data != null && _data.isLowSanityForm;
        var nodes = _entry != null ? _entry.GetEffectNodes(low) : null;
        if (nodes == null || nodes.Count == 0) return fallback;

        // 可融合字段节点顺序（DealDamage/GainBlock/ModifyAttribute/DrawCards/RestoreAP）
        var fieldOps = new List<LightMiniGame.CardEditor.EffectOperation>();
        foreach (var n in nodes)
        {
            if (n == null || !n.enabled) continue;
            switch (n.operation)
            {
                case LightMiniGame.CardEditor.EffectOperation.DealDamage:
                case LightMiniGame.CardEditor.EffectOperation.GainBlock:
                case LightMiniGame.CardEditor.EffectOperation.ModifyAttribute:
                case LightMiniGame.CardEditor.EffectOperation.DrawCards:
                case LightMiniGame.CardEditor.EffectOperation.RestoreActionPoints:
                    fieldOps.Add(n.operation);
                    break;
            }
        }
        if (fieldOps.Count == 0) return fallback;

        var tokens = EnumerateNumberTokens();
        if (tokens.Count == 0) return fallback;

        // 按序：第 k 个可融合字段 ↔ 第 k 个数字 token
        int idx = -1;
        for (int k = 0; k < fieldOps.Count; k++)
            if (fieldOps[k] == op) { idx = k; break; }
        if (idx < 0 || idx >= tokens.Count) return fallback;
        return tokens[idx].value;
    }

    private static bool TryGetStaticValue(ValueNode v, out int value)
    {
        value = 0;
        if (v == null) return false;
        switch (v.nodeType)
        {
            case ValueNodeType.IntegerConstant: value = v.intValue; return true;
            case ValueNodeType.FloatConstant: value = Mathf.RoundToInt(v.floatValue); return true;
            case ValueNodeType.Add:
                {
                    int a, b;
                    if (!TryGetStaticValue(Operand(v, 0), out a) || !TryGetStaticValue(Operand(v, 1), out b)) return false;
                    value = a + b; return true;
                }
            case ValueNodeType.Subtract:
                {
                    int a, b;
                    if (!TryGetStaticValue(Operand(v, 0), out a) || !TryGetStaticValue(Operand(v, 1), out b)) return false;
                    value = a - b; return true;
                }
            case ValueNodeType.Multiply:
                {
                    int a, b;
                    if (!TryGetStaticValue(Operand(v, 0), out a) || !TryGetStaticValue(Operand(v, 1), out b)) return false;
                    value = a * b; return true;
                }
            default: return true;   // 动态节点（读属性等）：按基础 0 参与，与卡面静态展示一致
        }
    }

    private static ValueNode Operand(ValueNode node, int index)
        => (node != null && node.operands != null && index < node.operands.Count) ? node.operands[index] : null;

    /// <summary>取 EffectNode 展示出的静态数字（用于文案定位）。</summary>
    private static bool TryGetStaticValue(EffectNode node, out int value)
    {
        value = 0;
        if (node == null || node.value == null) return false;
        return TryGetStaticValue(node.value, out value);
    }

    /// <summary>
    /// 设置为敌人卡牌（意图预览用），传入敌人当前力量/敏捷，使描述显示解析后的数值。
    /// </summary>
    public void SetEnemyAttributeContext(int strength, int dexterity)
    {
        _isEnemyCard = true;
        _enemyStrength = strength;
        _enemyDexterity = dexterity;
        if (_entry != null) UpdateDisplay();
    }

    /// <summary>当前描述上下文的力量值</summary>
    private int ContextStrength => _isEnemyCard ? _enemyStrength : (PlayerStrengthProvider?.Invoke() ?? 0);
    /// <summary>当前描述上下文的敏捷值</summary>
    private int ContextDexterity => _isEnemyCard ? _enemyDexterity : (PlayerDexterityProvider?.Invoke() ?? 0);

    public string GetDisplayDescription()
    {
        // 有 CardEntry 时优先使用属性解析描述
        if (_entry != null)
        {
            bool low = _data != null ? _data.isLowSanityForm : _displayLowSanity;
            return _entry.GetResolvedDescription(low, ContextStrength, ContextDexterity, _isEnemyCard);
        }
        // 有 CardData 时尝试从 sourceEntry 解析
        if (_data != null && _data.sourceEntry != null)
        {
            return _data.GetResolvedDescription(ContextStrength, ContextDexterity, _isEnemyCard);
        }
        return string.IsNullOrWhiteSpace(description) ? GetAutoDescription() : description;
    }

    public string GetAutoDescription()
    {
        var sb = new StringBuilder();
        switch (cardType)
        {
            case CardType.Attack:
                int effAtk = (LiveFusion != null && LiveFusion.overrideAttack) ? LiveFusion.attackValue : attackValue;
                // AttributeBased 时叠加当前力量值，显示解析后的实际伤害
                string dmg = attackValueType == ValueType.Fixed
                    ? effAtk.ToString()
                    : (effAtk + ContextStrength).ToString();
                sb.Append($"造成{attackCount}次").Append(dmg).Append("点伤害");
                if (ignoreArmor) sb.Append("\n无视护甲");
                break;
            case CardType.Skill:
                int effArm = (LiveFusion != null && LiveFusion.overrideArmor) ? LiveFusion.armorValue : armorValue;
                // AttributeBased 时叠加当前敏捷值，显示解析后的实际护甲
                string armor = armorValueType == ValueType.Fixed
                    ? effArm.ToString()
                    : (effArm + ContextDexterity).ToString();
                sb.Append($"获得{armor}点护甲");
                break;
            case CardType.Ability:
                foreach (var effect in buffEffects)
                    sb.AppendLine(CardData.GetBuffEffectText(effect));
                string dur = buffDuration switch
                {
                    BuffDurationType.GlobalPermanent => "全局永久",
                    BuffDurationType.BattlePermanent => "局内永久",
                    BuffDurationType.BattleXTurns => $"{buffDurationTurns}回合内",
                    _ => ""
                };
                sb.AppendLine($"时效: {dur}");
                if (buffStacks > 1) sb.AppendLine($"层数: {buffStacks}");
                break;
        }
        var kwNames = CardData.GetKeywordNames(keywords);
        if (kwNames.Count > 0)
            sb.AppendLine($"词条: {string.Join(", ", kwNames)}");
        return sb.ToString().TrimEnd();
    }

    // ========================================================================
    // 颜色辅助
    // ========================================================================

    private Color GetCardTypeColor() => cardType switch
    {
        CardType.Attack => attackColor,
        CardType.Skill => armorColor,
        CardType.Ability => buffColor,
        _ => Color.white
    };

    private Color GetCardGradeColor() => grade switch
    {
        CardGrade.Bronze => bronzeColor,
        CardGrade.Silver => silverColor,
        CardGrade.Gold => goldColor,
        _ => Color.white
    };

    // ========================================================================
    // 数字 token（融合原位高亮用）
    // ========================================================================

    private struct Token
    {
        public int startChar;
        public int endChar;
        public int value;
        public int StartChar => startChar;
        public int EndChar => endChar;
    }

    // ========================================================================
    // Editor 预览（编辑器中修改字段后自动刷新）
    // ========================================================================

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!CanMutateHierarchy(this)) return;
        UnityEditor.EditorApplication.delayCall += OnValidateDelayed;
    }

    private void OnValidateDelayed()
    {
        if (this == null) return;
        // Prefab 资源（含 Play 模式下被 Inspector/导入触发的资源）禁止改层级
        if (!CanMutateHierarchy(this))
            return;
        UpdateDisplay();
    }
#endif
}

/// <summary>全屏画布上唯一的词条说明框，始终画在最上层，定位在卡牌正上方。</summary>
internal static class KeywordTooltipOverlay
{
    private static RectTransform _root;
    private static TextMeshProUGUI _body;
    private static RectTransform _shownFor;
    private static RectTransform _canvasRT;
    private static Camera _canvasCam;

    public static void Show(RectTransform card, string text, Color bgColor, Color textColor, TMP_FontAsset font)
    {
        if (card == null) return;
        Ensure(font, bgColor);
        if (_root == null) return;

        _shownFor = card;
        _body.text = text;
        _body.color = textColor;
        if (font != null) _body.font = font;

        _root.gameObject.SetActive(true);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

        var canvas = card.GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas == null) return;
        _canvasRT = canvas.transform as RectTransform;
        _canvasCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        _root.SetParent(_canvasRT, false);
        _root.SetAsLastSibling();

        Reposition();
    }

    public static void Hide(RectTransform card)
    {
        if (card != null && _shownFor != card) return;
        _shownFor = null;
        if (_root != null)
            _root.gameObject.SetActive(false);
    }

    public static void Reposition()
    {
        if (_root == null || !_root.gameObject.activeSelf || _shownFor == null || _canvasRT == null) return;

        Vector3[] corners = new Vector3[4];
        _shownFor.GetWorldCorners(corners);
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(_canvasCam, topCenter);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRT, screen, _canvasCam, out Vector2 local))
            return;

        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0f);
        _root.anchoredPosition = local + new Vector2(0f, 8f);
    }

    private static void Ensure(TMP_FontAsset font, Color bgColor)
    {
        if (_root != null) return;

        var go = new GameObject("KeywordTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _root = go.GetComponent<RectTransform>();
        _root.sizeDelta = new Vector2(220f, 40f);

        var bg = go.GetComponent<Image>();
        bg.color = bgColor;
        bg.raycastTarget = false;

        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 8, 8);
        vlg.spacing = 0f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var labelGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        _body = labelGo.GetComponent<TextMeshProUGUI>();
        if (font != null) _body.font = font;
        _body.fontSize = 14;
        _body.color = new Color(0.85f, 0.8f, 0.95f, 1f);
        _body.alignment = TextAlignmentOptions.Top;
        _body.enableWordWrapping = true;
        _body.overflowMode = TextOverflowModes.Overflow;
        _body.raycastTarget = false;
        var le = labelGo.AddComponent<LayoutElement>();
        le.preferredWidth = 200f;

        go.SetActive(false);
    }
}