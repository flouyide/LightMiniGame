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
}
