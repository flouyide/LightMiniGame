using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/获得冒名层数", fileName = "AddImpostorStacks")]
    public class AddImpostorStacksEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "获得冒名层数";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int stacks = UnfinishedEffectParams.ReadInt(customParams, "stacks", 1);
            var battle = UnfinishedEffectParams.Battle(ctx);
            if (battle == null) return;
            int slot = battle.CurrentInitiatorEnemySlot;
            if (slot < 0) return;
            battle.AddImpostorStacks(slot, stacks);
        }
    }
}
