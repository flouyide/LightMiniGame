using System;
using LightMiniGame.Shop;
using UnityEngine;

/// <summary>
/// 敌人能力条目（敌人的遗物）。来自《《光明》游戏策划案》5.3：精英/boss 级敌人自带某些能力。
/// 模型：直接引用现有 RelicData 资产；能力逻辑由 RelicData.effectScriptName 指向的
/// IRelicEffect 类实现（BattleManager 战斗开始时反射实例化，见 InitEnemyAbilityEffects）。
/// </summary>
[Serializable]
public class EnemyAbilityEntry
{
    [Tooltip("能力引用的遗物资产（必填，其 Effect Script 决定能力逻辑）")]
    public RelicData relic;
}
