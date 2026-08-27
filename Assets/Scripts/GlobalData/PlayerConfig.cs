using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 玩家初始属性配置
/// </summary>
[CreateAssetMenu(fileName = "PlayerConfig", menuName = "LightGame/Player Config")]
public class PlayerConfig : ScriptableObject
{
    // 所有属性最小值=0
    public int maxHP = 64;       // 最大生命值
    public int startHP = 64;     // 初始生命值
    public int startGold = 50;   // 初始金币
    [Tooltip("理智初始值")]
    public int startSanity = 10; // 初始理智
    [Tooltip("理智上限")]
    public int maxSanity = 10;   // 理智上限
    [Tooltip("理智阈值：玩家理智低于此值时，所有敌人进入低理智阶段")]
    public int sanityThreshold = 4;  // 理智阈值
    [Tooltip("福报值初始值（融合重分配时加到选中数字之和上；无上限）")]
    public int startFortune = 0; // 初始福报值
    [Tooltip("每回合能量回复")]
    public int maxActionPoints = 3;  // 每回合行动点
    [Tooltip("每回合基础抽牌数")]
    public int drawPerTurn = 5;  // 每回合抽牌数
    [Tooltip("每回合可进入融合的次数（遗物追加的次数叠在这之上）。默认 1")]
    public int fusionUsesPerTurn = 1;
    
    [Tooltip("力量：影响攻击牌伤害")]
    public int strength;          // 力量
    [FormerlySerializedAs("agility")]
    [Tooltip("敏捷：影响护甲等 Dexterity 关联效果")]
    public int dexterity;         // 敏捷（Dexterity）
    [Tooltip("吸血：提升吸血词条的治疗比例")]
    public int lifesteal;         // 吸血
    [Tooltip("暴击率（0-100，影响重击词条的暴击概率）")]
    public int critRate;          // 暴击率
    [Tooltip("暴击伤害（百分比，影响重击词条的暴击倍率；200=2倍）")]
    public int critDamage;        // 暴击伤害

    [Header("伤害倍率（百分比，100=1.0倍）")]
    [Tooltip("玩家造成伤害倍率（百分比，100=正常）")]
    public int playerDamageMultiplier = 100;
    [Tooltip("玩家受击倍率（百分比，100=正常）")]
    public int playerDamageTakenMultiplier = 100;
}
