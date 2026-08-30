using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/超频配件增伤", fileName = "OverclockEffect")]
    public class OverclockEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "超频：配件增伤(能力)";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int bonusPerAccessory = ReadInt(customParams, "bonus", 2);
            ctx.SetCustomData("OverclockActive", 1);
            ctx.SetCustomData("OverclockBonusPerAccessory", bonusPerAccessory);
            // 立即为已安装的配件附加增伤
            int count = ctx.GetCustomData("HostAccessoryCount");
            if (count > 0)
                ctx.AddDamageBonusToHostCard(count * bonusPerAccessory);
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
