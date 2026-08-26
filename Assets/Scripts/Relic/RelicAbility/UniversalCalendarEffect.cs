using LightMiniGame.Card;
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：万向历。
    ///
    /// 每个玩家回合内，遗物归属角色打出的第一张技能牌会复制一张独立副本到当前手牌。
    ///
    /// 结算语义：
    ///   - 仅当前激活角色等于遗物持有者时才判定；另一角色的技能牌不触发，也不消耗次数；
    ///   - 监听原始出牌结算后的 BattleManager.OnPlayerCardPlayed，因此复制不会额外扣行动点、
    ///     不增加出牌计数，也不会递归触发本遗物；
    ///   - 副本保留原牌当前理智形态、融合覆盖和附加效果，但拥有独立的运行时 CardData；
    ///   - 若原牌不是“循环”牌，触发时可暂时比手牌上限多 1 张，随后原牌会在同次出牌流程中离开手牌；
    ///     若原牌会留在手牌且没有空位，则本次无法复制。
    ///
    /// 可配置参数（选中“万向历”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 每次触发复制数量，默认 1；小于 1 时按 1 处理。
    /// </summary>
    public class UniversalCalendarEffect : RelicEffectBase
    {
        public const int DefaultCopyCount = 1;

        private BattleManager _battle;
        private CharacterData _owner;
        private int _copyCount = DefaultCopyCount;
        private bool _triggeredThisTurn;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            Detach(_battle);

            _battle = ctx?.battle;
            _owner = ctx?.owner;
            if (_battle == null || _owner == null) return;

            _copyCount = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultCopyCount)));
            _triggeredThisTurn = false;

            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;
            _battle.OnPlayerCardPlayed += OnPlayerCardPlayed;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnPlayerTurnStarted()
        {
            _triggeredThisTurn = false;
        }

        private void OnPlayerCardPlayed(CardData card)
        {
            if (_battle == null || card == null || _triggeredThisTurn) return;
            if (!IsOwnerActive() || !card.IsSkillCard()) return;

            // 先锁定本回合次数，避免复制副本或后续扩展路径再次进入时重复触发。
            _triggeredThisTurn = true;

            int copiedCount = 0;
            for (int i = 0; i < _copyCount; i++)
            {
                if (!_battle.CopyCardToHand(card)) break;
                copiedCount++;
            }

            if (copiedCount > 0)
                Debug.Log($"[UniversalCalendar] {_owner.Label} 本回合第一张技能牌 {card.cardName} 已复制 {copiedCount} 张到手牌");
        }

        private bool IsOwnerActive()
            => _battle != null && _owner != null && _battle.ActiveCharacterData == _owner;

        private void Detach(BattleManager battle)
        {
            BattleManager target = battle ?? _battle;
            if (target != null)
            {
                target.OnPlayerTurnStarted -= OnPlayerTurnStarted;
                target.OnPlayerCardPlayed -= OnPlayerCardPlayed;
            }

            _battle = null;
            _owner = null;
            _copyCount = DefaultCopyCount;
            _triggeredThisTurn = false;
        }
    }
}
