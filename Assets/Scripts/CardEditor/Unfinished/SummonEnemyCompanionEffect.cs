using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/召唤同伴", fileName = "SummonEnemyCompanion")]
    public class SummonEnemyCompanionEffect : CustomEffectScript
    {
        [Tooltip("相对最后一名存活敌人的锚点偏移（像素）")]
        public Vector2 spawnOffset = new Vector2(220f, 0f);

        public override string GetDisplayName() => "召唤1只与自身同配置的同伴";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            var battle = UnfinishedEffectParams.Battle(ctx);
            if (battle == null) return;
            int slot = battle.CurrentInitiatorEnemySlot;
            if (slot < 0) slot = ctx.SelectedEnemyIndex;
            battle.SummonEnemyCompanion(slot, spawnOffset);
        }
    }
}
