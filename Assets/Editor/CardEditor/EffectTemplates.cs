using System.Collections.Generic;
using LightMiniGame.CardEditor;

namespace LightMiniGame.CardEditor.Editor
{
    /// <summary>
    /// 效果模板 —— 预填通用 EffectNode，策划选择后直接使用或微调。
    /// 模板只负责预填数据，不创建新的底层逻辑。
    /// </summary>
    public static class EffectTemplates
    {
        public static readonly string[] TemplateNames = new[]
        {
            "单体固定伤害",
            "单体力量伤害",
            "全体伤害",
            "多段攻击",
            "必定暴击攻击",
            "获得敏捷格挡",
            "修改属性(力量)",
            "修改资源(热度)",
            "修改资源(理智)",
            "施加破甲",
            "施加流血",
            "抽牌",
            "回复行动点",
            "下张牌减费",
            "下张攻击牌减费",
            "下次攻击增伤",
            "下次攻击必定暴击",
            "回合开始触发",
            "回合结束触发",
            "角色切换触发",
            "随机消耗手牌",
            "自动打牌堆顶",
            "注册能力",
            "生成指定卡到手牌",
            "施加疲惫",
            "按目标疲惫造成伤害",
            "出指定卡后触发",
            "本场覆盖卡牌状态",
            "给卡牌添加词条",
            "给抽牌堆随机卡加词条",
        };

        public static EffectNode CreateFromTemplate(string name)
        {
            return name switch
            {
                "单体固定伤害" => SingleDamage(),
                "单体力量伤害" => StrengthDamage(),
                "全体伤害" => AllEnemyDamage(),
                "多段攻击" => MultiHit(),
                "必定暴击攻击" => GuaranteedCrit(),
                "获得敏捷格挡" => DexterityBlock(),
                "修改属性(力量)" => ModifyStrength(),
                "修改资源(热度)" => ModifyHeat(),
                "修改资源(理智)" => ModifySanity(),
                "施加破甲" => ApplyArmorBreak(),
                "施加流血" => ApplyBleed(),
                "抽牌" => DrawCards(),
                "回复行动点" => RestoreAP(),
                "下张牌减费" => NextCardCostReduce(),
                "下张攻击牌减费" => NextAttackCardCostReduce(),
                "下次攻击增伤" => NextAttackDamageBonus(),
                "下次攻击必定暴击" => NextAttackGuaranteedCrit(),
                "回合开始触发" => TurnStartTrigger(),
                "回合结束触发" => TurnEndTrigger(),
                "角色切换触发" => CharacterSwitchTrigger(),
                "随机消耗手牌" => RandomExhaust(),
                "自动打牌堆顶" => AutoPlayTopCard(),
                "注册能力" => RegisterAbility(),
                "生成指定卡到手牌" => CreateCardToHand(),
                "施加疲惫" => ApplyFatigue(),
                "按目标疲惫造成伤害" => DamageFromFatigue(),
                "出指定卡后触发" => OnPlayedCardTrigger(),
                "本场覆盖卡牌状态" => OverrideCardStatus(),
                "给卡牌添加词条" => AddKeywordToCards(),
                "给抽牌堆随机卡加词条" => AddKeywordToRandomDrawPile(),
                _ => new EffectNode { displayName = name }
            };
        }

        // ========================================================================
        // 攻击类
        // ========================================================================

        private static EffectNode SingleDamage() => new EffectNode
        {
            displayName = "单体伤害",
            operation = EffectOperation.DealDamage,
            target = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.SelectedEnemy },
            value = ValueNode.Constant(8),
            repeatCount = ValueNode.Constant(1),
            scalingMode = ScalingMode.Fixed,
            criticalCheckMode = CriticalCheckMode.PerHit
        };

