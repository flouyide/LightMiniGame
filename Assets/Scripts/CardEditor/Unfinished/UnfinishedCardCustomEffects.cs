using System.Text.RegularExpressions;
using UnityEngine;

namespace LightMiniGame.CardEditor
{
    /// <summary>
    /// 未完成卡牌表里紫色格子对应的自定义效果。
    /// 策划：效果类型选「自定义」，把本目录 .asset 拖进脚本区。
    /// </summary>
    internal static class UnfinishedEffectParams
    {
        public static int ReadInt(string customParams, string key, int fallback)
        {
            if (string.IsNullOrEmpty(customParams)) return fallback;
            var match = Regex.Match(customParams, key + @"[""\s]*:\s*(-?\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed))
                return parsed;
            return fallback;
        }

        public static BattleManager Battle(ICardRuntimeContext ctx)
            => ctx is BattleCardContext battleCtx ? battleCtx.Battle : null;
    }

    [CreateAssetMenu(menuName = "CardEditor/自定义效果/添加监控目标词条", fileName = "MarkWatchTargetThisTurn")]
    public class MarkWatchTargetThisTurnEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "为随机手牌添加监控目标词条";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int count = UnfinishedEffectParams.ReadInt(customParams, "count", 3);
            UnfinishedEffectParams.Battle(ctx)?.Unfinished?.AddWatchTargetKeyword(count);
        }
    }

    [CreateAssetMenu(menuName = "CardEditor/自定义效果/半数手牌下回合无法打出", fileName = "LockHalfHandNextTurn")]
    public class LockHalfHandNextTurnEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "半数手牌（向上取整）下回合无法打出";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            UnfinishedEffectParams.Battle(ctx)?.Unfinished?.LockCeilHalfHandNextTurn();
        }
    }

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

    [CreateAssetMenu(menuName = "CardEditor/自定义效果/施加缠结", fileName = "ApplyEntangle")]
    public class ApplyEntangleEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "施加缠结（手牌费用+层数，回合结束-1层）";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int stacks = UnfinishedEffectParams.ReadInt(customParams, "stacks", 1);
            UnfinishedEffectParams.Battle(ctx)?.Unfinished?.AddEntangleStacks(stacks);
        }
    }

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

    [CreateAssetMenu(menuName = "CardEditor/自定义效果/牌库随机牌替换为攻击", fileName = "ApplyRenameThisTurn")]
    public class ApplyRenameThisTurnEffect : CustomEffectScript
    {
        [Tooltip("替换进去的攻击牌。表：变为可配置的另一张牌。")]
        public CardEntry replacementCard;

        public override string GetDisplayName() => "当前角色牌库1张随机牌替换为攻击";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            var battle = UnfinishedEffectParams.Battle(ctx);
            if (battle == null) return;
            if (replacementCard == null)
            {
                Debug.LogWarning("[UnfinishedCard] 改名未配置 replacementCard，请把攻击牌拖到该脚本资产上");
                return;
            }
            battle.ReplaceRandomDeckCard(replacementCard);
        }
    }

    [CreateAssetMenu(menuName = "CardEditor/自定义效果/施加脏活", fileName = "ApplyDirtyWork")]
    public class ApplyDirtyWorkEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "施加脏活（受伤时额外受到3×层数伤害）";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            int stacks = UnfinishedEffectParams.ReadInt(customParams, "stacks", 1);
            UnfinishedEffectParams.Battle(ctx)?.Unfinished?.AddDirtyWorkStacks(stacks);
        }
    }
}
