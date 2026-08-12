using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人技能数据
/// </summary>
[Serializable]
public class EnemySkill
{
    [Tooltip("技能名称")]
    public string skillName;
    [Tooltip("技能描述")]
    [TextArea(1, 3)] public string description;
    [Tooltip("技能卡面图（美术替换接口）")]
    public Sprite skillCardArt;
    [Tooltip("造成的伤害")]
    public int damage;
    [Tooltip("降低玩家理智值")]
    public int sanityReduction;
    [Tooltip("降低玩家力量值")]
    public int strengthReduction;
    [Tooltip("是否锁定当前角色（光束扫描）")]
    public bool lockCharacter;
    [Tooltip("是否仅命中被锁定的角色（光束命中：只有锁定角色未切换才生效）")]
    public bool hitsLockedCharacter;
    [Tooltip("凝视值变化（正数增加，0不变）")]
    public int gazeChange;
    [Tooltip("是否重置凝视值为0")]
    public bool resetGaze;
}

/// <summary>
/// 敌人配置 —— ScriptableObject，不同战斗只需更换此配置
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
    [Tooltip("最大生命值（阶段1）")]
    public int maxHP = 40;
    [Tooltip("阶段2最大生命值（0=不切阶段）")]
    public int phase2MaxHP = 60;
    [Tooltip("初始护甲")]
    public int armor = 0;
    [Tooltip("阶段1立绘（注视形态）")]
    public Sprite phase1Portrait;
    [Tooltip("阶段2立绘（睁眼形态）")]
    public Sprite phase2Portrait;

    [Header("阶段切换")]
    [Tooltip("HP低于此百分比时进入阶段2")]
    public int phase2HPThresholdPercent = 60;
    [Tooltip("玩家理智低于等于此值时进入阶段2")]
    public int phase2SanityThreshold = 4;
    [Tooltip("凝视值上限（达到此值触发特殊技能）")]
    public int gazeMaxValue = 3;

    [Header("阶段1技能（按顺序循环）")]
    [Tooltip("阶段1的技能列表，按回合顺序循环执行")]
    public List<EnemySkill> phase1Skills;

    [Header("阶段2技能")]
    [Tooltip("阶段2常规技能（每回合执行）")]
    public List<EnemySkill> phase2Skills;
    [Tooltip("阶段2凝视值满时触发的技能")]
    public EnemySkill phase2GazeSkill;

    // ===== 5.3 文档扩展字段（保留所有原字段，旧 .asset 自动取默认值兼容） =====

    [Header("难度")]
    [Tooltip("难度类型（影响掉落物：弱怪/强怪/精英/boss）")]
    public Difficulty difficulty = Difficulty.Weak;

    [Header("属性")]
    [Tooltip("多敌人情况下的出招优先级（1最高，同优先级则随机顺序；运行时由 SpawnInfo.actionOrder 决定，此字段仅作编辑器提示）")]
    public int actionPriority = 1;
    [Tooltip("力量：影响敌人技能伤害")]
    public int strength = 0;
    [Tooltip("敏捷：预留（每回合额外行动/闪避等）")]
    public int agility = 0;
    [Tooltip("被暴击率（百分比，0=5%默认）")]
    public int inCritRate = 0;
    [Tooltip("受到伤害倍率（1=正常）。运行时应用待第二阶段 EnemyInstance 改造")]
    public float damageTakenMultiplier = 1f;
    [Tooltip("造成伤害倍率（1=正常）。运行时应用待第二阶段 EnemyInstance 改造")]
    public float damageDealtMultiplier = 1f;

    [Header("出招牌库（高理智=phase1 / 低理智=phase2）")]
    [Tooltip("高理智出招数（从 phase1Skills 中抽/轮转的数量；0=全部轮转）")]
    public int highSanityCardCount = 0;
    [Tooltip("低理智出招数（从 phase2Skills 中抽/轮转的数量；0=全部轮转）")]
    public int lowSanityCardCount = 0;

    [Header("能力")]
    [Tooltip("敌人自带的能力（精英/boss 专用）。每项引用一个 RelicData + 触发备注")]
    public List<EnemyAbilityEntry> abilities;

    [Header("掉落物")]
    [Tooltip("按难度枚举配置的掉落物表。使用 LootTable.GetPreset(difficulty) 一键填充")]
    public LootTable lootTable = new LootTable();
}
