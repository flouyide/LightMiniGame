using System.Collections.Generic;
using LightMiniGame.Relic;
using LightMiniGame.Shop;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 敌人能力：冒名。
    ///
    /// 每名拥有本能力的敌人在战斗开始时获得若干层“冒名”。
    /// 玩家卡牌攻击该敌人时，若其剩余层数大于 0：
    ///   1. 同一张卡的多段伤害仅拦截第一段：反弹该段结算伤害给玩家；
    ///   2. 完整取消敌人的第一段受击，敌人不损失护甲或生命；
    ///   3. 后续同卡伤害正常结算到敌人；
    ///   4. 仅在第一段消耗 1 层“冒名”。
    ///
    /// 参数约定：
    ///   Effect Params[0] = 初始冒名层数，默认 3，向最近整数取整，最小为 0。
    ///
    /// 结算口径：
    ///   - 订阅 BattleManager.OnEnemyDamageIntercept，在敌方护甲结算前反弹并取消整次受击；
    ///   - 敌方护甲无论原本可吸收部分还是全部伤害，均不会变化；
    ///   - 反弹基数为本次已应用攻击倍率和受伤前修正后的常规伤害，加上 armorBreak 直接生命伤害；
    ///   - 反弹不叠加宿主敌人的 damageDealtMultiplier，避免对同一次伤害二次放大；
    ///     但玩家承伤倍率、玩家护甲、受伤触发器和死亡判定仍按标准路径生效。
    ///
    /// 运行时状态仅保存于本效果的字典中；不会修改 EnemyConfig 或 RelicData 资产。
    /// </summary>
    public class ImpostorEffect : RelicEffectBase
    {
        public const int DefaultInitialStacks = 3;

        private BattleManager _battle;
        private RelicData _relic;
        private int _initialStacks = DefaultInitialStacks;
        private readonly Dictionary<EnemyInstance, int> _remainingStacksByHost =
            new Dictionary<EnemyInstance, int>();

        // 每名宿主最后一次已被冒名拦截的玩家卡牌结算令牌。
        // 不记录 CardData 引用，避免同一张卡从弃牌堆再次打出时被误判为同一次出牌；
        // 令牌由 BattleManager 为每次执行新建，跨回合也不会与旧卡牌结算冲突。
        private readonly Dictionary<EnemyInstance, object> _lastInterceptedCardExecutionByHost =
            new Dictionary<EnemyInstance, object>();

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            Detach(_battle);

            _battle = ctx?.battle;
            _relic = ctx?.relic;
            if (_battle == null || _relic == null) return;

            _initialStacks = Mathf.Max(
                0,
                Mathf.RoundToInt(GetEffectParam(_relic, 0, DefaultInitialStacks))
            );

            foreach (EnemyInstance inst in _battle.EnemyInstances)
            {
                if (inst == null || inst.IsDead || !IsHost(inst)) continue;
                _remainingStacksByHost[inst] = _initialStacks;
            }

            _battle.OnEnemyDamageIntercept += OnEnemyDamageIntercept;
            Debug.Log($"[Impostor] 已初始化 {_remainingStacksByHost.Count} 名宿主，每名 {_initialStacks} 层冒名。");
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private bool OnEnemyDamageIntercept(
            EnemyInstance inst,
            int damage,
            int armorBreakDamage
        )
        {
            if (_battle == null || inst == null || !IsHost(inst))
                return false;

            int reflectedDamage = Mathf.Max(0, damage) + Mathf.Max(0, armorBreakDamage);
            if (reflectedDamage <= 0)
                return false;

            if (!_remainingStacksByHost.TryGetValue(inst, out int remainingStacks))
            {
                remainingStacks = _initialStacks;
                _remainingStacksByHost[inst] = remainingStacks;
            }

            if (remainingStacks <= 0)
                return false;

            object cardExecutionToken = _battle.ActivePlayerCardExecutionToken;
            if (cardExecutionToken != null
                && _lastInterceptedCardExecutionByHost.TryGetValue(inst, out object lastExecutionToken)
                && object.ReferenceEquals(lastExecutionToken, cardExecutionToken))
            {
                // 同一张卡的后续伤害段不再消耗冒名，直接继续原始敌人受击结算。
                return false;
            }

            remainingStacks--;
            _remainingStacksByHost[inst] = remainingStacks;
            if (cardExecutionToken != null)
                _lastInterceptedCardExecutionByHost[inst] = cardExecutionToken;

            _battle.DealReflectedDamageToPlayer(reflectedDamage);

            Debug.Log(
                $"[Impostor] {inst.Name} 反弹 {reflectedDamage} 点攻击伤害并免疫本次受击，" +
                $"剩余冒名层数 {remainingStacks}。"
            );
            return true;
        }

        private bool IsHost(EnemyInstance inst)
        {
            if (inst?.Config?.abilities == null) return false;

            foreach (EnemyAbilityEntry ability in inst.Config.abilities)
            {
                if (ability?.relic == _relic)
                    return true;
            }

            return false;
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
                battle.OnEnemyDamageIntercept -= OnEnemyDamageIntercept;

            _remainingStacksByHost.Clear();
            _lastInterceptedCardExecutionByHost.Clear();
            _battle = null;
            _relic = null;
            _initialStacks = DefaultInitialStacks;
        }
    }
}
