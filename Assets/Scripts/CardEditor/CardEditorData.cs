using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LightMiniGame.CardEditor
{
    // ========================================================================
    // 数值表达式 —— 支持固定值、属性加成、计数加成等公式
    // ========================================================================
    [Serializable]
    public class ValueExpression
    {
        [Tooltip("公式类型")]
        public ValueFormulaType formulaType = ValueFormulaType.Fixed;

        [Tooltip("基础数值")]
        public int baseValue = 0;

        [Tooltip("引用的属性（公式类型为属性加成时生效）")]
        public AttributeRef attributeRef = AttributeRef.Strength;

        [Tooltip("系数（公式类型含系数时生效）")]
        public float coefficient = 1f;

        [Tooltip("引用的计数（公式类型为计数加成时生效）")]
        public AttributeRef counterRef = AttributeRef.Strength;

        public string GetDescription()
        {
            switch (formulaType)
            {
                case ValueFormulaType.Fixed:
                    return baseValue.ToString();
                case ValueFormulaType.BasePlusAttribute:
                    return $"{baseValue} + {GetAttrName(attributeRef)}";
                case ValueFormulaType.BasePlusAttributeTimesCoeff:
                    return $"{baseValue} + {GetAttrName(attributeRef)} × {coefficient}";
                case ValueFormulaType.BasePlusCounterTimesCoeff:
                    return $"{baseValue} + {GetAttrName(counterRef)} × {coefficient}";
                default:
                    return baseValue.ToString();
            }
        }

        public static string GetAttrName(AttributeRef attr) => attr switch
        {
            AttributeRef.Strength => "力量",
            AttributeRef.Dexterity => "敏捷",
            AttributeRef.CurrentHP => "当前生命",
            AttributeRef.MaxHP => "最大生命",
            AttributeRef.LostHP => "已损失生命",
            AttributeRef.CurrentSanity => "当前理智",
            AttributeRef.SanityLostThisTurn => "本回合已损失理智",
            AttributeRef.TotalSanityLostThisBattle => "本场战斗累计失去理智",
            AttributeRef.BleedValue => "流血值",
            AttributeRef.ArmorBreakValue => "破甲值",
            AttributeRef.CritRate => "暴击率",
            AttributeRef.CritDamage => "暴击伤害",
            _ => attr.ToString()
        };
    }

    // ========================================================================
    // 效果条件 —— 每个效果可配置多个条件
    // ========================================================================
    [Serializable]
    public class EffectCondition
    {
        [Tooltip("条件类型")]
        public ConditionType conditionType = ConditionType.SourceAttributeCheck;

        [Tooltip("引用的属性")]
        public AttributeRef attributeRef = AttributeRef.Strength;

        [Tooltip("比较方式")]
        public ComparisonOp comparison = ComparisonOp.GreaterEqual;

        [Tooltip("比较值")]
        public float compareValue = 0;

        [Tooltip("事件名称（条件类型为事件发生时生效）")]
        public string eventName = "";

        [Tooltip("状态类型（条件类型为状态检查时生效）")]
        public StatusType statusType = StatusType.Bleed;

        [Tooltip("自定义条件脚本")]
        public CustomConditionScript customConditionScript;

        public string GetDescription()
        {
            switch (conditionType)
            {
                case ConditionType.SourceAttributeCheck:
                    return $"发起者{GetAttrName(attributeRef)} {GetCompName(comparison)} {compareValue}";
                case ConditionType.TargetAttributeCheck:
                    return $"目标{GetAttrName(attributeRef)} {GetCompName(comparison)} {compareValue}";
                case ConditionType.EventOccurred:
                    return $"事件[{eventName}]已发生";
                case ConditionType.SourceHasStatus:
                    return $"发起者处于{GetStatusName(statusType)}状态";
                case ConditionType.TargetHasStatus:
                    return $"目标处于{GetStatusName(statusType)}状态";
                case ConditionType.TurnCounterCheck:
                    return $"本回合计数 {GetCompName(comparison)} {compareValue}";
                case ConditionType.BattleCounterCheck:
                    return $"本场战斗计数 {GetCompName(comparison)} {compareValue}";
                case ConditionType.Custom:
                    return customConditionScript != null ? $"自定义条件: {customConditionScript.GetDisplayName()}" : "自定义条件(未绑定)";
                default:
                    return "未知条件";
            }
        }

        public static string GetCompName(ComparisonOp op) => op switch
        {
            ComparisonOp.Less => "<",
            ComparisonOp.LessEqual => "≤",
            ComparisonOp.Equal => "=",
            ComparisonOp.GreaterEqual => "≥",
            ComparisonOp.Greater => ">",
            ComparisonOp.NotEqual => "≠",
            _ => "?"
        };

        public static string GetStatusName(StatusType st) => st switch
        {
            StatusType.Bleed => "流血",
            StatusType.ArmorBreak => "破甲",
            StatusType.Strength => "力量",
            StatusType.Dexterity => "敏捷",
            StatusType.Insane => "疯狂",
            StatusType.NextAttackDamageBoost => "下次攻击增伤",
            StatusType.NextCardCostReduce => "下一张牌减费",
            StatusType.CritRateBoost => "暴击率提升",
            StatusType.CritDamageBoost => "暴击伤害提升",
            _ => st.ToString()
        };

        private static string GetAttrName(AttributeRef attr) => ValueExpression.GetAttrName(attr);
    }

    // ========================================================================
    // 统一效果结构 —— 所有普通效果共用此结构
    // ========================================================================
    [Serializable]
    public class CardEffect
    {
        [Header("基础")]
        [Tooltip("是否启用此效果")]
        public bool enabled = true;

        [Tooltip("效果名称（策划备注用）")]
        public string effectName = "新效果";

        [Tooltip("效果类型")]
        public EffectType effectType = EffectType.Damage;

        [Tooltip("效果发起者")]
        public EffectSource source = EffectSource.Player;

        [Tooltip("效果目标")]
        public EffectTarget target = EffectTarget.SelectedEnemy;

        [Header("数值表达式")]
        [Tooltip("主要数值（伤害值、护甲值、修改量等）")]
        public ValueExpression value = new ValueExpression();

        [Header("次数")]
        [Tooltip("执行次数（攻击多次等）")]
        public int times = 1;

        [Header("条件")]
        [Tooltip("生效条件列表")]
        public List<EffectCondition> conditions = new List<EffectCondition>();

        [Tooltip("条件逻辑（全部满足/任意满足）")]
        public ConditionLogic conditionLogic = ConditionLogic.All;

        [Header("时机与持续")]
        [Tooltip("生效时机")]
        public EffectTiming timing = EffectTiming.Immediate;

        [Tooltip("持续时间")]
        public EffectDuration duration = EffectDuration.OneAction;

        [Tooltip("持续数值（回合数或行动次数）")]
        public int durationValue = 1;

        // === 攻击专属 ===
        [Header("攻击专属")]
        [Tooltip("是否必定暴击")]
        public bool alwaysCrit = false;

        [Tooltip("是否无视护甲")]
        public bool ignoreArmor = false;

        [Tooltip("是否施加破甲")]
        public bool applyArmorBreak = false;

        [Tooltip("破甲数值")]
        public int armorBreakAmount = 0;

        // === 增减益专属 ===
        [Header("增减益专属")]
        [Tooltip("增减益目标")]
        public BuffTarget buffTarget = BuffTarget.Player;

        [Tooltip("修改的属性")]
        public ModifiableAttribute modAttribute = ModifiableAttribute.Strength;

        [Tooltip("修改方式")]
        public ModifyMethod modifyMethod = ModifyMethod.Add;

        // === 状态专属 ===
        [Header("状态专属")]
        [Tooltip("状态类型")]
        public StatusType statusType = StatusType.Bleed;

        [Tooltip("状态层数")]
        public int statusStacks = 1;

        [Tooltip("是否可叠加")]
        public bool stackable = true;

        // === 手牌费用专属 ===
        [Header("手牌费用专属")]
        [Tooltip("手牌费用目标")]
        public HandCostTarget handCostTarget = HandCostTarget.NextCard;

        [Tooltip("费用变化值")]
        public int costChange = 0;

        // === 自定义 ===
        [Header("自定义")]
        [Tooltip("自定义效果脚本")]
        public CustomEffectScript customEffectScript;

        [Tooltip("自定义参数（JSON或文本）")]
        [TextArea(1, 3)]
        public string customParams = "";

        public string GetDescription()
        {
            var sb = new StringBuilder();
            sb.Append(GetEffectTypeName(effectType));

            switch (effectType)
            {
                case EffectType.Damage:
                    sb.Append($" {value.GetDescription()}");
                    if (times > 1) sb.Append($" ×{times}次");
                    if (alwaysCrit) sb.Append(" 必定暴击");
                    if (ignoreArmor) sb.Append(" 无视护甲");
                    if (applyArmorBreak) sb.Append($" 施加破甲{armorBreakAmount}");
                    sb.Append($" → {GetTargetName(target)}");
                    break;
                case EffectType.Armor:
                    sb.Append($" {value.GetDescription()}");
                    if (timing != EffectTiming.Immediate) sb.Append($" ({GetTimingName(timing)})");
                    break;
                case EffectType.DrawCard:
                    sb.Append($" {value.GetDescription()}张");
                    if (timing != EffectTiming.Immediate) sb.Append($" ({GetTimingName(timing)})");
                    break;
                case EffectType.RestoreEnergy:
                    sb.Append($" {value.GetDescription()}点");
                    if (timing != EffectTiming.Immediate) sb.Append($" ({GetTimingName(timing)})");
                    break;
                case EffectType.ModifyAttribute:
                    sb.Append($" {GetModAttrName(modAttribute)} {GetModifyMethodName(modifyMethod)} {value.GetDescription()}");
                    sb.Append($" → {GetBuffTargetName(buffTarget)}");
                    if (duration != EffectDuration.OneAction) sb.Append($" ({GetDurationName(duration)})");
                    break;
                case EffectType.ModifyHandCost:
                    sb.Append($" {GetHandCostTargetName(handCostTarget)} {(costChange >= 0 ? "+" : "")}{costChange}");
                    break;
                case EffectType.ApplyStatus:
                    sb.Append($" {EffectCondition.GetStatusName(statusType)} {statusStacks}层");
                    sb.Append($" → {GetTargetName(target)}");
                    if (duration != EffectDuration.OneAction) sb.Append($" ({GetDurationName(duration)})");
                    break;
                case EffectType.Custom:
                    sb.Append(customEffectScript != null ? $": {customEffectScript.GetDisplayName()}" : "(未绑定脚本)");
                    break;
            }

            if (conditions.Count > 0)
            {
                sb.Append($" [条件: {conditions.Count}个]");
            }

            return sb.ToString();
        }

        public CardEffect Clone()
        {
            return new CardEffect
            {
                enabled = enabled,
                effectName = effectName,
                effectType = effectType,
                source = source,
                target = target,
                value = new ValueExpression
                {
                    formulaType = value.formulaType,
                    baseValue = value.baseValue,
                    attributeRef = value.attributeRef,
                    coefficient = value.coefficient,
                    counterRef = value.counterRef
                },
                times = times,
                conditions = conditions != null ? new List<EffectCondition>(conditions) : new List<EffectCondition>(),
                conditionLogic = conditionLogic,
                timing = timing,
                duration = duration,
                durationValue = durationValue,
                alwaysCrit = alwaysCrit,
                ignoreArmor = ignoreArmor,
                applyArmorBreak = applyArmorBreak,
                armorBreakAmount = armorBreakAmount,
                buffTarget = buffTarget,
                modAttribute = modAttribute,
                modifyMethod = modifyMethod,
                statusType = statusType,
                statusStacks = statusStacks,
                stackable = stackable,
                handCostTarget = handCostTarget,
                costChange = costChange,
                customEffectScript = customEffectScript,
                customParams = customParams
            };
        }

        public static string GetEffectTypeName(EffectType t) => t switch
        {
            EffectType.Damage => "伤害",
            EffectType.Armor => "护甲",
            EffectType.DrawCard => "抽牌",
            EffectType.RestoreEnergy => "回费",
            EffectType.ModifyAttribute => "修改属性",
            EffectType.ModifyHandCost => "修改费用",
            EffectType.ApplyStatus => "施加状态",
            EffectType.Custom => "自定义",
            _ => t.ToString()
        };

        public static string GetTargetName(EffectTarget t) => t switch
        {
            EffectTarget.Player => "玩家",
            EffectTarget.SelectedEnemy => "选定敌人",
            EffectTarget.RandomEnemy => "随机敌人",
            EffectTarget.AllEnemies => "所有敌人",
            EffectTarget.SwitchedCharacter => "切换后角色",
            EffectTarget.SourceSelf => "发起者自身",
            EffectTarget.Custom => "自定义",
            _ => t.ToString()
        };

        public static string GetSourceName(EffectSource s) => s switch
        {
            EffectSource.Player => "玩家",
            EffectSource.SelectedEnemy => "选定敌人",
            EffectSource.RandomEnemy => "随机敌人",
            EffectSource.AllEnemies => "所有敌人",
            EffectSource.Custom => "自定义",
            _ => s.ToString()
        };

        public static string GetTimingName(EffectTiming t) => t switch
        {
            EffectTiming.Immediate => "立即",
            EffectTiming.NextTurnStart => "下回合开始",
            EffectTiming.NextAttack => "下次攻击",
            EffectTiming.NextCard => "下一张牌",
            EffectTiming.NextSwitch => "下次切换",
            EffectTiming.EverySwitch => "每次切换",
            _ => t.ToString()
        };

        public static string GetDurationName(EffectDuration d) => d switch
        {
            EffectDuration.OneAction => "一次行动",
            EffectDuration.SpecifiedTurns => "指定回合",
            EffectDuration.BattlePermanent => "战斗持续",
            EffectDuration.GamePermanent => "本局永久",
            _ => d.ToString()
        };

        public static string GetBuffTargetName(BuffTarget b) => b switch
        {
            BuffTarget.Player => "玩家",
            BuffTarget.Enemy => "敌人",
            BuffTarget.CurrentHand => "当前手牌",
            BuffTarget.NextCard => "下一张牌",
            BuffTarget.SpecifiedCardType => "指定类型牌",
            BuffTarget.Custom => "自定义",
            _ => b.ToString()
        };

        public static string GetModAttrName(ModifiableAttribute a) => a switch
        {
            ModifiableAttribute.Strength => "力量",
            ModifiableAttribute.Dexterity => "敏捷",
            ModifiableAttribute.PlayerCritRate => "玩家暴击率",
            ModifiableAttribute.EnemyCritRate => "敌人被暴击率",
            ModifiableAttribute.PlayerCritDamage => "玩家暴击伤害",
            ModifiableAttribute.EnemyCritDamage => "敌人暴击伤害",
            ModifiableAttribute.ArmorBreakValue => "破甲值",
            ModifiableAttribute.MaxHP => "最大生命值",
            ModifiableAttribute.CurrentHP => "当前生命值",
            ModifiableAttribute.DrawPerTurn => "每回合抽牌数",
            ModifiableAttribute.EnergyPerTurn => "每回合能量",
            ModifiableAttribute.BleedValue => "流血值",
            ModifiableAttribute.Currency => "货币",
            ModifiableAttribute.HandCost => "手牌费用",
            _ => a.ToString()
        };

        public static string GetModifyMethodName(ModifyMethod m) => m switch
        {
            ModifyMethod.Add => "增加",
            ModifyMethod.Subtract => "减少",
            ModifyMethod.Multiply => "乘算",
            ModifyMethod.Override => "覆盖",
            _ => m.ToString()
        };

        public static string GetHandCostTargetName(HandCostTarget h) => h switch
        {
            HandCostTarget.NextCard => "下一张牌",
            HandCostTarget.CurrentHand => "当前手牌",
            _ => h.ToString()
        };
    }

    // ========================================================================
    // 能力数据 —— 能力卡的触发器与触发效果
    // ========================================================================
    [Serializable]
    public class AbilityData
    {
        [Tooltip("能力名称")]
        public string abilityName = "";

        [Tooltip("能力描述")]
        [TextArea(2, 4)]
        public string abilityDescription = "";

        [Tooltip("触发时机")]
        public AbilityTrigger trigger = AbilityTrigger.TurnStart;

        [Tooltip("触发条件列表")]
        public List<EffectCondition> triggerConditions = new List<EffectCondition>();

        [Tooltip("触发条件逻辑")]
        public ConditionLogic triggerConditionLogic = ConditionLogic.All;

        [Tooltip("总触发次数限制（0=无限制）")]
        public int maxTriggers = 0;

        [Tooltip("每回合触发次数限制（0=无限制）")]
        public int maxTriggersPerTurn = 0;

        [Tooltip("触发后执行的效果列表")]
        public List<CardEffect> triggeredEffects = new List<CardEffect>();

        [Tooltip("自定义能力脚本")]
        public CustomAbilityScript customAbilityScript;

        public AbilityData Clone()
        {
            return new AbilityData
            {
                abilityName = abilityName,
                abilityDescription = abilityDescription,
                trigger = trigger,
                triggerConditions = triggerConditions != null ? new List<EffectCondition>(triggerConditions) : new List<EffectCondition>(),
                triggerConditionLogic = triggerConditionLogic,
                maxTriggers = maxTriggers,
                maxTriggersPerTurn = maxTriggersPerTurn,
                triggeredEffects = triggeredEffects != null ? triggeredEffects.ConvertAll(e => e.Clone()) : new List<CardEffect>(),
                customAbilityScript = customAbilityScript
            };
        }

        public string GetDescription()
        {
            var sb = new StringBuilder();
            sb.Append($"触发: {GetTriggerName(trigger)}");
            if (triggerConditions.Count > 0)
                sb.Append($" 条件{triggerConditions.Count}个({triggerConditionLogic})");
            if (maxTriggers > 0) sb.Append($" 总限{maxTriggers}次");
            if (maxTriggersPerTurn > 0) sb.Append($" 每回合限{maxTriggersPerTurn}次");
            sb.Append($" 效果{triggeredEffects.Count}个");
            return sb.ToString();
        }

        public static string GetTriggerName(AbilityTrigger t) => t switch
        {
            AbilityTrigger.TurnStart => "回合开始",
            AbilityTrigger.TurnEnd => "回合结束",
            AbilityTrigger.OnCrit => "暴击时",
            AbilityTrigger.OnLoseSanity => "失去理智时",
            AbilityTrigger.OnSanityBelowThreshold => "理智低于阈值",
            AbilityTrigger.OnReceiveDebuff => "获得减益时",
            AbilityTrigger.FirstAttackEachTurn => "每回合首次攻击",
            AbilityTrigger.OnApplyArmorBreak => "施加破甲时",
            AbilityTrigger.Custom => "自定义事件",
            _ => t.ToString()
        };
    }
}
