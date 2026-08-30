using LightMiniGame.CardEditor;
using LightMiniGame.Relic;
using LightMiniGame.Shop;
using UnityEngine;
using System.Collections.Generic;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 敌人能力：序章·回合发牌机。
    ///
    /// 在玩家每个回合开始时（含战斗首回合），向当前激活角色手牌塞入配置好的牌。
    /// 支持「同一回合同时发多张不同的牌」，例如第 1 回合同时给 卡A×2 与 卡B×1。
    ///
    /// 配置方式：
    ///   1. 创建 TurnDealConfig 资产（Project 右键 -> Create -> CardGame -> Turn Deal Config）；
    ///   2. 在 turns 列表里逐回合配置 deals（每张牌 + 数量），可放多张不同的牌；
    ///   3. 把该 TurnDealConfig 拖入敌人能力 RelicData 的 Effect Object Params[0]。
    ///
    /// 只配 1 组 turns 则每回合发同一组；配多组则按回合循环（loop=true）或发完即止（loop=false）。
    /// 手牌已满时，只会实际塞入剩余空位可容纳的数量。宿主敌人死亡后不再发牌。
    ///
    /// 接入：将本 RelicData 拖入序章敌人 EnemyConfig -> abilities。
    /// </summary>
    public class PrologueTurnDealEffect : RelicEffectBase
    {
        private BattleManager _battle;
        private RelicData _relic;
        private TurnDealConfig _config;
        private int _turnIndex;
        private bool _hasLoggedMissingConfig;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx?.battle;
            _relic = ctx?.relic;
            if (_battle == null || _relic == null) return;

            _config = _relic.effectObjectParams != null && _relic.effectObjectParams.Count > 0
                ? _relic.effectObjectParams[0] as TurnDealConfig
                : null;
            _turnIndex = 0;
            _hasLoggedMissingConfig = false;

            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;

            // 首回合在 StartBattle 内预启动，OnPlayerTurnStarted 不会派发，主动补一次。
            InjectCards("战斗首回合");
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnPlayerTurnStarted() => InjectCards("回合开始");

        private void InjectCards(string trigger)
        {
            if (_battle == null) return;

            if (_config == null || _config.turns == null || _config.turns.Count == 0)
            {
                if (!_hasLoggedMissingConfig)
                {
                    Debug.LogWarning(
                        "[PrologueTurnDeal] 未在 Effect Object Params[0] 配置 TurnDealConfig，能力不会发牌。");
                    _hasLoggedMissingConfig = true;
                }
                return;
            }

            if (!IsHostAlive()) return;

            int idx = _turnIndex;
            if (idx >= _config.turns.Count)
            {
                if (!_config.loop) return; // 不循环：配几回合发几回合，之后停止
                idx %= _config.turns.Count;
            }

            var group = _config.turns[idx];
            _turnIndex++;

            if (group == null || group.deals == null) return;

            int inserted = 0;
            foreach (var deal in group.deals)
            {
                if (deal?.card == null || deal.count <= 0) continue;
                for (int i = 0; i < deal.count && _battle.HandCount < _battle.HandLimit; i++)
                {
                    inserted += _battle.AddGeneratedCards(deal.card, 1, CardZoneType.Hand);
                }
            }

            Debug.Log($"[PrologueTurnDeal] {trigger}（第 {idx + 1} 组）实际塞入 {inserted} 张手牌。");
        }

        /// <summary>
        /// 宿主敌人是否仍存活。敌人能力应随宿主死亡而失效。
        /// </summary>
        private bool IsHostAlive()
        {
            if (_battle == null) return false;
            foreach (EnemyInstance inst in _battle.EnemyInstances)
            {
                if (inst == null || inst.IsDead) continue;
                var abilities = inst.Config?.abilities;
                if (abilities == null) continue;
                foreach (EnemyAbilityEntry ability in abilities)
                {
                    if (ability?.relic == _relic) return true;
                }
            }
            return false;
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
                battle.OnPlayerTurnStarted -= OnPlayerTurnStarted;

            _config = null;
            _battle = null;
            _relic = null;
            _turnIndex = 0;
            _hasLoggedMissingConfig = false;
        }
    }
}
