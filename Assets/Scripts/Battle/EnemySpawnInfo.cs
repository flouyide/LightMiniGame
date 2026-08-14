using System;
using UnityEngine;

/// <summary>
/// 敌人出生信息：一个敌人 = 配置 + 位置。
/// 用于 PageEventData.enemies / EffectData.enterBattleEnemies / BattleManager.defaultEnemies。
/// 位置为 EnemyContainer（RectTransform）下的 anchoredPosition，运行时直接摆放。
/// </summary>
[Serializable]
public class EnemySpawnInfo
{
    [Tooltip("敌人配置资产")]
    public EnemyConfig config;
    [Tooltip("敌人在 EnemyContainer 下的 anchoredPosition（像素，容器中心为原点）")]
    public Vector2 anchoredPosition;
}
