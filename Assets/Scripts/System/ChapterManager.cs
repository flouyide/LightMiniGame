using System;
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
    [SerializeField] private MasterRelicLibrary masterRelicLibrary;  // 总遗物库（GrantRelic 随机抽遗物用）

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

    // —— 交互式效果请求：由 UI 层（BookUIController）订阅，开启对应界面；关闭后调用 resume 续体恢复效果序列 ——
    public event Action<float, Action> OnRequestDiscountShop;   // discountRatio, resume
    public event Action<Action> OnRequestRemoveCard;            // resume
    public event Action<KeywordType, Action> OnRequestAddKeyword; // keyword, resume

    // 玩家状态
    public int PlayerHP { get; private set; }
    public int PlayerMaxHP { get; private set; }
    public int PlayerGold { get; private set; }
    public int PlayerSanity { get; private set; }   // 理智（背景切换依据）
    public int PlayerMaxSanity { get; private set; }   // 理智上限
    public int PlayerMaxActionPoints { get; private set; }  // 每回合行动点
    public int PlayerDrawPerTurn { get; private set; }      // 每回合基础抽牌数

    // 持久基础属性（力量/敏捷/吸血/暴击率/暴伤）的【运行时副本】：
    // 只在单局游戏内保留（跨战斗有效），开局时由 InitPlayerStats 从 PlayerConfig 读入初始值，
    // 事件 ModifyAttribute 修改它；重新开始游戏时再次从 PlayerConfig 读入即归零回到初始。
    // 资产 PlayerConfig 本身【只当初始值来源】，运行时不再被改写。
    public int PlayerStrength { get; private set; }
    public int PlayerAgility { get; private set; }
    public int PlayerLifesteal { get; private set; }
    public int PlayerCritRate { get; private set; }
    public int PlayerCritDamage { get; private set; }

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
            PlayerSanity = playerConfig.startSanity;
            PlayerMaxSanity = playerConfig.maxSanity;
            PlayerMaxActionPoints = playerConfig.maxActionPoints;
            PlayerDrawPerTurn = playerConfig.drawPerTurn;
            // 持久基础属性：每次开局从 PlayerConfig（仅作初始值来源）重新读入
            PlayerStrength   = playerConfig.strength;
            PlayerAgility    = playerConfig.agility;
            PlayerLifesteal  = playerConfig.lifesteal;
            PlayerCritRate   = playerConfig.critRate;
            PlayerCritDamage = playerConfig.critDamage;
        }
        else
        {
            PlayerMaxHP = 64;
            PlayerHP = 64;
            PlayerGold = 50;
            PlayerSanity = 10;
            PlayerMaxSanity = 10;
            PlayerMaxActionPoints = 3;
            PlayerDrawPerTurn = 3;
            PlayerStrength = PlayerAgility = PlayerLifesteal = PlayerCritRate = PlayerCritDamage = 0;
            Debug.LogWarning("[ChapterManager] playerConfig 未配置，使用默认玩家属性");
        }
    }

    // 持久基础属性已改为 ChapterManager 运行时副本（PlayerStrength 等），
    // 开局由 InitPlayerStats 从 PlayerConfig 读入初始值，事件 ModifyAttribute 修改它，
    // 重新开始游戏即归零回初始。资产 PlayerConfig 仅作初始值，运行时不再被改写，故无需单独的"重置"方法。

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
    /// 应用选项效果。支持「挂起—恢复」续体：遇到交互式效果（EnterDiscountShop / RemoveCard / AddKeywordToCard）
    /// 会暂停序列、开启对应 UI，并在 UI 关闭后通过 resume 续体继续处理后续效果。
    /// onComplete 在所有效果（含交互式）处理完毕后调用。
    /// </summary>
    public void ApplyEffects(List<EffectData> effects, Action onComplete = null)
    {
        if (effects == null || effects.Count == 0)
        {
            OnPlayerStatsUpdated?.Invoke(PlayerHP, PlayerGold, PlayerSanity);
            onComplete?.Invoke();
            return;
        }
        ProcessEffect(effects, 0, onComplete);
    }

    /// <summary>按顺序处理效果；遇到交互式效果时挂起，待 UI 关闭后续体恢复。</summary>
    private void ProcessEffect(List<EffectData> effects, int index, Action onComplete)
    {
        if (index >= effects.Count)
        {
            // 全部处理完毕：刷新玩家属性 UI 并触发完成回调
            OnPlayerStatsUpdated?.Invoke(PlayerHP, PlayerGold, PlayerSanity);
            onComplete?.Invoke();
            return;
        }

        var effect = effects[index];
        int next = index + 1;
        bool suspended = false;

        // 每个效果触发时打印效果信息（便于策划/调试核对）
        Debug.Log($"[ChapterManager] ▶ 触发效果 type={effect.type} | op={effect.attributeOp} | target={effect.targetAttribute} | amount={effect.amount} | discount={effect.discountRatio}");

        switch (effect.type)
        {
            case EffectType.GainItem:
                Debug.Log($"[ChapterManager] 获得物品: {effect.itemDesc}");
                break;
            case EffectType.LoseItem:
                Debug.Log($"[ChapterManager] 失去物品: {effect.itemDesc}");
                break;
            case EffectType.EnterBattle:
                Debug.Log("[ChapterManager] 进入战斗！（战斗系统待实现）");
                break;

            case EffectType.ModifyAttribute:
                ApplyModifyAttribute(effect);
                break;
            case EffectType.GrantRelic:
                ApplyGrantRelic(effect);
                break;
            case EffectType.GrantCard:
                ApplyGrantCard(effect);
                break;
            case EffectType.EnterDiscountShop:
                suspended = RequestDiscountShop(effect, () => ProcessEffect(effects, next, onComplete));
                break;
            case EffectType.RemoveCard:
                suspended = RequestRemoveCard(() => ProcessEffect(effects, next, onComplete));
                break;
            case EffectType.AddKeywordToCard:
                suspended = RequestAddKeyword(effect.keywordToAdd, () => ProcessEffect(effects, next, onComplete));
                break;
            default:
                Debug.LogWarning($"[ChapterManager] 未处理的效果类型: {effect.type}");
                break;
        }

        if (!suspended)
            ProcessEffect(effects, next, onComplete);
    }

    // ===== 扩展效果实现 =====

    private void ApplyModifyAttribute(EffectData effect)
    {
        // amount 恒为正数，方向由 attributeOp 决定（Gain=加，Lose=减）
        int mag = Mathf.Abs(effect.amount);
        int delta = effect.attributeOp == PlayerAttributeOp.Lose ? -mag : mag;
        string sign = delta >= 0 ? "+" : "";

        switch (effect.targetAttribute)
        {
            // —— 持久基础属性：写入 ChapterManager 运行时副本（单局内跨战斗保留），重新开始游戏时由 InitPlayerStats 从 PlayerConfig 重新读回初始值 ——
            case PlayerBaseAttribute.Strength:
            case PlayerBaseAttribute.Agility:
            case PlayerBaseAttribute.Lifesteal:
            case PlayerBaseAttribute.CritRate:
            case PlayerBaseAttribute.CritDamage:
                ApplyPersistedAttribute(effect.targetAttribute, delta);
                break;

            // —— 运行时资源：直接改 ChapterManager 实例字段，并经 OnPlayerStatsUpdated 广播刷新 UI ——
            case PlayerBaseAttribute.Gold:
                PlayerGold = Mathf.Max(0, PlayerGold + delta);
                break;
            case PlayerBaseAttribute.HP:
                PlayerHP = Mathf.Clamp(PlayerHP + delta, 0, PlayerMaxHP);
                break;
            case PlayerBaseAttribute.Sanity:
                PlayerSanity = Mathf.Max(0, PlayerSanity + delta);
                break;

            default:
                Debug.LogWarning($"[ChapterManager] ModifyAttribute 未识别的基础属性: {effect.targetAttribute}");
                return;
        }

        Debug.Log($"[ChapterManager] 修改属性 {effect.targetAttribute} {sign}{delta}（{effect.attributeOp}）");
    }

    /// <summary>把 delta 应用到持久基础属性的运行时副本（Mathf.Max(0) 防负）。资产 PlayerConfig 仅作初始值，此处不再改写它。</summary>
    private void ApplyPersistedAttribute(PlayerBaseAttribute attr, int delta)
    {
        switch (attr)
        {
            case PlayerBaseAttribute.Strength:   PlayerStrength   = Mathf.Max(0, PlayerStrength + delta); break;
            case PlayerBaseAttribute.Agility:    PlayerAgility    = Mathf.Max(0, PlayerAgility + delta); break;
            case PlayerBaseAttribute.Lifesteal:  PlayerLifesteal  = Mathf.Max(0, PlayerLifesteal + delta); break;
            case PlayerBaseAttribute.CritRate:   PlayerCritRate   = Mathf.Max(0, PlayerCritRate + delta); break;
            case PlayerBaseAttribute.CritDamage: PlayerCritDamage = Mathf.Max(0, PlayerCritDamage + delta); break;
        }
    }

    private void ApplyGrantRelic(EffectData effect)
    {
        var ch = PickRandomCharacter();
        if (ch == null) return;
        GlobalRelicInventory.EnsureInstance();
        if (GlobalRelicInventory.Instance == null) return;

        if (effect.relic != null)
        {
            GlobalRelicInventory.Instance.Add(ch, effect.relic);
            Debug.Log($"[ChapterManager] 发放遗物 {effect.relic.relicName} → {ch.displayName}（随机角色）");
        }
        else
        {
            var r = DrawRandomRelic(ch);
            if (r != null)
            {
                GlobalRelicInventory.Instance.Add(ch, r);
                Debug.Log($"[ChapterManager] 随机发放遗物 {r.relicName} → {ch.displayName}（随机角色）");
            }
        }
    }

    private void ApplyGrantCard(EffectData effect)
    {
        if (effect.card == null)
        {
            Debug.LogWarning("[ChapterManager] GrantCard 失败：未指定 card");
            return;
        }
        var ch = PickRandomCharacter();
        if (ch == null) return;
        GlobalCardLibrary.EnsureInstance();
        if (GlobalCardLibrary.Instance == null) return;
        int copies = Mathf.Max(1, effect.amount);
        for (int i = 0; i < copies; i++)
            GlobalCardLibrary.Instance.AddCard(ch, effect.card);
        Debug.Log($"[ChapterManager] 发放卡牌 {effect.card.cardName} x{copies} → {ch.displayName}（随机角色）");
    }

    /// <summary>随机挑一个角色，用于发放卡牌/遗物（设计意图：单局内随机单个角色获得）。</summary>
    private CharacterData PickRandomCharacter()
    {
        var all = (gameConfig != null && gameConfig.characters != null)
            ? gameConfig.characters
            : new List<CharacterData>();
        if (all.Count == 0) return null;
        return all[UnityEngine.Random.Range(0, all.Count)];
    }

    private RelicData DrawRandomRelic(CharacterData ch)
    {
        if (masterRelicLibrary == null || ch == null) return null;
        var pool = masterRelicLibrary.GetRelics(ch);
        if (pool == null || pool.Count == 0) return null;
        var gri = GlobalRelicInventory.Instance;
        var candidates = new List<RelicData>();
        foreach (var r in pool)
            if (r != null && (gri == null || !gri.Has(ch, r)))
                candidates.Add(r);
        if (candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    // ===== 交互式效果请求（续体机制）=====

    private bool RequestDiscountShop(EffectData effect, Action resume)
    {
        if (OnRequestDiscountShop == null) return false;
        // 折扣比例来自 EffectData.discountRatio（已从 ChapterConfig.discountShopRatio 迁移，按事件单独配置）
        float ratio = (effect != null) ? effect.discountRatio : 1f;
        OnRequestDiscountShop.Invoke(ratio, resume);
        return true;
    }

    private bool RequestRemoveCard(Action resume)
    {
        if (OnRequestRemoveCard == null) return false;
        OnRequestRemoveCard.Invoke(resume);
        return true;
    }

    private bool RequestAddKeyword(KeywordType kw, Action resume)
    {
        if (OnRequestAddKeyword == null) return false;
        OnRequestAddKeyword.Invoke(kw, resume);
        return true;
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
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
