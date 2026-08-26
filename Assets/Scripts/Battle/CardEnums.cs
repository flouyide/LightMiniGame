using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡牌类型
/// </summary>
public enum CardType
{
    Attack,  // 攻击牌
    Skill,   // 技能牌
    Ability  // 能力牌
}

/// <summary>
/// 卡牌品级：金 / 银 / 铜
/// </summary>
public enum CardGrade
{
    Bronze,     // 铜
    Silver,     // 银
    Gold        // 金
}

/// <summary>
/// 消耗类型
/// </summary>
public enum ConsumeType
{
    None,         // 不消耗（打出后进入弃牌堆）
    ThisBattle,  // 本次战斗消耗
    ThisRun       // 本局游戏消耗
}

/// <summary>
/// 数值计算方式
/// </summary>
public enum ValueType
{
    Fixed,            // 固定x点
    AttributeBased    // (x + 玩家某种属性)点
}

/// <summary>
/// 玩家属性类型
/// </summary>
public enum PlayerAttributeType
{
    Strength,   // 力量（影响攻击）
    Dexterity,   // 敏捷（影响护甲）
    Vitality,   // 体质（影响生命）
    Agility     // 灵巧（影响抽牌等）
}

/// <summary>
/// 增益时效
/// </summary>
public enum BuffDurationType
{
    GlobalPermanent,   // 全局永久生效
    BattlePermanent,   // 局内永久生效
    BattleXTurns        // 局内x回合内生效
}

/// <summary>
/// 词条类型（位标，可叠加）。卡牌默认不带任何词条。
/// </summary>
[Flags]
public enum KeywordType
{
    None          = 0,
    [InspectorName("股神")]
    StockGod      = 1 << 0,  // 股神：融合中只要有股神，股神拿走全部（多名均分），其余为 0
    [InspectorName("韭菜")]
    Leek          = 1 << 1,  // 韭菜：融合时该卡获得最小数值（0）
    [InspectorName("回流")]
    Recycle       = 1 << 2,  // 回流：本回合第一次打出后回到手牌
    [InspectorName("配件")]
    Accessory     = 1 << 3,  // 配件（词条）：打出时若手牌有卡牌「主机」，主机获得本卡效果
    [InspectorName("查阅")]
    Consult       = 1 << 4,  // 查阅：打出时抽 1 张牌
    [InspectorName("内部价")]
    InternalPrice = 1 << 5,  // 内部价：费用 -1
    [InspectorName("贿赂")]
    Bribe         = 1 << 6,  // 贿赂：费用不足时可用 5 货币代替 1 点费用
    [InspectorName("摸鱼")]
    Slack         = 1 << 7,  // 摸鱼：打出后立即结束回合，下回合多抽 1 张
    [InspectorName("监控目标")]
    WatchTarget   = 1 << 8,  // 监控目标：打出后场上所有敌人力量+1（战斗内永久）
}

/// <summary>词条中文名、说明与编辑器枚举映射。</summary>
public static class CardKeywords
{
    public const string HostCardName = "主机";

    public static readonly string[] FlagMaskNames =
    {
        "股神", "韭菜", "回流", "配件", "查阅", "内部价", "贿赂", "摸鱼", "监控目标"
    };

    public static readonly string[] EditorPopupNames =
    {
        "无", "股神", "韭菜", "回流", "配件", "查阅", "内部价", "贿赂", "摸鱼", "监控目标"
    };

    public static readonly string[] FilterPopupNames =
    {
        "全部词条", "无", "股神", "韭菜", "回流", "配件", "查阅", "内部价", "贿赂", "摸鱼", "监控目标"
    };

    public static KeywordType FromEditor(LightMiniGame.CardEditor.CardKeyword k) =>
        (KeywordType)(int)k;

    public static bool Has(KeywordType flags, KeywordType k) => k != KeywordType.None && (flags & k) != 0;

    public static List<string> GetNames(KeywordType keywords)
    {
        var result = new List<string>();
        if (Has(keywords, KeywordType.StockGod)) result.Add("股神");
        if (Has(keywords, KeywordType.Leek)) result.Add("韭菜");
        if (Has(keywords, KeywordType.Recycle)) result.Add("回流");
        if (Has(keywords, KeywordType.Accessory)) result.Add("配件");
        if (Has(keywords, KeywordType.Consult)) result.Add("查阅");
        if (Has(keywords, KeywordType.InternalPrice)) result.Add("内部价");
        if (Has(keywords, KeywordType.Bribe)) result.Add("贿赂");
        if (Has(keywords, KeywordType.Slack)) result.Add("摸鱼");
        if (Has(keywords, KeywordType.WatchTarget)) result.Add("监控目标");
        return result;
    }

    public static string GetTooltip(KeywordType keywords)
    {
        var parts = new List<string>();
        if (Has(keywords, KeywordType.StockGod)) parts.Add("股神：融合中只要有股神，股神拿走全部数值（多名均分），其余为 0");
        if (Has(keywords, KeywordType.Leek)) parts.Add("韭菜：融合时该卡牌将获得最小数值（0）");
        if (Has(keywords, KeywordType.Recycle)) parts.Add("回流：本回合第一次打出后回到手牌");
        if (Has(keywords, KeywordType.Accessory)) parts.Add("配件：打出时若手牌有「主机」，主机获得本卡效果");
        if (Has(keywords, KeywordType.Consult)) parts.Add("查阅：打出时抽 1 张牌");
        if (Has(keywords, KeywordType.InternalPrice)) parts.Add("内部价：费用减少 1");
        if (Has(keywords, KeywordType.Bribe)) parts.Add("贿赂：费用不足时可用 5 货币代替 1 点费用");
        if (Has(keywords, KeywordType.Slack)) parts.Add("摸鱼：打出后立即结束回合，下回合多抽 1 张");
        if (Has(keywords, KeywordType.WatchTarget)) parts.Add("监控目标：打出后场上所有敌人力量+1（战斗内永久）");
        return string.Join("\n", parts);
    }
}

/// <summary>
/// 增益效果类型
/// </summary>
public enum BuffEffectType
{
    IncreaseAttribute,    // 提升x属性
    RestoreActionPoints,  // 回复x行动力
    DrawCards,            // 抽x张牌
    GainArmor,            // 获得x护甲
    HealHP                // 回复x生命
}
