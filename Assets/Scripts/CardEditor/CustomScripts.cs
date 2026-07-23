using UnityEngine;

namespace LightMiniGame.CardEditor
{
    // ========================================================================
    // 自定义脚本基类 —— 策划在编辑器中拖入对应 ScriptableObject 资产即可
    // 战斗系统运行时调用具体方法（由战斗层实现，此处只定义可拖拽引用基类）
    // ========================================================================

    /// <summary>
    /// 自定义效果脚本基类。
    /// 继承后创建为 ScriptableObject 资产，拖入卡牌效果的自定义脚本字段。
    /// 战斗系统运行时会调用 ExecuteEffect 方法。
    /// </summary>
    public abstract class CustomEffectScript : ScriptableObject
    {
        /// <summary>编辑器中显示的名称</summary>
        public abstract string GetDisplayName();
    }

    /// <summary>
    /// 自定义条件脚本基类。
    /// 继承后创建为 ScriptableObject 资产，拖入条件的自定义脚本字段。
    /// 战斗系统运行时会调用 Evaluate 方法。
    /// </summary>
    public abstract class CustomConditionScript : ScriptableObject
    {
        /// <summary>编辑器中显示的名称</summary>
        public abstract string GetDisplayName();
    }

    /// <summary>
    /// 自定义卡牌脚本基类。
    /// 继承后创建为 ScriptableObject 资产，拖入卡牌的自定义卡牌脚本字段。
    /// 处理整张卡的特殊流程（自定义目标选择、特殊结算顺序、玩家二次选择等）。
    /// </summary>
    public abstract class CustomCardScript : ScriptableObject
    {
        /// <summary>编辑器中显示的名称</summary>
        public abstract string GetDisplayName();
    }

    /// <summary>
    /// 自定义能力脚本基类。
    /// 继承后创建为 ScriptableObject 资产，拖入能力的自定义能力脚本字段。
    /// </summary>
    public abstract class CustomAbilityScript : ScriptableObject
    {
        /// <summary>编辑器中显示的名称</summary>
        public abstract string GetDisplayName();
    }
}
