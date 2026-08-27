using System;
using System.Collections.Generic;
using LightMiniGame.CardEditor;
using LightMiniGame.Relic;
using LightMiniGame.Shop;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 敌人能力：站队天平。
    ///
    /// 每个玩家回合开始时，为每个拥有此能力且存活的敌人，从其 EnemyConfig 的原始技能池随机抽取两张候选：
    /// 普通状态读取 phase1Skills，低理智状态读取 phase2Skills。候选展示在“站队天平选择界面”中，
    /// 玩家选中其中一张后，该敌人本回合的意图与实际行动都只使用这一张牌。
    ///
    /// 选择结果仅存入 EnemyInstance 的本回合强制技能层，不会修改 EnemyConfig 资产，
    /// 也不会覆盖抄袭专家的运行时复制牌池。下一个玩家回合开始时强制层清除。
    /// effectParams[0] 为候选数，默认且最大为 2（当前 UI 固定为两张卡槽）。
    /// </summary>
    public class TeamBalanceEffect : RelicEffectBase
    {
        private const int DefaultCandidateCount = 2;
        private const string InputLockKey = "TeamBalanceEffect";

        private sealed class ChoiceRequest
        {
            public EnemyInstance host;
            public List<CardEntry> candidates;
        }

        private BattleManager _battle;
        private RelicData _relic;
        private int _candidateCount = DefaultCandidateCount;
        private readonly Queue<ChoiceRequest> _pendingChoices = new Queue<ChoiceRequest>();
        private bool _isDetached;

        public override void OnBattleStart(RelicEffectContext ctx)
        {
            _battle = ctx?.battle;
            _relic = ctx?.relic;
            if (_battle == null || _relic == null) return;

            _candidateCount = Mathf.Clamp(
                Mathf.RoundToInt(GetEffectParam(_relic, 0, DefaultCandidateCount)),
                1,
                DefaultCandidateCount);
            _isDetached = false;
            _battle.OnPlayerTurnStarted += OnPlayerTurnStarted;

            // StartBattle 不会广播 OnPlayerTurnStarted；在此补齐首个玩家回合的选择。
            PrepareChoicesForPlayerTurn();
        }

        public override void OnBattleEnd(RelicEffectContext ctx, bool victory) => Detach(ctx?.battle);
        public override void OnLost(RelicEffectContext ctx) => Detach(ctx?.battle);

        private void OnPlayerTurnStarted()
        {
            PrepareChoicesForPlayerTurn();
        }

        private void PrepareChoicesForPlayerTurn()
        {
            if (_battle == null || _battle.IsBattleEnded || _isDetached) return;

            _pendingChoices.Clear();
            foreach (EnemyInstance host in _battle.EnemyInstances)
            {
                if (host == null) continue;

                // 站队天平的强制层只持续一个敌人回合；新玩家回合开始时先恢复其他运行时牌池。
                host.ClearForcedSkillsThisTurn();

                if (host.IsDead || !IsHost(host)) continue;

                List<CardEntry> candidates = DrawCandidatesFromConfiguredPool(host);
                if (candidates.Count == 0)
                {
                    _battle.RefreshEnemyIntent(host);
                    Debug.LogWarning($"[TeamBalance] {host.Name} 的当前技能池为空，跳过本回合选择。");
                    continue;
                }

                if (candidates.Count == 1)
                {
                    ApplyChoice(host, candidates[0]);
                    continue;
                }

                _pendingChoices.Enqueue(new ChoiceRequest
                {
                    host = host,
                    candidates = candidates
                });
            }

            if (_pendingChoices.Count == 0)
            {
                _battle.SetPlayerInputLocked(InputLockKey, false);
                return;
            }

            _battle.SetPlayerInputLocked(InputLockKey, true);
            ShowNextChoice();
        }

        private List<CardEntry> DrawCandidatesFromConfiguredPool(EnemyInstance host)
        {
            // 按需求直接读取 EnemyConfig 的配置池；不要读取 CurrentSkillPool，避免受抄袭专家覆盖池影响。
            bool lowSanity = _battle.IsLowSanityForFusion;
            List<CardEntry> source = lowSanity ? host.Config?.phase2Skills : host.Config?.phase1Skills;
            var result = new List<CardEntry>(_candidateCount);
            if (source == null || source.Count == 0) return result;

            var remaining = new List<CardEntry>();
            foreach (CardEntry entry in source)
            {
                if (entry != null)
                    remaining.Add(entry);
            }

            while (result.Count < _candidateCount && remaining.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, remaining.Count);
                result.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
            return result;
        }

        private void ShowNextChoice()
        {
            while (_pendingChoices.Count > 0)
            {
                ChoiceRequest request = _pendingChoices.Dequeue();
                if (request?.host == null || request.host.IsDead || request.candidates == null || request.candidates.Count == 0)
                    continue;

                bool opened = _battle.ShowTeamBalanceChoice(
                    request.host,
                    request.candidates,
                    selected => OnChoiceSelected(request.host, selected));
                if (opened) return;

                // 预制体未配置或节点损坏时避免永久锁死战斗：以第一张候选作安全兜底。
                Debug.LogError("[TeamBalance] 无法打开站队天平选择界面，已自动选择第一张候选牌。请检查 BattleManager 的预制体引用。");
                ApplyChoice(request.host, request.candidates[0]);
            }

            _battle?.SetPlayerInputLocked(InputLockKey, false);
        }

        private void OnChoiceSelected(EnemyInstance host, CardEntry selected)
        {
            if (_isDetached || _battle == null || _battle.IsBattleEnded) return;
            if (host != null && !host.IsDead && selected != null)
                ApplyChoice(host, selected);

            ShowNextChoice();
        }

        private void ApplyChoice(EnemyInstance host, CardEntry selected)
        {
            if (host == null || selected == null) return;

            host.SetForcedSkillsThisTurn(new[] { selected });
            _battle.RefreshEnemyIntent(host);
            Debug.Log($"[TeamBalance] 玩家为 {host.Name} 选择了技能：{selected.cardName}。该敌人本回合仅执行此牌。");
        }

        private bool IsHost(EnemyInstance inst)
        {
            var abilities = inst?.Config?.abilities;
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
            if (_isDetached) return;
            _isDetached = true;

            if (battle != null)
            {
                battle.OnPlayerTurnStarted -= OnPlayerTurnStarted;
                battle.HideTeamBalanceChoice();
                battle.SetPlayerInputLocked(InputLockKey, false);

                foreach (EnemyInstance host in battle.EnemyInstances)
                {
                    if (host != null && IsHost(host))
                        host.ClearForcedSkillsThisTurn();
                }
            }

            _pendingChoices.Clear();
            _battle = null;
            _relic = null;
            _candidateCount = DefaultCandidateCount;
        }
    }
}
