using System;

namespace LightMiniGame.CardEditor
{
    // ========================================================================
    // 卡牌稀有度
    // ========================================================================
    public enum CardRarity { Common, Rare, Legendary }

    // ========================================================================
    // 卡牌类型
    // ========================================================================
    public enum CardType2 { Attack, Skill, Ability }

    // ========================================================================
    // 卡牌存在形式
    // ========================================================================
    public enum CardRemovalType { Normal, ExhaustThisCombat, RemovePermanently }

    // ========================================================================
    // 卡牌词条
    // ========================================================================
    public enum CardKeyword2
    {
        None,
        StockGod,
        Leek,
        Recycle,
        Accessory,
        Consult,
        InternalPrice,
        Bribe,
        Slack,
        WatchTarget
    }

    // ========================================================================
    // 费用类型
    // ========================================================================
    public enum CardCostType { Fixed, XCost, Free, Dynamic }

    // ========================================================================
    // 玩家属性（可被修正的基础属性）
    // ========================================================================
    public enum PlayerAttributeType
    {
        MaxHealth,
        Strength,
        Dexterity,
        Recovery,
        LifeSteal,
        CriticalChance,
        CriticalDamageMultiplier,
        ActionPointsPerTurn,
        CardsDrawnPerTurn,
        TotalDamageMultiplier,
        IncomingDamageMultiplier
    }

    // ========================================================================
    // 玩家资源（有当前数值的资源）
    // ========================================================================
    public enum PlayerResourceType
    {
        CurrentHealth,
        Sanity,
        ActionPoints,
        Currency,
        Heat,
        Block,
        Fortune          // 福报值
    }

    // ========================================================================
    // 状态类型
    // ========================================================================
    public enum StatusType2
    {
        ArmorBreak,
        Bleed,
        Jammed,
        Madness,
        Vulnerable,
        TemporaryStrength,
        TemporaryDexterity,
        NextAttackDamageBonus,
        NextAttackCriticalDamageBonus,
        NextAttackGuaranteedCritical,
        NextCardCostModifier,
        NextAttackCardCostModifier,
        HandCostModifier,
        CriticalChanceModifier,
        CriticalDamageModifier,
        BlockRetention,
        CustomStatus,
        Fatigue                 // 疲惫（层数，每轮扣等量血并 -1 层）
    }

    // ========================================================================
    // 状态叠加方式
    // ========================================================================
    public enum StatusStackMode
    {
        AddStacks,
        AddValue,
        Replace,
        KeepHigher,
        KeepLower,
        RefreshDuration,
        IndependentInstances
    }

    // ========================================================================
    // 战斗计数器
    // ========================================================================
    public enum CombatCounterType
    {
        CardsPlayedThisTurn,
        AttackCardsPlayedThisTurn,
        SkillCardsPlayedThisTurn,
        AbilityCardsPlayedThisTurn,
        AttacksPerformedThisTurn,
        HitsPerformedThisTurn,
        CriticalHitsThisTurn,
        DamageTakenThisTurn,
        DamageInstancesTakenThisTurn,
        DamageDealtThisTurn,
        SanityLostThisTurn,
        SanityLostThisCombat,
        HeatGainedThisTurn,
        HeatLostThisTurn,
        CharactersSwitchedThisTurn,
        CharactersSwitchedThisCombat,
        EnemiesKilledThisTurn,
        EnemiesKilledThisCombat,
        BlockGainedThisTurn,
        BlockLostThisTurn,
        CardsDrawnThisTurn,
        CardsDiscardedThisTurn,
        CardsExhaustedThisTurn,
        CurrentAttackCardIndexThisTurn,
        CurrentHitIndex,
        CurrentHitCount
    }

    // ========================================================================
    // 运行时布尔标志
    // ========================================================================
    public enum CombatFlagType
    {
        TookDamageThisTurn,
        AttackedThisTurn,
        PlayedCardThisTurn,
        SwitchedCharacterThisTurn,
        CurrentHitWasCritical,
        CurrentAttackHadAnyCriticalHit,
        CurrentAttackKilledEnemy,
        IsLowSanity,
        IsOverheated,
        IsFirstAttackThisTurn,
        IsFirstAttackCardThisTurn
    }

