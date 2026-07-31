using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightMiniGame.CardEditor
{
    // ========================================================================
    // 理智规则配置
    // ========================================================================
    [Serializable]
    public class SanityRuleConfig
    {
        [Tooltip("初始理智值")]
        public int initialSanity = 10;
        [Tooltip("低理智阈值（理智 <= 此值时进入低理智状态）")]
        public int lowSanityThreshold = 4;
        [Tooltip("理智下限")]
        public int minimumSanity = 0;
        [Tooltip("理智上限")]
        public int maximumSanity = 10;
    }

    // ========================================================================
    // 暴击规则配置
    // ========================================================================
    [Serializable]
    public class CriticalRuleConfig
    {
        [Tooltip("每 1 点暴击属性增加的暴击概率")]
        public float criticalChancePerPoint = 0.1f;
        [Tooltip("默认暴击伤害倍率")]
        public float defaultCriticalDamageMultiplier = 2f;
    }

    // ========================================================================
    // 回合规则配置
    // ========================================================================
    [Serializable]
    public class TurnRuleConfig
    {
        [Tooltip("每回合基础行动点")]
        public int baseActionPointsPerTurn = 3;
        [Tooltip("每回合基础抽牌数")]
        public int baseCardsDrawnPerTurn = 3;
        [Tooltip("回合结束时清空行动点")]
        public bool clearActionPointsAtTurnEnd = true;
        [Tooltip("回合结束时清空格挡")]
        public bool clearBlockAtTurnEnd = true;
    }

    // ========================================================================
    // 热度规则配置
    // ========================================================================
    [Serializable]
    public class HeatRuleConfig
    {
        [Tooltip("每打出 1 张攻击牌增加的热度")]
        public int heatGainedPerAttackCard = 3;
        [Tooltip("过热阈值")]
        public int overheatThreshold = 25;
        [Tooltip("正常回合结束时热度衰减")]
        public int normalHeatDecayPerTurn = 1;
        [Tooltip("切换角色后热度衰减")]
        public int switchedCharacterHeatDecayPerTurn = 6;
        [Tooltip("过载每回合是否只触发一次")]
        public bool overloadTriggersOncePerTurn = true;
        [Tooltip("过载施加的状态类型")]
        public StatusType2 overloadStatus = StatusType2.Jammed;
        [Tooltip("过载施加的状态层数")]
        public int overloadStatusStacks = 1;
    }

    // ========================================================================
    // 破甲规则配置
    // ========================================================================
    [Serializable]
    public class ArmorBreakRuleConfig
    {
        [Tooltip("破甲模式")]
        public ArmorBreakMode mode = ArmorBreakMode.BypassBlock;
        [Tooltip("增加受到伤害模式下每层增加的伤害百分比")]
        public float damageIncreasePerStack = 0.1f;
        [Tooltip("绕过格挡模式下每层绕过的伤害")]
        public int bypassDamagePerStack = 1;
    }

    // ========================================================================
    // 品级配置项（保留旧设计兼容）
    // ========================================================================
    [Serializable]
    public class RarityConfigEntry
    {
        public CardRarity rarity;
        public int shopPrice = 50;
        public float shopRefreshWeight = 1f;
        public float rewardRefreshWeight = 1f;
    }

    // ========================================================================
    // 游戏卡牌规则总配置
    // ========================================================================
    [CreateAssetMenu(menuName = "CardEditor/Game Rule Config", fileName = "GameRuleConfig")]
    public class GameRuleConfig : ScriptableObject
    {
        public const string ResourcePath = "CardEditor/GameRuleConfig";

        [Header("理智规则")]
        public SanityRuleConfig sanity = new SanityRuleConfig();

        [Header("暴击规则")]
        public CriticalRuleConfig critical = new CriticalRuleConfig();

        [Header("回合规则")]
        public TurnRuleConfig turn = new TurnRuleConfig();

        [Header("热度规则")]
        public HeatRuleConfig heat = new HeatRuleConfig();

        [Header("破甲规则")]
        public ArmorBreakRuleConfig armorBreak = new ArmorBreakRuleConfig();

        [Header("品级配置")]
        public List<RarityConfigEntry> rarityConfigs = new List<RarityConfigEntry>
        {
            new RarityConfigEntry { rarity = CardRarity.Common, shopPrice = 50, shopRefreshWeight = 3f, rewardRefreshWeight = 3f },
            new RarityConfigEntry { rarity = CardRarity.Rare, shopPrice = 100, shopRefreshWeight = 2f, rewardRefreshWeight = 2f },
            new RarityConfigEntry { rarity = CardRarity.Legendary, shopPrice = 200, shopRefreshWeight = 1f, rewardRefreshWeight = 1f },
        };

        // === 便捷查询 ===
        public RarityConfigEntry GetRarityConfig(CardRarity r)
        {
            foreach (var e in rarityConfigs)
                if (e.rarity == r) return e;
            return null;
        }

        public int GetShopPrice(CardRarity r) => GetRarityConfig(r)?.shopPrice ?? 50;
        public float GetShopWeight(CardRarity r) => GetRarityConfig(r)?.shopRefreshWeight ?? 1f;
        public float GetRewardWeight(CardRarity r) => GetRarityConfig(r)?.rewardRefreshWeight ?? 1f;

        // === 加载 ===
        public static GameRuleConfig Load() => Resources.Load<GameRuleConfig>(ResourcePath);
    }
}
