using System;

/// <summary>
/// 卡牌类型
/// </summary>
public enum CardType
{
    Attack,  // 攻击牌
    Armor,   // 护甲牌
    Buff     // 增益牌
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
    Pierce     = 1 << 0,  // 穿透：无视敌人护甲
    Lifesteal  = 1 << 1,  // 吸血：回复造成伤害的一定比例
    Combo      = 1 << 2,  // 连击：额外攻击次数+1
    Heavy      = 1 << 3,  // 重击：暴击概率提升
    Swift      = 1 << 4,  // 迅捷：抽牌+1
    Sturdy     = 1 << 5,  // 坚固：护甲不随回合消失
    Toxic      = 1 << 6,  // 剧毒：附加中毒效果
    Burning    = 1 << 7,  // 燃烧：附加燃烧效果
    FreePlay   = 1 << 8,  // 免费打出：不消耗行动点
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
