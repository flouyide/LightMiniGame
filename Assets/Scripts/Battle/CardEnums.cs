using System;

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
/// 卡牌品级
/// </summary>
public enum CardGrade
{
    Common,     // 普通
    Fine,       // 优秀
    Rare,       // 精良
    Epic,       // 史诗
    Legendary   // 传说
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
/// 词条类型
/// </summary>
[Flags]
public enum KeywordType
{
    None       = 0,
    Echo       = 1 << 0,  // 回响：卡牌在本回合第一次打出后将回到手牌
    Calamity   = 1 << 1,  // 灾厄：卡牌数值得到提升，但打出后触发负面效果
    Fate       = 1 << 2,  // 命运：卡牌打出后随机触发好运与厄运效果
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
