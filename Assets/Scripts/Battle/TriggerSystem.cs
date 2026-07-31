using System;
using System.Collections.Generic;
using UnityEngine;
using LightMiniGame.CardEditor;

/// <summary>
/// 统一触发器系统 —— 能力和临时延迟效果共用同一套机制。
///
/// 能力卡打出后注册一个持久触发器（持续到战斗结束或被移除）。
/// 临时效果（如"下张攻击牌减费"）注册一个临时触发器（持续到下次触发后消失）。
///
/// 所有触发器监听 TriggerEvent，事件发生时检查条件，满足则执行子效果列表。
/// </summary>
public class TriggerSystem
{
    /// <summary>
    /// 一个已注册的触发器实例（持久或临时）。
    /// </summary>
    private class TriggerInstance
    {
        public string id;                    // 唯一标识
        public TriggerEvent triggerEvent;    // 监听的事件
        public ConditionGroup conditions;    // 触发条件
        public List<EffectNode> effects;      // 触发后执行的效果
        public EffectDuration duration;       // 持续时间
        public int triggersUsed;             // 已触发次数
        public int maxTriggers;               // 最大触发次数 (0=无限)
        public int maxPerTurn;                // 每回合最大触发次数 (0=无限)
        public int triggersThisTurn;          // 本回合已触发次数
        public bool isAbility;                // 是否为能力（能力卡注册的持久触发器）
        public bool activeOnlyWhenOwnerIsActive; // 是否仅在持有角色激活时生效
        public int ownerCharacterIndex;       // 持有角色索引 (-1=全局)
        public Dictionary<string, int> localVars; // 局部变量快照（临时触发器可能需要）
        public bool expired;
    }

    private readonly List<TriggerInstance> _triggers = new List<TriggerInstance>();
    private readonly List<TriggerInstance> _suspended = new List<TriggerInstance>();
    private readonly EffectExecutorV2 _executor;

    public int ActiveTriggerCount => _triggers.Count;

    public TriggerSystem(EffectExecutorV2 executor) { _executor = executor; }

    // ========================================================================
    // 注册触发器
    // ========================================================================

