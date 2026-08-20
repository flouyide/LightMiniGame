using LightMiniGame.Card;   // CharacterData
using LightMiniGame.Shop;    // RelicData
using UnityEngine;

namespace LightMiniGame.Relic
{
    /// <summary>
    /// 遗物效果接口。效果类通过 RelicData.effectScriptName（类名全名，含命名空间）在运行时反射实例化。
    /// 推荐继承 RelicEffectBase，只重写需要的方法即可（其余为空默认实现）。
    /// </summary>
    public interface IRelicEffect
    {
        /// <summary>遗物被获得/装备时调用一次（用于修改全局属性、注册监听等）。</summary>
        void OnGain(RelicEffectContext ctx);

        /// <summary>遗物被移除时调用（用于清理监听、还原状态）。</summary>
        void OnLost(RelicEffectContext ctx);

        /// <summary>战斗开始时调用（应用于本场战斗的遗物效果在此处理）。</summary>
        void OnBattleStart(RelicEffectContext ctx);

        /// <summary>战斗结束时调用。victory=true 表示胜利。</summary>
        void OnBattleEnd(RelicEffectContext ctx, bool victory);
    }

    /// <summary>
    /// 遗物效果基类：提供空默认实现与参数读取工具，子类只重写需要的方法，减少样板代码。
    /// </summary>
    public abstract class RelicEffectBase : IRelicEffect
    {
        public virtual void OnGain(RelicEffectContext ctx) { }
        public virtual void OnLost(RelicEffectContext ctx) { }
        public virtual void OnBattleStart(RelicEffectContext ctx) { }
        public virtual void OnBattleEnd(RelicEffectContext ctx, bool victory) { }

        /// <summary>
        /// 读取 RelicData.effectParams 指定下标的参数（Inspector 可配置）；
        /// 未配置 / 越界 / 资产为空时返回默认值，保证旧资产兼容。
        /// 参数含义由各效果类自定义（见类注释）。
        /// </summary>
        protected static float GetEffectParam(RelicData relic, int index, float fallback)
        {
            var ps = relic?.effectParams;
            if (ps != null && index < ps.Count)
                return ps[index];
            return fallback;
        }

        /// <summary>
        /// 读取 RelicData.effectStringParams 指定下标的字符串参数（Inspector 可配置）；
        /// 未配置 / 越界 / 资产为空时返回默认值。
        /// </summary>
        protected static string GetEffectStringParam(RelicData relic, int index, string fallback = "")
        {
            var ps = relic?.effectStringParams;
            if (ps != null && index < ps.Count)
                return ps[index];
            return fallback;
        }

        /// <summary>
        /// 读取 RelicData.effectObjectParams 指定下标的 UnityEngine.Object 参数（Inspector 可拖入资产引用）；
        /// 未配置 / 越界 / 资产为空 / 类型不匹配时返回默认值。
        /// </summary>
        protected static T GetEffectObjectParam<T>(RelicData relic, int index, T fallback = default) where T : UnityEngine.Object
        {
            var ps = relic?.effectObjectParams;
            if (ps != null && index < ps.Count)
                return ps[index] as T;
            return fallback;
        }
    }
}
