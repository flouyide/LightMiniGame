using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：抗压面具。
    ///
    /// 拾起时立即提高本局玩家理智上限；不额外回复理智。
    /// 加成由 ChapterManager 按来源登记，跨战斗持续生效；失去遗物时会精确移除。
    ///
    /// 可配置参数（选中“抗压面具”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 拾起时增加的理智上限，默认 2。
    /// </summary>
    public class PressureResistanceMaskEffect : RelicEffectBase
    {
        public const int DefaultMaxSanityBonus = 2;

        public override void OnGain(RelicEffectContext ctx)
        {
            ChapterManager chapter = ctx?.chapter;
            if (chapter == null)
            {
                Debug.LogWarning("[PressureResistanceMask] 找不到 ChapterManager，无法增加理智上限");
                return;
            }

            int bonus = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultMaxSanityBonus)));
            chapter.SetMaxSanityBonus(BuildSourceKey(ctx), bonus);
            Debug.Log($"[PressureResistanceMask] 拾起后理智上限 +{bonus}");
        }

        public override void OnLost(RelicEffectContext ctx)
        {
            ChapterManager chapter = ctx?.chapter;
            if (chapter == null) return;

            chapter.SetMaxSanityBonus(BuildSourceKey(ctx), 0);
            Debug.Log("[PressureResistanceMask] 已移除理智上限加成");
        }

        private static string BuildSourceKey(RelicEffectContext ctx)
        {
            return $"PressureResistanceMask:{ctx?.owner?.characterId ?? "UnknownOwner"}:{ctx?.relic?.relicId ?? "UnknownRelic"}";
        }
    }
}
