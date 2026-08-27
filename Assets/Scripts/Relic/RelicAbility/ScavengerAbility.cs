using LightMiniGame.Card;
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 拾荒者初始遗物：从拾荒者切换至其他角色时，增加福报值。
    ///
    /// 触发时机：BattleManager.OnCharacterSwitched 在角色索引已经切换后广播。
    /// 效果会保存切换前持有者是否处于激活状态，只在“持有者激活 -> 其他角色激活”时结算。
    /// 每次满足该角色切换条件都会获得福报；从其他角色切换、切换回持有者及未切换角色的融合均不会获得福报。
    ///
    /// 可配置参数（选中 ScavengerInit 的 RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 每次从拾荒者切换出去时增加的福报值，默认 5；小于 0 时按 0 处理。
    /// </summary>
    public class ScavengerFortuneOnSwitchEffect : RelicEffectBase
    {
        private const int DefaultFortuneGain = 5;

        private BattleManager _battle;
        private CharacterData _scavenger;
        private int _fortuneGain = DefaultFortuneGain;
        private bool _wasScavengerActive;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx?.battle;
            _scavenger = ctx?.owner;
            if (_battle == null || _scavenger == null) return;

            _fortuneGain = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultFortuneGain)));
            _wasScavengerActive = _battle.ActiveCharacterData == _scavenger;

            _battle.OnCharacterSwitched += OnCharacterSwitched;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnCharacterSwitched()
        {
            if (_battle == null || _scavenger == null) return;

            bool isScavengerActive = _battle.ActiveCharacterData == _scavenger;
            bool switchedAwayFromScavenger = _wasScavengerActive && !isScavengerActive;
            _wasScavengerActive = isScavengerActive;

            if (!switchedAwayFromScavenger || _fortuneGain <= 0) return;

            int before = _battle.PlayerFortune;
            _battle.ModifyFortune(_fortuneGain);
            Debug.Log($"[ScavengerFortune] {_scavenger.Label} 切换离场，福报 +{_fortuneGain}：{before} → {_battle.PlayerFortune}");
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
                battle.OnCharacterSwitched -= OnCharacterSwitched;

            _battle = null;
            _scavenger = null;
            _fortuneGain = DefaultFortuneGain;
            _wasScavengerActive = false;
        }
    }
}
