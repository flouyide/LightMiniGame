using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：折叠床。
    ///
    /// 获得后，之后所有 Rest 休息处按指定倍率提高百分比生命回复。
    /// 不影响扣血、金币、理智或其他非生命效果；通过 Rest 专用入口结算，
    /// 因而不会放大事件或战斗中的任何治疗。
    ///
    /// 可配置参数（选中“折叠床”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 休整处生命恢复倍率，默认 1.5（恢复量增加 50%）。
    /// 多个同类加成的额外比例相加，例如两个 1.5 倍来源会使休整恢复量变为 2 倍。
    /// </summary>
    public class FoldingBedEffect : RelicEffectBase
    {
        public const float DefaultRestHealingMultiplier = 1.5f;

        public override void OnGain(RelicEffectContext ctx)
        {
            ChapterManager chapter = ctx?.chapter;
            if (chapter == null)
            {
                Debug.LogWarning("[FoldingBed] 找不到 ChapterManager，无法登记休整治疗加成");
                return;
            }

            float multiplier = Mathf.Max(1f, GetEffectParam(ctx.relic, 0, DefaultRestHealingMultiplier));
            chapter.SetRestHealingMultiplier(BuildSourceKey(ctx), multiplier);
            Debug.Log($"[FoldingBed] 已登记后续休整处生命恢复倍率 {multiplier:P0}");
        }

        public override void OnLost(RelicEffectContext ctx)
        {
            ChapterManager chapter = ctx?.chapter;
            if (chapter == null) return;

            chapter.SetRestHealingMultiplier(BuildSourceKey(ctx), 1f);
            Debug.Log("[FoldingBed] 已移除休整治疗加成");
        }

        private static string BuildSourceKey(RelicEffectContext ctx)
        {
            return $"FoldingBed:{ctx?.owner?.characterId ?? "UnknownOwner"}:{ctx?.relic?.relicId ?? "UnknownRelic"}";
        }
    }
}
