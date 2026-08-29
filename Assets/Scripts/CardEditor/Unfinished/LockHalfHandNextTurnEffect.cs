using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/半数手牌下回合无法打出", fileName = "LockHalfHandNextTurn")]
    public class LockHalfHandNextTurnEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "半数手牌（向上取整）下回合无法打出";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            UnfinishedEffectParams.Battle(ctx)?.Unfinished?.LockCeilHalfHandNextTurn();
        }
    }
}
