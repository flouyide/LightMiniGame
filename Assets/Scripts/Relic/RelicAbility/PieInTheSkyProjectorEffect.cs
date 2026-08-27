using LightMiniGame.Relic;
using LightMiniGame.Shop;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 敌人能力：画饼投影仪 —— 宿主敌人每完整存活一个自身回合，
    /// 本场战斗的货币赏金额外增加指定数额。
    ///
    /// 初始赏金仍完全来自 PageEventData.lootTable 的 Currency 条目；本能力只把
    /// 运行时增量写入 BattleManager.RuntimeLootCurrencyBonus，绝不修改配置资产。
    ///
    /// 可配置参数（选中 RelicData 资产 → Inspector → Effect Params）：
    ///   [0] = 每个完整存活自身回合追加的金币，四舍五入取整，最小 0；未配置时默认 10。
    ///
    /// 结算时机：BattleManager 在敌人完成自身全部技能、且仍存活后广播
    /// OnEnemyTurnCompleted。因此敌人在同一敌人回合开始阶段因疲惫死亡，或行动中死亡，
    /// 都不会获得本次增长；多个宿主敌人则各自完成一次行动各加一次。
    ///
    /// 接入：将“画饼投影仪”RelicData 拖入 EnemyConfig → abilities。
    /// </summary>
    public class PieInTheSkyProjectorEffect : RelicEffectBase
    {
        /// <summary>每个宿主完整存活一回合时追加的默认货币赏金。</summary>
        public const int DefaultGoldBonusPerSurvivedTurn = 10;

        private BattleManager _battle;
        private RelicData _relic;
        private int _goldBonusPerSurvivedTurn = DefaultGoldBonusPerSurvivedTurn;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            _relic = ctx.relic;
            if (_battle == null || _relic == null) return;

            _goldBonusPerSurvivedTurn = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(_relic, 0, DefaultGoldBonusPerSurvivedTurn)));

            _battle.OnEnemyTurnCompleted += OnEnemyTurnCompleted;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnEnemyTurnCompleted(EnemyInstance inst)
        {
            if (_battle == null || inst == null || inst.IsDead) return;
            if (!IsHost(inst)) return;

            _battle.AddRuntimeLootCurrencyBonus(_goldBonusPerSurvivedTurn);
            Debug.Log($"[PieInTheSkyProjector] {inst.Name} 完成第 {inst.TurnInCycle} 个存活回合，" +
                      $"本场货币赏金 +{_goldBonusPerSurvivedTurn}（动态累计 {_battle.RuntimeLootCurrencyBonus}）");
        }

        /// <summary>
        /// 只匹配本效果实例对应的 RelicData 引用，而非仅比较脚本类型。
        /// 这样同场不同参数的同类能力资产不会互相重复计数。
        /// </summary>
        private bool IsHost(EnemyInstance inst)
        {
            var abilities = inst.Config?.abilities;
            if (abilities == null) return false;

            foreach (var ability in abilities)
            {
                if (ability?.relic == _relic)
                    return true;
            }
            return false;
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
                battle.OnEnemyTurnCompleted -= OnEnemyTurnCompleted;

            _battle = null;
            _relic = null;
            _goldBonusPerSurvivedTurn = DefaultGoldBonusPerSurvivedTurn;
        }
    }
}
