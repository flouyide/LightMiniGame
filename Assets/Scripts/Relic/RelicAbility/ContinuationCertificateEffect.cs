using LightMiniGame.Card;
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：续写证书。
    ///
    /// 遗物持有角色每经历指定数量的自身回合，额外抽取指定数量的牌。
    ///
    /// 结算语义：
    ///   - 仅当前激活角色是遗物持有者时，才计入“续写证书”的回合次数；
    ///   - 首回合由 BattleManager.StartBattle 直接抽牌，不会派发 OnPlayerTurnStarted，
    ///     因此 OnBattleStart 会补记一次首回合，但首回合不会额外抽牌；
    ///   - 达到第 2、4、6……个持有者回合时，于常规抽牌前额外抽牌；
    ///   - 额外抽牌通过 BattleManager.DrawCards 执行，遵循既有手牌上限、牌堆与弃牌堆重洗规则。
    ///
    /// 可配置参数（选中“续写证书”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 触发间隔（持有者回合数），默认 2；小于 1 时按 1 处理；
    ///   Effect Params [1] = 额外抽牌数，默认 1；小于 1 时按 1 处理。
    /// </summary>
    public class ContinuationCertificateEffect : RelicEffectBase
    {
        public const int DefaultTurnInterval = 2;
        public const int DefaultExtraDrawCount = 1;

        private BattleManager _battle;
        private CharacterData _owner;
        private int _turnInterval = DefaultTurnInterval;
        private int _extraDrawCount = DefaultExtraDrawCount;
        private int _ownerTurnCount;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            Detach(_battle);

            _battle = ctx?.battle;
            _owner = ctx?.owner;
            if (_battle == null || _owner == null) return;

            _turnInterval = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultTurnInterval)));
            _extraDrawCount = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 1, DefaultExtraDrawCount)));
            _ownerTurnCount = 0;

            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;

            // StartBattle 的首回合不会经过 StartPlayerTurn / OnPlayerTurnStarted，
            // 所以补记持有者的首回合；第 2 个持有者回合才首次额外抽牌。
            CountOwnerTurnAndDrawIfNeeded();
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnPlayerTurnStarted() => CountOwnerTurnAndDrawIfNeeded();

        private void CountOwnerTurnAndDrawIfNeeded()
        {
            if (_battle == null || !IsOwnerActive()) return;

            _ownerTurnCount++;
            if (_ownerTurnCount % _turnInterval != 0) return;

            _battle.DrawCards(_extraDrawCount);
            Debug.Log($"[ContinuationCertificate] {_owner.Label} 第 {_ownerTurnCount} 个回合，额外抽取 {_extraDrawCount} 张牌");
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
            _extraDrawCount = DefaultExtraDrawCount;
            _ownerTurnCount = 0;
        }
    }
}
