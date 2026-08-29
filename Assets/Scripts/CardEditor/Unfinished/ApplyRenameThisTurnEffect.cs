using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/牌库随机牌替换为攻击", fileName = "ApplyRenameThisTurn")]
    public class ApplyRenameThisTurnEffect : CustomEffectScript
    {
        [Tooltip("替换进去的攻击牌。表：变为可配置的另一张牌。")]
        public CardEntry replacementCard;

        public override string GetDisplayName() => "当前角色牌库1张随机牌替换为攻击";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            var battle = UnfinishedEffectParams.Battle(ctx);
            if (battle == null) return;
            if (replacementCard == null)
            {
                Debug.LogWarning("[UnfinishedCard] 改名未配置 replacementCard，请把攻击牌拖到该脚本资产上");
                return;
            }
            battle.ReplaceRandomDeckCard(replacementCard);
        }
    }
}
