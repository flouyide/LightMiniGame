using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：报销单据。
    ///
    /// 每累计消耗指定数量的卡牌，恢复指定点数的行动点。
    ///
    /// “消耗”仅指卡牌实际进入当前角色的 consumedPile：
    ///   - CardEntry 当前形态为 BattleRemove / PermanentRemove；
    ///   - 旧 CardData 的 consumeType 为 ThisBattle / ThisRun。
    /// 普通牌结算后进入 discardPile 不计入；复印机等免费重放也不会再次消耗原卡，故不计入。
    ///
    /// 可配置参数（选中“报销单据” RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 每次报销需要的消耗牌数量，默认 3；小于 1 时使用默认值；
    ///   Effect Params [1] = 每次报销恢复的行动点数量，默认 1；小于 1 时使用默认值。
    ///
    /// 计数在整场战斗内累计，不会于玩家回合开始时重置；一次进入 consumedPile 的卡只触发一次事件，
    /// 因此达到 3、6、9……张时各恢复一次行动点。
    /// </summary>
    public class ExpenseReceiptEffect : RelicEffectBase
    {
        public const int DefaultCardsPerReimbursement = 3;
        public const int DefaultEnergyRestored = 1;

        private BattleManager _battle;
        private int _cardsPerReimbursement = DefaultCardsPerReimbursement;
        private int _energyRestored = DefaultEnergyRestored;
        private int _consumedCardsSinceLastReimbursement;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            if (_battle == null) return;

            _cardsPerReimbursement = GetPositiveInt(
                GetEffectParam(ctx.relic, 0, DefaultCardsPerReimbursement),
                DefaultCardsPerReimbursement);
            _energyRestored = GetPositiveInt(
                GetEffectParam(ctx.relic, 1, DefaultEnergyRestored),
                DefaultEnergyRestored);
            _consumedCardsSinceLastReimbursement = 0;

            _battle.OnPlayerCardConsumed += OnPlayerCardConsumed;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnPlayerCardConsumed(CardData card)
        {
            if (_battle == null || card == null) return;

            _consumedCardsSinceLastReimbursement++;
            if (_consumedCardsSinceLastReimbursement < _cardsPerReimbursement) return;

            _consumedCardsSinceLastReimbursement -= _cardsPerReimbursement;
            _battle.AddActionPoints(_energyRestored);
            Debug.Log($"[ExpenseReceipt] 已消耗 {_cardsPerReimbursement} 张牌，恢复 {_energyRestored} 点行动点；当前累计 {_consumedCardsSinceLastReimbursement} 张");
        }

        private static int GetPositiveInt(float value, int fallback)
        {
            int result = Mathf.RoundToInt(value);
            return result >= 1 ? result : fallback;
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
                battle.OnPlayerCardConsumed -= OnPlayerCardConsumed;

            _battle = null;
            _consumedCardsSinceLastReimbursement = 0;
        }
    }
}