        private static EffectNode StrengthDamage() => new EffectNode
        {
            displayName = "力量伤害",
            operation = EffectOperation.DealDamage,
            target = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.SelectedEnemy },
            value = ValueNode.Constant(6),
            repeatCount = ValueNode.Constant(1),
            scalingMode = ScalingMode.AddStrength,
            criticalCheckMode = CriticalCheckMode.PerHit
        };

        private static EffectNode AllEnemyDamage() => new EffectNode
        {
            displayName = "全体伤害",
            operation = EffectOperation.DealDamage,
            target = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.AllEnemies },
            value = ValueNode.Constant(6),
            repeatCount = ValueNode.Constant(1),
            scalingMode = ScalingMode.Fixed,
            criticalCheckMode = CriticalCheckMode.PerHit
        };

        private static EffectNode MultiHit() => new EffectNode
        {
            displayName = "多段攻击",
            operation = EffectOperation.DealDamage,
            target = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.SelectedEnemy },
            value = ValueNode.Constant(6),
            repeatCount = ValueNode.Constant(3),
            scalingMode = ScalingMode.AddStrength,
            criticalCheckMode = CriticalCheckMode.PerHit
        };

        private static EffectNode GuaranteedCrit() => new EffectNode
        {
            displayName = "必定暴击攻击",
            operation = EffectOperation.DealDamage,
            target = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.SelectedEnemy },
            value = ValueNode.Constant(10),
            repeatCount = ValueNode.Constant(1),
            scalingMode = ScalingMode.AddStrength,
            criticalCheckMode = CriticalCheckMode.Guaranteed
        };

        // ========================================================================
        // 格挡类
        // ========================================================================

        private static EffectNode DexterityBlock() => new EffectNode
        {
            displayName = "敏捷格挡",
            operation = EffectOperation.GainBlock,
            value = ValueNode.Constant(5),
            scalingMode = ScalingMode.AddStrength // 实际是 AddDexterity，运行时映射为敏捷
        };

        // ========================================================================
        // 属性/资源类
        // ========================================================================

        private static EffectNode ModifyStrength() => new EffectNode
        {
            displayName = "力量+2",
            operation = EffectOperation.ModifyAttribute,
            attributeType = LightMiniGame.CardEditor.PlayerAttributeType.Strength,
            resourceOp = ResourceOperation.Add,
            value = ValueNode.Constant(2),
            duration = new EffectDuration { type = DurationType.UntilCombatEnd }
        };

        private static EffectNode ModifyHeat() => new EffectNode
        {
            displayName = "热度变化",
            operation = EffectOperation.ModifyResource,
            resourceType = LightMiniGame.CardEditor.PlayerResourceType.Heat,
            resourceOp = ResourceOperation.Subtract,
            value = ValueNode.Constant(8),
            outputVariableName = "ActualHeatReduced"
        };

        private static EffectNode ModifySanity() => new EffectNode
        {
            displayName = "理智变化",
            operation = EffectOperation.ModifyResource,
            resourceType = LightMiniGame.CardEditor.PlayerResourceType.Sanity,
            resourceOp = ResourceOperation.Subtract,
            value = ValueNode.Constant(1)
        };

        // ========================================================================
        // 状态类
        // ========================================================================

        private static EffectNode ApplyArmorBreak() => new EffectNode
        {
            displayName = "施加破甲",
            operation = EffectOperation.ApplyStatus,
            statusType = StatusType2.ArmorBreak,
            statusValue = ValueNode.Constant(2),
            stackMode = StatusStackMode.AddStacks,
            target = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.SelectedEnemy }
        };

        private static EffectNode ApplyBleed() => new EffectNode
        {
            displayName = "施加流血",
            operation = EffectOperation.ApplyStatus,
            statusType = StatusType2.Bleed,
            statusValue = ValueNode.Constant(3),
            stackMode = StatusStackMode.AddStacks,
            target = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.SelectedEnemy }
        };

        // ========================================================================
        // 牌堆类
        // ========================================================================

        private static EffectNode DrawCards() => new EffectNode
        {
            displayName = "抽牌",
            operation = EffectOperation.DrawCards,
            value = ValueNode.Constant(1)
        };

        private static EffectNode RestoreAP() => new EffectNode
        {
            displayName = "恢复行动点",
            operation = EffectOperation.RestoreActionPoints,
            value = ValueNode.Constant(1)
        };

        // ========================================================================
        // 触发器类
        // ========================================================================

        private static EffectNode NextCardCostReduce() => new EffectNode
        {
            displayName = "下张牌减费",
            operation = EffectOperation.RegisterTrigger,
            triggerEvent = TriggerEvent.OnCardPlayAttempt,
            duration = new EffectDuration { type = DurationType.NextTrigger },
            childEffects = new List<EffectNode>
            {
                new EffectNode
                {
                    displayName = "减费",
                    operation = EffectOperation.ModifyCardCost,
                    value = ValueNode.Constant(-1)
                }
            }
        };

        private static EffectNode NextAttackCardCostReduce() => new EffectNode
        {
            displayName = "下张攻击牌减费",
            operation = EffectOperation.RegisterTrigger,
            triggerEvent = TriggerEvent.OnAttackCardPlayed,
            duration = new EffectDuration { type = DurationType.NextTrigger },
            childEffects = new List<EffectNode>
            {
                new EffectNode
                {
                    displayName = "减费",
                    operation = EffectOperation.ModifyCardCost,
                    value = ValueNode.Constant(-1)
                }
            }
        };

        private static EffectNode NextAttackDamageBonus() => new EffectNode
        {
            displayName = "下次攻击增伤",
            operation = EffectOperation.RegisterTrigger,
            triggerEvent = TriggerEvent.BeforeAttack,
            duration = new EffectDuration { type = DurationType.NextTrigger },
            childEffects = new List<EffectNode>
            {
                new EffectNode
                {
                    displayName = "增伤",
                    operation = EffectOperation.ApplyStatus,
                    statusType = StatusType2.NextAttackDamageBonus,
                    statusValue = ValueNode.Constant(50),
                    target = new TargetSelector { category = TargetCategory.Character, unitTarget = CombatUnitTarget.CurrentCharacter }
                }
            }
        };

        private static EffectNode NextAttackGuaranteedCrit() => new EffectNode
        {
            displayName = "下次必暴",
            operation = EffectOperation.RegisterTrigger,
            triggerEvent = TriggerEvent.BeforeAttack,
            duration = new EffectDuration { type = DurationType.NextTrigger },
            childEffects = new List<EffectNode>
            {
                new EffectNode
                {
                    displayName = "必暴",
                    operation = EffectOperation.ApplyStatus,
                    statusType = StatusType2.NextAttackGuaranteedCritical,
                    statusValue = ValueNode.Constant(1),
                    target = new TargetSelector { category = TargetCategory.Character, unitTarget = CombatUnitTarget.CurrentCharacter }
                }
            }
        };

        private static EffectNode TurnStartTrigger() => new EffectNode
        {
            displayName = "回合开始触发",
            operation = EffectOperation.RegisterTrigger,
            triggerEvent = TriggerEvent.OnTurnStart,
            duration = new EffectDuration { type = DurationType.NextTrigger },
            childEffects = new List<EffectNode>
            {
                new EffectNode
                {
                    displayName = "效果",
                    operation = EffectOperation.DrawCards,
                    value = ValueNode.Constant(1)
                }
            }
        };

        private static EffectNode TurnEndTrigger() => new EffectNode
        {
            displayName = "回合结束触发",
            operation = EffectOperation.RegisterTrigger,
            triggerEvent = TriggerEvent.OnTurnEnd,
            duration = new EffectDuration { type = DurationType.CurrentTurn },
            childEffects = new List<EffectNode>
            {
                new EffectNode
                {
                    displayName = "效果",
                    operation = EffectOperation.ModifyResource,
                    resourceType = LightMiniGame.CardEditor.PlayerResourceType.Sanity,
                    resourceOp = ResourceOperation.Add,
                    value = ValueNode.Constant(1)
                }
            }
        };

        private static EffectNode CharacterSwitchTrigger() => new EffectNode
        {
            displayName = "角色切换触发",
            operation = EffectOperation.RegisterTrigger,
            triggerEvent = TriggerEvent.AfterCharacterSwitch,
            duration = new EffectDuration { type = DurationType.NextTrigger },
            childEffects = new List<EffectNode>
            {
                new EffectNode
                {
                    displayName = "登场效果",
                    operation = EffectOperation.ModifyAttribute,
                    attributeType = LightMiniGame.CardEditor.PlayerAttributeType.Strength,
                    resourceOp = ResourceOperation.Add,
                    value = ValueNode.Constant(2),
                    target = new TargetSelector { category = TargetCategory.Character, unitTarget = CombatUnitTarget.SwitchedInCharacter }
                }
            }
        };

        private static EffectNode RandomExhaust() => new EffectNode
        {
            displayName = "随机消耗手牌",
            operation = EffectOperation.MoveCards,
            zoneOperation = CardZoneOperation.ExhaustThisCombat,
            sourceZone = CardZoneType.Hand,
            destinationZone = CardZoneType.CombatExhaustPile,
            zoneCount = ValueNode.Constant(1)
        };

        private static EffectNode AutoPlayTopCard() => new EffectNode
        {
            displayName = "自动打牌堆顶",
            operation = EffectOperation.MoveCards,
            zoneOperation = CardZoneOperation.AutoPlay,
            sourceZone = CardZoneType.DrawPile,
            destinationZone = CardZoneType.Hand,
            zoneCount = ValueNode.Constant(1)
        };

        private static EffectNode RegisterAbility() => new EffectNode
        {
            displayName = "注册能力",
            operation = EffectOperation.RegisterTrigger,
            triggerEvent = TriggerEvent.OnTurnStart,
            duration = new EffectDuration { type = DurationType.UntilCombatEnd },
            maxTriggers = 0,
            maxTriggersPerTurn = 1,
            activeOnlyWhenOwnerIsActive = true,
            childEffects = new List<EffectNode>
            {
                new EffectNode
                {
                    displayName = "触发效果",
                    operation = EffectOperation.DrawCards,
                    value = ValueNode.Constant(1)
                }
            }
        };

        private static EffectNode CreateCardToHand() => new EffectNode
        {
            displayName = "生成指定卡",
            operation = EffectOperation.CreateCard,
            destinationZone = CardZoneType.Hand,
            value = ValueNode.Constant(1)
        };

        private static EffectNode ApplyFatigue() => new EffectNode
        {
            displayName = "施加疲惫",
            operation = EffectOperation.ApplyStatus,
            statusType = StatusType2.Fatigue,
            statusValue = ValueNode.Constant(2),
            target = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.SelectedEnemy }
        };

        private static EffectNode DamageFromFatigue() => new EffectNode
        {
            displayName = "按疲惫伤害",
            operation = EffectOperation.DealDamage,
            target = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.SelectedEnemy },
            value = new ValueNode { nodeType = ValueNodeType.ReadStatusStacks, statusRef = StatusType2.Fatigue },
            repeatCount = ValueNode.Constant(1)
        };

        private static EffectNode OnPlayedCardTrigger() => new EffectNode
        {
            displayName = "出指定卡后",
            operation = EffectOperation.RegisterTrigger,
            triggerEvent = TriggerEvent.OnCardPlayed,
            duration = new EffectDuration { type = DurationType.UntilCombatEnd },
            conditions = new ConditionGroup
            {
                logic = ConditionLogic2.All,
                conditions = new List<ConditionEntry>
                {
                    new ConditionEntry { conditionType = ConditionType2.PlayedCardMatches }
                }
            },
            childEffects = new List<EffectNode>
            {
                new EffectNode
                {
                    displayName = "触发效果",
                    operation = EffectOperation.ApplyStatus,
                    statusType = StatusType2.Fatigue,
                    statusValue = ValueNode.Constant(2),
                    target = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.RandomEnemy }
                }
            }
        };

        private static EffectNode OverrideCardStatus() => new EffectNode
        {
            displayName = "覆盖卡牌状态",
            operation = EffectOperation.ModifyCardProperty,
            statusType = StatusType2.Fatigue,
            statusValue = ValueNode.Constant(3)
        };

        private static EffectNode AddKeywordToCards() => new EffectNode
        {
            displayName = "添加词条",
            operation = EffectOperation.MoveCards,
            zoneOperation = CardZoneOperation.AddTemporaryKeyword,
            keywordToApply = CardKeyword.Recycle,
            target = new TargetSelector
            {
                category = TargetCategory.Card,
                cardTarget = CardTarget.AllCardsInHand
            },
            zoneCount = ValueNode.Constant(1),
            duration = new EffectDuration { type = DurationType.UntilCombatEnd, expireOnCombatEnd = true }
        };

        private static EffectNode AddKeywordToRandomDrawPile() => new EffectNode
        {
            displayName = "抽牌堆随机加词条",
            operation = EffectOperation.MoveCards,
            zoneOperation = CardZoneOperation.AddTemporaryKeyword,
            keywordToApply = CardKeyword.Recycle,
            target = new TargetSelector
            {
                category = TargetCategory.Card,
                cardTarget = CardTarget.RandomCardsInDrawPile,
                selectionMode = CardSelectionMode.RandomCount,
                selectionCount = 2
            },
            zoneCount = ValueNode.Constant(2),
            duration = new EffectDuration { type = DurationType.UntilCombatEnd, expireOnCombatEnd = true }
        };
    }
}