    // ========================================================================
    // 敌人属性
    // ========================================================================
    public enum EnemyAttributeType
    {
        MaxHealth,
        Strength,
        Dexterity,
        CriticalChance,
        CriticalDamageMultiplier,
        TotalDamageMultiplier,
        IncomingDamageMultiplier
    }

    // ========================================================================
    // 敌人资源
    // ========================================================================
    public enum EnemyResourceType
    {
        CurrentHealth,
        Block
    }

    // ========================================================================
    // 效果操作类型
    // ========================================================================
    public enum EffectOperation
    {
        DealDamage,
        GainBlock,
        ModifyAttribute,
        ModifyResource,
        ApplyStatus,
        RemoveStatus,
        DrawCards,
        RestoreActionPoints,
        MoveCards,
        CreateCard,
        CopyCard,
        PlayCardAutomatically,
        ReplayCurrentCard,
        ModifyCardCost,
        ModifyCardProperty,
        SwitchCharacter,
        RegisterTrigger,
        RemoveTrigger,
        SetVariable,
        ModifyVariable,
        CustomOperation
    }

    // ========================================================================
    // 执行时机
    // ========================================================================
    public enum ExecutionTiming
    {
        Immediate,
        AfterCurrentEffect,
        AfterCurrentCard,
        EndOfCurrentAction,
        TurnStart,
        TurnEnd,
        CombatEnd,
        CustomTiming
    }

    // ========================================================================
    // 触发事件（统一事件系统）
    // ========================================================================
    public enum TriggerEvent
    {
        // 卡牌事件
        OnCardPlayAttempt,
        OnCardPlayed,
        OnAttackCardPlayed,
        OnSkillCardPlayed,
        OnAbilityCardPlayed,
        OnCardDrawn,
        OnCardDiscarded,
        OnCardExhausted,
        OnCardCostPaid,
        // 攻击事件
        BeforeAttack,
        BeforeHit,
        OnHit,
        OnCriticalHit,
        OnDamageDealt,
        AfterHit,
        AfterAttack,
        OnEnemyKilled,
        // 防御事件
        OnBlockGained,
        OnBlockLost,
        OnDamageTaken,
        OnHealthLost,
        // 资源事件
        OnSanityChanged,
        OnSanityLost,
        OnHeatChanged,
        OnHeatGained,
        OnHeatReduced,
        OnOverload,
        // 回合事件
        OnTurnStart,
        OnTurnEnd,
        OnFirstAttackThisTurn,
        OnNthAttackThisTurn,
        // 状态事件
        OnStatusApplied,
        OnDebuffApplied,
        OnArmorBreakApplied,
        OnBleedApplied,
        // 角色事件
        BeforeCharacterSwitch,
        AfterCharacterSwitch,
        OnCharacterActivated,
        OnCharacterDeactivated,
        // 战斗事件
        OnCombatStart,
        OnCombatEnd
    }

    // ========================================================================
    // 持续时间类型
    // ========================================================================
    public enum DurationType
    {
        Instant,
        NextTrigger,
        TriggerCount,
        CurrentTurn,
        Turns,
        UntilCharacterSwitch,
        UntilCombatEnd,
        PermanentRun
    }

    // ========================================================================
    // 目标类别
    // ========================================================================
    public enum TargetCategory
    {
        CombatUnit,
        Character,
        Enemy,
        Card,
        CardZone,
        Trigger,
        Variable,
        Global
    }

    // ========================================================================
    // 战斗单位目标
    // ========================================================================
    public enum CombatUnitTarget
    {
        CurrentCharacter,
        SwitchedInCharacter,
        SwitchedOutCharacter,
        SelectedEnemy,
        RandomEnemy,
        AllEnemies,
        EffectSource,
        CurrentAttackTarget,
        EnemyKilledByCurrentEffect,
        AllCharacters,
        SpecificCharacter,
        LowestHPEnemy,
        HighestHPEnemy,
        HighestArmorBreakEnemy,
        RandomNEnemies
    }

