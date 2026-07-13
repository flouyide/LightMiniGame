using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 顶层游戏配置
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "BookGame/Game Config")]
public class GameConfig : ScriptableObject
{
    public List<ChapterConfig> chapters = new();
}
