using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LightMiniGame.CardEditor
{
    /// <summary>
    /// 可组合的数值表达式节点树。
    /// 支持常量、属性读取、资源读取、状态读取、计数器读取、标志读取、局部变量读取和运算。
    /// 通过递归 GetDescription() 生成人类可读的表达式预览。
    /// </summary>
    [Serializable]
    public class ValueNode
    {
        [Tooltip("节点类型")]
        public ValueNodeType nodeType = ValueNodeType.IntegerConstant;

        // === 常量值 ===
        [Tooltip("整数值（IntegerConstant 时生效）")]
        public int intValue;
        [Tooltip("浮点值（FloatConstant 时生效）")]
        public float floatValue;

        // === 引用目标 ===
        [Tooltip("引用的玩家属性")]
        public PlayerAttributeType attributeRef = PlayerAttributeType.Strength;
        [Tooltip("引用的玩家资源")]
        public PlayerResourceType resourceRef = PlayerResourceType.Sanity;
        [Tooltip("引用的状态类型")]
        public StatusType2 statusRef = StatusType2.ArmorBreak;
        [Tooltip("引用的计数器")]
        public CombatCounterType counterRef = CombatCounterType.CardsPlayedThisTurn;
        [Tooltip("引用的运行时标志")]
        public CombatFlagType flagRef = CombatFlagType.IsLowSanity;
        [Tooltip("引用的局部变量名")]
        public string variableName = "";
        [Tooltip("引用的效果结果类型")]
        public EffectResultType resultRef = EffectResultType.ActualValue;

        // === 运算参数 ===
        [Tooltip("子节点列表（运算节点使用，如 Add 需要 2 个子节点）")]
        [SerializeReference]
        public List<ValueNode> operands = new List<ValueNode>();

        // === EveryNConvertToM 参数 ===
        [Tooltip("每 N 个单位")]
        public int everyN = 1;
        [Tooltip("转换为 M 个单位")]
        public int convertToM = 1;

        /// <summary>
        /// 静态求值：给定力量/敏捷值，递归计算 ValueNode 表达式的数值。
        /// ReadAttribute(Strength/Dexterity) 用传入值替换；其他动态节点（读资源/状态/计数器等）按 0 参与运算。
        /// 用于卡面描述实时显示属性增幅后的数值。
        /// </summary>
        public static int ResolveValue(ValueNode node, int strength, int dexterity)
        {
            if (node == null) return 0;
            switch (node.nodeType)
            {
                case ValueNodeType.IntegerConstant: return node.intValue;
                case ValueNodeType.FloatConstant: return Mathf.RoundToInt(node.floatValue);
                case ValueNodeType.ReadAttribute:
                    return node.attributeRef switch
                    {
                        PlayerAttributeType.Strength => strength,
                        PlayerAttributeType.Dexterity => dexterity,
                        _ => 0
                    };
                case ValueNodeType.Add:
                    return ResolveValue(Operand(node, 0), strength, dexterity) + ResolveValue(Operand(node, 1), strength, dexterity);
                case ValueNodeType.Subtract:
                    return ResolveValue(Operand(node, 0), strength, dexterity) - ResolveValue(Operand(node, 1), strength, dexterity);
                case ValueNodeType.Multiply:
                    return ResolveValue(Operand(node, 0), strength, dexterity) * ResolveValue(Operand(node, 1), strength, dexterity);
                case ValueNodeType.Divide:
                    {
                        int b = ResolveValue(Operand(node, 1), strength, dexterity);
                        if (b == 0) return 0;
                        return ResolveValue(Operand(node, 0), strength, dexterity) / b;
                    }
                case ValueNodeType.Floor:
                    return Mathf.FloorToInt(ResolveValue(Operand(node, 0), strength, dexterity));
                case ValueNodeType.Ceil:
                    return Mathf.CeilToInt(ResolveValue(Operand(node, 0), strength, dexterity));
                case ValueNodeType.Round:
                    return Mathf.RoundToInt(ResolveValue(Operand(node, 0), strength, dexterity));
                case ValueNodeType.Min:
                    return Mathf.Min(ResolveValue(Operand(node, 0), strength, dexterity), ResolveValue(Operand(node, 1), strength, dexterity));
                case ValueNodeType.Max:
                    return Mathf.Max(ResolveValue(Operand(node, 0), strength, dexterity), ResolveValue(Operand(node, 1), strength, dexterity));
                case ValueNodeType.Clamp:
                    {
                        int v = ResolveValue(Operand(node, 0), strength, dexterity);
                        int min = ResolveValue(Operand(node, 1), strength, dexterity);
                        int max = ResolveValue(Operand(node, 2), strength, dexterity);
                        return Mathf.Clamp(v, min, max);
                    }
                case ValueNodeType.Absolute:
                    return Mathf.Abs(ResolveValue(Operand(node, 0), strength, dexterity));
                case ValueNodeType.Negate:
                    return -ResolveValue(Operand(node, 0), strength, dexterity);
                case ValueNodeType.Percentage:
                    return ResolveValue(Operand(node, 0), strength, dexterity);
                case ValueNodeType.EveryNConvertToM:
                    {
                        int v = ResolveValue(Operand(node, 0), strength, dexterity);
                        return node.everyN > 0 ? (v / node.everyN) * node.convertToM : 0;
                    }
                case ValueNodeType.Modulo:
                    {
                        int b = ResolveValue(Operand(node, 1), strength, dexterity);
                        return b != 0 ? ResolveValue(Operand(node, 0), strength, dexterity) % b : 0;
                    }
                default: return 0;
            }
        }

        /// <summary>表达式树是否读取了指定属性（如 6+力量）。已含该属性时不要再按 scalingMode 加一次。</summary>
        public static bool ReadsAttribute(ValueNode node, PlayerAttributeType attr)
        {
            if (node == null) return false;
            if (node.nodeType == ValueNodeType.ReadAttribute && node.attributeRef == attr)
                return true;
            if (node.operands == null) return false;
            for (int i = 0; i < node.operands.Count; i++)
            {
                if (ReadsAttribute(node.operands[i], attr)) return true;
            }
            return false;
        }

        /// <summary>
        /// 表达式是否含「已损失理智 × N」这类非属性公式。
        /// 这类伤害应按公式本身结算，不要再叠一层力量。
        /// </summary>
        public static bool ContainsNonAttributeFormula(ValueNode node)
        {
            if (node == null) return false;
            switch (node.nodeType)
            {
                case ValueNodeType.Percentage:
                case ValueNodeType.EveryNConvertToM:
                case ValueNodeType.Modulo:
                case ValueNodeType.ReadResource:
                case ValueNodeType.ReadResourceLostAmount:
                case ValueNodeType.ReadCounter:
                case ValueNodeType.ReadStatusStacks:
                case ValueNodeType.ReadAllEnemiesStatusStacks:
                case ValueNodeType.ReadHandCount:
                case ValueNodeType.ReadDrawPileCount:
                case ValueNodeType.ReadDiscardPileCount:
                case ValueNodeType.ReadEnemyCount:
                case ValueNodeType.ReadTargetCount:
                case ValueNodeType.ReadLocalVariable:
                case ValueNodeType.ReadLastEffectResult:
                case ValueNodeType.ReadCardCost:
                case ValueNodeType.ReadActualPaidCost:
                case ValueNodeType.ReadRuntimeFlag:
                case ValueNodeType.ReadMaxHandCount:
                case ValueNodeType.ReadHandVacancies:
                    return true;
            }
            if (node.operands == null) return false;
            for (int i = 0; i < node.operands.Count; i++)
            {
                if (ContainsNonAttributeFormula(node.operands[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// 普通 {N+力量} 才按 scaling / 敌人出牌再加力量。
        /// 公式伤害（理智×N）和树上已经读了力量的表达式都不再加。
        /// </summary>
        public static bool ShouldApplyStrengthBonus(ValueNode value)
            => !ReadsAttribute(value, PlayerAttributeType.Strength) && !ContainsNonAttributeFormula(value);

        /// <summary>
        /// 效果数值：先求值，再按缩放叠加力量/敏捷。树里已经 ReadAttribute 的不再加，避免 {N+力量} 双算。
        /// isEnemy 时 DealDamage 额外吃力量（与杀戮尖塔敌人攻击一致）。
        /// </summary>
        public static int ResolveCombatValue(ValueNode value, EffectOperation op, ScalingMode scaling, int strength, int dexterity, bool isEnemy)
        {
            int v = ResolveValue(value, strength, dexterity);
            switch (op)
            {
                case EffectOperation.DealDamage:
                    if ((scaling == ScalingMode.AddStrength || isEnemy) && ShouldApplyStrengthBonus(value))
                        v += strength;
                    break;
                case EffectOperation.GainBlock:
                    if (scaling == ScalingMode.AddStrength && !ReadsAttribute(value, PlayerAttributeType.Dexterity))
                        v += dexterity;
                    break;
            }
            return v;
        }

        /// <summary>取 ValueNode 的指定操作数子节点（越界返回 null）。</summary>
        private static ValueNode Operand(ValueNode node, int index)
            => (node != null && node.operands != null && index < node.operands.Count) ? node.operands[index] : null;

        // === 静态便捷工厂 ===
        public static ValueNode Constant(int v) => new ValueNode { nodeType = ValueNodeType.IntegerConstant, intValue = v };
        public static ValueNode ConstFloat(float v) => new ValueNode { nodeType = ValueNodeType.FloatConstant, floatValue = v };
        public static ValueNode Read(PlayerAttributeType a) => new ValueNode { nodeType = ValueNodeType.ReadAttribute, attributeRef = a };
        public static ValueNode Read(PlayerResourceType r) => new ValueNode { nodeType = ValueNodeType.ReadResource, resourceRef = r };
        public static ValueNode ReadCounter(CombatCounterType c) => new ValueNode { nodeType = ValueNodeType.ReadCounter, counterRef = c };
        public static ValueNode Add(ValueNode a, ValueNode b) => new ValueNode { nodeType = ValueNodeType.Add, operands = new List<ValueNode> { a, b } };
        public static ValueNode Multiply(ValueNode a, ValueNode b) => new ValueNode { nodeType = ValueNodeType.Multiply, operands = new List<ValueNode> { a, b } };
        public static ValueNode Floor(ValueNode a) => new ValueNode { nodeType = ValueNodeType.Floor, operands = new List<ValueNode> { a } };

        /// <summary>
        /// 递归生成人类可读的表达式描述。
        /// </summary>
        public string GetDescription()
        {
            switch (nodeType)
            {
                // 常量
                case ValueNodeType.IntegerConstant: return intValue.ToString();
                case ValueNodeType.FloatConstant: return floatValue.ToString("F1");

                // 读取
                case ValueNodeType.ReadAttribute: return GetAttrName(attributeRef);
                case ValueNodeType.ReadResource: return GetResourceName(resourceRef);
                case ValueNodeType.ReadResourceLostAmount: return $"已损失{GetResourceName(resourceRef)}";
                case ValueNodeType.ReadStatusStacks: return GetStatusName(statusRef) + "层数";
                case ValueNodeType.ReadCounter: return GetCounterName(counterRef);
                case ValueNodeType.ReadRuntimeFlag: return GetFlagName(flagRef) + "(0/1)";
                case ValueNodeType.ReadCardCost: return "本牌费用";
                case ValueNodeType.ReadActualPaidCost: return "实际支付费用";
                case ValueNodeType.ReadHandCount: return "手牌数";
                case ValueNodeType.ReadDrawPileCount: return "抽牌堆数";
                case ValueNodeType.ReadDiscardPileCount: return "弃牌堆数";
                case ValueNodeType.ReadEnemyCount: return "敌人数";
                case ValueNodeType.ReadTargetCount: return "目标数";
                case ValueNodeType.ReadLocalVariable: return variableName;
                case ValueNodeType.ReadLastEffectResult: return GetResultName(resultRef);
                case ValueNodeType.ReadAllEnemiesStatusStacks: return "全场" + GetStatusName(statusRef) + "层数";
                case ValueNodeType.ReadMaxHandCount: return "手牌上限";
                case ValueNodeType.ReadHandVacancies: return "手牌空位";

                // 运算
                case ValueNodeType.Add: return Join(" + ");
                case ValueNodeType.Subtract: return Join(" - ");
                case ValueNodeType.Multiply: return Join(" × ");
                case ValueNodeType.Divide: return Join(" ÷ ");
                case ValueNodeType.Floor: return $"Floor({Child(0)})";
                case ValueNodeType.Ceil: return $"Ceil({Child(0)})";
                case ValueNodeType.Round: return $"Round({Child(0)})";
                case ValueNodeType.Min: return $"Min({Child(0)}, {Child(1)})";
                case ValueNodeType.Max: return $"Max({Child(0)}, {Child(1)})";
                case ValueNodeType.Clamp: return $"Clamp({Child(0)}, {Child(1)}, {Child(2)})";
                case ValueNodeType.Absolute: return $"|{Child(0)}|";
                case ValueNodeType.Negate: return $"-{Child(0)}";
                case ValueNodeType.Percentage: return $"{Child(0)}%";
                case ValueNodeType.EveryNConvertToM: return $"Floor({Child(0)} ÷ {everyN}) × {convertToM}";
                case ValueNodeType.Modulo: return $"{Child(0)} % {Child(1)}";

                default: return "?";
            }
        }

        private string Child(int index) => index < operands.Count && operands[index] != null ? operands[index].GetDescription() : "0";
        private string Join(string op) => $"{Child(0)}{op}{Child(1)}";

        // === 中文名称 ===
        public static string GetAttrName(PlayerAttributeType a) => a switch
        {
            PlayerAttributeType.MaxHealth => "最大生命",
            PlayerAttributeType.Strength => "力量",
            PlayerAttributeType.Dexterity => "敏捷",
            PlayerAttributeType.Recovery => "回复",
            PlayerAttributeType.LifeSteal => "吸血",
            PlayerAttributeType.CriticalChance => "暴击率",
            PlayerAttributeType.CriticalDamageMultiplier => "暴击伤害倍率",
            PlayerAttributeType.ActionPointsPerTurn => "每回合行动点",
            PlayerAttributeType.CardsDrawnPerTurn => "每回合抽牌数",
            PlayerAttributeType.TotalDamageMultiplier => "总伤害倍率",
            PlayerAttributeType.IncomingDamageMultiplier => "受击倍率",
            _ => a.ToString()
        };

        public static string GetResourceName(PlayerResourceType r) => r switch
        {
            PlayerResourceType.CurrentHealth => "当前生命",
            PlayerResourceType.Sanity => "理智",
            PlayerResourceType.ActionPoints => "行动点",
            PlayerResourceType.Currency => "货币",
            PlayerResourceType.Heat => "热度",
            PlayerResourceType.Block => "格挡",
            PlayerResourceType.Fortune => "福报",
            _ => r.ToString()
        };

        public static string GetStatusName(StatusType2 s) => s switch
        {
            StatusType2.ArmorBreak => "破甲",
            StatusType2.Bleed => "流血",
            StatusType2.Jammed => "卡壳",
            StatusType2.Madness => "疯狂",
            StatusType2.Vulnerable => "易伤",
            StatusType2.TemporaryStrength => "临时力量",
            StatusType2.TemporaryDexterity => "临时敏捷",
            StatusType2.NextAttackDamageBonus => "下次攻击增伤",
            StatusType2.NextAttackCriticalDamageBonus => "下次暴伤提升",
            StatusType2.NextAttackGuaranteedCritical => "下次必暴",
            StatusType2.NextCardCostModifier => "下张牌减费",
            StatusType2.NextAttackCardCostModifier => "下张攻击牌减费",
            StatusType2.HandCostModifier => "手牌减费",
            StatusType2.CriticalChanceModifier => "暴击率变化",
            StatusType2.CriticalDamageModifier => "暴伤变化",
            StatusType2.BlockRetention => "格挡保留",
            StatusType2.CustomStatus => "自定义状态",
            StatusType2.Fatigue => "疲惫",
            _ => s.ToString()
        };

        public static string GetCounterName(CombatCounterType c) => c switch
        {
            CombatCounterType.CardsPlayedThisTurn => "本回合出牌数",
            CombatCounterType.AttackCardsPlayedThisTurn => "本回合攻击牌数",
            CombatCounterType.SkillCardsPlayedThisTurn => "本回合技能牌数",
            CombatCounterType.AbilityCardsPlayedThisTurn => "本回合能力牌数",
            CombatCounterType.AttacksPerformedThisTurn => "本回合攻击次数",
            CombatCounterType.HitsPerformedThisTurn => "本回合命中次数",
            CombatCounterType.CriticalHitsThisTurn => "本回合暴击次数",
            CombatCounterType.DamageTakenThisTurn => "本回合受到伤害",
            CombatCounterType.DamageDealtThisTurn => "本回合造成伤害",
            CombatCounterType.SanityLostThisTurn => "本回合失去理智",
            CombatCounterType.SanityLostThisCombat => "本场失去理智",
            CombatCounterType.HeatGainedThisTurn => "本回合获得热度",
            CombatCounterType.HeatLostThisTurn => "本回合降低热度",
            CombatCounterType.CharactersSwitchedThisTurn => "本回合切换角色数",
            CombatCounterType.EnemiesKilledThisTurn => "本回合击杀敌人数",
            CombatCounterType.BlockGainedThisTurn => "本回合获得格挡",
            CombatCounterType.CardsDrawnThisTurn => "本回合抽牌数",
            CombatCounterType.CardsDiscardedThisTurn => "本回合弃牌数",
            CombatCounterType.CardsExhaustedThisTurn => "本回合消耗牌数",
            CombatCounterType.CurrentHitIndex => "当前攻击段索引",
            CombatCounterType.CurrentHitCount => "当前攻击总段数",
            _ => c.ToString()
        };

        public static string GetFlagName(CombatFlagType f) => f switch
        {
            CombatFlagType.TookDamageThisTurn => "本回合受伤",
            CombatFlagType.AttackedThisTurn => "本回合攻击过",
            CombatFlagType.PlayedCardThisTurn => "本回合出过牌",
            CombatFlagType.SwitchedCharacterThisTurn => "本回合切换过角色",
            CombatFlagType.CurrentHitWasCritical => "当前段暴击",
            CombatFlagType.CurrentAttackHadAnyCriticalHit => "本次攻击有暴击",
            CombatFlagType.CurrentAttackKilledEnemy => "本次攻击击杀敌人",
            CombatFlagType.IsLowSanity => "低理智状态",
            CombatFlagType.IsOverheated => "过热状态",
            CombatFlagType.IsFirstAttackThisTurn => "本回合首次攻击",
            CombatFlagType.IsFirstAttackCardThisTurn => "本回合首张攻击牌",
            _ => f.ToString()
        };

        public static string GetResultName(EffectResultType r) => r switch
        {
            EffectResultType.RequestedValue => "请求值",
            EffectResultType.ActualValue => "实际值",
            EffectResultType.ActualDamage => "实际伤害",
            EffectResultType.ActualHealthDamage => "实际生命伤害",
            EffectResultType.BlockedDamage => "格挡吸收伤害",
            EffectResultType.ActualBlockGained => "实际获得格挡",
            EffectResultType.ActualBlockConsumed => "实际消耗格挡",
            EffectResultType.ActualResourceAdded => "实际增加资源",
            EffectResultType.ActualResourceRemoved => "实际减少资源",
            EffectResultType.ActualHeatReduced => "实际降低热度",
            EffectResultType.ActualSanityLost => "实际失去理智",
            EffectResultType.CardsDrawn => "抽牌数",
            EffectResultType.CardsDiscarded => "弃牌数",
            EffectResultType.CardsExhausted => "消耗牌数",
            EffectResultType.EnemiesKilled => "击杀敌人数",
            EffectResultType.TargetsAffected => "受影响目标数",
            EffectResultType.CriticalHitCount => "暴击次数",
            EffectResultType.AnyCriticalHit => "是否有暴击",
            EffectResultType.StatusStacksAdded => "施加状态层数",
            EffectResultType.StatusStacksRemoved => "移除状态层数",
            _ => r.ToString()
        };
    }

    /// <summary>
    /// 效果执行结果类型 — 用于效果间传递数据。
    /// </summary>
    public enum EffectResultType
    {
        RequestedValue,
        ActualValue,
        ActualDamage,
        ActualHealthDamage,
        BlockedDamage,
        ActualBlockGained,
        ActualBlockConsumed,
        ActualResourceAdded,
        ActualResourceRemoved,
        ActualHeatReduced,
        ActualSanityLost,
        CardsDrawn,
        CardsDiscarded,
        CardsExhausted,
        EnemiesKilled,
        TargetsAffected,
        CriticalHitCount,
        AnyCriticalHit,
        StatusStacksAdded,
        StatusStacksRemoved
    }
}
