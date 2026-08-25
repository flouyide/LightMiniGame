using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：续约合同。
    ///
    /// 首次死亡时复活：把玩家生命恢复到最大生命值的指定百分比，之后本遗物失效（整局仅一次）。
    ///
    /// 结算语义：
    ///   - 玩家生命归零、即将判负时（BattleManager.OnPlayerFatalDamage）拦截，改为恢复到
    ///     最大生命值的 revivePercent（向上取整）；
    ///   - 只在整局第一次死亡时生效，触发后本遗物失效，后续死亡按原逻辑判负；
    ///   - 只恢复生命，不改变战斗其它状态（敌人回合继续、不重置行动点/牌堆/理智等）。
    ///
    /// 可配置参数（选中“续约合同” RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 复活后恢复的最大生命值百分比，默认 30；小于 1 时使用默认值。
    ///
    /// 依赖 BattleManager.OnPlayerFatalDamage：玩家生命归零、判负前广播；
    /// 返回 true 表示有遗物接管死亡（复活），跳过本场战斗失败判定。
    /// </summary>
    public class ContractRenewalEffect : RelicEffectBase
    {
        public const float DefaultRevivePercent = 30f;

        private BattleManager _battle;
        private float _revivePercent = DefaultRevivePercent;
        private bool _used;   // 整局仅生效一次；触发后置 true，后续死亡不再复活

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            if (_battle == null || _used) return;   // 已失效则不订阅

            _revivePercent = GetEffectParam(ctx.relic, 0, DefaultRevivePercent);
            if (_revivePercent < 1f) _revivePercent = DefaultRevivePercent;

            _battle.OnPlayerFatalDamage += OnPlayerFatalDamage;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private bool OnPlayerFatalDamage()
        {
            if (_battle == null || _used) return false;

            // 先标记已用，避免同一帧内多次致命伤重复复活
            _used = true;
            int heal = Mathf.CeilToInt(_battle.PlayerMaxHP * _revivePercent / 100f);
            _battle.SetPlayerHP(heal);
            Debug.Log($"[ContractRenewal] 首次死亡触发续约合同：复活并恢复 {heal} 点生命" +
                      $"（{_revivePercent}% 最大生命值），本遗物失效");
            return true;   // 接管死亡，跳过判负
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
                battle.OnPlayerFatalDamage -= OnPlayerFatalDamage;

            _battle = null;
        }
    }
}
