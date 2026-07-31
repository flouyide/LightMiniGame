using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LightMiniGame.CardEditor
{
    // ========================================================================
    // 目标选择器 — 统一的目标选择结构
    // ========================================================================
    [Serializable]
    public class TargetSelector
    {
        [Tooltip("目标类别")]
        public TargetCategory category = TargetCategory.Enemy;

        [Tooltip("战斗单位目标（category=CombatUnit/Character/Enemy 时生效）")]
        public CombatUnitTarget unitTarget = CombatUnitTarget.SelectedEnemy;

        [Tooltip("卡牌目标（category=Card 时生效）")]
        public CardTarget cardTarget = CardTarget.AllCardsInHand;

        [Tooltip("卡牌选择模式")]
        public CardSelectionMode selectionMode = CardSelectionMode.All;

        [Tooltip("选择数量（部分模式生效）")]
        public int selectionCount = 1;

        [Tooltip("卡牌筛选：类型")]
        public CardType2 cardFilterType = CardType2.Attack;

        [Tooltip("卡牌筛选：稀有度")]
        public CardRarity cardFilterRarity = CardRarity.Common;

        [Tooltip("卡牌筛选：词条")]
        public CardKeyword2 cardFilterKeyword = CardKeyword2.None;

        [Tooltip("卡牌筛选：是否低理智形态")]
        public bool filterIsLowSanityForm = false;

        [Tooltip("卡牌筛选：标签")]
        public string filterTag = "";

        public string GetDescription()
        {
            switch (category)
            {
                case TargetCategory.Enemy:
                case TargetCategory.CombatUnit:
                    return GetUnitTargetName(unitTarget);
                case TargetCategory.Character:
                    return GetUnitTargetName(unitTarget);
                case TargetCategory.Card:
                    return $"{GetCardTargetName(cardTarget)}";
                default:
                    return category.ToString();
            }
        }

        public static string GetUnitTargetName(CombatUnitTarget t) => t switch
        {
            CombatUnitTarget.CurrentCharacter => "当前角色",
            CombatUnitTarget.SwitchedInCharacter => "登场角色",
            CombatUnitTarget.SwitchedOutCharacter => "退场角色",
            CombatUnitTarget.SelectedEnemy => "选定敌人",
            CombatUnitTarget.RandomEnemy => "随机敌人",
            CombatUnitTarget.AllEnemies => "所有敌人",
            CombatUnitTarget.EffectSource => "效果发起者",
            CombatUnitTarget.CurrentAttackTarget => "当前攻击目标",
            CombatUnitTarget.EnemyKilledByCurrentEffect => "被击杀敌人",
            CombatUnitTarget.AllCharacters => "所有角色",
            CombatUnitTarget.SpecificCharacter => "指定角色",
            CombatUnitTarget.LowestHPEnemy => "生命最低敌人",
            CombatUnitTarget.HighestHPEnemy => "生命最高敌人",
            CombatUnitTarget.HighestArmorBreakEnemy => "破甲最高敌人",
            CombatUnitTarget.RandomNEnemies => $"随机{0}个敌人",
            _ => t.ToString()
        };

        public static string GetCardTargetName(CardTarget t) => t switch
        {
            CardTarget.CurrentCard => "当前卡牌",
            CardTarget.NextPlayedCard => "下一张打出的牌",
            CardTarget.NextAttackCard => "下一张攻击牌",
            CardTarget.NextSkillCard => "下一张技能牌",
            CardTarget.NextAbilityCard => "下一张能力牌",
            CardTarget.SelectedCardInHand => "手牌中选中的牌",
            CardTarget.RandomCardInHand => "手牌中随机牌",
            CardTarget.AllCardsInHand => "全部手牌",
            CardTarget.TopCardsOfDrawPile => "抽牌堆顶牌",
            CardTarget.CardsInDiscardPile => "弃牌堆中的牌",
            CardTarget.CardsInExhaustPile => "消耗堆中的牌",
            CardTarget.CardsPlayedThisTurn => "本回合打出的牌",
            CardTarget.LastPlayedCard => "最后打出的牌",
            _ => t.ToString()
        };
    }

    // ========================================================================
    // 条件结构 — 支持嵌套条件组
    // ========================================================================
    [Serializable]
    public class ConditionEntry
    {
        [Tooltip("条件类型")]
        public ConditionType2 conditionType = ConditionType2.CompareValue;

        // CompareValue 参数
        [Tooltip("左侧表达式（CompareValue 时生效）")]
        	[SerializeReference]
	public ValueNode leftValue;
        [Tooltip("比较运算符")]
        public ComparisonOperator comparison = ComparisonOperator.GreaterOrEqual;
        [Tooltip("右侧表达式（CompareValue 时生效）")]
        	[SerializeReference]
	public ValueNode rightValue;

        // HasStatus 参数
        [Tooltip("检查的状态类型")]
        public StatusType2 statusType = StatusType2.ArmorBreak;
        [Tooltip("检查的目标")]
        public TargetSelector statusTarget = new TargetSelector { category = TargetCategory.CombatUnit, unitTarget = CombatUnitTarget.CurrentCharacter };

        // EventContextCheck 参数
        [Tooltip("事件名称")]
        public string eventName = "";

        // RuntimeFlagCheck 参数
        [Tooltip("检查的标志")]
        public CombatFlagType flagRef = CombatFlagType.IsLowSanity;

        // CardPropertyCheck 参数
        [Tooltip("卡牌属性筛选")]
        public CardType2 cardPropertyType = CardType2.Attack;

        // ChanceCheck 参数
        [Tooltip("概率（0-100）")]
        public float chancePercent = 50f;

        // CustomCondition 参数
        [Tooltip("自定义条件脚本")]
        public CustomConditionScript customConditionScript;

        // 嵌套条件组
        [Tooltip("嵌套条件组（条件类型为 Not 时使用单个子组）")]
        	[SerializeReference]
	public List<ConditionGroup> nestedGroups = new List<ConditionGroup>();

        public string GetDescription()
        {
            switch (conditionType)
            {
                case ConditionType2.CompareValue:
                    string leftStr = leftValue != null ? leftValue.GetDescription() : "0";
                    string rightStr = rightValue != null ? rightValue.GetDescription() : "0";
                    return $"{leftStr} {GetOpSymbol(comparison)} {rightStr}";

                case ConditionType2.HasStatus:
                    return $"{statusTarget.GetDescription()}有{ValueNode.GetStatusName(statusType)}";

                case ConditionType2.DoesNotHaveStatus:
                    return $"{statusTarget.GetDescription()}没有{ValueNode.GetStatusName(statusType)}";

                case ConditionType2.EventContextCheck:
                    return $"事件[{eventName}]已发生";

                case ConditionType2.RuntimeFlagCheck:
                    return $"{ValueNode.GetFlagName(flagRef)}为真";

                case ConditionType2.CardPropertyCheck:
                    return $"卡牌为{ValueNode.GetStatusName((StatusType2)cardPropertyType)}";

                case ConditionType2.TargetExists:
                    return $"目标存在";

                case ConditionType2.ChanceCheck:
                    return $"{chancePercent}%概率";

                case ConditionType2.CustomCondition:
                    return customConditionScript != null ? $"自定义: {customConditionScript.GetDisplayName()}" : "自定义(未绑定)";

                default:
                    return conditionType.ToString();
            }
        }

        public static string GetOpSymbol(ComparisonOperator op) => op switch
        {
            ComparisonOperator.Less => "<",
            ComparisonOperator.LessOrEqual => "≤",
            ComparisonOperator.Equal => "=",
            ComparisonOperator.NotEqual => "≠",
            ComparisonOperator.GreaterOrEqual => "≥",
            ComparisonOperator.Greater => ">",
            _ => "?"
        };
    }

    /// <summary>
    /// 条件组 — 支持 All/Any/None/Not 逻辑和嵌套
    /// </summary>
    [Serializable]
    public class ConditionGroup
    {
        [Tooltip("条件逻辑")]
        public ConditionLogic2 logic = ConditionLogic2.All;

        [Tooltip("条件列表")]
        public List<ConditionEntry> conditions = new List<ConditionEntry>();

        public string GetDescription()
        {
            if (conditions == null || conditions.Count == 0) return "无条件";

            string logicStr = logic switch
            {
                ConditionLogic2.All => "且",
                ConditionLogic2.Any => "或",
                ConditionLogic2.None => "都不满足",
                ConditionLogic2.Not => "不满足",
                _ => "?"
            };

            var sb = new StringBuilder();
            for (int i = 0; i < conditions.Count; i++)
            {
                if (i > 0) sb.Append($" {logicStr} ");
                sb.Append(conditions[i].GetDescription());
            }
            return sb.ToString();
        }

        public ConditionGroup Clone()
        {
            return new ConditionGroup
            {
                logic = logic,
                conditions = conditions != null ? new List<ConditionEntry>(conditions) : new List<ConditionEntry>()
            };
        }
    }

    // ========================================================================
    // 持续时间
    // ========================================================================
    [Serializable]
    public class EffectDuration
    {
        [Tooltip("持续时间类型")]
        public DurationType type = DurationType.Instant;

        [Tooltip("持续回合数（Turns 时生效）")]
        public int turns = 1;

        [Tooltip("触发次数限制（TriggerCount 时生效）")]
        public int triggerCount = 1;

        [Tooltip("回合开始时过期")]
        public bool expireAtTurnStart = false;

        [Tooltip("回合结束时过期")]
        public bool expireAtTurnEnd = false;

        [Tooltip("角色切换时过期")]
        public bool expireOnCharacterSwitch = false;

        [Tooltip("战斗结束时过期")]
        public bool expireOnCombatEnd = true;

        public string GetDescription()
        {
            return type switch
            {
                DurationType.Instant => "立即",
                DurationType.NextTrigger => "下次触发",
                DurationType.TriggerCount => $"触发{triggerCount}次",
                DurationType.CurrentTurn => "本回合",
                DurationType.Turns => $"{turns}回合",
                DurationType.UntilCharacterSwitch => "直到角色切换",
                DurationType.UntilCombatEnd => "直到战斗结束",
                DurationType.PermanentRun => "本局永久",
                _ => type.ToString()
            };
        }
    }

    // ========================================================================
    // 统一效果节点 — 替代旧 CardEffect
    // ========================================================================
    [Serializable]
    public class EffectNode
    {
        [Header("基础")]
        [Tooltip("是否启用")]
        public bool enabled = true;

        [Tooltip("效果名称（策划备注）")]
        public string displayName = "新效果";

        [Tooltip("执行时机")]
        public ExecutionTiming timing = ExecutionTiming.Immediate;

        [Header("发起者")]
        [Tooltip("发起者目标")]
        public TargetSelector source = new TargetSelector { category = TargetCategory.Character, unitTarget = CombatUnitTarget.CurrentCharacter };

        [Header("目标")]
        [Tooltip("效果目标")]
        public TargetSelector target = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.SelectedEnemy };

        [Header("操作")]
        [Tooltip("效果操作类型")]
        public EffectOperation operation = EffectOperation.DealDamage;

        [Header("数值")]
        [Tooltip("主数值表达式")]
        	[SerializeReference]
	public ValueNode value = ValueNode.Constant(0);

        [Tooltip("重复次数表达式")]
        	[SerializeReference]
	public ValueNode repeatCount = ValueNode.Constant(1);

        [Header("条件")]
        [Tooltip("生效条件")]
        public ConditionGroup conditions = new ConditionGroup();

        [Header("持续")]
        [Tooltip("持续时间")]
        public EffectDuration duration = new EffectDuration();

        [Header("输出")]
        [Tooltip("输出变量名（执行结果保存到局部变量）")]
        public string outputVariableName = "";

        [Header("子效果")]
        [Tooltip("子效果列表（RegisterTrigger 时作为触发后执行的效果）")]
        [SerializeReference]
        public List<EffectNode> childEffects = new List<EffectNode>();

        // === 伤害专属参数 ===
        [Header("伤害专属")]
        [Tooltip("伤害缩放模式")]
        public ScalingMode scalingMode = ScalingMode.AddStrength;

        [Tooltip("暴击判定模式")]
        public CriticalCheckMode criticalCheckMode = CriticalCheckMode.PerHit;

        [Tooltip("是否无视全部格挡")]
        public bool ignoreAllBlock = false;

        [Tooltip("是否使用破甲")]
        public bool useArmorBreak = false;

        [Tooltip("破甲数值表达式")]
        	[SerializeReference]
	public ValueNode armorBreakValue = ValueNode.Constant(0);

        [Tooltip("是否计为攻击")]
        public bool countAsAttack = true;

        // === 资源操作专属 ===
        [Header("资源操作专属")]
        [Tooltip("目标资源类型")]
        public PlayerResourceType resourceType = PlayerResourceType.Heat;

        [Tooltip("资源操作方式")]
        public ResourceOperation resourceOp = ResourceOperation.Add;

        // === 属性操作专属 ===
        [Header("属性操作专属")]
        [Tooltip("目标属性类型")]
        public PlayerAttributeType attributeType = PlayerAttributeType.Strength;

        // === 状态专属 ===
        [Header("状态专属")]
        [Tooltip("状态类型")]
        public StatusType2 statusType = StatusType2.ArmorBreak;

        [Tooltip("状态数值表达式")]
        	[SerializeReference]
	public ValueNode statusValue = ValueNode.Constant(1);

        [Tooltip("叠加方式")]
        public StatusStackMode stackMode = StatusStackMode.AddStacks;

        // === 卡牌区域专属 ===
        [Header("卡牌区域专属")]
        [Tooltip("源卡牌区域")]
        public CardZoneType sourceZone = CardZoneType.Hand;

        [Tooltip("目标卡牌区域")]
        public CardZoneType destinationZone = CardZoneType.Hand;

        [Tooltip("卡牌区域操作")]
        public CardZoneOperation zoneOperation = CardZoneOperation.Draw;

        [Tooltip("操作数量表达式")]
        	[SerializeReference]
	public ValueNode zoneCount = ValueNode.Constant(1);

        // === 触发器专属 ===
        [Header("触发器专属")]
        [Tooltip("触发事件")]
        public TriggerEvent triggerEvent = TriggerEvent.OnTurnStart;

        [Tooltip("最大总触发次数（0=无限）")]
        public int maxTriggers = 0;

        [Tooltip("每回合最大触发次数（0=无限）")]
        public int maxTriggersPerTurn = 0;

        [Tooltip("是否仅当前角色激活时生效")]
        public bool activeOnlyWhenOwnerIsActive = true;

        // === 自定义 ===
        [Header("自定义")]
        [Tooltip("自定义操作脚本")]
        public CustomEffectScript customOperation;

        [Tooltip("自定义参数")]
        [TextArea(1, 3)]
        public string customParams = "";

        // === 描述生成 ===
        public string GetDescription()
        {
            var sb = new StringBuilder();

            switch (operation)
            {
                case EffectOperation.DealDamage:
                    sb.Append($"对{target.GetDescription()}造成 ");
                    sb.Append(value?.GetDescription() ?? "0");
                    string repeat = repeatCount?.GetDescription() ?? "1";
                    if (repeat != "1") sb.Append($" ×{repeat}次");
                    if (criticalCheckMode == CriticalCheckMode.Guaranteed) sb.Append(" 必定暴击");
                    else if (criticalCheckMode == CriticalCheckMode.Disabled) sb.Append(" 无法暴击");
                    if (ignoreAllBlock) sb.Append(" 无视格挡");
                    if (useArmorBreak) sb.Append($" 破甲{armorBreakValue?.GetDescription() ?? "0"}");
                    break;

                case EffectOperation.GainBlock:
                    sb.Append($"获得 {value?.GetDescription() ?? "0"} 格挡");
                    break;

                case EffectOperation.ModifyAttribute:
                    sb.Append($"{ValueNode.GetAttrName(attributeType)} {resourceOp} {value?.GetDescription() ?? "0"}");
                    if (duration.type != DurationType.Instant) sb.Append($" ({duration.GetDescription()})");
                    break;

                case EffectOperation.ModifyResource:
                    sb.Append($"{ValueNode.GetResourceName(resourceType)} {resourceOp} {value?.GetDescription() ?? "0"}");
                    break;

                case EffectOperation.ApplyStatus:
                    sb.Append($"施加{ValueNode.GetStatusName(statusType)} {statusValue?.GetDescription() ?? "1"}层 → {target.GetDescription()}");
                    if (duration.type != DurationType.Instant) sb.Append($" ({duration.GetDescription()})");
                    break;

                case EffectOperation.RemoveStatus:
                    sb.Append($"移除{ValueNode.GetStatusName(statusType)} {statusValue?.GetDescription() ?? "1"}层 → {target.GetDescription()}");
                    break;

                case EffectOperation.DrawCards:
                    sb.Append($"抽 {value?.GetDescription() ?? "0"} 张牌");
                    break;

                case EffectOperation.RestoreActionPoints:
                    sb.Append($"恢复 {value?.GetDescription() ?? "0"} 行动点");
                    break;

                case EffectOperation.MoveCards:
                    sb.Append($"{zoneOperation} {zoneCount?.GetDescription() ?? "1"}张牌: {GetZoneName(sourceZone)}→{GetZoneName(destinationZone)}");
                    break;

                case EffectOperation.SwitchCharacter:
                    sb.Append("切换角色");
                    break;

                case EffectOperation.RegisterTrigger:
                    sb.Append($"注册触发器: {GetTriggerName(triggerEvent)}");
                    if (duration.type != DurationType.Instant) sb.Append($" ({duration.GetDescription()})");
                    if (childEffects.Count > 0) sb.Append($" → {childEffects.Count}个子效果");
                    break;

                case EffectOperation.SetVariable:
                    sb.Append($"设置变量 {outputVariableName} = {value?.GetDescription() ?? "0"}");
                    break;

                case EffectOperation.ModifyVariable:
                    sb.Append($"修改变量 {outputVariableName} {resourceOp} {value?.GetDescription() ?? "0"}");
                    break;

                case EffectOperation.CustomOperation:
                    sb.Append(customOperation != null ? $"自定义: {customOperation.GetDisplayName()}" : "自定义(未绑定)");
                    break;

                default:
                    sb.Append(operation.ToString());
                    break;
            }

            // 条件
            if (conditions != null && conditions.conditions != null && conditions.conditions.Count > 0)
            {
                sb.Append($" [条件: {conditions.GetDescription()}]");
            }

            // 输出变量
            if (!string.IsNullOrEmpty(outputVariableName) && operation != EffectOperation.SetVariable)
            {
                sb.Append($" →保存:{outputVariableName}");
            }

            return sb.ToString();
        }

        public EffectNode Clone()
        {
            return new EffectNode
            {
                enabled = enabled,
                displayName = displayName,
                timing = timing,
                source = source != null ? new TargetSelector
                {
                    category = source.category, unitTarget = source.unitTarget, cardTarget = source.cardTarget,
                    selectionMode = source.selectionMode, selectionCount = source.selectionCount,
                    cardFilterType = source.cardFilterType, cardFilterRarity = source.cardFilterRarity,
                    cardFilterKeyword = source.cardFilterKeyword, filterIsLowSanityForm = source.filterIsLowSanityForm,
                    filterTag = source.filterTag
                } : new TargetSelector(),
                target = target != null ? new TargetSelector
                {
                    category = target.category, unitTarget = target.unitTarget, cardTarget = target.cardTarget,
                    selectionMode = target.selectionMode, selectionCount = target.selectionCount,
                    cardFilterType = target.cardFilterType, cardFilterRarity = target.cardFilterRarity,
                    cardFilterKeyword = target.cardFilterKeyword, filterIsLowSanityForm = target.filterIsLowSanityForm,
                    filterTag = target.filterTag
                } : new TargetSelector(),
                operation = operation,
                value = value != null ? CloneValueNode(value) : ValueNode.Constant(0),
                repeatCount = repeatCount != null ? CloneValueNode(repeatCount) : ValueNode.Constant(1),
                conditions = conditions?.Clone() ?? new ConditionGroup(),
                duration = duration != null ? new EffectDuration
                {
                    type = duration.type, turns = duration.turns, triggerCount = duration.triggerCount,
                    expireAtTurnStart = duration.expireAtTurnStart, expireAtTurnEnd = duration.expireAtTurnEnd,
                    expireOnCharacterSwitch = duration.expireOnCharacterSwitch, expireOnCombatEnd = duration.expireOnCombatEnd
                } : new EffectDuration(),
                outputVariableName = outputVariableName,
                childEffects = childEffects != null ? childEffects.ConvertAll(e => e.Clone()) : new List<EffectNode>(),
                scalingMode = scalingMode, criticalCheckMode = criticalCheckMode, ignoreAllBlock = ignoreAllBlock,
                useArmorBreak = useArmorBreak, armorBreakValue = armorBreakValue != null ? CloneValueNode(armorBreakValue) : ValueNode.Constant(0),
                countAsAttack = countAsAttack, resourceType = resourceType, resourceOp = resourceOp,
                attributeType = attributeType, statusType = statusType, statusValue = statusValue != null ? CloneValueNode(statusValue) : ValueNode.Constant(1),
                stackMode = stackMode, sourceZone = sourceZone, destinationZone = destinationZone,
                zoneOperation = zoneOperation, zoneCount = zoneCount != null ? CloneValueNode(zoneCount) : ValueNode.Constant(1),
                triggerEvent = triggerEvent, maxTriggers = maxTriggers, maxTriggersPerTurn = maxTriggersPerTurn,
                activeOnlyWhenOwnerIsActive = activeOnlyWhenOwnerIsActive, customOperation = customOperation, customParams = customParams
            };
        }

        private static ValueNode CloneValueNode(ValueNode src)
        {
            var clone = new ValueNode
            {
                nodeType = src.nodeType, intValue = src.intValue, floatValue = src.floatValue,
                attributeRef = src.attributeRef, resourceRef = src.resourceRef, statusRef = src.statusRef,
                counterRef = src.counterRef, flagRef = src.flagRef, variableName = src.variableName,
                resultRef = src.resultRef, everyN = src.everyN, convertToM = src.convertToM,
                operands = new List<ValueNode>()
            };
            if (src.operands != null)
                foreach (var op in src.operands)
                    clone.operands.Add(op != null ? CloneValueNode(op) : null);
            return clone;
        }

        public static string GetZoneName(CardZoneType z) => z switch
        {
            CardZoneType.Hand => "手牌",
            CardZoneType.DrawPile => "抽牌堆",
            CardZoneType.DiscardPile => "弃牌堆",
            CardZoneType.CombatExhaustPile => "消耗堆",
            CardZoneType.PermanentDeck => "永久牌库",
            CardZoneType.CardsPlayedThisTurn => "本回合打出牌",
            CardZoneType.TemporaryGeneratedCards => "临时生成牌",
            _ => z.ToString()
        };

        public static string GetTriggerName(TriggerEvent t) => t switch
        {
            TriggerEvent.OnCardPlayed => "出牌后",
            TriggerEvent.OnAttackCardPlayed => "出攻击牌后",
            TriggerEvent.OnCardDrawn => "抽牌后",
            TriggerEvent.OnHit => "命中后",
            TriggerEvent.OnCriticalHit => "暴击后",
            TriggerEvent.OnDamageDealt => "造成伤害后",
            TriggerEvent.AfterAttack => "攻击结束后",
            TriggerEvent.OnEnemyKilled => "击杀敌人后",
            TriggerEvent.OnBlockGained => "获得格挡后",
            TriggerEvent.OnDamageTaken => "受到伤害后",
            TriggerEvent.OnSanityChanged => "理智变化后",
            TriggerEvent.OnSanityLost => "失去理智后",
            TriggerEvent.OnHeatChanged => "热度变化后",
            TriggerEvent.OnOverload => "过热时",
            TriggerEvent.OnTurnStart => "回合开始时",
            TriggerEvent.OnTurnEnd => "回合结束时",
            TriggerEvent.OnFirstAttackThisTurn => "本回合首次攻击",
            TriggerEvent.BeforeCharacterSwitch => "角色切换前",
            TriggerEvent.AfterCharacterSwitch => "角色切换后",
            TriggerEvent.OnCharacterActivated => "角色激活时",
            TriggerEvent.OnCharacterDeactivated => "角色停用时",
            TriggerEvent.OnCombatStart => "战斗开始",
            TriggerEvent.OnCombatEnd => "战斗结束",
            _ => t.ToString()
        };

        public static string GetOperationName(EffectOperation op) => op switch
        {
            EffectOperation.DealDamage => "伤害",
            EffectOperation.GainBlock => "格挡",
            EffectOperation.ModifyAttribute => "修改属性",
            EffectOperation.ModifyResource => "修改资源",
            EffectOperation.ApplyStatus => "施加状态",
            EffectOperation.RemoveStatus => "移除状态",
            EffectOperation.DrawCards => "抽牌",
            EffectOperation.RestoreActionPoints => "恢复行动点",
            EffectOperation.MoveCards => "卡牌区域操作",
            EffectOperation.CreateCard => "创建卡牌",
            EffectOperation.CopyCard => "复制卡牌",
            EffectOperation.PlayCardAutomatically => "自动打牌",
            EffectOperation.ReplayCurrentCard => "重新释放本牌",
            EffectOperation.ModifyCardCost => "修改费用",
            EffectOperation.SwitchCharacter => "切换角色",
            EffectOperation.RegisterTrigger => "注册触发器",
            EffectOperation.RemoveTrigger => "移除触发器",
            EffectOperation.SetVariable => "设置变量",
            EffectOperation.ModifyVariable => "修改变量",
            EffectOperation.CustomOperation => "自定义操作",
            _ => op.ToString()
        };
    }
}
