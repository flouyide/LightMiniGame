using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 章节状态管理与刷新算法
/// </summary>
public class ChapterManager : MonoBehaviour
{
    [Header("游戏配置")]
    [SerializeField] private GameConfig gameConfig;

    [Header("调试")]
    [Tooltip("跳过前N章，直接从第N+1章开始（0=从第一章开始）")]
    [SerializeField] private int debugStartChapterIndex;

    // --- 状态 ---
    private int _currentChapterIndex = -1;
    private ChapterConfig _currentChapter;
    private int _remainingSelections;
    private List<PageEventData> _currentPages = new();
    private HashSet<string> _completedEvents = new();   // 已完成事件
    private HashSet<string> _unlockedEvents = new();    // 已解锁事件（后续事件解锁）
    private HashSet<string> _excludedEvents = new();    // 已排斥事件（互斥事件排除）
    private bool _finalNodeCompleted;
    private bool _isStarted;

    // --- 事件回调 ---
    public UnityEvent<List<PageEventData>> OnPagesRefreshed = new();
    public UnityEvent<PageEventData, int> OnPageSelected = new(); // data, selectedIndex
    public UnityEvent OnChapterComplete = new();
    public UnityEvent<string, int> OnChapterInfoUpdated = new(); // chapterName, remaining
    public UnityEvent<int, int> OnPlayerStatsUpdated = new();    // hp, gold

    // 玩家状态（简化）
    public int PlayerHP { get; private set; } = 64;
    public int PlayerMaxHP { get; private set; } = 64;
    public int PlayerGold { get; private set; } = 50;

    private const int RefreshCount = 3;

    private void Start()
    {
        if (!_isStarted)
            StartGame();
    }

    public void StartGame()
    {
        _isStarted = true;
        _currentChapterIndex = Mathf.Max(0, debugStartChapterIndex) - 1;
        StartNextChapter();
    }

    private void StartNextChapter()
    {
        _currentChapterIndex++;

        if (gameConfig == null || _currentChapterIndex >= gameConfig.chapters.Count)
        {
            Debug.Log("[ChapterManager] 所有章节完成！");
            OnChapterComplete?.Invoke();
            return;
        }

        _currentChapter = gameConfig.chapters[_currentChapterIndex];
        _remainingSelections = _currentChapter.maxSelections;
        _completedEvents.Clear();
        _unlockedEvents.Clear();
        _excludedEvents.Clear();
        _finalNodeCompleted = false;

        // 无前置条件的事件默认已解锁
        foreach (var evt in _currentChapter.events)
        {
            if (evt.prerequisiteIds == null || evt.prerequisiteIds.Count == 0)
                _unlockedEvents.Add(evt.eventId);
        }

        OnChapterInfoUpdated?.Invoke(_currentChapter.chapterName, _remainingSelections);
        OnPlayerStatsUpdated?.Invoke(PlayerHP, PlayerGold);
        RefreshPages();
    }

