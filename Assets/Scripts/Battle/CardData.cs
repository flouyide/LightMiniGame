using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 增益效果数据
/// </summary>
[Serializable]
public class BuffEffect
{
    public BuffEffectType effectType;
    public int value;
    [Tooltip("当effectType为IncreaseAttribute时生效")]
    public PlayerAttributeType targetAttribute;
}

/// <summary>
/// 卡牌数据 ScriptableObject —— 通过 Inspector 配置创建新卡牌
/// </summary>
[CreateAssetMenu(menuName = "CardGame/Card Data", fileName = "NewCard")]
public class CardData : ScriptableObject
{
    [Header("基础信息")]
    public string cardName;
    [TextArea(2, 4)] public string description;
    public CardType cardType;
    public Sprite cardArt;

    [Header("通用属性")]
    [Tooltip("商店价值")]
    public int value = 10;
    [Tooltip("品级")]
    public CardGrade grade = CardGrade.Common;
    [Tooltip("需要消耗的行动点")]
    public int actionPointCost = 1;
    [Tooltip("消耗类型")]
    public ConsumeType consumeType = ConsumeType.None;
    [Tooltip("词条（不同词条具有不同效果）")]
    public KeywordType keywords = KeywordType.None;

    // === 攻击牌属性 ===
    [Header("攻击属性")]
    [Tooltip("攻击次数")]
    public int attackCount = 1;
    [Tooltip("攻击数值计算方式")]
    public ValueType attackValueType = ValueType.Fixed;
    [Tooltip("基础攻击数值")]
    public int attackValue = 5;
    [Tooltip("当attackValueType为AttributeBased时，附加的玩家属性")]
    public PlayerAttributeType attackAttribute = PlayerAttributeType.Strength;
    [Tooltip("攻击是否无视敌人护甲")]
    public bool ignoreArmor = false;

    // === 护甲牌属性 ===
    [Header("护甲属性")]
    [Tooltip("护甲值计算方式")]
    public ValueType armorValueType = ValueType.Fixed;
    [Tooltip("基础护甲值")]
    public int armorValue = 5;
    [Tooltip("当armorValueType为AttributeBased时，附加的玩家属性")]
    public PlayerAttributeType armorAttribute = PlayerAttributeType.Dexterity;

    // === 增益牌属性 ===
    [Header("增益属性")]
    [Tooltip("增益时效")]
    public BuffDurationType buffDuration = BuffDurationType.BattlePermanent;
    [Tooltip("当buffDuration为BattleXTurns时生效的回合数")]
    public int buffDurationTurns = 3;
    [Tooltip("增益层数")]
    public int buffStacks = 1;
    [Tooltip("增益效果列表")]
    public List<BuffEffect> buffEffects = new List<BuffEffect>();

    /// <summary>
    /// 获取品级中文名
    /// </summary>
    public static string GetGradeName(CardGrade grade) => grade switch
    {
        CardGrade.Common => "普通",
        CardGrade.Fine => "优秀",
        CardGrade.Rare => "精良",
        CardGrade.Epic => "史诗",
        CardGrade.Legendary => "传说",
        _ => "未知"
    };

    /// <summary>
    /// 获取卡牌类型中文名
    /// </summary>
    public static string GetCardTypeName(CardType type) => type switch
    {
        CardType.Attack => "攻击",
        CardType.Armor => "护甲",
        CardType.Buff => "增益",
        _ => "未知"
    };

    /// <summary>
    /// 获取消耗类型中文名
    /// </summary>
    public static string GetConsumeTypeName(ConsumeType type) => type switch
    {
        ConsumeType.None => "不消耗",
        ConsumeType.ThisBattle => "本战消耗",
        ConsumeType.ThisRun => "本局消耗",
        _ => "未知"
    };

    /// <summary>
    /// 获取属性中文名
    /// </summary>
    public static string GetAttributeName(PlayerAttributeType attr) => attr switch
    {
        PlayerAttributeType.Strength => "力量",
        PlayerAttributeType.Dexterity => "敏捷",
        PlayerAttributeType.Vitality => "体质",
        PlayerAttributeType.Agility => "灵巧",
        _ => "未知"
    };

    /// <summary>
    /// 获取词条中文名列表
    /// </summary>
    public static List<string> GetKeywordNames(KeywordType keywords)
    {
        var result = new List<string>();
        if ((keywords & KeywordType.Pierce) != 0) result.Add("穿透");
        if ((keywords & KeywordType.Lifesteal) != 0) result.Add("吸血");
        if ((keywords & KeywordType.Combo) != 0) result.Add("连击");
        if ((keywords & KeywordType.Heavy) != 0) result.Add("重击");
        if ((keywords & KeywordType.Swift) != 0) result.Add("迅捷");
        if ((keywords & KeywordType.Sturdy) != 0) result.Add("坚固");
        if ((keywords & KeywordType.Toxic) != 0) result.Add("剧毒");
        if ((keywords & KeywordType.Burning) != 0) result.Add("燃烧");
        if ((keywords & KeywordType.FreePlay) != 0) result.Add("免费");
        return result;
    }

    /// <summary>
    /// 获取增益效果描述文本
    /// </summary>
    public static string GetBuffEffectText(BuffEffect effect)
    {
        return effect.effectType switch
        {
            BuffEffectType.IncreaseAttribute => $"提升{GetAttributeName(effect.targetAttribute)}{effect.value}点",
            BuffEffectType.RestoreActionPoints => $"回复{effect.value}行动力",
            BuffEffectType.DrawCards => $"抽{effect.value}张牌",
            BuffEffectType.GainArmor => $"获得{effect.value}点护甲",
            BuffEffectType.HealHP => $"回复{effect.value}点生命",
            _ => "未知效果"
        };
    }

    /// <summary>
    /// 自动生成卡牌描述文本
    /// </summary>
    public string GetAutoDescription()
    {
        var sb = new StringBuilder();

        switch (cardType)
        {
            case CardType.Attack:
                string dmg = attackValueType == ValueType.Fixed
                    ? attackValue.ToString()
                    : $"({attackValue}+{GetAttributeName(attackAttribute)})";
                sb.Append($"造成{attackCount}次").Append(dmg).Append("点伤害");
                if (ignoreArmor) sb.Append("\n无视护甲");
                break;

            case CardType.Armor:
                string armor = armorValueType == ValueType.Fixed
                    ? armorValue.ToString()
                    : $"({armorValue}+{GetAttributeName(armorAttribute)})";
                sb.Append($"获得{armor}点护甲");
                break;

            case CardType.Buff:
                foreach (var effect in buffEffects)
                    sb.AppendLine(GetBuffEffectText(effect));
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

        var kwNames = GetKeywordNames(keywords);
        if (kwNames.Count > 0)
            sb.AppendLine($"词条: {string.Join(", ", kwNames)}");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 获取完整描述（优先使用自定义描述，为空时自动生成）
    /// </summary>
    public string GetDisplayDescription()
    {
        return string.IsNullOrWhiteSpace(description) ? GetAutoDescription() : description;
    }
}
