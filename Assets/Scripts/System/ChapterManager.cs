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

    [Header("场景画布（单场景内 局外↔战斗 切换）")]
    [Tooltip("战斗画布（BattleCanvas）。进入战斗时启用，退出时禁用。")]
    [SerializeField] private GameObject battleCanvas;
    [Tooltip("局外画布（BookCanvas）。进入战斗时禁用，退出时启用。")]
    [SerializeField] private GameObject bookCanvas;
    [Tooltip("战斗管理器（BattleManager）。进入战斗时驱动其 BeginBattle()。留空则运行时自动查找。")]
    [SerializeField] private BattleManager battleManager;
    private bool _inBattle;
    private System.Action _battleResume;   // 由“选项效果 EnterBattle”发起战斗时的效果序列续体；非空则战后先续体再推进章节

    // --- 当前激活/未激活角色（局外角色切换，进入战斗时传给 BattleManager）---
    private CharacterData _activeCharacter;
    private CharacterData _inactiveCharacter;
    public CharacterData ActiveCharacter => _activeCharacter;
    public CharacterData InactiveCharacter => _inactiveCharacter;

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
        InitCharacters();          // 初始化局外激活/未激活角色状态
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
                // 作为选项效果：挂起效果序列，战斗结束（ReturnFromBattle）后续体，继续后续效果并最终触发 onComplete
                suspended = EnterBattle(() => ProcessEffect(effects, next, onComplete));
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
        // 编辑器格式 CardEntry → 运行时 CardData（牌库/战斗内部格式）
        var cardData = CardEntryAdapter.ConvertSingle(effect.card);
        if (cardData == null) return;
        GlobalCardLibrary.EnsureInstance();
        if (GlobalCardLibrary.Instance == null) return;
        int copies = Mathf.Max(1, effect.amount);
        for (int i = 0; i < copies; i++)
            GlobalCardLibrary.Instance.AddCard(ch, cardData);
        Debug.Log($"[ChapterManager] 发放卡牌 {cardData.cardName} x{copies} → {ch.displayName}（随机角色）");
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

    // ===== 局外 ↔ 战斗 场景切换（单场景：同场景内切换 Canvas）=====

    /// <summary>初始化局外角色激活状态：默认 characters[0] 激活、characters[1] 未激活。幂等。</summary>
    public void InitCharacters()
    {
        // 幂等：若已被设置（开局默认 / 局外交换 / 战斗写回）则不重置，
        // 否则 BookCanvas 战后重新启用时 OnEnable→InitCharacters 会把战斗内切换的角色结果覆盖回 characters[0]/[1]。
        if (_activeCharacter != null) return;
        if (gameConfig == null || gameConfig.characters == null) return;
        var chars = gameConfig.characters;
        _activeCharacter   = chars.Count > 0 ? chars[0] : null;
        _inactiveCharacter = chars.Count > 1 ? chars[1] : null;
        Debug.Log($"[ChapterManager] 初始化角色激活状态：激活={_activeCharacter?.displayName}，未激活={_inactiveCharacter?.displayName}");
    }

    /// <summary>局外交换激活/未激活角色（由 BookUIController 的角色栏按钮调用）。</summary>
    public void SwapCharacters()
    {
        (_activeCharacter, _inactiveCharacter) = (_inactiveCharacter, _activeCharacter);
        Debug.Log($"[ChapterManager] 交换激活角色：激活={_activeCharacter?.displayName}，未激活={_inactiveCharacter?.displayName}");
    }

    /// <summary>
    /// 进入战斗：禁用 BookCanvas、启用 BattleCanvas，并驱动 BattleManager 读取局外玩家属性后开始战斗。
    /// - 来自「战斗」类型书页（PageEventType.Battle）直接调用时 onResolved 为 null，战斗结束由 ReturnFromBattle 推进章节；
    /// - 来自选项效果 EffectType.EnterBattle 时，传入续体回调，使效果序列在战斗结束后才继续并触发 onComplete（=OnOptionResolved），
    ///   避免「进入战斗的同时立刻推进章节 / 战后再次推进」的双调用问题。
    /// </summary>
    /// <returns>true=已发起战斗（调用方应暂停序列，等战斗结束后续体）；false=未发起（如已在战斗中）。</returns>
    public bool EnterBattle(System.Action onResolved = null)
    {
        if (_inBattle) return false;          // 防止重复进入
        _inBattle = true;
        _battleResume = onResolved;

        if (bookCanvas != null) bookCanvas.SetActive(false);
        if (battleCanvas != null) battleCanvas.SetActive(true);

        var bm = battleManager != null ? battleManager : FindObjectOfType<BattleManager>();
        if (bm != null)
        {
            bm.StartActiveChar = _activeCharacter;   // 局外当前激活角色作为战斗起始激活角色
            bm.StartInactiveChar = _inactiveCharacter;
            bm.OnBattleEnded -= OnBattleEnded;
            bm.OnBattleEnded += OnBattleEnded;
            bm.BeginBattle();        // 读取局外属性 + 启动战斗
            return true;
        }
        else
        {
            Debug.LogError("[ChapterManager] 未找到 BattleManager，无法进入战斗");
            ReturnFromBattle();      // 兜底：直接回到局外（会触发 _battleResume 或 OnOptionResolved）
            return false;
        }
    }

    /// <summary>BattleManager.QuitButton 回调：切回局外并推进章节。</summary>
    private void OnBattleEnded()
    {
        ReturnFromBattle();
    }

    private void ReturnFromBattle()
    {
        if (!_inBattle) return;
        _inBattle = false;

        if (battleCanvas != null) battleCanvas.SetActive(false);
        if (bookCanvas != null) bookCanvas.SetActive(true);

        // 战斗后的玩家属性已由 BattleManager 在退出前通过 ApplyBattleResult 写回本管理器
        OnPlayerStatsUpdated?.Invoke(PlayerHP, PlayerGold, PlayerSanity);

        // 若本次战斗由“选项效果 EnterBattle”发起（_battleResume 非空），则战后续体：
        // 继续处理剩余效果并最终触发 onComplete（=OnOptionResolved，由 ApplyEffects 传入，负责推进章节）。
        // 否则（直接点「战斗」书页进入）按原逻辑直接推进章节。
        if (_battleResume != null)
        {
            var resume = _battleResume;
            _battleResume = null;
            resume.Invoke();
        }
        else
        {
            OnOptionResolved();
        }
    }

    /// <summary>
    /// 战斗结束后由 BattleManager 调用：把战斗后的玩家属性写回局外系统。
    /// hp/maxHp：战斗结算后的当前/最大 HP；sanity/maxSanity：理智；
    /// 其余为战斗内可能变化的持久基础属性与每回合数值。
    /// </summary>
    public void ApplyBattleResult(int hp, int maxHp, int sanity, int maxSanity,
        int strength, int agility, int lifesteal, int critRate, int critDamage,
        int maxActionPoints, int drawPerTurn,
        CharacterData activeChar = null, CharacterData inactiveChar = null)
    {
        PlayerMaxHP   = maxHp > 0 ? maxHp : PlayerMaxHP;
        PlayerHP      = Mathf.Clamp(hp, 0, PlayerMaxHP);
        PlayerMaxSanity  = maxSanity > 0 ? maxSanity : PlayerMaxSanity;
        PlayerSanity  = Mathf.Clamp(sanity, 0, PlayerMaxSanity);
        PlayerMaxActionPoints = maxActionPoints;
        PlayerDrawPerTurn      = drawPerTurn;
        PlayerStrength   = strength;
        PlayerAgility    = agility;
        PlayerLifesteal  = lifesteal;
        PlayerCritRate   = critRate;
        PlayerCritDamage  = critDamage;

        // 战斗结束时的激活/未激活角色同步回局外：若战斗内切换过角色，则保留切换结果，
        // 局外角色栏与下次进入战斗都以此时状态为准。
        if (activeChar != null)   _activeCharacter = activeChar;
        if (inactiveChar != null) _inactiveCharacter = inactiveChar;

        Debug.Log($"[ChapterManager] 战斗结果已写回：HP {PlayerHP}/{PlayerMaxHP}，理智 {PlayerSanity}/{PlayerMaxSanity}，力量 {PlayerStrength}；激活角色={_activeCharacter?.displayName}，未激活={_inactiveCharacter?.displayName}");
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
