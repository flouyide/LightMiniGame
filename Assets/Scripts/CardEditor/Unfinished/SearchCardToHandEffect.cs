using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/定向抽牌", fileName = "SearchCardToHand")]
    public class SearchCardToHandEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "从牌库定向抽取卡牌";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            string cardName = ReadString(customParams, "cardName", "主机");
            int count = ReadInt(customParams, "count", 1);
            ctx.SearchCardInDrawPileToHand(cardName, count);
        }

        private static int ReadInt(string customParams, string key, int fallback)
        {
            if (string.IsNullOrEmpty(customParams)) return fallback;
            var match = System.Text.RegularExpressions.Regex.Match(customParams, key + @"[""\s]*:\s*(-?\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed))
                return parsed;
            return fallback;
        }

        private static string ReadString(string customParams, string key, string fallback)
        {
            if (string.IsNullOrEmpty(customParams)) return fallback;
            var match = System.Text.RegularExpressions.Regex.Match(customParams, key + @"[""\s]*:\s*""?([^""\n]+)""?");
            if (match.Success)
                return match.Groups[1].Value.Trim();
            return fallback;
        }
    }
}
