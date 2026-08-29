using System.Text.RegularExpressions;
using UnityEngine;

namespace LightMiniGame.CardEditor
{
    /// <summary>
    /// 未完成卡牌表里紫色格子对应的自定义效果。
    /// 策划：效果类型选「自定义」，把本目录 .asset 拖进脚本区。
    /// </summary>
    internal static class UnfinishedEffectParams
    {
        public static int ReadInt(string customParams, string key, int fallback)
        {
            if (string.IsNullOrEmpty(customParams)) return fallback;
            var match = Regex.Match(customParams, key + @"[""\s]*:\s*(-?\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed))
                return parsed;
            return fallback;
        }

        public static BattleManager Battle(ICardRuntimeContext ctx)
            => ctx is BattleCardContext battleCtx ? battleCtx.Battle : null;
    }
}
