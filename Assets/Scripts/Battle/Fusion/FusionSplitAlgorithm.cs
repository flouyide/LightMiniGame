using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 融合（Fusion）随机拆分算法。
/// 把 total 拆成 parts 个非负整数，结果和 == total（数值守恒），每份 ∈ [0, total]。
/// 规则：
///  - minEach：每份保底（默认 0）。当 total 足够放 minEach*parts 时保证每份 ≥ minEach；
///    否则退化为每份 ≥0 的普通拆分（尽力而为，和仍守恒）。
///  - capPercent：(0,1] 时尽量把单份控制在 total*capPercent 之下，压低“69/1”这类极端；
///    若物理上无法同时满足和守恒与上限，则优先保证和守恒、放宽上限。
/// 实现用“隔板法”：在 [0, 剩余量] 均匀取 parts-1 个切割点，排序后差分，天然整数、非负、和守恒。
/// </summary>
public static class FusionSplitAlgorithm
{
    /// <summary>
    /// 把 total 拆成 parts 份随机整数（结果和 == total）。
    /// parts <= 1 时返回 [total]。
    /// </summary>
    public static List<int> Split(int total, int parts, int minEach = 0, float capPercent = 0f)
    {
        parts = Math.Max(1, parts);
        var result = SplitRaw(total, parts, minEach);

        // 可选：压低极端（如单份占比过大）。capPercent 大于 1/parts 才有意义。
        if (capPercent > 0f && parts > 1)
        {
            int cap = (int)Mathf.Ceil(total * capPercent);
            if (cap < Mathf.CeilToInt(total * 0.5f)) // 仅当 cap 明显低于 50% 才限制，避免破坏守恒
                ApplyCap(result, total, cap);
        }

        NormalizeSum(result, total);
        return result;
    }

    /// <summary>隔板法核心：保证整数、非负、和守恒。minEach 尽量满足。</summary>
    private static List<int> SplitRaw(int total, int parts, int minEach)
    {
        var result = new List<int>(parts);
        if (parts <= 1)
        {
            result.Add(total);
            return result;
        }

        if (minEach * parts <= total)
        {
            // 保底可全满足：先扣保底，剩余自由切分，再每份加回保底
            int baseVal = Math.Max(0, minEach);
            int remainder = total - baseVal * parts;
            var cuts = BuildCuts(remainder, parts);
            for (int i = 0; i < parts; i++)
                result.Add(baseVal + cuts[i]);
            return result;
        }

        // 保底放不下：回到每份 >=0，和仍守恒
        var cuts2 = BuildCuts(total, parts);
        for (int i = 0; i < parts; i++)
            result.Add(cuts2[i]);
        return result;
    }

    /// <summary>返回 parts 份非负整数，和为 amount（可能含 0）。</summary>
    private static List<int> BuildCuts(int amount, int parts)
    {
        var result = new List<int>(parts);
        if (parts <= 1)
        {
            result.Add(amount);
            return result;
        }

        // 切割点集合（含首尾 0 与 amount）
        List<int> cuts = new List<int>(parts + 1) { 0, amount };
        for (int i = 0; i < parts - 1; i++)
            cuts.Add(Random.Range(0, amount + 1));
        cuts.Sort();

        for (int i = 1; i < cuts.Count; i++)
            result.Add(cuts[i] - cuts[i - 1]);
        return result;
    }

    /// <summary>把单份 > cap 的削到 cap，把削出的额尽量均摊给低于 cap 的份（守恒优先，无法均摊则保留少量超高）。</summary>
    private static void ApplyCap(List<int> parts, int total, int cap)
    {
        if (parts.Count == 0 || cap < 1) return;
        int excess = 0;
        for (int i = 0; i < parts.Count; i++)
            if (parts[i] > cap) excess += parts[i] - cap;
        if (excess <= 0) return;

        // 可回收空间（低于 cap 的份还能塞多少）
        int room = 0;
        for (int i = 0; i < parts.Count; i++)
            room += Math.Max(0, cap - parts[i]);
        int reclaim = Math.Min(excess, room);

        // 削减超上限份
        int taken = 0;
        for (int i = parts.Count - 1; i >= 0 && taken < excess; i--)
        {
            if (parts[i] > cap)
            {
                int cut = Math.Min(parts[i] - cap, excess - taken);
                parts[i] -= cut;
                taken += cut;
            }
        }
        // 回填可塞空间
        int fill = reclaim;
        for (int i = 0; i < parts.Count && fill > 0; i++)
        {
            int add = Math.Min(cap - parts[i], fill);
            parts[i] += add;
            fill -= add;
        }
        // 未能回填的部分就不塞回去（保持和守恒由 NormalizeSum 兜底），这里 reclaim 已全塞回，
        // 剩余超出则仍守恒（excess==reclaim）
    }

    /// <summary>修整使得和严格等于 total（隔板法已保证，此处仅兜底浮点误差）。</summary>
    private static void NormalizeSum(List<int> parts, int total)
    {
        int sum = 0;
        foreach (int p in parts) sum += p;
        int diff = total - sum;
        if (diff != 0 && parts.Count > 0)
        {
            parts[0] += diff;
            if (parts[0] < 0) parts[0] = 0;
        }
    }
}