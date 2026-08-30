using LightMiniGame.Relic;
using LightMiniGame.Shop;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    public class FusionHpRelicEffect : RelicEffectBase
    {
        public override void OnBattleStart(RelicEffectContext ctx)
        {
            if (ctx?.battle == null) return;

            ctx.battle.EnableFusionHP();
            Debug.Log("[FusionHpRelic] Advanced effect 2 unlocked: HP/MaxHP enter fusion pool (includeHPInFusion = true)");
        }
    }
}
