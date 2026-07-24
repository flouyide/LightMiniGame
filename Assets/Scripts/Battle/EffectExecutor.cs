using System.Collections.Generic;
using UnityEngine;
using LightMiniGame.CardEditor;

/// <summary>
/// 效果执行器 —— 将 CardEntry 的 List<CardEffect> 翻译为实际战斗操作。
/// 核心桥梁：卡牌编辑器数据 → 战斗系统执行。
///
/// 使用方式：
///   var ctx = new BattleCardContext(battleManager);
///   var executor = new EffectExecutor(ctx);
///   executor.ExecuteEffects(card.GetEffects(upgraded), ctx);
/// </summary>
public class EffectExecutor
{
    // 类型别名，避免和全局 EffectType 冲突
    private static readonly LightMiniGame.CardEditor.EffectType T_Damage = LightMiniGame.CardEditor.EffectType.Damage;
    private static readonly LightMiniGame.CardEditor.EffectType T_Armor = LightMiniGame.CardEditor.EffectType.Armor;
    private static readonly LightMiniGame.CardEditor.EffectType T_DrawCard = LightMiniGame.CardEditor.EffectType.DrawCard;
    private static readonly LightMiniGame.CardEditor.EffectType T_RestoreEnergy = LightMiniGame.CardEditor.EffectType.RestoreEnergy;
    private static readonly LightMiniGame.CardEditor.EffectType T_ModifyAttribute = LightMiniGame.CardEditor.EffectType.ModifyAttribute;
    private static readonly LightMiniGame.CardEditor.EffectType T_ModifyHandCost = LightMiniGame.CardEditor.EffectType.ModifyHandCost;
    private static readonly LightMiniGame.CardEditor.EffectType T_ApplyStatus = LightMiniGame.CardEditor.EffectType.ApplyStatus;
    private static readonly LightMiniGame.CardEditor.EffectType T_Custom = LightMiniGame.CardEditor.EffectType.Custom;
    private readonly BattleCardContext _ctx;

    public EffectExecutor(BattleCardContext ctx) { _ctx = ctx; }

    /// <summary>
    /// 按列表顺序依次结算所有启用的效果。
    /// </summary>
    public void ExecuteEffects(List<CardEffect> effects, CardEntry sourceCard = null, bool upgraded = false)
    {
        if (effects == null) return;

        // 先检查自定义卡牌脚本是否拦截
        if (sourceCard != null && sourceCard.customCardScript != null)
        {
            bool proceed = sourceCard.customCardScript.OnCardPlayed(_ctx, sourceCard, upgraded);
            if (!proceed) return; // 脚本完全接管，跳过通用效果
        }

        for (int i = 0; i < effects.Count; i++)
        {
            var eff = effects[i];
            if (!eff.enabled) continue;

            // 条件检查
            if (eff.conditions != null && eff.conditions.Count > 0)
            {
                if (!EvaluateConditions(eff))
                {
                    Debug.Log($"[EffectExecutor] 效果[{i}] {eff.effectName} 条件不满足，跳过");
                    continue;
                }
            }

            ExecuteSingleEffect(eff);
        }
    }

    /// <summary>
    /// 执行能力触发后的效果列表。
    /// </summary>
    public void ExecuteAbilityEffects(AbilityData ability)
    {
        if (ability == null) return;

        // 检查自定义能力脚本是否拦截
        if (ability.customAbilityScript != null)
        {
            bool proceed = ability.customAbilityScript.OnTrigger(_ctx, ability);
            if (!proceed) return;
        }

        ExecuteEffects(ability.triggeredEffects);
    }

