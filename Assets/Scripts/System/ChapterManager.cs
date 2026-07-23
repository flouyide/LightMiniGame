using System.Collections.Generic;
using System.Linq;
using LightMiniGame.Card;
using LightMiniGame.Shop;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 章节状态管理与刷新算法
/// </summary>
public class ChapterManager : MonoBehaviour
{
    [Header("游戏配置")]
    [SerializeField] private GameConfig gameConfig;
    [SerializeField] private PlayerConfig playerConfig;

    [Header("调试")]
    [Tooltip("跳过前N章，直接从第N+1章开始（0=从第一章开始）")]
    [SerializeField] private int debugStartChapterIndex;

    // --- 状态 ---
    private int _currentChapterIndex = -1;  // 当前章节索引（-1表示未开始）
    private ChapterConfig _currentChapter;  // 当前章节配置引用
    private int _remainingSelections;   // 章节剩余选择次数
    private List<PageEventData> _currentPages = new();  // 当前显示的3个页面事件列表
    private HashSet<string> _completedEvents = new();   // 已完成事件ID集合
    private HashSet<string> _unlockedEvents = new();    // 已解锁事件ID集合（后续事件解锁）
    private HashSet<string> _excludedEvents = new();    // 已排斥事件ID集合（互斥事件排除）
    private bool _finalNodeCompleted;
    private bool _isStarted; // 游戏是否已启动
    private int _lastSelectedIndex = -1; // 最后选中的页面索引（供 OnOptionResolved 使用）
    private int _remainingBeforeSelection; // 本次选择/删除「之前」的剩余次数（区分"刷新"还是"消耗"）

    // --- 事件回调 ---
    public UnityEvent<List<PageEventData>> OnPagesRefreshed = new();
    public UnityEvent<PageEventData, int> OnPageSelected = new(); // data, selectedIndex
    public UnityEvent<PageEventData, int> OnPageRefreshed = new(); // newData, refreshedIndex
    public UnityEvent<int> OnPageDeleted = new(); // deleted index
    public UnityEvent<int> OnPageConsumed = new(); // consumed index（剩余页数为0时选中卡片 → 删除该卡片不刷新）
    public UnityEvent OnChapterComplete = new();
    public UnityEvent<string, int> OnChapterInfoUpdated = new(); // chapterName, remaining
    public UnityEvent<int, int, int> OnPlayerStatsUpdated = new();    // hp, gold, sanity

    // 玩家状态
    public int PlayerHP { get; private set; }
    public int PlayerMaxHP { get; private set; }
    public int PlayerGold { get; private set; }
    public int PlayerSanity { get; private set; }   // 理智（背景切换依据）

    /// <summary>当前章节配置（供 BookUIController 读取背景图与阈值）。</summary>
    public ChapterConfig CurrentChapter => _currentChapter;

    /// <summary>是否买得起 amount 金币。</summary>
    public bool CanAfford(int amount) => PlayerGold >= amount;

