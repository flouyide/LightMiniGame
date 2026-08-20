using System.Collections.Generic;
using LightMiniGame.Relic;
using LightMiniGame.Shop;    // RelicData
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 敌人能力：破绽 —— 每回合首次被玩家命中时，受到额外比例伤害。
    ///
    /// 可配置参数（选中 RelicData 资产 → Inspector → Effect Params）：
    ///   [0] = 首次命中额外伤害比例（0.25 即 +25%）。未配置时用默认值 0.25。
    ///
    /// 宿主判定：敌人 Config.abilities 里存在引用本效果 RelicData 的条目即为宿主；
    /// 多个敌人可共享同一能力资产（一个效果实例统一管理全部宿主）。
    ///
    /// 时序：
    ///   - 玩家回合开始（OnPlayerTurnStarted）→ 清空"已消耗"标记，所有宿主恢复首次命中加成；
    ///   - 敌人受伤前（OnEnemyDamageModify，在 BattleManager 伤害倍率结算后、护甲结算前）→
    ///     若该敌人为宿主且本回合未触发过 → 伤害 ×(1+比例)（向上取整）并消耗标记。
    ///     注意：加成在护甲结算前生效，会被护甲吸收（同杀戮尖塔易伤语义，先破甲再享受加成）。
    ///
    /// 接入：在 RelicData 资产（Inspector）把本脚本拖到 Effect Script 字段
    /// （自动填 effectScriptName = "LightMiniGame.RelicEffects.FirstHitVulnerableEffect"），
    /// 再把该 RelicData 拖进 EnemyConfig → 能力 → abilities 的任一条目。
    /// </summary>
    public class FirstHitVulnerableEffect : RelicEffectBase
    {
        /// <summary>首次被命中的额外伤害比例默认值（Effect Params 未配置时使用）。</summary>
        public const float DefaultBonusRatio = 0.25f;

        private BattleManager _battle;

        /// <summary>首次被命中的额外伤害比例（每场战斗从 RelicData.effectParams[0] 读取）。</summary>
        private float _bonusRatio = DefaultBonusRatio;

        /// <summary>本回合已消耗"首次命中"加成的宿主敌人（不在集合中的宿主 = 下次命中可触发）。</summary>
        private readonly HashSet<EnemyInstance> _firstHitConsumed = new HashSet<EnemyInstance>();

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            if (_battle == null) return;

            _bonusRatio = GetEffectParam(ctx.relic, 0, DefaultBonusRatio);   // 参数[0]：额外伤害比例

            _firstHitConsumed.Clear();
            _battle.OnEnemyDamageModify += OnEnemyDamageModify;
            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnPlayerTurnStarted() => _firstHitConsumed.Clear();   // 每回合重置

        private int OnEnemyDamageModify(EnemyInstance inst, int damage)
        {
            if (_battle == null || inst == null || inst.IsDead) return damage;
            if (_firstHitConsumed.Contains(inst)) return damage;   // 本回合已触发过

            if (!IsHost(inst)) return damage;                      // 非宿主：不修正

            _firstHitConsumed.Add(inst);   // 消耗本回合首次命中
            int boosted = Mathf.CeilToInt(damage * (1f + _bonusRatio));
            if (boosted > damage)
                Debug.Log($"[FirstHitVulnerable] {inst.Name} 本回合首次被命中，伤害 {damage} → {boosted}（+{_bonusRatio:P0}）");
            return boosted;
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
            {
                battle.OnEnemyDamageModify -= OnEnemyDamageModify;
                battle.OnPlayerTurnStarted -= OnPlayerTurnStarted;
            }
            _battle = null;
            _firstHitConsumed.Clear();
        }
    }
}
