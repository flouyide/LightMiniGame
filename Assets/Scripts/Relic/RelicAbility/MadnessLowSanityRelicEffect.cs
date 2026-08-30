using LightMiniGame.Card;
using LightMiniGame.Relic;
using LightMiniGame.Shop;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 遗物：低理智惩罚·疯狂。
    ///
    /// 玩家回合结束时，若当前激活角色正处于低理智状态（_playerSanity &lt;= 阈值，
    /// 与敌人阶段切换同口径），则该角色获得 3 层「疯狂」减益。
    ///
    /// 归属校验：遗物库按角色隔离，同一件遗物若两个角色都装备，会各自实例化一份效果
    /// （key = 角色名_遗物名），NotifyBattleStart 会对全部实例调用 OnBattleStart。
    /// 因此必须校验「当前激活角色正是本实例的持有者」，否则两个角色会同时结算、层数翻倍。
    ///
    /// 「疯狂」本身的效果——大回合结束按 层数×(理智上限-当前理智) 扣除生命，然后层数 -1——
    /// 由 BattleManager.TickMadness() 统一结算。
    ///
    /// 接入：把本 RelicData 设为角色的初始遗物（加入 GlobalRelicInventory 或 CharacterData 的起始遗物列表），
    ///       Effect Script 拖入 MadnessLowSanityRelicEffect.cs，无需额外参数。
    /// </summary>
    public class MadnessLowSanityRelicEffect : RelicEffectBase
    {
        public const int DefaultMadnessStacks = 2;

        private BattleManager _battle;
        private CharacterData _owner;
        private int _stacks = DefaultMadnessStacks;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            Detach(_battle);

            _battle = ctx?.battle;
            _owner = ctx?.owner;
            if (_battle == null || _owner == null) return;

            _stacks = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultMadnessStacks)));

            // 玩家回合结束事件每回合都会派发（含首回合玩家手动结束回合时），
            // 因此无需像 OnPlayerTurnStarted 那样额外补一次首回合结算。
            _battle.OnPlayerTurnEnded += OnPlayerTurnEnded;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnPlayerTurnEnded() => GrantMadnessOnLowSanity();

        private void GrantMadnessOnLowSanity()
        {
            if (_battle == null || _stacks <= 0 || !IsOwnerActive()) return;
            if (!_battle.IsLowSanityForFusion) return;

            _battle.AddPlayerBuff(BuffAttributeType.Madness, _stacks, 0);
            Debug.Log($"[MadnessRelic] {_owner.Label} 回合结束处于低理智，获得 {_stacks} 层疯狂");
        }

        // 只有当前激活角色正是本遗物持有者时才生效（角色可中途切换，故在事件触发时判定）。
        private bool IsOwnerActive()
            => _battle != null && _owner != null && _battle.ActiveCharacterData == _owner;

        private void Detach(BattleManager battle)
        {
            BattleManager target = battle ?? _battle;
            if (target != null)
                target.OnPlayerTurnEnded -= OnPlayerTurnEnded;

            _battle = null;
            _owner = null;
            _stacks = DefaultMadnessStacks;
        }
    }
}