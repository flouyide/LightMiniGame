using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LightMiniGame.CardEditor;

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
    [Tooltip("黑暗卡面（理智转阶段时替换为此图），留空则仅变色")]
    public Sprite darkCardArt;

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

    // === 升级数据来源（从卡牌编辑器读取） ===
    [Header("升级数据来源")]
    [Tooltip("关联的 CardEntry（卡牌编辑器数据），理智转阶段时从其 upgradeEffects 读取升级效果")]
    public CardEntry sourceEntry;

    /// <summary>运行时标记：本张卡是否已升级（不持久化，每场战斗重置）</summary>
    [NonSerialized] public bool isLowSanityForm = false;

    /// <summary>融合覆盖层（战斗内运行时覆盖，不写入SO；进阶1开启后允许跨战斗持久化）</summary>
    [NonSerialized] public FusionCardDelta fusion;

    /// <summary>运行时费用附加（遗物效果如过载+1使用），不持久化；叠加在基础/融合费用之上。</summary>
    [NonSerialized] public int extraCost = 0;

    /// <summary>
    /// 获取当前效果列表（EffectNode 格式）。如果有关联的 CardEntry，从其读取；否则返回 null。
    /// </summary>
    public List<EffectNode> GetEffectNodes(bool lowSanity)
    {
        if (sourceEntry == null) return null;
        return sourceEntry.GetEffectNodes(lowSanity);
    }

    /// <summary>
    /// 获取当前费用。如果有关联的 CardEntry，从其读取；融合覆盖优先。
    /// </summary>
    public int GetEffectiveCost()
    {
        int baseCost = sourceEntry != null ? sourceEntry.GetCost(isLowSanityForm) : actionPointCost;
        int cost = (fusion != null && fusion.overrideCost) ? fusion.cost : baseCost;
        return Mathf.Max(0, cost + extraCost);
    }

    /// <summary>有效攻击值（融合覆盖优先；供显示/执行统一使用）。</summary>
    public int EffectiveAttack
    {
        get
        {
            int baseVal = sourceEntry != null ? normalAttackValue(attackValue, sourceEntry) : attackValue;
            return (fusion != null && fusion.overrideAttack) ? fusion.attackValue : baseVal;
        }
    }

    /// <summary>有效护甲值（融合覆盖优先）。</summary>
    public int EffectiveArmor
    {
        get
        {
            int baseVal = sourceEntry != null ? normalArmorValue(armorValue, sourceEntry) : armorValue;
            return (fusion != null && fusion.overrideArmor) ? fusion.armorValue : baseVal;
        }
    }

    private static int normalAttackValue(int fallback, CardEntry e)
        => ResolveFromEffects(e, EffectOperation.DealDamage, fallback);

    private static int normalArmorValue(int fallback, CardEntry e)
        => ResolveFromEffects(e, EffectOperation.GainBlock, fallback);

    /// <summary>
    /// 从 CardEntry 效果列表里找到首个指定操作的效果节点，若其数值是常量节点则返回该值，
    /// 否则回退到 fallback。这保证融合展示/回填与描述中显示的数值一致（如“造成6点伤害”→6）。
    /// </summary>
    private static int ResolveFromEffects(CardEntry e, EffectOperation op, int fallback)
    {
        if (e == null) return fallback;
        var nodes = e.GetEffectNodes(false);   // 普通形态
        if (nodes == null || nodes.Count == 0) return fallback;
        foreach (var n in nodes)
        {
            if (n == null || !n.enabled || n.operation != op || n.value == null) continue;
            int v;
            if (TryStaticValue(n.value, out v)) return v;
        }
        return fallback;
    }

    /// <summary>若 ValueNode 是“整数常量”，返回其值；否则 false。</summary>
    private static bool TryStaticValue(ValueNode node, out int value)
    {
        value = 0;
        if (node == null) return false;
        if (node.nodeType == ValueNodeType.IntegerConstant) { value = node.intValue; return true; }
        return false;
    }

    /// <summary>是否攻击牌（供融合提供方与执行用）。</summary>
    public bool IsAttackCard()
    {
        if (sourceEntry != null) return sourceEntry.cardType == LightMiniGame.CardEditor.CardType.Attack;
        return cardType == CardType.Attack;
    }

    /// <summary>是否护甲牌。</summary>
    public bool IsSkillCard()
    {
        if (sourceEntry != null) return sourceEntry.cardType == LightMiniGame.CardEditor.CardType.Skill;
        return cardType == CardType.Skill;
    }

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
        CardType.Skill => "技能",
        CardType.Ability => "能力",
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
        if ((keywords & KeywordType.Echo) != 0) result.Add("回响");
        if ((keywords & KeywordType.Calamity) != 0) result.Add("灾厄");
        if ((keywords & KeywordType.Fate) != 0) result.Add("命运");
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

            case CardType.Skill:
                string armor = armorValueType == ValueType.Fixed
                    ? armorValue.ToString()
                    : $"({armorValue}+{GetAttributeName(armorAttribute)})";
                sb.Append($"获得{armor}点护甲");
                break;

            case CardType.Ability:
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
