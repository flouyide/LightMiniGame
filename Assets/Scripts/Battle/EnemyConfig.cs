using System;
using System.Collections.Generic;
using LightMiniGame.CardEditor;
using UnityEngine;

/// <summary>
/// 敌人配置 —— ScriptableObject，不同战斗只需更换此配置。
/// 敌人的行动（高理智/低理智）即出牌：phase1Skills/phase2Skills 直接引用 CardEntry 卡牌。
/// </summary>
[CreateAssetMenu(menuName = "CardGame/Enemy Config", fileName = "NewEnemy")]
public class EnemyConfig : ScriptableObject
{
    /// <summary>
    /// 旧 .asset 反序列化时，新字段（lootTable/abilities）可能为 null；OnEnable 兜底初始化。
    /// 新建资产因字段默认值也会经过这里，保证运行时非空。
    /// </summary>
    private void OnEnable()
    {
        if (abilities == null) abilities = new List<EnemyAbilityEntry>();
        if (lootTable == null) lootTable = new LootTable();
    }
    [Header("基础信息")]
    [Tooltip("敌人名称")]
    public string enemyName = "敌人";
    [Tooltip("最大生命值（唯一血条，不随阶段切换变化）")]
    public int maxHP = 40;
    [Tooltip("初始护甲")]
    public int armor = 0;
    public Sprite phase1Portrait;
    public Sprite phase2Portrait;

    [Header("高理智牌库（出牌）")]
    [Tooltip("高理智形态下敌人按轮转顺序打出的卡牌（引用 CardEntry）")]
    public List<CardEntry> phase1Skills;

    [Header("低理智牌库（出牌）")]
    [Tooltip("低理智形态下敌人按轮转顺序打出的卡牌（引用 CardEntry）")]
    public List<CardEntry> phase2Skills;

    // ===== 5.3 文档扩展字段（保留所有原字段，旧 .asset 自动取默认值兼容） =====

    [Header("难度")]
    [Tooltip("难度类型（影响掉落物：弱怪/强怪/精英/boss）")]
    public Difficulty difficulty = Difficulty.Weak;

    [Header("属性")]
    [Tooltip("多敌人情况下的出招优先级（1最高，同优先级则随机顺序）。战斗时作为敌人行动顺序值：数值小的先行动；相同值的敌人每回合从中随机先后")]
    public int actionPriority = 1;
    [Tooltip("力量：加到敌人每个技能的伤害上")]
    public int strength = 0;
    [Tooltip("敏捷：加到敌人每个技能的格挡上（同杀戮尖塔敏捷）")]
    public int dexterity = 0;
    [Tooltip("敌人造成伤害倍率（百分比，100=1.0倍=正常）")]
    public int damageDealtMultiplier = 100;
    [Tooltip("敌人受击倍率（百分比，100=1.0倍=正常）")]
    public int damageTakenMultiplier = 100;

    [Header("出牌库（高理智=phase1 / 低理智=phase2）")]
    [Tooltip("高理智出牌数（从 phase1Skills 中抽/轮转的数量；0=全部轮转）")]
    public int highSanityCardCount = 0;
    [Tooltip("低理智出牌数（从 phase2Skills 中抽/轮转的数量；0=全部轮转）")]
    public int lowSanityCardCount = 0;

    [Header("能力")]
    [Tooltip("敌人自带的能力")]
    public List<EnemyAbilityEntry> abilities;

    [Header("掉落物")]
    [Tooltip("按难度枚举配置的掉落物表。使用 LootTable.GetPreset(difficulty) 一键填充")]
    public LootTable lootTable = new LootTable();
}
