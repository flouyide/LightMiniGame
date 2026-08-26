using LightMiniGame.Card;
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：冠冕。
    ///
    /// 遗物持有角色成为当前激活角色时，在其每个玩家回合开始获得指定数量的格挡。
    ///
    /// 结算语义：
    ///   - 首回合：BattleManager.StartBattle 不会派发 OnPlayerTurnStarted，故在 OnBattleStart
    ///     完成订阅后立即结算一次，保证初始激活角色持有冠冕时也获得格挡；
    ///   - 后续回合：由 BattleManager.OnPlayerTurnStarted 触发；
    ///   - 归属校验：仅当当前激活角色正是此遗物的持有者时生效；
    ///   - 格挡通过 BattleManager.AddPlayerArmor 累加，遵循既有护甲显示、消耗与回合结束清空规则。
    ///
    /// 可配置参数（选中“冠冕”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 每回合开始获得的格挡数量，默认 3；小于 0 时按 0 处理。
    /// </summary>
    public class CrownEffect : RelicEffectBase
    {
        public const int DefaultBlockAmount = 3;

        private BattleManager _battle;
        private CharacterData _owner;
        private int _blockAmount = DefaultBlockAmount;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            Detach(_battle);

            _battle = ctx?.battle;
            _owner = ctx?.owner;
            if (_battle == null || _owner == null) return;

            _blockAmount = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultBlockAmount)));
            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;

            // StartBattle 的首回合不会经过 StartPlayerTurn / OnPlayerTurnStarted，
            // 所以在遗物启动完成后补齐首回合结算。
            GrantBlockAtTurnStart();
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnPlayerTurnStarted() => GrantBlockAtTurnStart();

        private void GrantBlockAtTurnStart()
        {
            if (_battle == null || _blockAmount <= 0 || !IsOwnerActive()) return;

            _battle.AddPlayerArmor(_blockAmount);
            Debug.Log($"[Crown] {_owner.Label} 回合开始，获得 {_blockAmount} 点格挡（当前 {_battle.PlayerArmor}）");
        }

        private bool IsOwnerActive()
            => _battle != null && _owner != null && _battle.ActiveCharacterData == _owner;

        private void Detach(BattleManager battle)
        {
            BattleManager target = battle ?? _battle;
            if (target != null)
                target.OnPlayerTurnStarted -= OnPlayerTurnStarted;

            _battle = null;
            _owner = null;
            _blockAmount = DefaultBlockAmount;
        }
    }
}
