using System;
using System.Collections.Generic;
using LightMiniGame.CardEditor;

/// <summary>
/// 卡牌描述里一个可独立融合的数字槽（同一张牌可有多个伤害/次数/破甲等）。
/// </summary>
public enum FusionSlotKind
{
    Damage,
    Repeat,
    ArmorBreak,
    Block,
    Buff,
    Draw,
    Restore,
    Status,
    Resource
}

/// <summary>单个效果节点上某个数字槽的融合覆盖。</summary>
[Serializable]
public class FusionSlotOverride
{
    public int nodeIndex;
    public FusionSlotKind kind;
    public int value;
}

/// <summary>枚举结果：描述中出现的顺序，与卡面数字 token 按值对齐。</summary>
public struct CardFusionSlotInfo
{
    public int nodeIndex;
    public FusionSlotKind kind;
    public int baseValue;
    public int displayValue;
    public string label;
}

/// <summary>
/// 按效果节点与描述文案顺序，列出一张牌上所有可融合数字。
/// 伤害节点会依次产出：伤害值、次数（≠1 时）、破甲（开启时）。
/// </summary>
public static class CardFusionSlots
{
    public static List<CardFusionSlotInfo> Collect(
        List<EffectNode> nodes, int strength, int dexterity, bool isEnemy, FusionCardDelta fusion)
    {
        var list = new List<CardFusionSlotInfo>();
        if (nodes == null) return list;

        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null || !n.enabled) continue;

