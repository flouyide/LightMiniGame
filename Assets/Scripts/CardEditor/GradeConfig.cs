using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightMiniGame.CardEditor
{
    /// <summary>
    /// 单个品级的全局配置项
    /// </summary>
    [Serializable]
    public class GradeConfigEntry
    {
        public CardGrade grade;

        [Tooltip("商店基础价格")]
        public int shopBasePrice = 50;

        [Tooltip("商店刷新权重")]
        public float shopRefreshWeight = 1f;

        [Tooltip("奖励刷新权重")]
        public float rewardRefreshWeight = 1f;
    }

    /// <summary>
    /// 品级全局配置 —— 独立于卡牌，策划通过独立窗口调整。
    /// 价格和刷新权重不写死在卡牌中。
    /// </summary>
    [CreateAssetMenu(menuName = "CardEditor/Grade Config", fileName = "GradeConfig")]
    public class GradeConfig : ScriptableObject
    {
        public const string ResourcePath = "CardEditor/GradeConfig";

        [Tooltip("每个品级的全局配置")]
        public List<GradeConfigEntry> gradeConfigs = new List<GradeConfigEntry>
        {
            new GradeConfigEntry { grade = CardGrade.Bronze, shopBasePrice = 50,  shopRefreshWeight = 3f, rewardRefreshWeight = 3f },
            new GradeConfigEntry { grade = CardGrade.Silver, shopBasePrice = 100, shopRefreshWeight = 2f, rewardRefreshWeight = 2f },
            new GradeConfigEntry { grade = CardGrade.Gold,   shopBasePrice = 200, shopRefreshWeight = 1f, rewardRefreshWeight = 1f },
        };

        /// <summary>获取某品级的配置</summary>
        public GradeConfigEntry GetConfig(CardGrade grade)
        {
            foreach (var entry in gradeConfigs)
                if (entry.grade == grade) return entry;
            return null;
        }

        /// <summary>获取某品级的商店基础价格</summary>
        public int GetShopPrice(CardGrade grade) => GetConfig(grade)?.shopBasePrice ?? 50;

        /// <summary>获取某品级的商店刷新权重</summary>
        public float GetShopWeight(CardGrade grade) => GetConfig(grade)?.shopRefreshWeight ?? 1f;

        /// <summary>获取某品级的奖励刷新权重</summary>
        public float GetRewardWeight(CardGrade grade) => GetConfig(grade)?.rewardRefreshWeight ?? 1f;

        /// <summary>从 Resources 加载，找不到返回 null</summary>
        public static GradeConfig Load() => Resources.Load<GradeConfig>(ResourcePath);
    }
}
