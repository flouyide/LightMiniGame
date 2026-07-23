using System.Collections.Generic;
using LightMiniGame.Card;
using UnityEngine;

namespace LightMiniGame.Shop
{
    /// <summary>
    /// 卡牌抽取策略接口。
    /// 当前默认 <see cref="UniformCardDraw"/>（均匀无放回）。
    /// 后续要「按 CardGrade 概率抽取」时，实现 <see cref="GradeWeightedCardDraw"/> 并在
    /// ShopManager.CardDrawStrategy 上替换即可——UI / 抽卡流程无需改动。
    /// </summary>
    public interface ICardDrawStrategy
    {
        /// <summary>
        /// 从 pool 中无放回抽取最多 count 张卡，排除 exclude 中已存在的卡（避免同一商店里出现重复模板）。
        /// </summary>
        List<CardData> Draw(List<CardData> pool, int count, HashSet<CardData> exclude);
    }

    /// <summary>
    /// 均匀随机抽取（当前默认策略）：Fisher–Yates 洗牌后从前取 count 张，跳过 exclude 中的卡。
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
                int j = Random.Range(0, i + 1);
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
    /// 按品级（CardGrade）权重抽取 —— 预留接口。
    /// TODO: 实现加权无放回抽取（参考 WeightedShuffle，按 GradeWeight 权重逐张抽取）。
    ///       当前先退化为均匀抽取，保证可直接启用而不报错。
    /// 各品级权重表可后续挪到 ShopConfig 让策划配置。
    /// </summary>
    public class GradeWeightedCardDraw : ICardDrawStrategy
    {
        // 各品级抽取权重（越高越常见）。后续可配置到 ShopConfig。
        private static readonly Dictionary<CardGrade, int> GradeWeight = new Dictionary<CardGrade, int>
        {
            { CardGrade.Common,    100 },
            { CardGrade.Fine,       60 },
            { CardGrade.Rare,       30 },
            { CardGrade.Epic,       12 },
            { CardGrade.Legendary,   4 },
        };

        public List<CardData> Draw(List<CardData> pool, int count, HashSet<CardData> exclude)
        {
            // TODO: 实现加权无放回抽取。当前退化为均匀抽取以保留可用实现。
            return new UniformCardDraw().Draw(pool, count, exclude);
        }
    }
}
