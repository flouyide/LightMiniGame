using System.Collections.Generic;
using UnityEngine;
using LightMiniGame.CardEditor;

/// <summary>
/// 能力系统 —— 运行时管理已激活能力的触发与执行。
/// 能力卡打出后，能力被激活并注册到此系统；
/// 当战斗事件发生时（暴击、回合开始等），此系统检查所有已激活能力是否满足触发条件，
/// 满足则通过 EffectExecutor 执行其触发后效果列表。
///
/// 角色能力库彼此独立：A 激活的能力只对 A 生效，切换到 B 后 A 的能力暂停。
/// </summary>
public class AbilitySystem
{
    private class ActiveAbility
    {
        public AbilityData data;
        public CardEntry sourceEntry;
        public bool upgraded;
        public int totalTriggersUsed;
        public int triggersThisTurn;
    }

    private readonly List<ActiveAbility> _abilities = new List<ActiveAbility>();
    private readonly List<ActiveAbility> _suspended = new List<ActiveAbility>();
    private readonly EffectExecutor _executor;
    private readonly BattleCardContext _ctx;

    public AbilitySystem(EffectExecutor executor, BattleCardContext ctx)
    {
        _executor = executor;
        _ctx = ctx;
    }

    /// <summary>激活一张能力卡（打出后调用）</summary>
    public void Activate(AbilityData ability, CardEntry sourceEntry, bool upgraded)
    {
        if (ability == null) return;
        _abilities.Add(new ActiveAbility
        {
            data = ability,
            sourceEntry = sourceEntry,
            upgraded = upgraded
        });
        Debug.Log($"[AbilitySystem] 能力已激活: {ability.abilityName}");
    }

    /// <summary>切换角色时，挂起当前角色所有能力（暂停但不消失）</summary>
    public void SuspendAll()
    {
        _suspended.AddRange(_abilities);
        _abilities.Clear();
        Debug.Log($"[AbilitySystem] 挂起 {_suspended.Count} 个能力");
    }

    /// <summary>切换回来时，恢复之前挂起的能力</summary>
    public void ResumeAll()
    {
        _abilities.AddRange(_suspended);
        _suspended.Clear();
        Debug.Log($"[AbilitySystem] 恢复 {_abilities.Count} 个能力");
    }

    /// <summary>每回合开始时重置本回合触发计数</summary>
    public void OnTurnStart()
    {
        foreach (var ab in _abilities)
            ab.triggersThisTurn = 0;
    }

    /// <summary>
    /// 触发指定时机的能力。由 BattleManager 在对应事件发生时调用。
    /// </summary>
    public void OnTrigger(AbilityTrigger trigger)
    {
        for (int i = 0; i < _abilities.Count; i++)
        {
            var ab = _abilities[i];
            if (ab.data.trigger != trigger) continue;

            // 检查总触发次数限制
            if (ab.data.maxTriggers > 0 && ab.totalTriggersUsed >= ab.data.maxTriggers)
                continue;

            // 检查每回合触发次数限制
            if (ab.data.maxTriggersPerTurn > 0 && ab.triggersThisTurn >= ab.data.maxTriggersPerTurn)
                continue;

            // 检查触发条件
            if (ab.data.triggerConditions != null && ab.data.triggerConditions.Count > 0)
            {
                if (!EvaluateConditions(ab.data.triggerConditions, ab.data.triggerConditionLogic))
                    continue;
            }

            // 执行触发后效果
            Debug.Log($"[AbilitySystem] 触发能力: {ab.data.abilityName}");
            _executor.ExecuteAbilityEffects(ab.data);
            ab.totalTriggersUsed++;
            ab.triggersThisTurn++;
        }
    }

    /// <summary>清空所有能力（战斗结束时）</summary>
    public void Clear()
    {
        _abilities.Clear();
        _suspended.Clear();
    }

    // ========================================================================
    // 条件评估
    // ========================================================================
    private bool EvaluateConditions(List<EffectCondition> conditions, ConditionLogic logic)
    {
        bool result = logic == ConditionLogic.All;
        foreach (var cond in conditions)
        {
            bool met = EvaluateSingle(cond);

            if (logic == ConditionLogic.All)
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

    private bool EvaluateSingle(EffectCondition cond)
    {
        switch (cond.conditionType)
        {
            case ConditionType.SourceAttributeCheck:
            case ConditionType.TargetAttributeCheck:
                int val = GetAttrValue(cond.attributeRef);
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

            case ConditionType.TurnCounterCheck:
                return CompareValue(_ctx.GetTurnCounter(cond.eventName), cond.comparison, cond.compareValue);

            case ConditionType.BattleCounterCheck:
                return CompareValue(_ctx.GetBattleCounter(cond.eventName), cond.comparison, cond.compareValue);

            case ConditionType.Custom:
                return cond.customConditionScript != null && cond.customConditionScript.Evaluate(_ctx, "");

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

    private int GetAttrValue(AttributeRef attr) => attr switch
    {
        AttributeRef.Strength => _ctx.PlayerStrength,
        AttributeRef.Dexterity => _ctx.PlayerDexterity,
        AttributeRef.CurrentHP => _ctx.PlayerHP,
        AttributeRef.MaxHP => _ctx.PlayerMaxHP,
        AttributeRef.LostHP => _ctx.PlayerMaxHP - _ctx.PlayerHP,
        AttributeRef.CurrentSanity => _ctx.PlayerSanity,
        AttributeRef.CritRate => Mathf.RoundToInt(_ctx.PlayerCritRate * 100),
        AttributeRef.CritDamage => Mathf.RoundToInt(_ctx.PlayerCritDamage * 100),
        _ => 0
    };
}