    // ========================================================================
    // 单个效果执行
    // ========================================================================
    private void ExecuteSingleEffect(CardEffect eff)
    {
        if (eff.effectType == T_Damage) ExecuteDamage(eff);
        else if (eff.effectType == T_Armor) ExecuteArmor(eff);
        else if (eff.effectType == T_DrawCard) ExecuteDrawCard(eff);
        else if (eff.effectType == T_RestoreEnergy) ExecuteRestoreEnergy(eff);
        else if (eff.effectType == T_ModifyAttribute) ExecuteModifyAttribute(eff);
        else if (eff.effectType == T_ModifyHandCost) ExecuteModifyHandCost(eff);
        else if (eff.effectType == T_ApplyStatus) ExecuteApplyStatus(eff);
        else if (eff.effectType == T_Custom) ExecuteCustom(eff);
    }

    // ========================================================================
    // 伤害
    // ========================================================================
    private void ExecuteDamage(CardEffect eff)
    {
        int baseDamage = EvaluateValue(eff.value);
        int times = Mathf.Max(1, eff.times);

        // 确定目标敌人
        int targetIndex = ResolveTargetEnemy(eff.target);
        if (targetIndex < -1 && eff.target != EffectTarget.AllEnemies)
        {
            Debug.Log($"[EffectExecutor] 伤害效果无有效目标，跳过");
            return;
        }

        for (int hit = 0; hit < times; hit++)
        {
            // 逐段独立判定暴击
            bool isCrit = eff.alwaysCrit || Random.value < _ctx.PlayerCritRate;
            int hitDamage = baseDamage;
            if (isCrit) hitDamage = Mathf.RoundToInt(hitDamage * _ctx.PlayerCritDamage);

            if (eff.target == EffectTarget.AllEnemies)
            {
                _ctx.DealDamageToAllEnemies(hitDamage, eff.ignoreArmor);
            }
            else
            {
                int actualTarget = targetIndex >= 0 ? targetIndex : _ctx.SelectedEnemyIndex;
                if (actualTarget >= 0)
                    _ctx.DealDamageToEnemy(actualTarget, hitDamage, eff.ignoreArmor);
            }

            // 暴击事件
            if (isCrit)
                _ctx.RecordEvent("OnCrit");

            // 施加破甲
            if (eff.applyArmorBreak && eff.armorBreakAmount > 0)
            {
                int breakTarget = targetIndex >= 0 ? targetIndex : _ctx.SelectedEnemyIndex;
                if (breakTarget >= 0)
                    _ctx.ApplyStatusToEnemy(breakTarget, StatusType.ArmorBreak, eff.armorBreakAmount);
            }

            Debug.Log($"[EffectExecutor] 伤害第{hit + 1}/{times}击: {hitDamage}{(isCrit ? " 暴击" : "")} → 目标[{targetIndex}]");
        }
    }

    // ========================================================================
    // 护甲
    // ========================================================================
    private void ExecuteArmor(CardEffect eff)
    {
        int armor = EvaluateValue(eff.value);
        _ctx.AddPlayerArmor(armor);
        Debug.Log($"[EffectExecutor] 获得护甲 {armor}");
    }

    // ========================================================================
    // 抽牌
    // ========================================================================
    private void ExecuteDrawCard(CardEffect eff)
    {
        int count = EvaluateValue(eff.value);
        // 非立即时机的抽牌需要延迟处理，这里先只处理立即
        if (eff.timing == EffectTiming.Immediate)
        {
            _ctx.DrawCards(count);
            Debug.Log($"[EffectExecutor] 抽牌 {count} 张");
        }
        else
        {
            Debug.Log($"[EffectExecutor] 抽牌 {count} 张 (时机={eff.timing}，需延迟处理)");
        }
    }

    // ========================================================================
    // 回费
    // ========================================================================
    private void ExecuteRestoreEnergy(CardEffect eff)
    {
        int amount = EvaluateValue(eff.value);
        if (eff.timing == EffectTiming.Immediate)
        {
            _ctx.AddPlayerEnergy(amount);
            Debug.Log($"[EffectExecutor] 回复能量 {amount}");
        }
        else
        {
            Debug.Log($"[EffectExecutor] 回复能量 {amount} (时机={eff.timing}，需延迟处理)");
        }
    }

