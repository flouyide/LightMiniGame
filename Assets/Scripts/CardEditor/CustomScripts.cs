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
    /// 战斗系统运行时会调用 Execute 方法。
    /// </summary>
    public abstract class CustomEffectScript : ScriptableObject
    {
        /// <summary>编辑器中显示的名称</summary>
        public abstract string GetDisplayName();

        /// <summary>
        /// 执行自定义效果。战斗系统在结算到此效果时调用。
        /// customParams 来自编辑器中填写的「自定义参数」文本。
        /// </summary>
        public abstract void Execute(ICardRuntimeContext ctx, string customParams);
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

        /// <summary>
        /// 评估条件是否满足。返回 true = 满足。
        /// </summary>
        public abstract bool Evaluate(ICardRuntimeContext ctx, string customParams);
    }

    /// <summary>
    /// 自定义卡牌脚本基类。
    /// 继承后创建为 ScriptableObject 资产，拖入卡牌的自定义卡牌脚本字段。
    /// 处理整张卡的特殊流程（自定义目标选择、特殊结算顺序、玩家二次选择等）。
    /// 如果 OnCardPlayed 返回 false，则跳过卡牌自身的通用效果列表，全部由脚本接管。
    /// </summary>
    public abstract class CustomCardScript : ScriptableObject
    {
        /// <summary>编辑器中显示的名称</summary>
        public abstract string GetDisplayName();

        /// <summary>
        /// 当卡牌被打出时调用。
        /// 返回 true = 继续执行卡牌自身的通用效果列表；
        /// 返回 false = 跳过通用效果，全部由本脚本接管。
        /// </summary>
        public virtual bool OnCardPlayed(ICardRuntimeContext ctx, CardEntry card, bool upgraded)
        {
            return true; // 默认不拦截，通用效果照常执行
        }
    }

    /// <summary>
    /// 自定义能力脚本基类。
    /// 继承后创建为 ScriptableObject 资产，拖入能力的自定义能力脚本字段。
    /// </summary>
    public abstract class CustomAbilityScript : ScriptableObject
    {
        /// <summary>编辑器中显示的名称</summary>
        public abstract string GetDisplayName();

        /// <summary>
        /// 当能力触发时机到达时调用。
        /// 返回 true = 继续执行能力配置的触发后效果列表；
        /// 返回 false = 跳过触发效果，全部由本脚本接管。
        /// </summary>
        public virtual bool OnTrigger(ICardRuntimeContext ctx, AbilityData ability)
        {
            return true; // 默认不拦截，触发效果照常执行
        }
    }
}
