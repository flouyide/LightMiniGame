using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：暖手宝。
    ///
    /// 玩家回合结束时，若当前没有格挡（护甲为 0），则获得指定数量的格挡。
    ///
    /// 结算语义：
    ///   - 触发点：玩家回合结束（BattleManager.OnPlayerTurnEnded），此时手牌已弃、行动点已清，
    ///     玩家护甲尚未清空（护甲要到下个玩家回合 StartPlayerTurn 才归零）；
    ///   - 判定：PlayerArmor &lt;= 0 才补格挡；若玩家本回合已打出护甲牌（护甲 &gt; 0），则不重复获得；
    ///   - 获得的格挡立即生效，可在随后的敌人回合抵挡伤害（敌人攻击会先扣护甲）；
    ///   - 护甲显示会在敌人回合开始时的 UpdateUI 刷新（同牧师遗物的 UI 时序）。
    ///
    /// 可配置参数（选中“暖手宝”RelicData 资产 -&gt; Inspector）：
    ///   Effect Params [0] = 获得的格挡数量，默认 4；小于 0 时按 0 处理（即不获得）。
    ///
    /// 依赖 BattleManager.OnPlayerTurnEnded（回合结束事件）与 AddPlayerArmor / PlayerArmor。
    /// </summary>
    public class HandWarmerEffect : RelicEffectBase
    {
        public const int DefaultBlockAmount = 4;

        private BattleManager _battle;
        private int _blockAmount = DefaultBlockAmount;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            if (_battle == null) return;

            _blockAmount = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultBlockAmount)));

            _battle.OnPlayerTurnEnded += OnPlayerTurnEnded;
            Debug.Log($"[HandWarmer] 已装备 {ctx.relic?.relicName}，回合结束无格挡时将获得 {_blockAmount} 点格挡");
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        /// <summary>玩家回合结束：当前无格挡则获得格挡。</summary>
        private void OnPlayerTurnEnded()
        {
            if (_battle == null || _blockAmount <= 0) return;

            if (_battle.PlayerArmor <= 0)
            {
                _battle.AddPlayerArmor(_blockAmount);
                Debug.Log($"[HandWarmer] 回合结束无格挡，获得 {_blockAmount} 点格挡（当前 {_battle.PlayerArmor}）");
            }
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
                battle.OnPlayerTurnEnded -= OnPlayerTurnEnded;
            _battle = null;
        }
    }
}
