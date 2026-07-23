using System.Collections.Generic;
using LightMiniGame.Card;
using UnityEngine;

/// <summary>
/// 顶层游戏配置
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "LightGame/Game Config")]
public class GameConfig : ScriptableObject
{
    public List<ChapterConfig> chapters = new();
    public List<CharacterData> characters = new();   // 游戏中的角色（含头像与初始牌组）；开局时据此构建初始卡组并展示头像
}
