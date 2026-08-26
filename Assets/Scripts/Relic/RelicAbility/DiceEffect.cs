using LightMiniGame.Card;
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：骰子。
    ///
    /// 遗物持有角色处于激活状态时，每个玩家回合额外获得 N 次融合机会（默认 1）。
    /// 因此该角色每回合最多可融合 2 次；切换至未持有骰子的角色后，额外次数不再可用。
    ///
    /// 可配置参数（选中“骰子”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 每回合额外融合次数，默认 1；小于 1 时按 1 处理。
    /// </summary>
    public class DiceEffect : RelicEffectBase
    {
        public const int DefaultExtraFusionUses = 1;

        private BattleManager _battle;
        private CharacterData _owner;
        private string _sourceId;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            Detach(_battle);

            _battle = ctx?.battle;
            _owner = ctx?.owner;
            if (_battle == null || _owner == null) return;

            int extraUses = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultExtraFusionUses)));
            _sourceId = $"{GetType().FullName}:{_owner.name}";
            _battle.SetExtraFusionUses(_sourceId, _owner, extraUses);
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void Detach(BattleManager battle)
        {
            BattleManager target = battle ?? _battle;
            if (target != null && !string.IsNullOrEmpty(_sourceId))
                target.SetExtraFusionUses(_sourceId, null, 0);

            _battle = null;
            _owner = null;
            _sourceId = null;
        }
    }
}