    /// <summary>
    /// 计算可用事件池并随机刷新3页
    /// </summary>
    public void RefreshPages()
    {
        _currentPages.Clear();

        var availablePool = new List<PageEventData>();
        var finalNodeCandidates = new List<PageEventData>();

        foreach (var evt in _currentChapter.events)
        {
            // 已完成且不可重复 → 跳过
            if (!evt.isRepeatable && _completedEvents.Contains(evt.eventId))
                continue;

            // 已完成且是最终节点 → 跳过
            if (evt.isFinalNode && _finalNodeCompleted)
                continue;

            // 被排斥（互斥）→ 跳过
            if (_excludedEvents.Contains(evt.eventId))
                continue;

            // 未解锁（前置未满足）→ 跳过
            if (!_unlockedEvents.Contains(evt.eventId))
                continue;

            if (evt.isFinalNode)
                finalNodeCandidates.Add(evt);
            else
                availablePool.Add(evt);
        }

        // 最终节点逻辑：剩余选择次数为1时强制出现
        bool forceFinal = _remainingSelections == 1 && finalNodeCandidates.Count > 0 && !_finalNodeCompleted;

        if (forceFinal)
        {
            var finalNode = finalNodeCandidates[0];
            _currentPages.Add(finalNode);

            // 从普通池中补充剩余位置
            var remainingPool = availablePool.ToList();
            Shuffle(remainingPool);
            while (_currentPages.Count < RefreshCount && remainingPool.Count > 0)
            {
                _currentPages.Add(remainingPool[0]);
                remainingPool.RemoveAt(0);
            }
        }
        else
        {
            // 最终节点也可正常参与随机
            var fullPool = availablePool.ToList();
            fullPool.AddRange(finalNodeCandidates);
            Shuffle(fullPool);

            while (_currentPages.Count < RefreshCount && fullPool.Count > 0)
            {
                _currentPages.Add(fullPool[0]);
                fullPool.RemoveAt(0);
            }
        }

        OnPagesRefreshed?.Invoke(_currentPages);
    }

    /// <summary>
    /// 玩家选择某一页
    /// </summary>
    public void SelectPage(int index)
    {
        if (index < 0 || index >= _currentPages.Count)
        {
            Debug.LogWarning($"[ChapterManager] 无效的页面索引: {index}");
            return;
        }

        var selected = _currentPages[index];
        OnPageSelected?.Invoke(selected, index);

        // 标记完成
        _completedEvents.Add(selected.eventId);

        if (selected.isFinalNode)
            _finalNodeCompleted = true;

        // 处理互斥：将互斥事件加入排除列表
        if (selected.mutuallyExclusiveIds != null)
        {
            foreach (var id in selected.mutuallyExclusiveIds)
                _excludedEvents.Add(id);
        }

        // 处理后续解锁：将后续事件加入解锁列表
        if (selected.followUpIds != null)
        {
            foreach (var id in selected.followUpIds)
                _unlockedEvents.Add(id);
        }

        _remainingSelections--;
        OnChapterInfoUpdated?.Invoke(_currentChapter.chapterName, _remainingSelections);

        if (_remainingSelections <= 0)
        {
            Debug.Log("[ChapterManager] 章节完成！");
            OnChapterComplete?.Invoke();
        }
    }

    /// <summary>
    /// 应用选项效果
    /// </summary>
    public void ApplyEffects(List<EffectData> effects)
    {
        if (effects == null) return;

        foreach (var effect in effects)
        {
            switch (effect.type)
            {
                case EffectType.GainGold:
                    PlayerGold += effect.amount;
                    break;
                case EffectType.LoseGold:
                    PlayerGold = Mathf.Max(0, PlayerGold - effect.amount);
                    break;
                case EffectType.GainHP:
                    PlayerHP = Mathf.Min(PlayerMaxHP, PlayerHP + effect.amount);
                    break;
                case EffectType.LoseHP:
                    PlayerHP = Mathf.Max(0, PlayerHP - effect.amount);
                    break;
                case EffectType.GainItem:
                    Debug.Log($"[ChapterManager] 获得物品: {effect.itemDesc}");
                    break;
                case EffectType.LoseItem:
                    Debug.Log($"[ChapterManager] 失去物品: {effect.itemDesc}");
                    break;
                case EffectType.EnterBattle:
                    Debug.Log("[ChapterManager] 进入战斗！（战斗系统待实现）");
                    break;
            }
        }

        OnPlayerStatsUpdated?.Invoke(PlayerHP, PlayerGold);
    }

    /// <summary>
    /// 选项选择后继续刷新
    /// </summary>
    public void OnOptionResolved()
    {
        if (_remainingSelections > 0)
            RefreshPages();
        else
            OnChapterComplete?.Invoke();
    }

    /// <summary>
    /// 进入下一章
    /// </summary>
    public void NextChapter()
    {
        StartNextChapter();
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
