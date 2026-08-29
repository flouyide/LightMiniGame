using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/添加监控目标词条", fileName = "MarkWatchTargetThisTurn")]
    public class MarkWatchTargetThisTurnEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "为随机手牌添加监控目标词条";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int count = UnfinishedEffectParams.ReadInt(customParams, "count", 3);
            UnfinishedEffectParams.Battle(ctx)?.Unfinished?.AddWatchTargetKeyword(count);
        }
    }
}
