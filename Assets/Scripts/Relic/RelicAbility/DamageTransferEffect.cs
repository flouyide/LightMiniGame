using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 敌人能力：伤害共担 —— 宿主敌人受到的伤害按比例转移到另一名敌人身上。
    ///
    /// 可配置参数（选中 RelicData 资产 → Inspector）：
    ///   Effect Params        [0] = 转移比例（0.5 即 50%）。未配置时用默认值 0.5。
    ///   Effect Object Params  [0] = 转移目标的 EnemyConfig 资产（直接拖入，避免拼字符串出错）。
    ///
    /// 宿主判定：敌人 Config.abilities 里存在引用本效果 RelicData 的条目即为宿主。
    ///
    /// 结算语义：
    ///   - 转移在伤害倍率结算后、护甲结算前拆分：转移份额 = 向下取整(伤害×比例)，宿主保留剩余部分；
    ///   - 转移份额作为一次独立攻击结算（DealDamageToEnemy），目标会走自己的受击倍率、护甲、
    ///     其他能力钩子（如目标有破绽会吃掉它的首次命中加成）、死亡事件等完整链路；
    ///   - 目标死亡、配置为空或该 EnemyConfig 不在场上时本能力不生效，宿主正常承受全额伤害；
    ///   - 内置防递归保护：A→B 转移过程中，B 身上的共担能力不会把伤害再转回 A（避免无限循环）。
    ///
    /// 接入：在 RelicData 资产把本脚本拖到 Effect Script 字段
    /// （自动填 effectScriptName = "LightMiniGame.RelicEffects.DamageTransferEffect"），
    /// 配置 Effect Params[0]=0.5、Effect Object Params[0]=目标 EnemyConfig 资产，
    /// 再把该 RelicData 拖进宿主敌人的 EnemyConfig → 能力 → abilities。
    /// </summary>
    public class DamageTransferEffect : RelicEffectBase
    {
        /// <summary>转移比例默认值（Effect Params 未配置时使用）。</summary>
        public const float DefaultTransferRatio = 0.5f;

        private BattleManager _battle;

        /// <summary>转移比例（每场战斗从 RelicData.effectParams[0] 读取）。</summary>
        private float _transferRatio = DefaultTransferRatio;

        /// <summary>转移目标的 EnemyConfig 资产引用（每场战斗从 RelicData.effectObjectParams[0] 读取）。</summary>
        private EnemyConfig _targetConfig;

        /// <summary>防递归标记：转移结算期间屏蔽本效果的再次介入（A→B→A 死循环保护）。</summary>
        private bool _transferring;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            if (_battle == null) return;

            _transferRatio = GetEffectParam(ctx.relic, 0, DefaultTransferRatio);          // 参数[0]：转移比例
            _targetConfig = GetEffectObjectParam<EnemyConfig>(ctx.relic, 0);                // 对象参数[0]：目标 EnemyConfig
            _transferring = false;

            if (_targetConfig == null)
                Debug.LogWarning("[DamageTransfer] 未配置目标 EnemyConfig（Effect Object Params[0]），能力将不会生效");

            _battle.OnEnemyDamageModify += OnEnemyDamageModify;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private int OnEnemyDamageModify(EnemyInstance inst, int damage)
        {
            if (_battle == null || inst == null || inst.IsDead) return damage;
            if (_transferring) return damage;        // 转移结算中：不再介入，防递归
            if (_targetConfig == null) return damage;

            if (!IsHost(inst)) return damage;        // 非宿主：不转移

            var target = FindTarget(inst);
            if (target == null) return damage;       // 目标不在场上或已死亡：宿主承受全额

            int transfer = Mathf.FloorToInt(damage * _transferRatio);
            if (transfer <= 0 || transfer >= damage) return damage;
            int hostKeeps = damage - transfer;

            // 转移份额作为独立攻击结算（会走目标的倍率/护甲/能力钩子/死亡事件）
            _transferring = true;
            try
            {
                _battle.DealDamageToEnemy(target.SlotIndex, transfer, false);
            }
            finally
            {
                _transferring = false;
            }

            Debug.Log($"[DamageTransfer] {inst.Name} 受到 {damage} 伤害，转移 {transfer} → {target.Name}，自身保留 {hostKeeps}");
            return hostKeeps;
        }

        /// <summary>在场上的存活敌人中找 Config 等于目标的实例（排除自身，取首个匹配）。</summary>
        private EnemyInstance FindTarget(EnemyInstance self)
            => _battle.FindAliveEnemyByConfig(_targetConfig, self.SlotIndex);

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
                battle.OnEnemyDamageModify -= OnEnemyDamageModify;
            _battle = null;
            _transferring = false;
        }
    }
}