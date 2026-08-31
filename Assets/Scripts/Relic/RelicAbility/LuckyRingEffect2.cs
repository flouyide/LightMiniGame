using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 幸运戒指 / 彻底疯狂魔方（共用）：拾起时立即提高本局玩家福报值（Fortune）。
    ///
    /// 福报值 = 融合重分配总值加成：每次融合时，把选中数字之和 + 当前福报值 作为重分配总值。
    /// 福报值由 ChapterManager 按来源登记，跨战斗持续生效；遗物被移除时会精确撤销自身提供的加成。
    /// 初始福报值来自 PlayerConfig.startFortune（见 ChapterManager.InitPlayerStats）。
    ///
    /// 可配置参数（选中对应 RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 拾起时增加的福报值，默认 5；小于 1 时按 1 处理。
    /// </summary>
    public class LuckyRingEffect2 : RelicEffectBase
    {
        public const int DefaultFortuneBonus = 5;

        public override void OnGain(RelicEffectContext ctx)
        {
            ChapterManager chapter = ctx?.chapter;
            if (chapter == null)
            {
                Debug.LogWarning("[LuckyRing] 找不到 ChapterManager，无法增加玩家福报值");
                return;
            }

            int bonus = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultFortuneBonus)));
            chapter.SetFortuneBonus(BuildSourceKey(ctx), bonus);
            Debug.Log($"[LuckyRing] 拾起后玩家福报 +{bonus}");
        }

        public override void OnLost(RelicEffectContext ctx)
        {
            ChapterManager chapter = ctx?.chapter;
            if (chapter != null)
                chapter.SetFortuneBonus(BuildSourceKey(ctx), 0);
            Debug.Log("[LuckyRing] 已移除玩家福报加成");
        }

        private static string BuildSourceKey(RelicEffectContext ctx)
        {
            return $"LuckyRing:{ctx?.owner?.characterId ?? "UnknownOwner"}:{ctx?.relic?.relicId ?? "UnknownRelic"}";
        }
    }
}
