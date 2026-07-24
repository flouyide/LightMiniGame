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

    [Header("通用属性")]
    [Tooltip("商店价值")] public int value = 10;
    [Tooltip("品级")] public CardGrade grade = CardGrade.Common;
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
    [SerializeField] private Image typeBadgeImage;
    [SerializeField] private Image costBadgeImage;

    [Header("类型颜色")]
    [SerializeField] private Color attackColor = new Color(0.75f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color armorColor = new Color(0.22f, 0.45f, 0.78f, 1f);
    [SerializeField] private Color buffColor = new Color(0.22f, 0.68f, 0.35f, 1f);

    [Header("品级颜色（边框）")]
    [SerializeField] private Color commonColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color fineColor = new Color(0.30f, 0.75f, 0.30f, 1f);
    [SerializeField] private Color rareColor = new Color(0.35f, 0.55f, 1.00f, 1f);
    [SerializeField] private Color epicColor = new Color(0.70f, 0.30f, 0.90f, 1f);
    [SerializeField] private Color legendaryColor = new Color(1.00f, 0.70f, 0.10f, 1f);

    [Header("不可打出状态")]
    [SerializeField] private Color unplayableColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

    // —— 黑暗卡面（理智转阶段时启用，策划可在此配置黑暗素材） ——
    [Header("黑暗卡面（理智转阶段）")]
    [Tooltip("黑暗模式边框 Sprite，留空则仅变色")]
    [SerializeField] private Sprite darkFrameSprite;
    [Tooltip("黑暗模式背景 Sprite，留空则仅变色")]
    [SerializeField] private Sprite darkBackgroundSprite;
    [Tooltip("黑暗模式边框颜色")]
    [SerializeField] private Color darkFrameColor = new Color(0.35f, 0.1f, 0.45f, 1f);
    [Tooltip("黑暗模式背景颜色")]
    [SerializeField] private Color darkBackgroundColor = new Color(0.08f, 0.04f, 0.12f, 0.95f);
    [Tooltip("黑暗模式文本颜色")]
    [SerializeField] private Color darkTextColor = new Color(0.75f, 0.6f, 0.85f, 1f);
    [Tooltip("侵蚀词条徽章颜色")]
    [SerializeField] private Color corruptedBadgeColor = new Color(0.6f, 0.15f, 0.75f, 1f);

    private bool _playable = true;
    private bool _darkMode = false;
    private Sprite _darkCardArt;  // 从 CardData 读入的黑暗卡面

    // 缓存正常模式颜色/精灵，退出黑暗模式时恢复
    private Sprite _origFrameSprite;
    private Sprite _origBgSprite;
    private Color _origFrameColor;
    private Color _origBgColor;

    // ========================================================================
    // 公共方法
    // ========================================================================

    /// <summary>
    /// 设置是否可打出（行动点不足时灰显）
    /// </summary>
    public void SetPlayable(bool playable)
    {
        _playable = playable;
        UpdateDisplay();
    }

    /// <summary>
    /// 开启/关闭黑暗卡面模式（理智转阶段时调用）。
    /// 开启时替换边框/背景 Sprite 与颜色，文本变为暗紫色。
    /// </summary>
    public void SetDarkMode(bool enabled)
    {
        if (_darkMode == enabled) return;
        _darkMode = enabled;

        if (enabled)
        {
            // 缓存原始值
            if (frameImage) { _origFrameSprite = frameImage.sprite; _origFrameColor = frameImage.color; }
            if (backgroundImage) { _origBgSprite = backgroundImage.sprite; _origBgColor = backgroundImage.color; }
        }

        UpdateDisplay();
    }

    /// <summary>
    /// 刷新卡牌UI
    /// </summary>
    public void UpdateDisplay()
    {
        // 基础文本
        if (nameText) nameText.text = cardName;
        if (costText) costText.text = actionPointCost.ToString();
        if (typeText) typeText.text = CardData.GetCardTypeName(cardType);
        if (gradeText) gradeText.text = CardData.GetGradeName(grade);
        if (descText) descText.text = GetDisplayDescription();

        // 词条文本
        if (keywordText)
        {
            var kwNames = CardData.GetKeywordNames(keywords);
            keywordText.text = kwNames.Count > 0 ? string.Join("  ", kwNames) : "";
            keywordText.gameObject.SetActive(kwNames.Count > 0);
        }

        // —— 黑暗模式覆盖 ——
        if (_darkMode)
        {
            ApplyDarkTheme();
            return;
        }

        // —— 正常模式 ——
        // 恢复精灵（从黑暗模式切回时）
        if (frameImage && _origFrameSprite != null) frameImage.sprite = _origFrameSprite;
        if (backgroundImage && _origBgSprite != null) backgroundImage.sprite = _origBgSprite;

        Color typeColor = GetCardTypeColor();
        if (typeBadgeImage) typeBadgeImage.color = typeColor;
        if (costBadgeImage) costBadgeImage.color = _playable ? typeColor : new Color(0.5f, 0.5f, 0.5f, 1f);

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

    /// <summary>应用黑暗卡面主题：替换边框/背景精灵与颜色，文本变为暗紫色</summary>
    private void ApplyDarkTheme()
    {
        Color typeColor = GetCardTypeColor();

        // 边框：替换精灵 + 黑暗颜色
        if (frameImage)
        {
            if (darkFrameSprite != null) frameImage.sprite = darkFrameSprite;
            frameImage.color = _playable ? darkFrameColor : new Color(0.2f, 0.08f, 0.25f, 1f);
        }

        // 背景：替换精灵 + 黑暗颜色
        if (backgroundImage)
        {
            if (darkBackgroundSprite != null) backgroundImage.sprite = darkBackgroundSprite;
            backgroundImage.color = darkBackgroundColor;
        }

        // 类型徽章 / 费用徽章
        if (typeBadgeImage) typeBadgeImage.color = darkFrameColor;
        if (costBadgeImage) costBadgeImage.color = _playable ? darkFrameColor : new Color(0.3f, 0.15f, 0.35f, 1f);

        // 卡牌插图：优先使用黑暗卡面，无则叠加紫色滤镜
        if (artImage)
        {
            if (_darkCardArt != null)
            {
                artImage.sprite = _darkCardArt;
                artImage.color = Color.white;
            }
            else if (cardArt != null)
            {
                artImage.sprite = cardArt;
                artImage.color = new Color(0.5f, 0.4f, 0.6f, 1f);  // 紫色滤镜
            }
            else
            {
                artImage.color = new Color(0.15f, 0.08f, 0.2f, 0.3f);
            }
        }

        // 文本颜色
        if (nameText) nameText.color = darkTextColor;
        if (descText) descText.color = darkTextColor;
        if (costText) costText.color = darkTextColor;
        if (typeText) typeText.color = darkTextColor;
        if (gradeText) gradeText.color = darkTextColor;

        // 灾厄词条高亮
        if (keywordText && (keywords & KeywordType.Calamity) != 0)
            keywordText.color = corruptedBadgeColor;
        else if (keywordText)
            keywordText.color = darkTextColor;
    }
    
    /// <summary>
    /// 从 CardData ScriptableObject 复制全部字段到本组件并刷新显示
    /// </summary>
    public void ApplyCardData(CardData data)
    {
        if (data == null) return;

        // 如果有关联的 CardEntry，优先从 CardEntry 读取显示数据
        if (data.sourceEntry != null)
        {
            ApplyCardEntry(data.sourceEntry, data.isUpgraded);
            // 仍然复制运行时字段（费用可能被修改过）
            actionPointCost = data.GetEffectiveCost();
            UpdateDisplay();
            return;
        }

        cardName = data.cardName;
        description = data.description;
        cardType = data.cardType;
        cardArt = data.cardArt;
        _darkCardArt = data.darkCardArt;
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
    }

    /// <summary>
    /// 从 CardEntry（卡牌编辑器数据）读取显示信息并刷新。
    /// </summary>
    public void ApplyCardEntry(CardEntry entry, bool upgraded = false)
    {
        if (entry == null) return;

        cardName = entry.cardName;
        description = entry.GetDescription(upgraded);
        cardArt = entry.cardArt;
        actionPointCost = entry.GetCost(upgraded);

        // 映射品级
        grade = entry.grade switch
        {
            LightMiniGame.CardEditor.CardGrade.Bronze => CardGrade.Common,
            LightMiniGame.CardEditor.CardGrade.Silver => CardGrade.Fine,
            LightMiniGame.CardEditor.CardGrade.Gold => CardGrade.Rare,
            _ => CardGrade.Common
        };

        // 映射卡牌类型（与编辑器统一）
        cardType = entry.cardType switch
        {
            LightMiniGame.CardEditor.CardType.Attack => CardType.Attack,
            LightMiniGame.CardEditor.CardType.Skill => CardType.Skill,
            LightMiniGame.CardEditor.CardType.Ability => CardType.Ability,
            _ => CardType.Attack
        };

        // 黑暗卡面
        _darkCardArt = entry.darkCardArt;

        // 词条映射（3词条：回响/灾厄/命运）
        keywords = KeywordType.None;
        if (entry.keyword == LightMiniGame.CardEditor.CardKeyword.Echo)
            keywords |= KeywordType.Echo;
        if (entry.keyword == LightMiniGame.CardEditor.CardKeyword.Calamity)
            keywords |= KeywordType.Calamity;
        if (entry.keyword == LightMiniGame.CardEditor.CardKeyword.Fate)
            keywords |= KeywordType.Fate;

        UpdateDisplay();
    }

    // ========================================================================
    // 描述生成（复用CardData的静态方法）
    // ========================================================================

    public string GetDisplayDescription()
    {
        return string.IsNullOrWhiteSpace(description) ? GetAutoDescription() : description;
    }

    public string GetAutoDescription()
    {
        var sb = new StringBuilder();
        switch (cardType)
        {
            case CardType.Attack:
                string dmg = attackValueType == ValueType.Fixed
                    ? attackValue.ToString()
                    : $"({attackValue}+{CardData.GetAttributeName(attackAttribute)})";
                sb.Append($"造成{attackCount}次").Append(dmg).Append("点伤害");
                if (ignoreArmor) sb.Append("\n无视护甲");
                break;
            case CardType.Skill:
                string armor = armorValueType == ValueType.Fixed
                    ? armorValue.ToString()
                    : $"({armorValue}+{CardData.GetAttributeName(armorAttribute)})";
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
        CardGrade.Common => commonColor,
        CardGrade.Fine => fineColor,
        CardGrade.Rare => rareColor,
        CardGrade.Epic => epicColor,
        CardGrade.Legendary => legendaryColor,
        _ => Color.white
    };

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