    /// <summary>
    /// 注册一个持久能力触发器。
    /// </summary>
    public string RegisterAbility(
        TriggerEvent triggerEvent,
        ConditionGroup conditions,
        List<EffectNode> effects,
        EffectDuration duration,
        int maxTriggers = 0,
        int maxPerTurn = 0,
        bool activeOnlyWhenOwnerIsActive = true,
        int ownerCharacterIndex = -1)
    {
        var inst = new TriggerInstance
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8),
            triggerEvent = triggerEvent,
            conditions = conditions,
            effects = effects ?? new List<EffectNode>(),
            duration = duration ?? new EffectDuration { type = DurationType.UntilCombatEnd },
            maxTriggers = maxTriggers,
            maxPerTurn = maxPerTurn,
            isAbility = true,
            activeOnlyWhenOwnerIsActive = activeOnlyWhenOwnerIsActive,
            ownerCharacterIndex = ownerCharacterIndex
        };
        _triggers.Add(inst);
        Debug.Log($"[TriggerSystem] 注册能力触发器: {triggerEvent} (id={inst.id})");
        return inst.id;
    }

    /// <summary>
    /// 注册一个临时触发器（由 RegisterTrigger 效果节点创建）。
    /// </summary>
    public string RegisterTemporary(
        TriggerEvent triggerEvent,
        ConditionGroup conditions,
        List<EffectNode> effects,
        EffectDuration duration,
        int ownerCharacterIndex = -1,
        Dictionary<string, int> localVarSnapshot = null)
    {
        var inst = new TriggerInstance
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8),
            triggerEvent = triggerEvent,
            conditions = conditions,
            effects = effects ?? new List<EffectNode>(),
            duration = duration ?? new EffectDuration { type = DurationType.NextTrigger },
            isAbility = false,
            activeOnlyWhenOwnerIsActive = false,
            ownerCharacterIndex = ownerCharacterIndex,
            localVars = localVarSnapshot != null ? new Dictionary<string, int>(localVarSnapshot) : null
        };
        _triggers.Add(inst);
        Debug.Log($"[TriggerSystem] 注册临时触发器: {triggerEvent} (id={inst.id}, duration={duration.type})");
        return inst.id;
    }

    /// <summary>移除指定触发器</summary>
    public void RemoveTrigger(string id)
    {
        _triggers.RemoveAll(t => t.id == id);
    }

    // ========================================================================
    // 事件触发
    // ========================================================================

    /// <summary>
    /// 触发指定事件。由 BattleManager / EffectExecutor 在对应事件发生时调用。
    /// </summary>
    public void FireEvent(TriggerEvent evt)
    {
        for (int i = _triggers.Count - 1; i >= 0; i--)
        {
            var t = _triggers[i];
            if (t.expired || t.triggerEvent != evt) continue;

            // 检查触发次数限制
            if (t.maxTriggers > 0 && t.triggersUsed >= t.maxTriggers)
            {
                t.expired = true;
                continue;
            }
            if (t.maxPerTurn > 0 && t.triggersThisTurn >= t.maxPerTurn)
                continue;

            // 检查条件
            if (t.conditions != null && t.conditions.conditions != null && t.conditions.conditions.Count > 0)
            {
                if (!_executor.EvaluateConditions(t.conditions))
                    continue;
            }

            // 执行子效果（传入局部变量快照）
            Debug.Log($"[TriggerSystem] 触发 {evt} → {(t.isAbility ? "能力" : "临时")}触发器(id={t.id})");
            _executor.ExecuteEffectList(t.effects, t.localVars);

            t.triggersUsed++;
            t.triggersThisTurn++;

            // 检查是否过期
            CheckExpiry(t);
        }

        // 清理过期触发器
        _triggers.RemoveAll(t => t.expired);
    }

    // ========================================================================
    // 生命周期管理
    // ========================================================================

    /// <summary>每回合开始时重置本回合触发计数</summary>
    public void OnTurnStart()
    {
        foreach (var t in _triggers)
        {
            t.triggersThisTurn = 0;
            if (t.duration.expireAtTurnStart)
                t.expired = true;
        }
        _triggers.RemoveAll(t => t.expired);
        FireEvent(TriggerEvent.OnTurnStart);
    }

    /// <summary>每回合结束时检查过期</summary>
    public void OnTurnEnd()
    {
        foreach (var t in _triggers)
        {
            if (t.duration.expireAtTurnEnd)
                t.expired = true;
            if (t.duration.type == DurationType.CurrentTurn)
                t.expired = true;
            if (t.duration.type == DurationType.Turns)
            {
                t.duration.turns--;
                if (t.duration.turns <= 0)
                    t.expired = true;
            }
        }
        FireEvent(TriggerEvent.OnTurnEnd);
        _triggers.RemoveAll(t => t.expired);
    }

    /// <summary>角色切换时挂起/恢复</summary>
    public void OnCharacterSwitch(int newActiveIndex)
    {
        // 挂起仅当前角色激活时生效的触发器
        for (int i = _triggers.Count - 1; i >= 0; i--)
        {
            var t = _triggers[i];
            if (t.activeOnlyWhenOwnerIsActive && t.ownerCharacterIndex >= 0 && t.ownerCharacterIndex != newActiveIndex)
            {
                _suspended.Add(t);
                _triggers.RemoveAt(i);
            }
            else if (t.duration.expireOnCharacterSwitch)
            {
                t.expired = true;
            }
        }

        // 恢复之前挂起的、属于新激活角色的触发器
        for (int i = _suspended.Count - 1; i >= 0; i--)
        {
            var t = _suspended[i];
            if (t.ownerCharacterIndex == newActiveIndex)
            {
                _triggers.Add(t);
                _suspended.RemoveAt(i);
                Debug.Log($"[TriggerSystem] 恢复触发器: {t.triggerEvent} (id={t.id})");
            }
        }

        _triggers.RemoveAll(t => t.expired);
        FireEvent(TriggerEvent.AfterCharacterSwitch);
    }

    /// <summary>战斗结束时清空所有触发器</summary>
    public void OnCombatEnd()
    {
        FireEvent(TriggerEvent.OnCombatEnd);
        _triggers.Clear();
        _suspended.Clear();
    }

    /// <summary>战斗开始时触发</summary>
    public void OnCombatStart()
    {
        FireEvent(TriggerEvent.OnCombatStart);
    }

    // ========================================================================
    // 过期检查
    // ========================================================================

    private void CheckExpiry(TriggerInstance t)
    {
        switch (t.duration.type)
        {
            case DurationType.NextTrigger:
                t.expired = true; // 触发一次后过期
                break;
            case DurationType.TriggerCount:
                if (t.triggersUsed >= t.duration.triggerCount)
                    t.expired = true;
                break;
        }
    }

    /// <summary>获取所有触发器的描述（用于测试窗口显示）</summary>
    public List<string> GetTriggerDescriptions()
    {
        var result = new List<string>();
        foreach (var t in _triggers)
        {
            string status = t.isAbility ? "能力" : "临时";
            string cond = t.conditions != null && t.conditions.conditions != null && t.conditions.conditions.Count > 0
                ? $" 条件:{t.conditions.conditions.Count}个" : "";
            result.Add($"[{status}] {EffectNode.GetTriggerName(t.triggerEvent)} 效果{t.effects.Count}个 已触发{t.triggersUsed}次{cond}");
        }
        return result;
    }
}
