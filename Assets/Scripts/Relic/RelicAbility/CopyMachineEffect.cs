using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：复印机。
    ///
    /// 每个玩家回合打出的第 3 张原始卡牌，在原始效果结算完成后自动免费重放一次。
    ///
    /// 结算语义：
    ///   - 原始第 3 张牌正常支付行动点、计入 CardsPlayed，并按原逻辑消耗/弃置；
    ///   - 重放只再次执行卡牌效果：不额外支付行动点、不增加出牌计数、不触发本遗物、
    ///     不移动牌堆或再次消耗该卡；
    ///   - 攻击牌沿用玩家打出原牌时已选定的目标；
    ///   - 第 3 张牌的原始效果已结束战斗时，不执行重放。
    ///
    /// 可配置参数（选中“复印机”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 每回合触发的第几张牌，默认 3；小于 1 时仍使用默认值。
    ///
    /// 依赖 BattleManager.OnPlayerCardPlayed：该事件只会为玩家原始出牌触发，
    /// 自动重放不会再次广播，因此不会递归复制。
    /// </summary>
    public class CopyMachineEffect : RelicEffectBase
    {
        public const int DefaultTriggerCardIndex = 3;

        private BattleManager _battle;
        private int _triggerCardIndex = DefaultTriggerCardIndex;
        private bool _triggeredThisTurn;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            if (_battle == null) return;

            _triggerCardIndex = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultTriggerCardIndex)));
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
            if (_battle.GetTurnCounter("CardsPlayed") != _triggerCardIndex) return;

            // 先消耗本回合触发次数，再进入重放，保证未来重放路径即便扩展事件也不会重复触发。
            _triggeredThisTurn = true;
            if (_battle.ReplayCardEffects(card))
                Debug.Log($"[CopyMachine] 本回合第 {_triggerCardIndex} 张牌 {card.cardName} 自动重放一次");
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
            {
                battle.OnPlayerTurnStarted -= OnPlayerTurnStarted;
                battle.OnPlayerCardPlayed -= OnPlayerCardPlayed;
            }

            _battle = null;
            _triggeredThisTurn = false;
        }
    }
}
