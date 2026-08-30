using LightMiniGame.Relic;
using LightMiniGame.Shop;    // RelicData
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    public class FusionPersistenceRelicEffect : RelicEffectBase
    {
        public override void OnBattleStart(RelicEffectContext ctx)
        {
            if (ctx?.battle == null) return;

            ctx.battle.EnableFusionPersistence();
            Debug.Log("[FusionPersistenceRelic] 进阶效果1已解锁：融合修改将跨战斗持久化（persistFusion = true）");
        }
    }
}

