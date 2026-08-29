using UnityEngine;

namespace LightMiniGame.CardEditor
{
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/按冒名层数获得力量", fileName = "GainStrengthFromImpostor")]
    public class GainStrengthFromImpostorEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "获得等同于当前冒名层数的力量（最多+3）";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int bonus = UnfinishedEffectParams.ReadInt(customParams, "bonus", 0);
            var battle = UnfinishedEffectParams.Battle(ctx);
            if (battle == null) return;
            int slot = battle.CurrentInitiatorEnemySlot;
            if (slot < 0) return;
            int stacks = battle.GetImpostorStacks(slot);
            int gain = Mathf.Min(UnfinishedCardRuntime.ImpostorStrengthCap, Mathf.Max(0, stacks + bonus));
            if (gain <= 0) return;
            battle.ApplyEnemyAttributeBuff(slot, PlayerAttributeType.Strength, gain);
            Debug.Log($"[UnfinishedCard] 敌人[{slot}] 按冒名 {stacks} 获得力量+{gain}（上限 {UnfinishedCardRuntime.ImpostorStrengthCap}）");
        }
    }
}
