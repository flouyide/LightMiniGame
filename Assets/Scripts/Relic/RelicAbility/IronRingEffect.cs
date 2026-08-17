using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 示例遗物效果：铁戒指风格——战斗胜利后获得额外金币。
    /// 演示如何编写遗物效果并通过 RelicData.effectScriptName 接入。
    ///
    /// 接入步骤：
    /// 1. 在 RelicData 资产（Inspector）把本脚本拖到 Effect Script 字段，
    ///    编辑器会自动把 effectScriptName 填成 "LightMiniGame.RelicEffects.IronRingEffect"；
    ///    或直接在 effectScriptName 手填该全名。
    /// 2. 获得该遗物时 RelicEffectManager 反射实例化本类并调用 OnGain；
    ///    战斗结束（胜利）时调用 OnBattleEnd，按逻辑发放奖励。
    /// </summary>
    public class IronRingEffect : RelicEffectBase
    {
        private const int BonusGoldOnVictory = 10;

        public override void OnGain(RelicEffectContext ctx)
        {
            // 获得时无需特殊处理；此处仅作日志示例
            Debug.Log($"[IronRingEffect] 已装备遗物 {ctx.relic?.relicName}（归属 {ctx.owner?.name}）");
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory)
        {
            if (!victory || ctx.chapter == null) return;
            ctx.chapter.AddGold(BonusGoldOnVictory);
            Debug.Log($"[IronRingEffect] 战斗胜利，获得 +{BonusGoldOnVictory} 金币");
        }
    }
}
