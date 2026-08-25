using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：门禁卡。
    ///
    /// 每一章额外增加指定次数的书页选择机会。获得时立即将本章剩余次数增加同等数值，
    /// 之后进入每一章时由 ChapterManager 自动叠加；失去遗物时会移除来源对应的加成。
    ///
    /// 可配置参数（选中“门禁卡”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 每章额外书页选择次数，默认 3。
    /// </summary>
    public class AccessCardEffect : RelicEffectBase
    {
        public const int DefaultSelectionsPerChapter = 3;

        public override void OnGain(RelicEffectContext ctx)
        {
            ChapterManager chapter = ctx?.chapter;
            if (chapter == null)
            {
                Debug.LogWarning("[AccessCard] 找不到 ChapterManager，无法登记章节书页选择次数加成");
                return;
            }

            int bonus = Mathf.Max(0, Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultSelectionsPerChapter)));
            chapter.SetChapterSelectionBonus(BuildSourceKey(ctx), bonus);
            Debug.Log($"[AccessCard] 已登记每章 +{bonus} 次书页选择；当前章节立即生效");
        }

        public override void OnLost(RelicEffectContext ctx)
        {
            ChapterManager chapter = ctx?.chapter;
            if (chapter == null) return;

            chapter.SetChapterSelectionBonus(BuildSourceKey(ctx), 0);
            Debug.Log("[AccessCard] 已移除每章书页选择次数加成");
        }

        private static string BuildSourceKey(RelicEffectContext ctx)
        {
            return $"AccessCard:{ctx?.owner?.characterId ?? "UnknownOwner"}:{ctx?.relic?.relicId ?? "UnknownRelic"}";
        }
    }
}
