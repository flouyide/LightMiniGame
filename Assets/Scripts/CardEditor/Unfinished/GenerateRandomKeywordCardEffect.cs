using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/随机生成词条卡", fileName = "GenerateRandomKeywordCard")]
    public class GenerateRandomKeywordCardEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "随机生成指定词条卡牌";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int keywordInt = ReadInt(customParams, "keyword", 4); // 4 = Accessory
            int count = ReadInt(customParams, "count", 2);
            var keyword = (CardKeyword2)keywordInt;
            ctx.GenerateRandomCardsByKeyword(keyword, count, CardZoneType.Hand);
        }

        private static int ReadInt(string customParams, string key, int fallback)
        {
            if (string.IsNullOrEmpty(customParams)) return fallback;
            var match = System.Text.RegularExpressions.Regex.Match(customParams, key + @"[""\s]*:\s*(-?\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed))
                return parsed;
            return fallback;
        }
    }
}
