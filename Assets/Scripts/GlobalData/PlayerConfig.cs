using UnityEngine;

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
    [Tooltip("每回合能量回复")]
    public int maxActionPoints = 3;  // 每回合行动点
    [Tooltip("每回合基础抽牌数")]
    public int drawPerTurn = 3;  // 每回合抽牌数
    
    [Tooltip("力量：影响攻击牌伤害（AttributeBased 时加成）")]
    public int strength;          // 力量
    [Tooltip("敏捷（灵巧）：每回合额外抽牌数")]
    public int agility;           // 敏捷
    [Tooltip("吸血：提升吸血词条的治疗比例")]
    public int lifesteal;         // 吸血
    [Tooltip("暴击率（0-100，影响重击词条的暴击概率）")]
    public int critRate;          // 暴击率
    [Tooltip("暴击伤害（百分比，影响重击词条的暴击倍率；200=2倍）")]
    public int critDamage;        // 暴击伤害
}
