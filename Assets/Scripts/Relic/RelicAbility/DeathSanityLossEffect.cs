using System.Collections.Generic;
using LightMiniGame.Relic;
using LightMiniGame.Shop;    // RelicData
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 敌人能力：临终侵蚀 —— 当该敌人死亡时，玩家理智降低。
    ///
    /// 可配置参数（选中 RelicData 资产 → Inspector → Effect Params）：
    ///   [0] = 死亡时扣除的玩家理智（四舍五入取整，最小 0）。未配置时用默认值 1。
    ///
    /// 宿主判定：敌人 Config.abilities 里存在引用本效果 RelicData 的条目即为宿主；
    /// 多个敌人共享同一能力资产（每死一个触发一次，互不干扰）。
    ///
    /// 时序：宿主敌人被标记死亡（HandleEnemyFatalDamage 内 OnEnemyDied 事件）时立即扣理智。
    /// 扣理智经由 BattleManager.ModifySanity，会联动：
    ///   - 低理智阈值判定（敌人阶段切换 / 黑暗模式 / 背景切换）
    ///   - lowSanityVolume 干扰特效开关
    /// 击杀最后一个敌人同样生效（理智变化随战后 ApplyBattleResult 写回局外）。
    ///
    /// 接入：在 RelicData 资产（Inspector）把本脚本拖到 Effect Script 字段
    /// （自动填 effectScriptName = "LightMiniGame.RelicEffects.DeathSanityLossEffect"），
    /// 再把该 RelicData 拖进 EnemyConfig → 能力 → abilities 的任一条目。
    /// </summary>
    public class DeathSanityLossEffect : RelicEffectBase
    {
        /// <summary>宿主死亡时扣除的玩家理智默认值（Effect Params 未配置时使用）。</summary>
        public const int DefaultSanityLoss = 1;

        private BattleManager _battle;

        /// <summary>宿主死亡时扣除的玩家理智（每场战斗从 RelicData.effectParams[0] 读取）。</summary>
        private int _sanityLoss = DefaultSanityLoss;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            if (_battle == null) return;

            // 参数[0]：死亡扣除理智（四舍五入，最小 0 防止误配成加理智）
            _sanityLoss = Mathf.Max(0, Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultSanityLoss)));

            _battle.OnEnemyDied += OnEnemyDied;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnEnemyDied(EnemyInstance inst)
        {
            if (_battle == null || inst == null) return;
            if (!IsHost(inst)) return;   // 非宿主死亡不触发

            _battle.ModifySanity(-_sanityLoss);
            Debug.Log($"[DeathSanityLoss] {inst.Name} 死亡，玩家理智 -{_sanityLoss}" +
                      $"（当前 {_battle.PlayerSanity}）");
        }

        /// <summary>该敌人的能力表是否引用了本效果类（effectScriptName 与本类全名一致）。</summary>
        private bool IsHost(EnemyInstance inst)
        {
            var abilities = inst.Config?.abilities;
            if (abilities == null) return false;

            string myType = GetType().FullName;
            foreach (var ab in abilities)
            {
                var relic = ab?.relic;
                if (relic != null && relic.effectScriptName == myType)
                    return true;
            }
            return false;
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
                battle.OnEnemyDied -= OnEnemyDied;
            _battle = null;
        }
    }
}
