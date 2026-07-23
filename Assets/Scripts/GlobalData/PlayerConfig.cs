using UnityEngine;

/// <summary>
/// 玩家初始属性配置
/// </summary>
[CreateAssetMenu(fileName = "PlayerConfig", menuName = "LightGame/Player Config")]
public class PlayerConfig : ScriptableObject
{
    public int maxHP = 64;       // 最大生命值
    public int startHP = 64;     // 初始生命值
    public int startGold = 50;   // 初始金币
    public int Sanity = 0;  // 理智
}
