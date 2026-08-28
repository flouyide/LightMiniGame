using System.Collections.Generic;
using LightMiniGame.CardEditor;
using LightMiniGame.Relic;
using LightMiniGame.Shop;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 敌人能力：抄袭专家。
    ///
    /// 每个玩家回合，拥有本能力的敌人会记录玩家实际打出的前两张 CardData。
    /// 原卡效果结算完成后，能力会深拷贝该卡当前形态的完整 EffectNode 列表（包含运行时附加效果），
    /// 并将每个节点及其子节点改写为“Enemy(EffectSource) -> Character(CurrentCharacter)”。
    /// 敌人回合中，宿主敌人的常规出牌池会被这两张战斗内 CardEntry 副本临时替换，
    /// 因而意图与实际执行都会严格使用玩家本回合前两张牌的效果。
    ///
    /// 复制牌只保存在 EnemyInstance 的运行时覆盖池，不会写入 EnemyConfig.phase1Skills /
    /// phase2Skills，也不会修改 CardData、CardEntry 或 EffectNode 的原始配置资产。
    /// 当前玩家处于低理智时写入 phase2 对应运行时池，否则写入 phase1 对应运行时池。
    /// </summary>
    public class CopycatExpertEffect : RelicEffectBase
    {
        private const int CardsToCopyPerPlayerTurn = 2;

        private BattleManager _battle;
        private RelicData _relic;
        private readonly List<CardData> _recordedCards = new List<CardData>(CardsToCopyPerPlayerTurn);
        private readonly Dictionary<EnemyInstance, List<CardEntry>> _runtimeCopiesByHost =
            new Dictionary<EnemyInstance, List<CardEntry>>();

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx.battle;
            _relic = ctx.relic;
            if (_battle == null || _relic == null) return;

            _recordedCards.Clear();
            ClearAllRuntimeCopies();
            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;
            _battle.OnPlayerCardPlayed += OnPlayerCardPlayed;
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        /// <summary>
        /// 每次玩家回合开始都移除上回合复制牌；本场战斗的首个玩家回合不会广播
        /// OnPlayerTurnStarted，因此 OnBattleStart 已同步完成首次清理。
        /// </summary>
        private void OnPlayerTurnStarted()
        {
            _recordedCards.Clear();
            ClearAllRuntimeCopies();
        }

        private void OnPlayerCardPlayed(CardData card)
        {
            if (_battle == null || card == null || _recordedCards.Count >= CardsToCopyPerPlayerTurn)
                return;

            List<EffectNode> nodes = card.GetEffectNodes(card.isLowSanityForm);
            if (nodes == null || nodes.Count == 0)
            {
                Debug.LogWarning($"[CopycatExpert] 无法复制 {card.cardName}：该 CardData 未提供 EffectNode 效果。");
                return;
            }

            _recordedCards.Add(card);
            foreach (EnemyInstance host in _battle.EnemyInstances)
            {
                if (host == null || host.IsDead || !IsHost(host)) continue;
                ReplaceHostRuntimeCopies(host);
            }
        }

        /// <summary>
        /// 对单个宿主重建完整复制池。每个敌人拥有独立的 CardEntry 与 EffectNode 副本，
        /// 避免多敌人共享同一运行时 ScriptableObject 后出现串改或销毁干扰。
        /// </summary>
        private void ReplaceHostRuntimeCopies(EnemyInstance host)
        {
            DestroyHostRuntimeCopies(host);

            var copies = new List<CardEntry>(_recordedCards.Count);
            foreach (CardData card in _recordedCards)
            {
                CardEntry copy = CreateEnemySkillCopy(card);
                if (copy != null) copies.Add(copy);
            }

            if (copies.Count == 0)
            {
                host.ClearRuntimeCopiedSkills();
                _battle.RefreshEnemyIntent(host);
                return;
            }

            _runtimeCopiesByHost[host] = copies;
            host.ClearRuntimeCopiedSkills();
            host.SetRuntimeCopiedSkills(_battle.IsLowSanityForFusion, copies);
            _battle.RefreshEnemyIntent(host);

            Debug.Log($"[CopycatExpert] {host.Name} 记录了 {copies.Count} 张玩家牌，已刷新敌人意图。");
        }

        /// <summary>
        /// 创建只在当前战斗存活的敌方技能 CardEntry。
        /// normalEffectNodes 与 lowSanityEffectNodes 均使用当前玩家实际打出形态的独立副本，
        /// 使敌人进入/退出低理智状态后仍会执行本次已记录的同一张效果牌。
        /// </summary>
        private static CardEntry CreateEnemySkillCopy(CardData source)
        {
            if (source == null) return null;

            List<EffectNode> sourceNodes = source.GetEffectNodes(source.isLowSanityForm);
            if (sourceNodes == null || sourceNodes.Count == 0) return null;

            CardEntry origin = source.sourceEntry;
            var copy = ScriptableObject.CreateInstance<CardEntry>();
            copy.name = $"CopycatRuntime_{source.cardName}";
            copy.cardId = $"copycat_runtime_{source.GetInstanceID()}";
            copy.cardName = $"抄袭·{source.cardName}";
            copy.cardArt = source.cardArt;
            copy.descBoxSprite = source.descBoxSprite;
            copy.typeBoxSprite = source.typeBoxSprite;
            // CardData 与 CardEntry 分别使用全局和 CardEditor 命名空间的 CardGrade，
            // 两者枚举值均为 Bronze=0、Silver=1、Gold=2，按底层数值显式转换。
            copy.grade = (LightMiniGame.CardEditor.CardGrade)(int)source.grade;
            // 同理，CardType 也存在运行时与 CardEditor 两套枚举；值序一致时显式转换。
            copy.cardType = origin != null
                ? origin.cardType
                : (LightMiniGame.CardEditor.CardType)(int)source.cardType;
            copy.normalCost = 0;
            copy.lowSanityCost = 0;
            copy.hasLowSanityForm = true;
            // 复制牌仍保留描述数据供效果检查与调试，但敌人意图/出牌卡面不展示 DescText。
            copy.hideDescriptionText = true;
            copy.normalEffectNodes = CloneAsEnemyEffects(sourceNodes);
            copy.lowSanityEffectNodes = CloneAsEnemyEffects(sourceNodes);

            // 运行时 CardEntry 不会序列化到资产。这里在目标已改写后固化两套描述，
            // 避免卡面只依赖空文本的自动回退路径而出现 DescText 为空；生成文案也会
            // 使用敌方目标（当前角色），与实际执行的 EffectNode 保持一致。
            copy.normalDescription = copy.AutoGenerateDescription(false);
            copy.lowSanityDescription = copy.AutoGenerateDescription(true);
            return copy;
        }

        private static List<EffectNode> CloneAsEnemyEffects(List<EffectNode> sourceNodes)
        {
            var result = new List<EffectNode>(sourceNodes?.Count ?? 0);
            if (sourceNodes == null) return result;

            foreach (EffectNode sourceNode in sourceNodes)
            {
                if (sourceNode == null) continue;
                EffectNode copy = sourceNode.Clone();
                RewriteTargetsForEnemy(copy);
                result.Add(copy);
            }
            return result;
        }

        /// <summary>递归改写每个效果节点，保证嵌套 childEffects 不会保留玩家阵营目标。</summary>
        private static void RewriteTargetsForEnemy(EffectNode node)
        {
            if (node == null) return;

            node.source ??= new TargetSelector();
            node.target ??= new TargetSelector();
            node.source.category = TargetCategory.Enemy;
            node.source.unitTarget = CombatUnitTarget.EffectSource;
            node.target.category = TargetCategory.Character;
            node.target.unitTarget = CombatUnitTarget.CurrentCharacter;

            if (node.childEffects == null) return;
            foreach (EffectNode child in node.childEffects)
                RewriteTargetsForEnemy(child);
        }

        private bool IsHost(EnemyInstance inst)
        {
            if (inst?.Config?.abilities == null) return false;
            foreach (EnemyAbilityEntry ability in inst.Config.abilities)
            {
                if (ability?.relic == _relic)
                    return true;
            }
            return false;
        }

        private void ClearAllRuntimeCopies()
        {
            foreach (KeyValuePair<EnemyInstance, List<CardEntry>> pair in _runtimeCopiesByHost)
            {
                pair.Key?.ClearRuntimeCopiedSkills();
                DestroyCopies(pair.Value);
            }
            _runtimeCopiesByHost.Clear();
        }

        private void DestroyHostRuntimeCopies(EnemyInstance host)
        {
            if (host == null) return;

            host.ClearRuntimeCopiedSkills();
            if (!_runtimeCopiesByHost.TryGetValue(host, out List<CardEntry> oldCopies)) return;

            DestroyCopies(oldCopies);
            _runtimeCopiesByHost.Remove(host);
        }

        private static void DestroyCopies(List<CardEntry> copies)
        {
            if (copies == null) return;
            foreach (CardEntry copy in copies)
            {
                if (copy != null)
                    Object.Destroy(copy);
            }
        }

        private void Detach(BattleManager battle)
        {
            if (battle != null)
            {
                battle.OnPlayerTurnStarted -= OnPlayerTurnStarted;
                battle.OnPlayerCardPlayed -= OnPlayerCardPlayed;
            }

            ClearAllRuntimeCopies();
            _recordedCards.Clear();
            _battle = null;
            _relic = null;
        }
    }
}