    // ========================================================================
    // 卡牌目标
    // ========================================================================
    public enum CardTarget
    {
        CurrentCard,
        NextPlayedCard,
        NextAttackCard,
        NextSkillCard,
        NextAbilityCard,
        SelectedCardInHand,
        RandomCardInHand,
        AllCardsInHand,
        TopCardsOfDrawPile,
        CardsInDiscardPile,
        CardsInExhaustPile,
        CardsPlayedThisTurn,
        LastPlayedCard
    }

    // ========================================================================
    // 卡牌区域
    // ========================================================================
    public enum CardZoneType
    {
        Hand,
        DrawPile,
        DiscardPile,
        CombatExhaustPile,
        PermanentDeck,
        CardsPlayedThisTurn,
        TemporaryGeneratedCards
    }

    // ========================================================================
    // 卡牌选择模式
    // ========================================================================
    public enum CardSelectionMode
    {
        All,
        RandomCount,
        ChooseCount,
        TopCount,
        BottomCount,
        FirstMatching,
        LastMatching,
        CurrentCard,
        LastPlayedCard
    }

    // ========================================================================
    // 卡牌区域操作
    // ========================================================================
    public enum CardZoneOperation
    {
        Draw,
        Discard,
        ExhaustThisCombat,
        RemovePermanently,
        MoveToHand,
        MoveToDrawPileTop,
        MoveToDrawPileBottom,
        MoveToDiscardPile,
        ShuffleIntoDrawPile,
        Create,
        Copy,
        AutoPlay,
        Replay,
        ModifyCost,
        AddTemporaryKeyword,
        RemoveTemporaryKeyword
    }

    // ========================================================================
    // 伤害缩放模式
    // ========================================================================
    public enum ScalingMode
    {
        Fixed,
        AddStrength,
        CustomExpression
    }

    // ========================================================================
    // 暴击判定模式
    // ========================================================================
    public enum CriticalCheckMode
    {
        PerHit,
        PerAttack,
        Guaranteed,
        Disabled
    }

    // ========================================================================
    // 破甲规则模式
    // ========================================================================
    public enum ArmorBreakMode
    {
        BypassBlock,
        IncreaseDamageTaken
    }

    // ========================================================================
    // 资源操作方式
    // ========================================================================
    public enum ResourceOperation
    {
        Add,
        Subtract,
        Set,
        Multiply,
        Consume,
        ConsumeAll,
        RestoreToMax,
        Clamp
    }

    // ========================================================================
    // 数值表达式节点类型
    // ========================================================================
    public enum ValueNodeType
    {
        // 常量
        IntegerConstant,
        FloatConstant,
        // 读取
        ReadAttribute,
        ReadResource,
        ReadResourceLostAmount,
        ReadStatusStacks,
        ReadCounter,
        ReadRuntimeFlag,
        ReadCardCost,
        ReadActualPaidCost,
        ReadHandCount,
        ReadDrawPileCount,
        ReadDiscardPileCount,
        ReadEnemyCount,
        ReadTargetCount,
        ReadLocalVariable,
        ReadLastEffectResult,
        // 运算
        Add,
        Subtract,
        Multiply,
        Divide,
        Floor,
        Ceil,
        Round,
        Min,
        Max,
        Clamp,
        Absolute,
        Negate,
        Percentage,
        EveryNConvertToM
    }

    // ========================================================================
    // 条件类型
    // ========================================================================
    public enum ConditionType2
    {
        CompareValue,
        HasStatus,
        DoesNotHaveStatus,
        EventContextCheck,
        RuntimeFlagCheck,
        CardPropertyCheck,
        TargetExists,
        ChanceCheck,
        CustomCondition
    }

    // ========================================================================
    // 条件逻辑
    // ========================================================================
    public enum ConditionLogic2
    {
        All,
        Any,
        None,
        Not
    }

    // ========================================================================
    // 比较运算符
    // ========================================================================
    public enum ComparisonOperator
    {
        Less,
        LessOrEqual,
        Equal,
        NotEqual,
        GreaterOrEqual,
        Greater
    }
}
