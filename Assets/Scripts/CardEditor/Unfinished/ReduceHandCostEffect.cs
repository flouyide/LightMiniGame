using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/手牌费用减少", fileName = "ReduceHandCost")]
    public class ReduceHandCostEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "本回合手牌费用减少";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int amount = UnfinishedEffectParams.ReadInt(customParams, "amount", 1);
            UnfinishedEffectParams.Battle(ctx)?.Unfinished?.ReduceHandCostThisTurn(amount);
        }
    }
}
