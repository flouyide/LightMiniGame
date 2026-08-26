using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：续命奶茶。
    ///
    /// 每场战斗胜负结算时增加玩家最大生命，并回复等量当前生命。
    /// BattleManager 会在玩家离开战斗前把更新后的生命数据写回 ChapterManager，
    /// 因此该成长与回复结果会在本局后续战斗中持续生效。
    ///
    /// 可配置参数（选中“续命奶茶”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 每场战斗结束后增加的最大生命与当前生命，默认 3。
    /// </summary>
    public class LifeSavingMilkTeaEffect : RelicEffectBase
    {
        public const int DefaultMaxHealthGain = 3;

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory)
        {
            BattleManager battle = ctx?.battle;
            if (battle == null) return;

            int gain = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultMaxHealthGain)));
            if (gain <= 0) return;

            int previousMaxHealth = battle.PlayerMaxHP;
            int previousHealth = battle.PlayerHP;
            battle.SetPlayerMaxHP(previousMaxHealth + gain);
            battle.HealPlayer(gain);
            battle.SetDirtyUI();

            Debug.Log($"[LifeSavingMilkTea] 战斗结束（{(victory ? "胜利" : "失败")}），最大生命 +{gain}：{previousMaxHealth} → {battle.PlayerMaxHP}；当前生命 +{gain}：{previousHealth} → {battle.PlayerHP}");
        }
    }
}
