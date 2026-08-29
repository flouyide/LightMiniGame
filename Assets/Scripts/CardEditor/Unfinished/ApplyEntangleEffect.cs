using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/施加缠结", fileName = "ApplyEntangle")]
    public class ApplyEntangleEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "施加缠结（手牌费用+层数，回合结束-1层）";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int stacks = UnfinishedEffectParams.ReadInt(customParams, "stacks", 1);
            UnfinishedEffectParams.Battle(ctx)?.Unfinished?.AddEntangleStacks(stacks);
        }
    }
}
