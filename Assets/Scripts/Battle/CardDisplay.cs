using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private bool _playable = true;

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

        // 类型颜色
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
    }

    /// <summary>
    /// 从 CardData ScriptableObject 复制全部字段到本组件并刷新显示
    /// </summary>
    public void ApplyCardData(CardData data)
    {
        if (data == null) return;
        cardName = data.cardName;
        description = data.description;
        cardType = data.cardType;
        cardArt = data.cardArt;
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
            case CardType.Armor:
                string armor = armorValueType == ValueType.Fixed
                    ? armorValue.ToString()
                    : $"({armorValue}+{CardData.GetAttributeName(armorAttribute)})";
                sb.Append($"获得{armor}点护甲");
                break;
            case CardType.Buff:
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
        CardType.Armor => armorColor,
        CardType.Buff => buffColor,
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