    // ========================================================================
    // 修改属性
    // ========================================================================
    private void ExecuteModifyAttribute(CardEffect eff)
    {
        int amount = EvaluateValue(eff.value);
        _ctx.ModifyPlayerAttribute(eff.modAttribute, eff.modifyMethod, amount);
        Debug.Log($"[EffectExecutor] 修改属性 {eff.modAttribute} {eff.modifyMethod} {amount}");
    }

    // ========================================================================
    // 修改手牌费用
    // ========================================================================
    private void ExecuteModifyHandCost(CardEffect eff)
    {
        Debug.Log($"[EffectExecutor] 修改手牌费用 {eff.handCostTarget} {(eff.costChange >= 0 ? "+" : "")}{eff.costChange}");
        // 实际费用修改需要手牌系统支持，这里记录意图
    }

    // ========================================================================
    // 施加状态
    // ========================================================================
    private void ExecuteApplyStatus(CardEffect eff)
    {
        int stacks = Mathf.Max(1, eff.statusStacks);

        if (eff.target == EffectTarget.Player)
        {
            _ctx.ApplyStatusToPlayer(eff.statusType, stacks);
            Debug.Log($"[EffectExecutor] 施加状态 {eff.statusType} {stacks}层 → 玩家");
        }
        else
        {
            int targetIndex = ResolveTargetEnemy(eff.target);
            if (targetIndex >= 0)
            {
                _ctx.ApplyStatusToEnemy(targetIndex, eff.statusType, stacks);
                Debug.Log($"[EffectExecutor] 施加状态 {eff.statusType} {stacks}层 → 敌人[{targetIndex}]");
            }
            else if (eff.target == EffectTarget.AllEnemies)
            {
                for (int i = 0; i < _ctx.EnemyCount; i++)
                    _ctx.ApplyStatusToEnemy(i, eff.statusType, stacks);
                Debug.Log($"[EffectExecutor] 施加状态 {eff.statusType} {stacks}层 → 所有敌人");
            }
        }
    }

    // ========================================================================
    // 自定义效果
    // ========================================================================
    private void ExecuteCustom(CardEffect eff)
    {
        if (eff.customEffectScript != null)
        {
            eff.customEffectScript.Execute(_ctx, eff.customParams);
            Debug.Log($"[EffectExecutor] 自定义效果: {eff.customEffectScript.GetDisplayName()}");
        }
        else
        {
            Debug.LogWarning($"[EffectExecutor] 自定义效果未绑定脚本，跳过");
        }
    }

    // ========================================================================
    // 条件评估
    // ========================================================================
    private bool EvaluateConditions(CardEffect eff)
    {
        if (eff.conditions == null || eff.conditions.Count == 0) return true;

        bool result = eff.conditionLogic == ConditionLogic.All;
        foreach (var cond in eff.conditions)
        {
            bool met = EvaluateSingleCondition(cond);

            if (eff.conditionLogic == ConditionLogic.All)
            {
                result = result && met;
                if (!met) break;
            }
            else
            {
                result = result || met;
            }
        }
        return result;
    }

    private bool EvaluateSingleCondition(EffectCondition cond)
    {
        switch (cond.conditionType)
        {
            case ConditionType.SourceAttributeCheck:
            case ConditionType.TargetAttributeCheck:
                int val = GetAttributeValue(cond.attributeRef);
                return cond.comparison switch
                {
                    ComparisonOp.Less => val < cond.compareValue,
                    ComparisonOp.LessEqual => val <= cond.compareValue,
                    ComparisonOp.Equal => Mathf.Approximately(val, cond.compareValue),
                    ComparisonOp.GreaterEqual => val >= cond.compareValue,
                    ComparisonOp.Greater => val > cond.compareValue,
                    ComparisonOp.NotEqual => !Mathf.Approximately(val, cond.compareValue),
                    _ => true
                };

            case ConditionType.EventOccurred:
                return _ctx.HasEventOccurred(cond.eventName);

            case ConditionType.SourceHasStatus:
            case ConditionType.TargetHasStatus:
                // 状态检查需要战斗状态系统支持，暂返回 false
                return false;

            case ConditionType.TurnCounterCheck:
                return CompareValue(_ctx.GetTurnCounter(cond.eventName), cond.comparison, cond.compareValue);

            case ConditionType.BattleCounterCheck:
                return CompareValue(_ctx.GetBattleCounter(cond.eventName), cond.comparison, cond.compareValue);

            case ConditionType.Custom:
                if (cond.customConditionScript != null)
                    return cond.customConditionScript.Evaluate(_ctx, "");
                return false;

            default:
                return true;
        }
    }

