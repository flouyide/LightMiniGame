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
    public int Sanity = 0;  // 理智
    
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
