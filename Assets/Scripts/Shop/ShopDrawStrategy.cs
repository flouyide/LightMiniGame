using System;
using System.Collections.Generic;
using LightMiniGame.Card;
using UnityEngine;

namespace LightMiniGame.Shop
{
    /// <summary>
    /// 卡牌抽取策略接口。
    /// 默认 <see cref="UniformCardDraw"/>（均匀无放回）；
    /// 按品级概率抽取使用 <see cref="GradeWeightedCardDraw"/>（权重由 ShopManager 的品级概率字段提供）。
    /// </summary>
    public interface ICardDrawStrategy
    {
        /// <summary>
        /// 从 pool 中无放回抽取最多 count 张卡，排除 exclude 中已存在的卡（避免同一商店里出现重复模板）。
        /// </summary>
        List<CardData> Draw(List<CardData> pool, int count, HashSet<CardData> exclude);
    }

    /// <summary>
    /// 均匀随机抽取：Fisher–Yates 洗牌后从前取 count 张，跳过 exclude 中的卡。
    /// </summary>
    public class UniformCardDraw : ICardDrawStrategy
    {
        public List<CardData> Draw(List<CardData> pool, int count, HashSet<CardData> exclude)
        {
            var result = new List<CardData>();
            if (pool == null || count <= 0) return result;

            var bag = new List<CardData>(pool);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            foreach (var c in bag)
            {
                if (result.Count >= count) break;
                if (c == null || (exclude != null && exclude.Contains(c))) continue;
                result.Add(c);
            }
            return result;
        }
    }

    /// <summary>
    /// 按品级（CardGrade 金/银/铜）加权抽取：每张先按品级权重随机掷出品级，
    /// 再在该品级的剩余候选中等概率取一张；掷中的品级无候选时自动在其余品级中重掷（归一化）。
    /// 权重来源：构造时传入的 weightOf（ShopManager 用它的 3 个品级概率字段提供）。
    /// </summary>
    public class GradeWeightedCardDraw : ICardDrawStrategy
    {
        private readonly Func<CardGrade, float> _weightOf;

        public GradeWeightedCardDraw(Func<CardGrade, float> weightOf)
        {
            _weightOf = weightOf ?? (_ => 1f);
        }

        public List<CardData> Draw(List<CardData> pool, int count, HashSet<CardData> exclude)
        {
            var result = new List<CardData>();
            if (pool == null || count <= 0) return result;

            var remaining = new List<CardData>(pool);
            remaining.RemoveAll(c => c == null || (exclude != null && exclude.Contains(c)));

            while (result.Count < count && remaining.Count > 0)
            {
                var card = GradeWeightedPick.Pick(remaining, c => c.grade, _weightOf);
                if (card == null) break;
                result.Add(card);
                remaining.Remove(card);
            }
            return result;
        }
    }

    /// <summary>
    /// 品级加权随机工具：卡牌与遗物抽取共用。
    /// 规则：仅对仍有候选的品级做加权（权重归一化），掷出品级后在组内均匀取一个；
    /// 所有权重 ≤ 0 时退化为全体均匀随机。
    /// </summary>
    public static class GradeWeightedPick
    {
        public static T Pick<T>(List<T> candidates, Func<T, CardGrade> gradeOf, Func<CardGrade, float> weightOf)
        {
            if (candidates == null || candidates.Count == 0) return default;

            // 按品级分组
            var byGrade = new Dictionary<CardGrade, List<T>>();
            foreach (var c in candidates)
            {
                var g = gradeOf(c);
                if (!byGrade.TryGetValue(g, out var list))
                {
                    list = new List<T>();
                    byGrade[g] = list;
                }
                list.Add(c);
            }

            if (byGrade.Count == 1)
            {
                foreach (var list in byGrade.Values)
                    return list[UnityEngine.Random.Range(0, list.Count)];
            }

            // 仅对非空品级累加权重
            float total = 0f;
            foreach (var kvp in byGrade)
                total += Mathf.Max(0f, weightOf(kvp.Key));

            // 权重全 0：退化为全体均匀
            if (total <= 0f)
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];

            float roll = UnityEngine.Random.value * total;
            List<T> picked = null;
            foreach (var kvp in byGrade)
            {
                roll -= Mathf.Max(0f, weightOf(kvp.Key));
                if (roll <= 0f) { picked = kvp.Value; break; }
            }
            picked ??= LastValue(byGrade);

            return picked[UnityEngine.Random.Range(0, picked.Count)];
        }

        private static List<T> LastValue<T>(Dictionary<CardGrade, List<T>> dict)
        {
            List<T> last = null;
            foreach (var kvp in dict) last = kvp.Value;
            return last;
        }
    }
}
