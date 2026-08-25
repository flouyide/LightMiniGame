using LightMiniGame.Card;
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：计算器。
    ///
    /// 仅对遗物归属角色生效：该角色在整场战斗内每累计打出第 N 张牌（第 N、2N、3N……张），
    /// 其下一张牌的费用减少 X 点（最低 0）；另一角色的出牌不参与计数，也不会获得减费。
    ///
    /// 结算语义：
    ///   - 计数在整场战斗内累计，不会于玩家回合开始时重置（与“报销单据”一致，区别于“复印机”的每回合计数）；
    ///   - 打出第 4、9、14……张【归属角色】卡牌后，立即把该角色当前手牌的 CardData.relicCostReduction 写为减免值，
    ///     因而下一张牌的 CostText 会先显示减免后的费用；打出该张牌后立即清除这次减免；
    ///   - 费用统一通过 CardData.GetEffectiveCost 结算，最低固定为 0，所以 0 费牌仍显示并支付 0；
    ///   - 自动重放（复印机）不经过 PlayCard 的扣费路径，故不会消耗或干扰本遗物的计数；
    ///   - 若某张牌因行动点/贿赂不足而未能打出，则计数不递增，下一次真正打出的牌仍按原序号享受减免。
    ///
    /// 可配置参数（选中“计算器”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 触发间隔（每第几张牌），默认 5；小于 1 时使用默认值；
    ///   Effect Params [1] = 减免的费用点数，默认 1；小于 0 时按 0 处理（即不减免）。
    ///
    /// 依赖 BattleManager.HandCards（写入当前手牌运行时费用减免）与 OnPlayerCardPlayed（成功出牌后累计计数）。
    /// </summary>
    public class CalculatorEffect : RelicEffectBase
    {
        public const int DefaultTriggerEvery = 5;
        public const int DefaultCostReduction = 1;

        private BattleManager _battle;
        private CharacterData _owner;
        private int _triggerEvery = DefaultTriggerEvery;
        private int _costReduction = DefaultCostReduction;
        private int _cardsPlayedThisBattle;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            _owner = ctx.owner;
            if (_battle == null || _owner == null) return;

            _triggerEvery = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultTriggerEvery)));
            _costReduction = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 1, DefaultCostReduction)));
            _cardsPlayedThisBattle = 0;

            _battle.OnPlayerCardPlayed += OnPlayerCardPlayed;
            _battle.OnHandCardsChanged += ApplyPendingCostReduction;
            ApplyPendingCostReduction();
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        /// <summary>
        /// 成功出牌后累计计数。若刚打出的是第 4、9、14……张牌，下一张牌（第 5、10、15……张）
        /// 会立即获得费用减免，并在 RefreshHandUI 重建卡面时显示到 CostText。
        /// </summary>
        private void OnPlayerCardPlayed(CardData card)
        {
            if (_battle == null || card == null) return;

            // 当前打出的卡必定来自当前激活角色；非遗物归属角色的卡牌完全不参与计算器逻辑。
            if (!IsOwnerActive()) return;

            // 当前被打出的归属角色卡已不应保留下一张牌的预览减免。
            card.relicCostReduction = 0;
            _cardsPlayedThisBattle++;
            ApplyPendingCostReduction();
        }

        /// <summary>仅在遗物归属角色激活时，按其下一张即将打出的序号刷新当前手牌费用减免。</summary>
        private void ApplyPendingCostReduction()
        {
            if (_battle == null || !IsOwnerActive()) return;

            bool shouldReduceNextCard = _costReduction > 0
                && (_cardsPlayedThisBattle + 1) % _triggerEvery == 0;
            int reduction = shouldReduceNextCard ? _costReduction : 0;

            foreach (CardData handCard in _battle.HandCards)
            {
                if (handCard != null)
                    handCard.relicCostReduction = reduction;
            }
        }

        /// <summary>当前手牌只属于激活角色，因此以激活角色和遗物归属是否一致来限定作用范围。</summary>
        private bool IsOwnerActive()
            => _battle != null && _owner != null && _battle.ActiveCharacterData == _owner;

        private void Detach(BattleManager battle)
        {
            if (battle != null)
            {
                foreach (CardData handCard in battle.HandCards)
                {
                    if (handCard != null)
                        handCard.relicCostReduction = 0;
                }

                battle.OnPlayerCardPlayed -= OnPlayerCardPlayed;
                battle.OnHandCardsChanged -= ApplyPendingCostReduction;
            }

            _battle = null;
            _cardsPlayedThisBattle = 0;
        }
    }
}
