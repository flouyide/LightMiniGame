using System;
using LightMiniGame.Shop;
using UnityEngine;

/// <summary>
/// 敌人能力条目（敌人的遗物）。来自《《光明》游戏策划案》5.3：精英/boss 级敌人自带某些能力。
/// 当前模型：直接引用现有 RelicData 资产；triggerNote 是触发条件的备注文本（运行时暂不解析，留给后续扩展）。
/// </summary>
[Serializable]
public class EnemyAbilityEntry
{
    [Tooltip("能力显示名（用于编辑器内部识别，非运行时强制使用）")]
    public string displayName = "新能力";

    [Tooltip("能力引用的遗物资产（必填）")]
    public RelicData relic;

    [Tooltip("触发条件备注（运行时暂不解析）")]
    public string triggerNote = "";
}