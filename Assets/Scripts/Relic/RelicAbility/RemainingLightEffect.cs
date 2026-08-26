using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：残存之光。
    ///
    /// 每个玩家回合开始时回复指定点数的共享玩家理智。
    ///
    /// 结算语义：
    ///   - 首回合：BattleManager.StartBattle 不会派发 OnPlayerTurnStarted，故在 OnBattleStart
    ///     完成订阅后立即结算一次；
    ///   - 后续回合：由 BattleManager.OnPlayerTurnStarted 触发；
    ///   - 理智经 BattleManager.ModifySanity 回复，受最大理智上限夹取，并同步低理智状态、背景与 UI。
    ///
    /// 可配置参数（选中“残存之光”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 每回合开始回复的理智，默认 1；小于 0 时按 0 处理。
    /// </summary>
    public class RemainingLightEffect : RelicEffectBase
    {
        public const int DefaultSanityRestore = 1;

        private BattleManager _battle;
        private int _restoreSanity = DefaultSanityRestore;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            Detach(_battle);

            _battle = ctx?.battle;
            if (_battle == null) return;

            _restoreSanity = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultSanityRestore)));
            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;

            // StartBattle 的首回合不会经过 StartPlayerTurn / OnPlayerTurnStarted，
            // 所以在遗物启动完成后补齐首回合结算。
            RestoreSanityAtTurnStart();
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnPlayerTurnStarted() => RestoreSanityAtTurnStart();

        private void RestoreSanityAtTurnStart()
        {
            if (_battle == null || _restoreSanity <= 0) return;

            int previousSanity = _battle.PlayerSanity;
            _battle.ModifySanity(_restoreSanity);
            Debug.Log($"[RemainingLight] 回合开始，理智 +{_restoreSanity}：{previousSanity} → {_battle.PlayerSanity}");
        }

        private void Detach(BattleManager battle)
        {
            BattleManager target = battle ?? _battle;
            if (target != null)
                target.OnPlayerTurnStarted -= OnPlayerTurnStarted;

            _battle = null;
            _restoreSanity = DefaultSanityRestore;
        }
    }
}
