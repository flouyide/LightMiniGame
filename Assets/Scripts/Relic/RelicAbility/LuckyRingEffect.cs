using LightMiniGame.Card;
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 幸运戒指：遗物持有角色激活时，每次融合的重分配总值额外增加 N（默认 5）。
    ///
    /// 可配置参数（选中“幸运戒指”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 每次融合增加的总值，默认 5；小于 1 时按 1 处理。
    /// </summary>
    public class LuckyRingEffect : RelicEffectBase
    {
        public const int DefaultFusionPoolBonus = 5;

        private BattleManager _battle;
        private CharacterData _owner;
        private string _sourceId;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            Detach(_battle);

            _battle = ctx?.battle;
            _owner = ctx?.owner;
            if (_battle == null || _owner == null) return;

            int bonus = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultFusionPoolBonus)));
            _sourceId = $"{GetType().FullName}:{_owner.name}";
            _battle.SetFusionPoolBonus(_sourceId, _owner, bonus);
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void Detach(BattleManager battle)
        {
            BattleManager target = battle ?? _battle;
            if (target != null && !string.IsNullOrEmpty(_sourceId))
                target.SetFusionPoolBonus(_sourceId, null, 0);

            _battle = null;
            _owner = null;
            _sourceId = null;
        }
    }
}
