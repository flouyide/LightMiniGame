using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：哑铃。
    ///
    /// 拾起时立即提高本局玩家力量；加成由 ChapterManager 按来源登记，跨战斗持续生效。
    /// 遗物被移除时会精确撤销自身提供的力量加成。
    ///
    /// 可配置参数（选中“哑铃”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 拾起时增加的力量，默认 1。
    /// </summary>
    public class DumbbellEffect1 : RelicEffectBase
    {
        public const int DefaultStrengthBonus = 1;

        public override void OnGain(RelicEffectContext ctx)
        {
            ChapterManager chapter = ctx?.chapter;
            if (chapter == null)
            {
                Debug.LogWarning("[Dumbbell] 找不到 ChapterManager，无法增加玩家力量");
                return;
            }

            int bonus = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultStrengthBonus)));
            string sourceKey = BuildSourceKey(ctx);
            chapter.SetStrengthBonus(sourceKey, bonus);

            // 若哑铃在战斗已存在时获得，也立即更新可见的 Buff UI；正常进战仍会在 OnBattleStart 重新登记。
            Object.FindObjectOfType<BattleManager>()?.SetPlayerDisplayOnlyBuff(
                sourceKey, BuffAttributeType.Strength, bonus);
            Debug.Log($"[Dumbbell] 拾起后玩家力量 +{bonus}");
        }

        public override void OnLost(RelicEffectContext ctx)
        {
            ChapterManager chapter = ctx?.chapter;
            if (chapter != null)
                chapter.SetStrengthBonus(BuildSourceKey(ctx), 0);

            // 正常流程会在 OnBattleEnd 清理；这里额外处理战斗中遗物被移除的情况。
            BattleManager battle = ctx?.battle ?? Object.FindObjectOfType<BattleManager>();
            battle?.SetPlayerDisplayOnlyBuff(BuildSourceKey(ctx), BuffAttributeType.Strength, 0);
            Debug.Log("[Dumbbell] 已移除玩家力量加成");
        }

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            BattleManager battle = ctx?.battle;
            if (battle == null) return;

            int bonus = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultStrengthBonus)));

            // 数值已由 ChapterManager.PlayerStrength 读入战斗；此处只登记 Buff UI，不能再写入 BuffSystem。
            battle.SetPlayerDisplayOnlyBuff(BuildSourceKey(ctx), BuffAttributeType.Strength, bonus);
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory)
        {
            ctx?.battle?.SetPlayerDisplayOnlyBuff(BuildSourceKey(ctx), BuffAttributeType.Strength, 0);
        }

        private static string BuildSourceKey(RelicEffectContext ctx)
        {
            return $"Dumbbell:{ctx?.owner?.characterId ?? "UnknownOwner"}:{ctx?.relic?.relicId ?? "UnknownRelic"}";
        }
    }
}
