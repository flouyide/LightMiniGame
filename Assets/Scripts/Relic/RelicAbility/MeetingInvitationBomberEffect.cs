using System.Collections.Generic;
using LightMiniGame.CardEditor;
using LightMiniGame.Relic;
using LightMiniGame.Shop;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 敌人能力：会议邀请轰炸机。
    ///
    /// 每当拥有本能力的敌人完成自身一整次行动且仍存活时，向玩家当前手牌塞入会议邀请牌。
    /// 普通理智状态塞入 effectParams[0] 张（默认 3）；低理智状态塞入 effectParams[1] 张（默认 5）。
    ///
    /// 牌池配置：RelicData.effectObjectParams 中的每一个 CardEntry 都是可随机抽取的会议邀请牌。
    /// 每次塞牌均独立从该池随机选择一张，因此可以配置一张牌重复塞入，或配置多张牌随机混入。
    /// 生成的 CardData 仅为本场临时实例，直接加入当前激活角色的手牌，不修改 CardEntry、EnemyConfig
    /// 或任何 ScriptableObject 配置资产。若手牌已满，只会实际塞入剩余空位可容纳的数量。
    ///
    /// 接入：将“会议邀请轰炸机”RelicData 拖入 EnemyConfig -> abilities；再在其 Effect Object Params
    /// 中依次拖入要塞给玩家的 CardEntry。
    /// </summary>
    public class MeetingInvitationBomberEffect : RelicEffectBase
    {
        public const int DefaultNormalCardCount = 3;
        public const int DefaultLowSanityCardCount = 5;

        private BattleManager _battle;
        private RelicData _relic;
        private int _normalCardCount = DefaultNormalCardCount;
        private int _lowSanityCardCount = DefaultLowSanityCardCount;
        private readonly List<CardEntry> _cardPool = new List<CardEntry>();
        private bool _hasLoggedMissingCardPool;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx?.battle;
            _relic = ctx?.relic;
            if (_battle == null || _relic == null) return;

            _normalCardCount = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(_relic, 0, DefaultNormalCardCount)));
            _lowSanityCardCount = Mathf.Max(0,
                Mathf.RoundToInt(GetEffectParam(_relic, 1, DefaultLowSanityCardCount)));
            BuildCardPool();
            _hasLoggedMissingCardPool = false;

            _battle.OnEnemyTurnCompleted += OnEnemyTurnCompleted;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnEnemyTurnCompleted(EnemyInstance inst)
        {
            if (_battle == null || inst == null || inst.IsDead || !IsHost(inst)) return;

            if (_cardPool.Count == 0)
            {
                if (!_hasLoggedMissingCardPool)
                {
                    Debug.LogWarning(
                        "[MeetingInvitationBomber] 未在 Effect Object Params 配置任何 CardEntry，能力不会塞入卡牌。");
                    _hasLoggedMissingCardPool = true;
                }
                return;
            }

            int requestedCount = _battle.IsLowSanityForFusion ? _lowSanityCardCount : _normalCardCount;
            int insertedCount = 0;
            for (int i = 0; i < requestedCount && _battle.HandCount < _battle.HandLimit; i++)
            {
                CardEntry entry = _cardPool[Random.Range(0, _cardPool.Count)];
                insertedCount += _battle.AddGeneratedCards(entry, 1, CardZoneType.Hand);
            }

            Debug.Log($"[MeetingInvitationBomber] {inst.Name} 完成自身回合，" +
                      $"{(_battle.IsLowSanityForFusion ? "低理智" : "普通")}状态请求塞入 {requestedCount} 张，" +
                      $"实际塞入 {insertedCount} 张会议邀请牌。");
        }

        private void BuildCardPool()
        {
            _cardPool.Clear();
            var objectParams = _relic?.effectObjectParams;
            if (objectParams == null) return;

            foreach (Object param in objectParams)
            {
                if (param is CardEntry entry)
                    _cardPool.Add(entry);
            }
        }

        /// <summary>
        /// 只匹配本效果实例绑定的 RelicData，防止同脚本、不同参数的敌人能力互相触发。
        /// </summary>
        private bool IsHost(EnemyInstance inst)
        {
            var abilities = inst.Config?.abilities;
            if (abilities == null) return false;

            foreach (EnemyAbilityEntry ability in abilities)
            {
                if (ability?.relic == _relic)
                    return true;
            }
            return false;
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
                battle.OnEnemyTurnCompleted -= OnEnemyTurnCompleted;

            _cardPool.Clear();
            _battle = null;
            _relic = null;
            _normalCardCount = DefaultNormalCardCount;
            _lowSanityCardCount = DefaultLowSanityCardCount;
            _hasLoggedMissingCardPool = false;
        }
    }
}