            switch (n.operation)
            {
                case EffectOperation.DealDamage:
                    if (!ValueNode.ContainsNonAttributeFormula(n.value))
                    {
                        Add(list, i, FusionSlotKind.Damage, "伤害",
                            ValueNode.ResolveCombatValue(n.value, n.operation, n.scalingMode, strength, dexterity, isEnemy),
                            fusion);
                    }
                    int repeat = ValueNode.ResolveValue(n.repeatCount, strength, dexterity);
                    bool fuseRepeat = fusion != null && fusion.TryGetSlot(i, FusionSlotKind.Repeat, out _);
                    if (repeat != 1 || fuseRepeat)
                        Add(list, i, FusionSlotKind.Repeat, "次数", repeat, fusion);
                    if (n.useArmorBreak)
                        Add(list, i, FusionSlotKind.ArmorBreak, "破甲",
                            ValueNode.ResolveValue(n.armorBreakValue, strength, dexterity), fusion);
                    break;

                case EffectOperation.GainBlock:
                {
                    int v = ValueNode.ResolveCombatValue(n.value, n.operation, n.scalingMode, strength, dexterity, isEnemy);
                    if (ShouldInclude(v, fusion, i, FusionSlotKind.Block, fusion != null && fusion.overrideArmor))
                        Add(list, i, FusionSlotKind.Block, "护甲", v, fusion);
                    break;
                }

                case EffectOperation.ModifyAttribute:
                {
                    int v = ValueNode.ResolveValue(n.value, strength, dexterity);
                    if (ShouldInclude(v, fusion, i, FusionSlotKind.Buff, fusion != null && fusion.overrideBuff))
                        Add(list, i, FusionSlotKind.Buff, "增益", v, fusion);
                    break;
                }

                case EffectOperation.DrawCards:
                {
                    int v = ValueNode.ResolveValue(n.value, strength, dexterity);
                    if (ShouldInclude(v, fusion, i, FusionSlotKind.Draw, fusion != null && fusion.overrideDraw))
                        Add(list, i, FusionSlotKind.Draw, "抽牌", v, fusion);
                    break;
                }

                case EffectOperation.RestoreActionPoints:
                {
                    int v = ValueNode.ResolveValue(n.value, strength, dexterity);
                    if (ShouldInclude(v, fusion, i, FusionSlotKind.Restore, fusion != null && fusion.overrideRestore))
                        Add(list, i, FusionSlotKind.Restore, "回费", v, fusion);
                    break;
                }

                case EffectOperation.ApplyStatus:
                {
                    int v = ValueNode.ResolveValue(n.statusValue, strength, dexterity);
                    if (ShouldInclude(v, fusion, i, FusionSlotKind.Status, false))
                        Add(list, i, FusionSlotKind.Status, "状态", v, fusion);
                    break;
                }

                case EffectOperation.ModifyResource:
                {
                    int v = ValueNode.ResolveValue(n.value, strength, dexterity);
                    if (ShouldInclude(v, fusion, i, FusionSlotKind.Resource, false))
                        Add(list, i, FusionSlotKind.Resource, ResourceLabel(n.resourceType), v, fusion);
                    break;
                }
            }
        }

        return list;
    }

    /// <summary>
    /// 只有「已损失理智*3」这类公式伤害、没有其它可融合数字时，不要走旧逻辑把文案里的系数当成攻击值。
    /// </summary>
    public static bool ShouldSkipLegacyNumericFusion(List<EffectNode> nodes)
    {
        if (nodes == null) return false;
        bool anyFormulaDamage = false;
        bool anyFusable = false;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null || !n.enabled) continue;
            switch (n.operation)
            {
                case EffectOperation.DealDamage:
                    if (ValueNode.ContainsNonAttributeFormula(n.value))
                        anyFormulaDamage = true;
                    else
                        anyFusable = true;
                    break;
                case EffectOperation.GainBlock:
                case EffectOperation.ModifyAttribute:
                case EffectOperation.ModifyResource:
                case EffectOperation.DrawCards:
                case EffectOperation.RestoreActionPoints:
                case EffectOperation.ApplyStatus:
                    anyFusable = true;
                    break;
            }
        }
        return anyFormulaDamage && !anyFusable;
    }

    private static string ResourceLabel(PlayerResourceType type) => type switch
    {
        PlayerResourceType.Sanity => "理智",
        PlayerResourceType.CurrentHealth => "生命",
        PlayerResourceType.ActionPoints => "能量",
        PlayerResourceType.Currency => "金币",
        PlayerResourceType.Heat => "热度",
        PlayerResourceType.Block => "护甲",
        PlayerResourceType.Fortune => "福报",
        _ => "资源"
    };

    private static bool ShouldInclude(int baseValue, FusionCardDelta fusion, int nodeIndex, FusionSlotKind kind, bool legacy)
    {
        if (baseValue != 0) return true;
        if (fusion != null && fusion.TryGetSlot(nodeIndex, kind, out _)) return true;
        return legacy && fusion != null && !fusion.HasSlotKind(kind);
    }

    private static void Add(
        List<CardFusionSlotInfo> list, int nodeIndex, FusionSlotKind kind, string label, int baseValue, FusionCardDelta fusion)
    {
        int display = baseValue;
        if (fusion != null)
        {
            if (fusion.TryGetSlot(nodeIndex, kind, out int slotVal))
                display = slotVal;
            else if (!fusion.HasSlotKind(kind))
            {
                switch (kind)
                {
                    case FusionSlotKind.Damage when fusion.overrideAttack:
                        display = fusion.attackValue; break;
                    case FusionSlotKind.Block when fusion.overrideArmor:
                        display = fusion.armorValue; break;
                    case FusionSlotKind.Buff when fusion.overrideBuff:
                        display = fusion.buffValue; break;
                    case FusionSlotKind.Draw when fusion.overrideDraw:
                        display = fusion.drawCount; break;
                    case FusionSlotKind.Restore when fusion.overrideRestore:
                        display = fusion.restoreAP; break;
                }
            }
        }

        list.Add(new CardFusionSlotInfo
        {
            nodeIndex = nodeIndex,
            kind = kind,
            baseValue = baseValue,
            displayValue = display,
            label = label
        });
    }
}

/// <summary>
/// 融合（Fusion）机制 —— 卡牌数值的运行时覆盖层。
/// 融合后把选中的卡牌数值槽位（费用/攻击/护甲/增益/抽牌/回费，以及同牌多个伤害/次数/破甲）写入本覆盖层，
/// 显示（CardDisplay 的 cost 徽标、攻击/护甲数字）与打出效果（EffectExecutorV2）都优先读取覆盖值。
/// 默认仅本场战斗生效（CardData.fusion 不持久化，每场重置）；
/// 进阶1（persistFusion）开启后，战斗结束会把本覆盖层合并进 CardInstance.overrideData 以跨战斗保留。
/// </summary>
[Serializable]
public class FusionCardDelta
{
    // 费用
    public bool overrideCost;
    public int cost;
    // 攻击值（兼容：无逐槽覆盖时，所有 DealDamage 共用）
    public bool overrideAttack;
    public int attackValue;
    // 护甲/格挡值
    public bool overrideArmor;
    public int armorValue;
    // 增益值（buff value）
    public bool overrideBuff;
    public int buffValue;
    // 抽牌数
    public bool overrideDraw;
    public int drawCount;
    // 回费数（回复行动点）
    public bool overrideRestore;
    public int restoreAP;

