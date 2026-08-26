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
    /// <summary>是否为敌人卡牌（意图预览用）</summary>
    private bool _isEnemyCard;
    /// <summary>敌人力量（意图预览用）</summary>
    private int _enemyStrength;
    /// <summary>敌人敏捷（意图预览用）</summary>
    private int _enemyDexterity;
    /// <summary>当前显示低理智形态（ApplyCardEntry 的 upgraded 参数）</summary>
    private bool _displayLowSanity;

    /// <summary>实时融合覆盖层：优先读 CardData.fusion（融合可能新建覆盖层使旧引用失效），否则用缓存。</summary>
    private FusionCardDelta LiveFusion => _data != null && _data.fusion != null ? _data.fusion : _fusion;

    private CardData _data;  // 源 CardData（融合精确数字定位用）
    private LightMiniGame.CardEditor.CardEntry _entry;   // 源 CardEntry（融合感知描述用）
    private Sprite _descBoxSprite;  // 从 CardData/CardEntry 读入的中层描述框
    private Sprite _typeBoxSprite;  // 从 CardData/CardEntry 读入的顶层类型框

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
        if (keywordTooltip == null) return;
        if (!show) { keywordTooltip.SetActive(false); return; }

        var desc = GetKeywordTooltipText();
        if (string.IsNullOrEmpty(desc)) { keywordTooltip.SetActive(false); return; }

        if (tooltipText != null)
        {
            tooltipText.text = desc;
            tooltipText.color = tooltipTextColor;
        }
        keywordTooltip.SetActive(true);
    }

    /// <summary>生成词条提示文本</summary>
    private string GetKeywordTooltipText()
    {
        return CardKeywords.GetTooltip(keywords);
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
            costText.text = _data != null
                ? _data.GetEffectiveCost().ToString()
                : actionPointCost.ToString();
        if (typeText) typeText.text = CardData.GetCardTypeName(cardType);
        if (gradeText) gradeText.text = CardData.GetGradeName(grade);
        if (descText) descText.text = GetFusionAwareDescription();

        // 词条文本
        if (keywordText)
        {
            var kwNames = CardData.GetKeywordNames(keywords);
            keywordText.text = kwNames.Count > 0 ? string.Join("  ", kwNames) : "";
            keywordText.gameObject.SetActive(kwNames.Count > 0);
        }

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

        // 如果有关联的 CardEntry，优先从 CardEntry 读取显示数据
        if (data.sourceEntry != null)
        {
            ApplyCardEntry(data.sourceEntry, data.isLowSanityForm);
            // 运行时字段覆盖（费用可能被修改过，keywords 可能被理智转阶段改过）
            actionPointCost = data.GetEffectiveCost();
            keywords = data.keywords;
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

        // 词条映射（默认无词条）
        keywords = CardKeywords.FromEditor(entry.keyword);
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
    /// 融合感知描述：若存在融合覆盖，按效果节点顺序把文案中对应数字替换为融合后的值。
    /// 无融合覆盖或无法解析时回退原文案。
    /// </summary>
    public string GetFusionAwareDescription()
    {
        if (LiveFusion == null || !LiveFusion.HasAny || _entry == null)
            return GetDisplayDescription();

        // 使用属性解析后的描述作为基础文本（力量/敏捷已替换为实际数值）
        var desc = GetDisplayDescription();
        if (string.IsNullOrEmpty(desc)) return desc;

        bool low = _data != null && _data.isLowSanityForm;
        var nodes = _entry.GetEffectNodes(low);
        if (nodes == null || nodes.Count == 0) return desc;

        int str = ContextStrength;
        int dex = ContextDexterity;

        var replacements = new List<(int start, int len, string newVal)>();
        int searchFrom = 0;
        foreach (var node in nodes)
        {
            if (node == null || !node.enabled) continue;
            int oldVal;
            bool fuseSet = false;
            int newVal = 0;
            switch (node.operation)
            {
                case LightMiniGame.CardEditor.EffectOperation.DealDamage:
                    if (LiveFusion.overrideAttack) { fuseSet = true; newVal = LiveFusion.attackValue; }
                    break;
                case LightMiniGame.CardEditor.EffectOperation.GainBlock:
                    if (LiveFusion.overrideArmor) { fuseSet = true; newVal = LiveFusion.armorValue; }
                    break;
                case LightMiniGame.CardEditor.EffectOperation.ModifyAttribute:
                    if (LiveFusion.overrideBuff) { fuseSet = true; newVal = LiveFusion.buffValue; }
                    break;
                case LightMiniGame.CardEditor.EffectOperation.DrawCards:
                    if (LiveFusion.overrideDraw) { fuseSet = true; newVal = LiveFusion.drawCount; }
                    break;
                case LightMiniGame.CardEditor.EffectOperation.RestoreActionPoints:
                    if (LiveFusion.overrideRestore) { fuseSet = true; newVal = LiveFusion.restoreAP; }
                    break;
            }
            if (!fuseSet) continue;

            // 取节点属性解析后的值（与描述文本中显示的数值一致，用于定位替换）
            oldVal = ComputeResolvedEffectValue(node, str, dex, _isEnemyCard);

            // 从上次位置向后找值为 oldVal 的数字 token
            int pos = FindNumberToken(desc, oldVal, searchFrom);
            if (pos < 0) continue;   // 找不到匹配位置，跳过该替换
            int start = pos;
            int len = 0;
            if (start > 0 && desc[start - 1] == '-') { start--; len++; }   // 吸收负号（一般不会，防御）
            while (start + len < desc.Length && char.IsDigit(desc[start + len])) len++;
            replacements.Add((start, len, newVal.ToString()));
            searchFrom = start + len;
        }
        // 从后往前替换，避免索引错乱
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
    /// 计算效果节点的属性解析后数值（与描述文本中显示的数值一致）。
    /// DealDamage 叠加力量（scalingMode==AddStrength 或敌人牌），GainBlock 叠加敏捷。
    /// </summary>
    private int ComputeResolvedEffectValue(LightMiniGame.CardEditor.EffectNode node, int str, int dex, bool isEnemy)
    {
        if (node == null || node.value == null) return 0;
        return LightMiniGame.CardEditor.ValueNode.ResolveCombatValue(
            node.value, node.operation, node.scalingMode, str, dex, isEnemy);
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
        // 延迟调用，确保所有字段已更新
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            UpdateDisplay();
        };
    }
#endif
}