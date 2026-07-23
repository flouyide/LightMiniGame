namespace LightMiniGame.CardEditor
{
    // ========================================================================
    // 品级
    // ========================================================================
    public enum CardGrade { Bronze, Silver, Gold }

    // ========================================================================
    // 卡牌类型
    // ========================================================================
    public enum CardType { Attack, Skill, Ability }

    // ========================================================================
    // 存在形式
    // ========================================================================
    public enum CardExistence { Normal, BattleRemove, PermanentRemove }

    // ========================================================================
    // 词条
    // ========================================================================
    public enum CardKeyword { None, Echo }

    // ========================================================================
    // 效果类型
    // ========================================================================
    public enum EffectType
    {
        Damage,             // 伤害
        Armor,              // 护甲
        DrawCard,           // 抽牌
        RestoreEnergy,      // 回费
        ModifyAttribute,    // 修改属性
        ModifyHandCost,     // 修改手牌费用
        ApplyStatus,        // 施加状态
        Custom              // 自定义效果
    }

    // ========================================================================
    // 效果发起者
    // ========================================================================
    public enum EffectSource
    {
        Player,             // 玩家
        SelectedEnemy,      // 选定的敌人
        RandomEnemy,         // 随机敌人
        AllEnemies,          // 所有敌人
        Custom               // 自定义脚本决定
    }

    // ========================================================================
    // 效果目标
    // ========================================================================
    public enum EffectTarget
    {
        Player,             // 玩家当前角色
        SelectedEnemy,      // 选定的敌人
        RandomEnemy,         // 随机敌人
        AllEnemies,          // 所有敌人
        SwitchedCharacter,  // 切换后的角色
        SourceSelf,          // 效果发起者自身
        Custom              // 自定义脚本决定
    }

    // ========================================================================
    // 数值表达式类型
    // ========================================================================
    public enum ValueFormulaType
    {
        Fixed,                      // 固定值
        BasePlusAttribute,          // 基础值 + 某属性
        BasePlusAttributeTimesCoeff,// 基础值 + 某属性 × 系数
        BasePlusCounterTimesCoeff   // 基础值 + 某计数 × 系数
    }

    // ========================================================================
    // 可引用属性 / 计数
    // ========================================================================
    public enum AttributeRef
    {
        Strength,                   // 力量
        Dexterity,                  // 敏捷
        CurrentHP,                  // 当前生命
        MaxHP,                      // 最大生命
        LostHP,                     // 已损失生命
        CurrentSanity,              // 当前理智
        SanityLostThisTurn,         // 本回合已损失理智
        TotalSanityLostThisBattle,  // 本场战斗累计失去理智
        BleedValue,                 // 流血值
        ArmorBreakValue,            // 破甲值
        CritRate,                   // 暴击率
        CritDamage                  // 暴击伤害
    }

    // ========================================================================
    // 比较方式
    // ========================================================================
    public enum ComparisonOp
    {
        Less,           // 小于
        LessEqual,      // 小于等于
        Equal,          // 等于
        GreaterEqual,   // 大于等于
        Greater,        // 大于
        NotEqual         // 不等于
    }

    // ========================================================================
    // 条件类型
    // ========================================================================
    public enum ConditionType
    {
        SourceAttributeCheck,   // 效果发起者的某个属性达到指定值
        TargetAttributeCheck,   // 效果目标的某个属性达到指定值
        EventOccurred,          // 某个事件发生
        SourceHasStatus,        // 效果发起者是否处于某状态
        TargetHasStatus,        // 效果目标是否处于某状态
        TurnCounterCheck,       // 本回合计数达到指定值
        BattleCounterCheck,     // 本场战斗计数达到指定值
        Custom                  // 自定义脚本条件
    }

    // ========================================================================
    // 条件逻辑
    // ========================================================================
    public enum ConditionLogic { All, Any }

    // ========================================================================
    // 生效时机
    // ========================================================================
    public enum EffectTiming
    {
        Immediate,          // 当前立即生效
        NextTurnStart,      // 下回合开始时生效
        NextAttack,         // 下次攻击时生效
        NextCard,           // 下一张牌生效
        NextSwitch,        // 下次切换角色时生效
        EverySwitch         // 每次切换角色时生效
    }

    // ========================================================================
    // 持续时间
    // ========================================================================
    public enum EffectDuration
    {
        OneAction,          // 持续一次行动
        SpecifiedTurns,     // 持续指定回合
        BattlePermanent,    // 本次战斗持续生效
        GamePermanent       // 本局游戏永久生效
    }

    // ========================================================================
    // 状态类型
    // ========================================================================
    public enum StatusType
    {
        Bleed,                  // 流血
        ArmorBreak,             // 破甲
        Strength,               // 力量
        Dexterity,              // 敏捷
        Insane,                 // 疯狂
        NextAttackDamageBoost,  // 下次攻击增伤
        NextCardCostReduce,     // 下一张牌减费
        CritRateBoost,          // 暴击率提升
        CritDamageBoost         // 暴击伤害提升
    }

    // ========================================================================
    // 增减益目标
    // ========================================================================
    public enum BuffTarget
    {
        Player,             // 玩家
        Enemy,              // 敌人
        CurrentHand,        // 当前手牌
        NextCard,           // 下一张打出的牌
        SpecifiedCardType,  // 指定类型的牌
        Custom              // 自定义目标
    }

    // ========================================================================
    // 修改方式
    // ========================================================================
    public enum ModifyMethod
    {
        Add,        // 增加
        Subtract,   // 减少
        Multiply,   // 乘算
        Override    // 覆盖
    }

    // ========================================================================
    // 可修改属性
    // ========================================================================
    public enum ModifiableAttribute
    {
        Strength,           // 力量
        Dexterity,          // 敏捷
        PlayerCritRate,     // 玩家暴击率
        EnemyCritRate,      // 敌人被暴击率
        PlayerCritDamage,   // 玩家暴击伤害
        EnemyCritDamage,    // 敌人受到的暴击伤害
        ArmorBreakValue,    // 破甲值
        MaxHP,               // 最大生命值
        CurrentHP,           // 当前生命值
        DrawPerTurn,         // 每回合抽牌数
        EnergyPerTurn,       // 每回合获得能量
        BleedValue,          // 流血值
        Currency,            // 货币
        HandCost             // 手牌费用
    }

    // ========================================================================
    // 手牌费用目标
    // ========================================================================
    public enum HandCostTarget
    {
        NextCard,       // 下一张打出的牌
        CurrentHand     // 当前手牌
    }

    // ========================================================================
    // 能力触发时机
    // ========================================================================
    public enum AbilityTrigger
    {
        TurnStart,               // 回合开始时
        TurnEnd,                 // 回合结束时
        OnCrit,                  // 暴击时
        OnLoseSanity,            // 失去理智时
        OnSanityBelowThreshold,  // 理智低于指定值时
        OnReceiveDebuff,         // 获得减益时
        FirstAttackEachTurn,     // 每回合第一次攻击时
        OnApplyArmorBreak,       // 给敌人施加破甲时
        Custom                   // 自定义事件
    }
}