    /// <summary>按效果节点索引区分的数字槽覆盖（同一张牌上的第二段伤害、次数、破甲等）。</summary>
    public List<FusionSlotOverride> slots;

    /// <summary>清空所有覆盖标记，恢复卡牌原始状态。</summary>
    public void Clear()
    {
        overrideCost = overrideAttack = overrideArmor = false;
        overrideBuff = overrideDraw = overrideRestore = false;
        cost = attackValue = armorValue = buffValue = drawCount = restoreAP = 0;
        slots?.Clear();
    }

    /// <summary>是否含任何生效的覆盖。</summary>
    public bool HasAny =>
        overrideCost || overrideAttack || overrideArmor ||
        overrideBuff || overrideDraw || overrideRestore ||
        (slots != null && slots.Count > 0);

    public bool HasSlotKind(FusionSlotKind kind)
    {
        if (slots == null) return false;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].kind == kind) return true;
        }
        return false;
    }

    public bool TryGetSlot(int nodeIndex, FusionSlotKind kind, out int value)
    {
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s != null && s.nodeIndex == nodeIndex && s.kind == kind)
                {
                    value = s.value;
                    return true;
                }
            }
        }
        value = 0;
        return false;
    }

    /// <summary>写入某个效果节点上的数字槽，并同步该类型的首个槽到兼容字段（遗物/旧读取路径）。</summary>
    public void SetSlot(int nodeIndex, FusionSlotKind kind, int value)
    {
        if (slots == null) slots = new List<FusionSlotOverride>();
        bool found = false;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s != null && s.nodeIndex == nodeIndex && s.kind == kind)
            {
                s.value = value;
                found = true;
                break;
            }
        }
        if (!found)
            slots.Add(new FusionSlotOverride { nodeIndex = nodeIndex, kind = kind, value = value });
        SyncLegacyField(kind);
    }

    private void SyncLegacyField(FusionSlotKind kind)
    {
        if (!TryGetFirstSlot(kind, out int v)) return;
        switch (kind)
        {
            case FusionSlotKind.Damage:
                overrideAttack = true; attackValue = v; break;
            case FusionSlotKind.Block:
                overrideArmor = true; armorValue = v; break;
            case FusionSlotKind.Buff:
                overrideBuff = true; buffValue = v; break;
            case FusionSlotKind.Draw:
                overrideDraw = true; drawCount = v; break;
            case FusionSlotKind.Restore:
                overrideRestore = true; restoreAP = v; break;
        }
    }

    private bool TryGetFirstSlot(FusionSlotKind kind, out int value)
    {
        value = 0;
        if (slots == null) return false;
        int bestIndex = int.MaxValue;
        bool found = false;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null || s.kind != kind) continue;
            if (s.nodeIndex < bestIndex)
            {
                bestIndex = s.nodeIndex;
                value = s.value;
                found = true;
            }
        }
        return found;
    }

    /// <summary>把另一份覆盖合并进本份（用于持久化读回）。</summary>
    public void Merge(FusionCardDelta other)
    {
        if (other == null) return;
        if (other.overrideCost)        { overrideCost = true; cost = other.cost; }
        if (other.overrideAttack)    { overrideAttack = true; attackValue = other.attackValue; }
        if (other.overrideArmor)     { overrideArmor = true; armorValue = other.armorValue; }
        if (other.overrideBuff)      { overrideBuff = true; buffValue = other.buffValue; }
        if (other.overrideDraw)      { overrideDraw = true; drawCount = other.drawCount; }
        if (other.overrideRestore)   { overrideRestore = true; restoreAP = other.restoreAP; }
        if (other.slots == null) return;
        for (int i = 0; i < other.slots.Count; i++)
        {
            var s = other.slots[i];
            if (s != null) SetSlot(s.nodeIndex, s.kind, s.value);
        }
    }

    /// <summary>返回当前执行的标签+数值对，用于调试/展示。</summary>
    public List<string> Describe()
    {
        var parts = new List<string>();
        if (overrideCost)      parts.Add($"费用:{cost}");
        if (overrideAttack)    parts.Add($"攻击:{attackValue}");
        if (overrideArmor)     parts.Add($"护甲:{armorValue}");
        if (overrideBuff)      parts.Add($"增益:{buffValue}");
        if (overrideDraw)      parts.Add($"抽牌:{drawCount}");
        if (overrideRestore)   parts.Add($"回费:{restoreAP}");
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s != null) parts.Add($"{s.kind}:{s.nodeIndex}={s.value}");
            }
        }
        return parts;
    }
}
