using LightMiniGame.Card;   // CharacterData
using LightMiniGame.Shop;    // RelicData

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
    /// 遗物效果基类：提供空默认实现，子类只重写需要的方法，减少样板代码。
    /// </summary>
    public abstract class RelicEffectBase : IRelicEffect
    {
        public virtual void OnGain(RelicEffectContext ctx) { }
        public virtual void OnLost(RelicEffectContext ctx) { }
        public virtual void OnBattleStart(RelicEffectContext ctx) { }
        public virtual void OnBattleEnd(RelicEffectContext ctx, bool victory) { }
    }
}
