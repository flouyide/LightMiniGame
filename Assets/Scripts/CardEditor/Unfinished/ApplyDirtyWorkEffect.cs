using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/施加脏活", fileName = "ApplyDirtyWork")]
    public class ApplyDirtyWorkEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "施加脏活（受伤时额外受到3×层数伤害）";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int stacks = UnfinishedEffectParams.ReadInt(customParams, "stacks", 1);
            UnfinishedEffectParams.Battle(ctx)?.Unfinished?.AddDirtyWorkStacks(stacks);
        }
    }
}
