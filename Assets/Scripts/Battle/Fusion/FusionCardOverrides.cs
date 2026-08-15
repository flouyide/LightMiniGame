using System;
using System.Collections.Generic;

/// <summary>
/// 融合（Fusion）机制 —— 卡牌数值的运行时覆盖层。
/// 融合后把选中的卡牌数值槽位（费用/攻击/护甲/增益/抽牌/回费）写入本覆盖层，
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
    // 攻击值
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

    /// <summary>清空所有覆盖标记，恢复卡牌原始状态。</summary>
    public void Clear()
    {
        overrideCost = overrideAttack = overrideArmor = false;
        overrideBuff = overrideDraw = overrideRestore = false;
        cost = attackValue = armorValue = buffValue = drawCount = restoreAP = 0;
    }

    /// <summary>是否含任何生效的覆盖。</summary>
    public bool HasAny =>
        overrideCost || overrideAttack || overrideArmor ||
        overrideBuff || overrideDraw || overrideRestore;

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
        return parts;
    }
}