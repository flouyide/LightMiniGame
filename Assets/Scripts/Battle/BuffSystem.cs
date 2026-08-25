using System;
using System.Collections.Generic;
using UnityEngine;

// ========================================================================
// Buff 属性类型
// ========================================================================
public enum BuffAttributeType
{
    Strength,       // 力量（可负）
    Dexterity,      // 敏捷（可负）
    Recovery,        // 回复（最小0）
    LifeSteal,      // 吸血（最小0）
    CriticalChance, // 暴击率（最小0）
    CriticalDamage, // 暴击伤害（最小2）
}

// ========================================================================
// Buff 数据 —— 每个属性一个 ScriptableObject，Inspector 配置图标
// ========================================================================
[CreateAssetMenu(menuName = "CardGame/Buff Data", fileName = "NewBuff")]
public class BuffData : ScriptableObject
{
    [Tooltip("Buff 名称")]
    public string buffName = "新Buff";

    [Tooltip("图标")]
    public Sprite icon;

    [Tooltip("属性类型")]
    public BuffAttributeType attributeType = BuffAttributeType.Strength;

    [Tooltip("该属性的最小值（实际应用值不低于此）")]
    public int minValue = 0;
}

// ========================================================================
// Buff 实例（运行时）
// ========================================================================
[Serializable]
public class BuffInstance
{
    public BuffAttributeType attributeType;
    public int stacks;           // 层数（正=增益，负=减益）
    public int remainingTurns;   // 剩余回合（0=永久持续至战斗结束）

    public BuffInstance(BuffAttributeType type, int stacks, int turns)
    {
        attributeType = type;
        this.stacks = stacks;
        remainingTurns = turns;
    }
}

// ========================================================================
// Buff 系统 —— 管理一个单位（玩家或敌人）的所有 buff
// ========================================================================
public class BuffSystem
{
    private readonly List<BuffInstance> _buffs = new();
    private readonly Dictionary<BuffAttributeType, int> _minValues = new();

    /// <summary>设置某属性的最小值约束</summary>
    public void SetMinValue(BuffAttributeType type, int min) => _minValues[type] = min;

    /// <summary>添加一个 buff</summary>
    /// <param name="type">属性类型</param>
    /// <param name="stacks">层数（正=增益，负=减益）</param>
    /// <param name="duration">持续回合数（0=永久至战斗结束）</param>
    public void AddBuff(BuffAttributeType type, int stacks, int duration = 0)
    {
        if (stacks == 0) return;

        // 同属性、同持续类型（都永久或都有持续）的 buff 合并
        bool isPermanent = duration == 0;
        for (int i = 0; i < _buffs.Count; i++)
        {
            var b = _buffs[i];
            bool bPermanent = b.remainingTurns == 0;
            if (b.attributeType == type && bPermanent == isPermanent)
            {
                b.stacks += stacks;
                // 有持续时间的 buff 刷新为更长的持续时间
                if (!isPermanent && duration > b.remainingTurns)
                    b.remainingTurns = duration;
                if (b.stacks == 0) _buffs.RemoveAt(i);
                return;
            }
        }

        // 新 buff
        _buffs.Add(new BuffInstance(type, stacks, duration));
    }

    /// <summary>获取某属性的临时修正值（所有该属性 buff 的层数之和）</summary>
    public int GetTempValue(BuffAttributeType type)
    {
        int total = 0;
        foreach (var b in _buffs)
            if (b.attributeType == type) total += b.stacks;
        return total;
    }

    /// <summary>获取某属性的有效值（基础值 + 临时值，受最小值约束）</summary>
    public int GetEffectiveValue(BuffAttributeType type, int baseValue)
    {
        int effective = baseValue + GetTempValue(type);
        if (_minValues.TryGetValue(type, out int min))
            effective = Mathf.Max(effective, min);
        return effective;
    }

    /// <summary>回合结束：递减持续回合，移除过期 buff</summary>
    public void OnTurnEnd()
    {
        for (int i = _buffs.Count - 1; i >= 0; i--)
        {
            var b = _buffs[i];
            if (b.remainingTurns > 0)
            {
                b.remainingTurns--;
                if (b.remainingTurns <= 0)
                    _buffs.RemoveAt(i);
            }
        }
    }

    /// <summary>移除指定属性最多 stacks 层 buff（减少绝对值，不产生反方向 buff）</summary>
    public void RemoveBuff(BuffAttributeType type, int stacks)
    {
        if (stacks <= 0) return;
        for (int i = _buffs.Count - 1; i >= 0; i--)
        {
            var b = _buffs[i];
            if (b.attributeType != type) continue;
            int magnitude = Mathf.Abs(b.stacks);
            int remove = Mathf.Min(magnitude, stacks);
            b.stacks += b.stacks > 0 ? -remove : remove;
            stacks -= remove;
            if (b.stacks == 0) _buffs.RemoveAt(i);
            if (stacks <= 0) return;
        }
    }

    /// <summary>清空所有 buff</summary>
    public void Clear() => _buffs.Clear();

    /// <summary>获取用于 UI 显示的聚合 buff 列表（按属性聚合，同属性合并为一个显示条目）</summary>
    public List<DisplayedBuff> GetDisplayedBuffs()
    {
        var result = new Dictionary<BuffAttributeType, int>();
        foreach (var b in _buffs)
        {
            if (result.TryGetValue(b.attributeType, out int existing))
                result[b.attributeType] = existing + b.stacks;
            else
                result[b.attributeType] = b.stacks;
        }

        var list = new List<DisplayedBuff>();
        foreach (var kv in result)
        {
            if (kv.Value == 0) continue; // 0 层不显示
            list.Add(new DisplayedBuff { attributeType = kv.Key, totalStacks = kv.Value });
        }
        return list;
    }
}

/// <summary>UI 显示用的聚合 buff 数据</summary>
public struct DisplayedBuff
{
    public BuffAttributeType attributeType;
    public int totalStacks;  // 正=增益，负=减益
}
