using LightMiniGame.Card;    // CharacterData
using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 牧师专属遗物效果：牧师为当前激活角色时，玩家回合结束后回复 1 点生命值。
    ///
    /// 规则：
    ///   - 每次玩家回合结束（OnPlayerTurnEnded）判定：牧师（ctx.owner）为当前激活角色才生效。
    ///   - 生效时回复 HealPerTurnEnd（默认 1）点 HP，由 BattleManager.HealPlayer 夹取到生命上限，
    ///     满血时不溢出；牧师在后台（另一角色激活）时不回复。
    ///
    /// UI 时序说明：EndPlayerTurn 触发本事件后不再调 UpdateUI，
    /// 回复后的数字会在下一次 UpdateUI（如下个玩家回合开始 / 敌人造成伤害）时刷新到界面。
    ///
    /// 接入：在 RelicData 资产（Inspector）把本脚本拖到 Effect Script 字段，
    /// 编辑器自动填 effectScriptName = "LightMiniGame.RelicEffects.PriestTurnEndHealEffect"；
    /// 或手填该全名。获得遗物时 RelicEffectManager 反射实例化，
    /// 战斗开始订阅回合结束事件，战斗结束/遗物丢失时退订。
    /// </summary>
    public class PriestAbilityEffect : RelicEffectBase
    {
        /// <summary>每次玩家回合结束的回复量。</summary>
        private const int HealPerTurnEnd = 1;

        private BattleManager _battle;
        private CharacterData _priest;     // 牧师角色数据（= ctx.owner）

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            _priest = ctx.owner;
            if (_battle == null || _priest == null) return;

            _battle.OnPlayerTurnEnded += OnPlayerTurnEnded;
            Debug.Log($"[PriestTurnEndHeal] 已装备 {ctx.relic?.relicName}（归属 {ctx.owner?.name}），" +
                      $"牧师激活的回合结束将回复 {HealPerTurnEnd} 点生命");
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx.battle);

        /// <summary>牧师是否为当前激活角色。</summary>
        private bool IsPriestActive()
            => _battle != null && _battle.ActiveCharacterData == _priest;

        private void OnPlayerTurnEnded()
        {
            if (_battle == null) return;
            if (!IsPriestActive()) return;   // 牧师在后台：本回合不回复

            int before = _battle.PlayerHP;
            _battle.HealPlayer(HealPerTurnEnd);
            int healed = _battle.PlayerHP - before;   // 满血时为 0，不刷无意义日志

            if (healed > 0)
                Debug.Log($"[PriestTurnEndHeal] 回合结束，回复 {healed} 点生命（{before} → {_battle.PlayerHP}）");
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
                battle.OnPlayerTurnEnded -= OnPlayerTurnEnded;
            _battle = null;
        }
    }
}
