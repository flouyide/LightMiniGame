using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：速效救心丸。
    ///
    /// 每场战斗首次进入低理智状态时，回复 N 点理智（默认 2）。
    ///
    /// 结算语义：
    ///   - 触发点：玩家理智从阈值以上降至阈值（含）以下、进入低理智状态时，由
    ///     BattleManager.OnPlayerEnteredLowSanity 在阶段切换判定之前广播；
    ///   - 整场战斗仅生效一次：首次触发后置位标志，理智恢复后再次进入低理智不再回复；
    ///   - 回复发生在阶段切换判定之前，因此小幅理智下降（回复后仍高于阈值）可被“救回”，
    ///     避免进入低理智/黑暗阶段；大幅下降（回复后仍 ≤ 阈值）则照常进入低理智；
    ///   - 回复量受理智上限封顶（ModifySanity 内部 Clamp）。
    ///
    /// 可配置参数（选中“速效救心丸”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 回复的理智点数，默认 2；小于 1 时按 1 处理。
    ///
    /// 依赖 BattleManager.OnPlayerEnteredLowSanity（进入低理智事件）与 ModifySanity。
    /// </summary>
    public class QuickReliefPillEffect2 : RelicEffectBase
    {
        public const int DefaultRestoreSanity = 2;

        private BattleManager _battle;
        private int _restoreSanity = DefaultRestoreSanity;
        private bool _usedThisBattle;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            if (_battle == null) return;

            _restoreSanity = Mathf.Max(1,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultRestoreSanity)));
            _usedThisBattle = false;

            _battle.OnPlayerEnteredLowSanity += OnPlayerEnteredLowSanity;
            Debug.Log($"[QuickReliefPill] 已装备 {ctx.relic?.relicName}，首次进入低理智时将回复 {_restoreSanity} 点理智");
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        /// <summary>进入低理智状态：整场首次触发时回复理智。</summary>
        private void OnPlayerEnteredLowSanity()
        {
            if (_battle == null || _usedThisBattle) return;
            _usedThisBattle = true;
            _battle.ModifySanity(_restoreSanity);
            Debug.Log($"[QuickReliefPill] 首次进入低理智，回复 {_restoreSanity} 点理智");
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
                battle.OnPlayerEnteredLowSanity -= OnPlayerEnteredLowSanity;
            _battle = null;
            _usedThisBattle = false;
        }
    }
}