    private static bool CompareValue(int val, ComparisonOp op, float compareValue) => op switch
    {
        ComparisonOp.Less => val < compareValue,
        ComparisonOp.LessEqual => val <= compareValue,
        ComparisonOp.Equal => Mathf.Approximately(val, compareValue),
        ComparisonOp.GreaterEqual => val >= compareValue,
        ComparisonOp.Greater => val > compareValue,
        ComparisonOp.NotEqual => !Mathf.Approximately(val, compareValue),
        _ => true
    };

    // ========================================================================
    // 数值表达式求值
    // ========================================================================
    private int EvaluateValue(ValueExpression val)
    {
        int attrVal = GetAttributeValue(val.attributeRef);
        int counterVal = GetAttributeValue(val.counterRef);

        return val.formulaType switch
        {
            ValueFormulaType.Fixed => val.baseValue,
            ValueFormulaType.BasePlusAttribute => val.baseValue + attrVal,
            ValueFormulaType.BasePlusAttributeTimesCoeff => val.baseValue + Mathf.RoundToInt(attrVal * val.coefficient),
            ValueFormulaType.BasePlusCounterTimesCoeff => val.baseValue + Mathf.RoundToInt(counterVal * val.coefficient),
            _ => val.baseValue
        };
    }

    private int GetAttributeValue(AttributeRef attr) => attr switch
    {
        AttributeRef.Strength => _ctx.PlayerStrength,
        AttributeRef.Dexterity => _ctx.PlayerDexterity,
        AttributeRef.CurrentHP => _ctx.PlayerHP,
        AttributeRef.MaxHP => _ctx.PlayerMaxHP,
        AttributeRef.LostHP => _ctx.PlayerMaxHP - _ctx.PlayerHP,
        AttributeRef.CurrentSanity => _ctx.PlayerSanity,
        AttributeRef.SanityLostThisTurn => _ctx.GetTurnCounter("SanityLost"),
        AttributeRef.TotalSanityLostThisBattle => _ctx.GetBattleCounter("TotalSanityLost"),
        AttributeRef.BleedValue => _ctx.EnemyCount > 0 ? _ctx.GetEnemyBleed(_ctx.SelectedEnemyIndex) : 0,
        AttributeRef.ArmorBreakValue => _ctx.EnemyCount > 0 ? _ctx.GetEnemyArmorBreak(_ctx.SelectedEnemyIndex) : 0,
        AttributeRef.CritRate => Mathf.RoundToInt(_ctx.PlayerCritRate * 100),
        AttributeRef.CritDamage => Mathf.RoundToInt(_ctx.PlayerCritDamage * 100),
        _ => 0
    };

    // ========================================================================
    // 目标解析
    // ========================================================================
    private int ResolveTargetEnemy(EffectTarget target)
    {
        return target switch
        {
            EffectTarget.SelectedEnemy => _ctx.SelectedEnemyIndex,
            EffectTarget.RandomEnemy => _ctx.EnemyCount > 0 ? Random.Range(0, _ctx.EnemyCount) : -1,
            EffectTarget.AllEnemies => -2, // 特殊值表示所有
            EffectTarget.Player => -1,
            EffectTarget.SourceSelf => -1,
            _ => -1
        };
    }
}
