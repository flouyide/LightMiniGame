using System;
using System.Collections.Generic;
using LightMiniGame.CardEditor;
using UnityEngine;

/// <summary>
/// 未完成卡牌紫色机制的运行时状态（对照策划表）。
/// 敌人出牌时玩家手牌通常已被弃光，针对手牌的效果推迟到下次抽牌之后。
/// </summary>
public sealed class UnfinishedCardRuntime
{
    public const int DirtyWorkDamagePerStack = 3;
    public const int ImpostorStrengthCap = 3;
    public const int MaxSummonedEnemies = 4;

    private readonly BattleManager _battle;
    private readonly List<Action> _pendingAfterDraw = new List<Action>();
    private readonly HashSet<CardData> _lockedNextTurn = new HashSet<CardData>();
    private int _entangleStacks;
    private int _dirtyWorkStacks;

    public UnfinishedCardRuntime(BattleManager battle)
    {
        _battle = battle;
    }

    public int EntangleStacks => _entangleStacks;
    public int DirtyWorkStacks => _dirtyWorkStacks;
    public int IncomingDamageBonus => _dirtyWorkStacks * DirtyWorkDamagePerStack;

    public void Clear()
    {
        _pendingAfterDraw.Clear();
        ClearLocks();
        _entangleStacks = 0;
        _dirtyWorkStacks = 0;
        SyncHandCostBonus();
    }

    public void ReplaceCard(CardData from, CardData to)
    {
        if (from == null || to == null || from == to) return;
        if (_lockedNextTurn.Remove(from)) _lockedNextTurn.Add(to);
    }

    public void RunAfterNextPlayerDraw(Action action)
    {
        if (action != null) _pendingAfterDraw.Add(action);
    }

    public void ApplyPendingHandEffects()
    {
        if (_pendingAfterDraw.Count == 0)
        {
            SyncHandCostBonus();
            return;
        }
        var pending = new List<Action>(_pendingAfterDraw);
        _pendingAfterDraw.Clear();
        for (int i = 0; i < pending.Count; i++)
        {
            try { pending[i]?.Invoke(); }
            catch (Exception ex)
            {
                Debug.LogError($"[UnfinishedCard] 抽牌后延迟效果异常: {ex}");
            }
        }
        SyncHandCostBonus();
        _battle.RefreshHandDisplays();
    }

    public void OnPlayerTurnEnded()
    {
        ClearLocks();
        if (_entangleStacks > 0)
        {
            _entangleStacks--;
            Debug.Log($"[UnfinishedCard] 缠结回合结束 -1 → {_entangleStacks}");
        }
        SyncHandCostBonus();
    }

    public bool IsLockedNextTurn(CardData card) => card != null && _lockedNextTurn.Contains(card);

    public void AddDirtyWorkStacks(int stacks)
    {
        _dirtyWorkStacks = Mathf.Max(0, _dirtyWorkStacks + stacks);
        Debug.Log($"[UnfinishedCard] 脏活 {stacks:+0;-0;0} → {_dirtyWorkStacks} 层（受伤额外 {IncomingDamageBonus}）");
    }

    public void AddEntangleStacks(int stacks)
    {
        _entangleStacks = Mathf.Max(0, _entangleStacks + stacks);
        SyncHandCostBonus();
        _battle.RefreshHandDisplays();
        Debug.Log($"[UnfinishedCard] 缠结 {stacks:+0;-0;0} → {_entangleStacks} 层（手牌费用+{_entangleStacks}）");
    }

    public void AddWatchTargetKeyword(int count)
    {
        RunAfterNextPlayerDraw(() => _battle.AddKeywordToRandomHandCards(KeywordType.WatchTarget, count));
    }

    public void LockCeilHalfHandNextTurn()
    {
        RunAfterNextPlayerDraw(ApplyCeilHalfLock);
    }

    public void SyncHandCostBonus()
    {
        var hand = _battle.HandCards;
        if (hand == null) return;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] != null)
                hand[i].statusCostBonus = _entangleStacks;
        }
    }

    private void ApplyCeilHalfLock()
    {
        var hand = CopyHand();
        int n = (hand.Count + 1) / 2;
        if (n <= 0) return;
        Shuffle(hand);
        for (int i = 0; i < n; i++)
        {
            var card = _battle.EnsureRuntimeCard(hand[i]);
            if (card == null) continue;
            _lockedNextTurn.Add(card);
            card.lockReason = "考勤警告：本回合无法打出";
        }
        Debug.Log($"[UnfinishedCard] {n} 张手牌下回合无法打出（向上取整半数）");
    }

    private void ClearLocks()
    {
        if (_lockedNextTurn.Count > 0)
        {
            foreach (var card in _lockedNextTurn)
                if (card != null) card.lockReason = "";
        }
        _lockedNextTurn.Clear();
    }

    private List<CardData> CopyHand()
    {
        var list = new List<CardData>();
        var hand = _battle.HandCards;
        if (hand == null) return list;
        for (int i = 0; i < hand.Count; i++)
            if (hand[i] != null) list.Add(hand[i]);
        return list;
    }

    private static void Shuffle(List<CardData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
