using LightMiniGame.Card;
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：创新证书。
    ///
    /// 遗物持有角色每经历指定数量的自身回合，获得指定点数的行动点。
    ///
    /// 结算语义：
    ///   - 仅当前激活角色是遗物持有者时，才计入“创新证书”的回合次数；
    ///   - 首回合由 BattleManager.StartBattle 直接初始化，不会派发 OnPlayerTurnStarted，
    ///     因此 OnBattleStart 会补记一次首回合，但首回合不会提前获得行动点；
    ///   - 达到第 2、4、6……个持有者回合时，获得额外行动点；
    ///   - 行动点通过 BattleManager.AddActionPoints 累加，遵循既有能量 UI 与卡牌消耗逻辑。
    ///
    /// 可配置参数（选中“创新证书”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 触发间隔（持有者回合数），默认 2；小于 1 时按 1 处理；
    ///   Effect Params [1] = 获得的行动点数量，默认 1；小于 1 时按 1 处理。
    /// </summary>
    public class InnovationCertificateEffect : RelicEffectBase
    {
        public const int DefaultTurnInterval = 2;
        public const int DefaultEnergyGain = 1;

        private BattleManager _battle;
        private CharacterData _owner;
        private int _turnInterval = DefaultTurnInterval;
        private int _energyGain = DefaultEnergyGain;
        private int _ownerTurnCount;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            Detach(_battle);

            _battle = ctx?.battle;
            _owner = ctx?.owner;
            if (_battle == null || _owner == null) return;

            _turnInterval = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultTurnInterval)));
            _energyGain = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 1, DefaultEnergyGain)));
            _ownerTurnCount = 0;

            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;

            // StartBattle 的首回合不会经过 StartPlayerTurn / OnPlayerTurnStarted，
            // 所以补记持有者的首回合；第 2 个持有者回合才首次获得行动点。
            CountOwnerTurnAndGainEnergyIfNeeded();
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnPlayerTurnStarted() => CountOwnerTurnAndGainEnergyIfNeeded();

        private void CountOwnerTurnAndGainEnergyIfNeeded()
        {
            if (_battle == null || !IsOwnerActive()) return;

            _ownerTurnCount++;
            if (_ownerTurnCount % _turnInterval != 0) return;

            _battle.AddActionPoints(_energyGain);
            Debug.Log($"[InnovationCertificate] {_owner.Label} 第 {_ownerTurnCount} 个回合，获得 {_energyGain} 点行动点");
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
            _turnInterval = DefaultTurnInterval;
            _energyGain = DefaultEnergyGain;
            _ownerTurnCount = 0;
        }
    }
}