    /// <summary>
    /// 扣减金币（用于商店消费）。成功返回 true 并广播玩家属性变化（刷新金币 UI）。
    /// 余额不足或金额为负时返回 false，不扣款。
    /// </summary>
    public bool SpendGold(int amount)
    {
        if (amount < 0) return false;
        if (PlayerGold < amount) return false;
        PlayerGold -= amount;
        OnPlayerStatsUpdated?.Invoke(PlayerHP, PlayerGold, PlayerSanity);
        return true;
    }

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
        InitPlayerStats();
        BuildInitialLibraries();   // 从 GameConfig.characters 读取 startingLibrary 构建初始牌库
        StartNextChapter();
    }

    /// <summary>
    /// 从 PlayerConfig 初始化玩家属性（无配置时用默认值兜底）
    /// </summary>
    private void InitPlayerStats()
    {
        if (playerConfig != null)
        {
            PlayerMaxHP = playerConfig.maxHP;
            PlayerHP = playerConfig.startHP;
            PlayerGold = playerConfig.startGold;
            PlayerSanity = playerConfig.Sanity;
        }
        else
        {
            PlayerMaxHP = 64;
            PlayerHP = 64;
            PlayerGold = 50;
            PlayerSanity = 0;
            Debug.LogWarning("[ChapterManager] playerConfig 未配置，使用默认玩家属性");
        }
    }

    /// <summary>
    /// 从 GameConfig.characters 读取每个角色的 startingLibrary，构建初始牌库。
    /// （原由 GameManager 负责，现已集中到开局流程，避免分散两处初始化。）
    /// </summary>
    private void BuildInitialLibraries()
    {
        if (gameConfig == null || gameConfig.characters == null || gameConfig.characters.Count == 0)
        {
            Debug.LogWarning("[ChapterManager] gameConfig 未配置或 characters 为空，跳过初始牌库构建");
            return;
        }

        GlobalCardLibrary.EnsureInstance();
        if (GlobalCardLibrary.Instance == null) return;

        foreach (var ch in gameConfig.characters)
        {
            if (ch != null && ch.startingLibrary != null)
                GlobalCardLibrary.Instance.BuildFromStartingLibrary(ch.startingLibrary);
            else if (ch != null)
                GlobalCardLibrary.Instance.RegisterCharacter(ch);   // 无初始牌组也登记角色，避免后续操作时报空
        }

        // 初始化按角色隔离的遗物库：为每个角色登记独立遗物库，并放入起始遗物
        GlobalRelicInventory.EnsureInstance();
        if (GlobalRelicInventory.Instance != null)
        {
            foreach (var ch in gameConfig.characters)
            {
                if (ch == null) continue;
                GlobalRelicInventory.Instance.RegisterCharacter(ch);
                if (ch.startingRelics != null)
                {
                    foreach (var r in ch.startingRelics)
                        GlobalRelicInventory.Instance.Add(ch, r);
                }
            }
        }
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
        OnPlayerStatsUpdated?.Invoke(PlayerHP, PlayerGold, PlayerSanity);
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

        // 最终节点逻辑：仅当剩余选择次数为 0 时强制出现 Boss（Boss 只在剩余 0 时必出现）
        bool forceFinal = _remainingSelections == 0 && finalNodeCandidates.Count > 0 && !_finalNodeCompleted;

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
    /// 刷新指定位置的页面（删除页面功能）
    /// </summary>
    public void RefreshPageAt(int index)
    {
        if (index < 0 || index >= _currentPages.Count)
        {
            Debug.LogWarning($"[ChapterManager] 无效的页面索引: {index}");
            return;
        }

        // 计算可用事件池
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

            // 跳过当前已经在显示的其他页面，避免重复
            for (int i = 0; i < _currentPages.Count; i++)
            {
                if (i != index && _currentPages[i].eventId == evt.eventId)
                {
                    goto SkipEvent;
                }
            }

            if (evt.isFinalNode)
                finalNodeCandidates.Add(evt);
            else
                availablePool.Add(evt);

            SkipEvent:;
        }

        // 最终节点逻辑：仅当剩余选择次数为 0 时强制出现 Boss（Boss 只在剩余 0 时必出现）
        bool forceFinal = _remainingSelections == 0 && finalNodeCandidates.Count > 0 && !_finalNodeCompleted;

        PageEventData newPage;
        if (forceFinal && finalNodeCandidates.Count > 0)
        {
            newPage = finalNodeCandidates[0];
        }
        else
        {
            var fullPool = availablePool.ToList();
            fullPool.AddRange(finalNodeCandidates);
            Shuffle(fullPool);

            if (fullPool.Count > 0)
            {
                newPage = fullPool[0];
            }
            else
            {
                // 如果没有可用事件，创建一个默认的空事件
                newPage = new PageEventData
                {
                    eventId = "empty_" + index,
                    displayName = "无事件",
                    description = "暂无可用事件",
                    eventType = PageEventType.Rest,
                    icon = null,
                    isRepeatable = true,
                    isFinalNode = false
                };
            }
        }

        // 替换指定位置的页面
        _currentPages[index] = newPage;
        OnPageRefreshed?.Invoke(newPage, index);
    }

    /// <summary>
    /// 删除某一页（刷新为新页面）
    /// </summary>
    public void DeletePage(int index)
    {
        if (index < 0 || index >= _currentPages.Count)
        {
            Debug.LogWarning($"[ChapterManager] 无效的页面索引: {index}");
            return;
        }

        var deleted = _currentPages[index];
        Debug.Log($"[删除] 删除页面「{deleted.displayName}」——类型：{TypeNameOf(deleted.eventType)}");

        // 标记为已完成但不触发事件效果
        _completedEvents.Add(deleted.eventId);

        // 处理互斥
        if (deleted.mutuallyExclusiveIds != null)
        {
            foreach (var id in deleted.mutuallyExclusiveIds)
                _excludedEvents.Add(id);
        }

        // 处理后续解锁
        if (deleted.followUpIds != null)
        {
            foreach (var id in deleted.followUpIds)
                _unlockedEvents.Add(id);
        }

        // 记录点击「之前」的剩余次数，再递减（不能为负）
        int remainingBeforeDelete = _remainingSelections;
        _remainingSelections = Mathf.Max(0, _remainingSelections - 1);

        OnChapterInfoUpdated?.Invoke(_currentChapter.chapterName, _remainingSelections);

        // 通知删除事件
        OnPageDeleted?.Invoke(index);

        if (remainingBeforeDelete <= 0 && !_finalNodeCompleted)
        {
            // 点击删除前已无剩余页数 → 删除卡片不刷新
            if (index >= 0 && index < _currentPages.Count)
                _currentPages.RemoveAt(index);
            OnPageConsumed?.Invoke(index);
        }
        else
        {
            // 还有剩余页数 → 刷新被删除位置的页面
            RefreshPageAt(index);
        }
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
        _lastSelectedIndex = index;

        // 占位符：进入战斗/事件，先打印事件类型到 Console（战斗/商店/休整/事件）
        Debug.Log($"[事件] 进入「{selected.displayName}」——类型：{TypeNameOf(selected.eventType)}");

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

        // 记录点击「之前」的剩余次数：>=1 表示还有选择机会 → 之后应刷新；==0 表示已无机会 → 之后应消耗（删除卡片不刷新）
        _remainingBeforeSelection = _remainingSelections;

        _remainingSelections = Mathf.Max(0, _remainingSelections - 1);

        OnChapterInfoUpdated?.Invoke(_currentChapter.chapterName, _remainingSelections);
        // 此时所有状态已更新完毕，OnOptionResolved 能拿到正确的 _finalNodeCompleted 和 _remainingBeforeSelection
        OnPageSelected?.Invoke(selected, index);
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
                case EffectType.GainSanity:
                    PlayerSanity += effect.amount;
                    break;
                case EffectType.LoseSanity:
                    PlayerSanity = Mathf.Max(0, PlayerSanity - effect.amount);
                    break;
            }
        }

        OnPlayerStatsUpdated?.Invoke(PlayerHP, PlayerGold, PlayerSanity);
    }

    /// <summary>
    /// 选项选择后继续：Boss 已打完 → 进入下一章；点击前已无剩余次数 → 删除卡片不刷新；否则刷新页面
    /// </summary>
    public void OnOptionResolved()
    {
        if (_finalNodeCompleted)
        {
            // Boss 已打完，立即进入下一章（不管剩余页数）
            OnChapterComplete?.Invoke();
            return;
        }

        if (_remainingBeforeSelection <= 0)
        {
            // 点击「进入」前就已经没有剩余次数 → 删除当前卡片，不刷新新卡片
            if (_lastSelectedIndex >= 0 && _lastSelectedIndex < _currentPages.Count)
                _currentPages.RemoveAt(_lastSelectedIndex);
            OnPageConsumed?.Invoke(_lastSelectedIndex);
            return;
        }

        // 点击前还有剩余次数 → 刷新 3 张新卡片
        RefreshPages();
    }

    /// <summary>
    /// 进入下一章
    /// </summary>
    public void NextChapter()
    {
        StartNextChapter();
    }

    private static string TypeNameOf(PageEventType type) => type switch
    {
        PageEventType.Battle => "战斗",
        PageEventType.Shop => "商店",
        PageEventType.Rest => "休整",
        PageEventType.Event => "事件",
        _ => "未知事件"
    };

    private static void Shuffle<T>(List<T> list)    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
