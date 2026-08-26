using LightMiniGame.Card;
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：褪色胸针。
    ///
    /// 每个玩家回合开始时，若玩家当前处于低理智状态，额外抽取 N 张牌（默认 1）。
    ///
    /// 结算语义：
    ///   - 低理智判定与战斗阶段、低理智卡牌形态共用 BattleManager 的理智阈值；
    ///   - 额外抽牌发生在本回合常规抽牌之前，仍受手牌上限、抽牌堆与弃牌堆重洗规则约束；
    ///   - 理智恢复至阈值以上后，本回合不会触发；下一回合会重新判定；
    ///   - 仅当遗物归属角色是当前激活角色时生效；另一角色即使共享低理智状态，也不会额外抽牌。
    ///
    /// 可配置参数（选中“褪色胸针”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 低理智回合额外抽牌数，默认 1；小于 1 时按 1 处理。
    /// </summary>
    public class FadedBroochEffect : RelicEffectBase
    {
        public const int DefaultExtraDrawCount = 1;

        private BattleManager _battle;
        private CharacterData _owner;
        private int _extraDrawCount = DefaultExtraDrawCount;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            Detach(_battle);

            _battle = ctx?.battle;
            _owner = ctx?.owner;
            if (_battle == null || _owner == null) return;

            _extraDrawCount = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultExtraDrawCount)));
            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnPlayerTurnStarted()
        {
            if (_battle == null || !IsOwnerActive() || !_battle.IsLowSanityForFusion) return;

            _battle.DrawCards(_extraDrawCount);
            Debug.Log($"[FadedBrooch] {_owner.Label} 低理智回合开始，额外抽取 {_extraDrawCount} 张牌");
        }

        /// <summary>当前手牌属于激活角色，仅在该角色正是遗物持有者时触发。</summary>
        private bool IsOwnerActive()
            => _battle != null && _owner != null && _battle.ActiveCharacterData == _owner;

        private void Detach(BattleManager battle)
        {
            BattleManager target = battle ?? _battle;
            if (target != null)
                target.OnPlayerTurnStarted -= OnPlayerTurnStarted;

            _battle = null;
            _owner = null;
            _extraDrawCount = DefaultExtraDrawCount;
        }
    }
}
