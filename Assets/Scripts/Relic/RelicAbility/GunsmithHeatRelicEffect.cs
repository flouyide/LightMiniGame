using LightMiniGame.Card;        // CharacterData
using LightMiniGame.CardEditor; // TriggerEvent
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 枪械师初始遗物：热度系统（热度逻辑完全由此遗物拥有，BattleManager 不再内置热度）。
    ///
    /// 规则（按"枪械师是否为当前激活角色"区分）：
    ///   激活时：
    ///     - 每打出 1 张攻击牌 +3 热度（HeatGainPerAttackCard）。
    ///     - 热度 ≥ 25（OverheatThreshold）时过载：枪械师手牌费用 +1（不影响其他角色）。
    ///     - 每次玩家回合结束时热度 -1（NormalHeatDecayPerTurn）。
    ///   未激活时：
    ///     - 不加热、不过载。
    ///     - 每次玩家回合结束时热度 -6（SwitchedHeatDecayPerTurn）。
    ///
    /// 数据存储：以 BattleManager._customData["Heat"] 为唯一真相源（卡牌效果 IncreaseHeatEffect /
    /// HeatAboveThresholdCondition / EffectExecutorV2 均读写此键，保持兼容）。
    ///
    /// 接入：在 RelicData 资产把本脚本拖到 Effect Script 字段（自动填 effectScriptName），
    /// 或手填全名 "LightMiniGame.RelicEffects.GunsmithHeatRelicEffect"。
    /// </summary>
    public class GunsmithHeatRelicEffect : RelicEffectBase
    {
        private BattleManager _battle;
        private CharacterData _gunsmith;        // 枪械师角色数据（= ctx.owner）
        private int _heatGainedThisTurn;
        private int _heatLostThisTurn;
        private bool _overloadedThisTurn;       // 本回合是否已触发过过热事件
        private bool _isOverloaded;             // 当前是否处于过载（费用+1）状态

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            _gunsmith = ctx.owner;
            if (_battle == null || _gunsmith == null) return;

            _heatGainedThisTurn = 0;
            _heatLostThisTurn = 0;
            _overloadedThisTurn = false;
            _isOverloaded = false;

            // 重置热度真相源（会经 SetCustomData 触发 OnHeatChanged，但此时还未订阅，无副作用）
            _battle.SetCustomData("Heat", 0);
            _battle.SetHandCostBonus(0);

            // 订阅热度变化（加热/衰减都经此重算过载）与时机事件
            _battle.OnHeatChanged += OnHeatChanged;
            _battle.OnAttackCardPlayed += OnAttackCardPlayed;
            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;
            _battle.OnPlayerTurnEnded += OnPlayerTurnEnded;
            _battle.OnCharacterSwitched += OnCharacterSwitched;

            Log("战斗开始，热度重置为 0");
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx.battle);

        private void Detach(BattleManager battle)
        {
            if (battle != null)
            {
                battle.OnHeatChanged -= OnHeatChanged;
                battle.OnAttackCardPlayed -= OnAttackCardPlayed;
                battle.OnPlayerTurnStarted -= OnPlayerTurnStarted;
                battle.OnPlayerTurnEnded -= OnPlayerTurnEnded;
                battle.OnCharacterSwitched -= OnCharacterSwitched;
                battle.SetHandCostBonus(0);
            }
            _battle = null;
        }

        // ===== 判定 =====
        /// <summary>枪械师是否为当前激活角色。</summary>
        private bool IsGunsmithActive()
            => _battle != null && _gunsmith != null && _battle.ActiveCharacterData == _gunsmith;

        // ===== 时机事件 =====
        private void OnAttackCardPlayed()
        {
            // 仅当枪械师激活时，攻击牌才加热
            if (!IsGunsmithActive()) return;

            int gain = _battle.HeatGainPerAttackCard;
            _heatGainedThisTurn += gain;
            _battle.SetTurnCounter("HeatGained", _heatGainedThisTurn);
            _battle.ModifyCustomData("Heat", gain);   // 加热度（会触发 OnHeatChanged → 重算过载）
            _battle.FireTrigger(TriggerEvent.OnHeatGained);
            Log($"打出攻击牌 +{gain}");
        }

        private void OnCharacterSwitched()
        {
            // 切换角色后，枪械师激活状态可能改变，需重算过载费用加成
            // （切换瞬间 _hand 已清空、新牌尚未抽，SetHandCostBonus 会作用于随后 DrawCards 的新牌）
            ReevaluateOverload();
        }

        private void OnPlayerTurnStarted()
        {
            _heatGainedThisTurn = 0;
            _heatLostThisTurn = 0;
            _overloadedThisTurn = false;
        }

        private void OnPlayerTurnEnded()
        {
            // 衰减：枪械师激活 → -1；未激活 → -6
            int decay = IsGunsmithActive() ? _battle.NormalHeatDecayPerTurn : _battle.SwitchedHeatDecayPerTurn;
            int current = _battle.GetCustomData("Heat");
            if (current > 0 && decay > 0)
            {
                int actual = Mathf.Min(current, decay);
                _heatLostThisTurn += actual;
                _battle.SetTurnCounter("HeatLost", _heatLostThisTurn);
                _battle.ModifyCustomData("Heat", -actual);  // 减热度（会触发 OnHeatChanged → 重算过载）
                _battle.FireTrigger(TriggerEvent.OnHeatReduced);
                Log($"回合结束衰减 -{actual}");
            }
            // 回合末过热事件判定
            CheckOverload();
        }

        // ===== 过载驱动 =====
        private void OnHeatChanged(int heat)
        {
            // 热度变化时重算过载（仅枪械师激活才施加费用加成）
            ReevaluateOverload();
        }

        /// <summary>
        /// 重算过载状态：枪械师激活 且 热度 ≥ 阈值 → 手牌费用 +1；否则取消。
        /// 因为 _hand 始终是当前激活角色的手牌，枪械师未激活时 _hand 是其他角色的牌，不应加费用。
        /// </summary>
        private void ReevaluateOverload()
        {
            int heat = _battle.GetCustomData("Heat");
            int threshold = _battle.OverheatThreshold;
            bool should = IsGunsmithActive() && heat >= threshold;

            if (should && !_isOverloaded)
            {
                _isOverloaded = true;
                _battle.SetHandCostBonus(1);
                Log($"过载！热度 {heat} ≥ {threshold}，枪械师手牌费用 +1");
            }
            else if (!should && _isOverloaded)
            {
                _isOverloaded = false;
                _battle.SetHandCostBonus(0);
                Log($"取消费用加成（热度 {heat}，枪械师激活={IsGunsmithActive()}）");
            }
        }

        private void CheckOverload()
        {
            if (_overloadedThisTurn) return;
            int heat = _battle.GetCustomData("Heat");
            if (IsGunsmithActive() && heat >= _battle.OverheatThreshold)
            {
                _overloadedThisTurn = true;
                _battle.FireTrigger(TriggerEvent.OnOverload);
                Log($"过热触发（阈值 {_battle.OverheatThreshold}）");
            }
        }

        private void Log(string msg) => Debug.Log($"[GunsmithHeat] {msg}（热度={_battle?.GetCustomData("Heat") ?? 0}，激活={IsGunsmithActive()}）");
    }
}
