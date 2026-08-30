using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/主机增伤触发", fileName = "HostDamageBonus")]
    public class HostDamageBonusEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "主机每回合增伤(触发器)";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int bonus = ReadInt(customParams, "bonus", 6);
            ctx.AddDamageBonusToHostCard(bonus);
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
