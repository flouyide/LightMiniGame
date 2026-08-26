using LightMiniGame.Card;
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：烧水壶。
    ///
    /// 遗物持有角色处于激活状态时，若当前打出的手牌融合覆盖了攻击值，且力量缩放后的单次伤害达到阈值，
    /// 则该攻击牌的每一段伤害必定暴击。
    ///
    /// 可配置参数（选中“烧水壶”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 触发必暴所需的单次伤害，默认 20；小于 1 时按 1 处理。
    /// </summary>
    public class KettleEffect : RelicEffectBase
    {
        public const int DefaultMinimumSingleHitDamage = 20;

        private BattleManager _battle;
        private CharacterData _owner;
        private string _sourceId;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            Detach(_battle);

            _battle = ctx?.battle;
            _owner = ctx?.owner;
            if (_battle == null || _owner == null) return;

            int threshold = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultMinimumSingleHitDamage)));
            _sourceId = $"{GetType().FullName}:{_owner.name}";
            _battle.SetFusedAttackCriticalRule(_sourceId, _owner, threshold);
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void Detach(BattleManager battle)
        {
            BattleManager target = battle ?? _battle;
            if (target != null && !string.IsNullOrEmpty(_sourceId))
                target.SetFusedAttackCriticalRule(_sourceId, null, 0);

            _battle = null;
            _owner = null;
            _sourceId = null;
        }
    }
}
