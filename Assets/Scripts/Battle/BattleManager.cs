using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using LightMiniGame.Card;
using LightMiniGame.CardEditor;
using Random = UnityEngine.Random;

/// <summary>
/// 战斗管理器（双角色回合制）。
/// 流程: 回合开始(AP=3,抽3牌) → 玩家出牌 → 玩家结束 → 敌人行动(按P跳过) → 回合结束 → 循环
/// 每个角色拥有独立的抽牌堆/弃牌堆/消耗堆。切换角色时当前手牌洗入该角色弃牌堆。
/// </summary>
public class BattleManager : MonoBehaviour
{
    // ========================================================================
    // Inspector 字段
    // ========================================================================

    [Header("角色配置")]
    [Tooltip("游戏配置（含角色列表）")]
    [SerializeField] private GameConfig gameConfig;

    [Header("卡牌编辑器初始牌组（可选）")]
    [Tooltip("如果填写，战斗开始时用这些 CardEntry 卡牌代替角色的 startingLibrary。每个角色一组。")]
    [SerializeField] private List<CardEntry> character1Cards;
    [SerializeField] private List<CardEntry> character2Cards;

    [Header("运行时属性来源（持久基础属性运行时副本）")]
    [Tooltip("ChapterManager 持有持久基础属性（力量/敏捷/吸血/暴击率/暴伤）的运行时副本，单局内跨战斗保留。战斗开始时从此读取。留空则回退到 PlayerConfig（仅初始值，不含事件累积）")]
    [SerializeField] private ChapterManager chapterManager;

    [Header("卡牌预制体（按类型）")]
    [SerializeField] private GameObject attackCardPrefab;
    [FormerlySerializedAs("armorCardPrefab")] [SerializeField] private GameObject skillCardPrefab;
    [FormerlySerializedAs("buffCardPrefab")] [SerializeField] private GameObject abilityCardPrefab;

    [Header("玩家属性（双角色共享）")]
    [SerializeField] private int playerMaxHP = 100;
    [SerializeField] private int playerArmor = 0;
    [SerializeField] private int playerStrength = 0;
    [SerializeField] private int playerDexterity = 0;

    [Header("玩家属性来源（持久基础属性）")]
    [Tooltip("可选：配置玩家持久基础属性（力量/敏捷/吸血/暴击率/暴击伤害），由特殊事件 ModifyAttribute 修改，战斗开始时读入替换上方临时变量")]
    [SerializeField] private PlayerConfig playerConfig;

    [Header("敌人配置（默认/回退）")]
    [Tooltip("默认敌人配置资产；仅在 Battle 事件的 PageEventData.enemy 未指定时作为回退使用")]
    [SerializeField] private EnemyConfig enemyConfig;

    [Header("回合设置")]
    [SerializeField] private int maxActionPoints = 3;
    [SerializeField] private int drawPerTurn = 3;
    [SerializeField] private int initialDraw = 3;
    [SerializeField] private int handLimit = 10;

    [Header("UI引用 - 玩家")]
    [SerializeField] private HandCardLayout handLayout;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI actionPointText;
    [SerializeField] private TextMeshProUGUI armorText;
    [SerializeField] private TextMeshProUGUI strengthText;
    [SerializeField] private TextMeshProUGUI dexterityText;
    [SerializeField] private Slider playerHPBar;

    [Header("UI引用 - 理智")]
    [SerializeField] private TextMeshProUGUI sanityText;
    [SerializeField] private Slider sanityBar;

    [Header("UI引用 - 敌人")]
    [SerializeField] private TextMeshProUGUI enemyHPText;
    [SerializeField] private TextMeshProUGUI enemyArmorText;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI enemyIntentText;
    [SerializeField] private TextMeshProUGUI enemyDamageText;
    [Tooltip("伤害数字预制体（TextMeshProUGUI，用于多段伤害飘字）。留空则用 enemyDamageText 单个文本。")]
    [SerializeField] private GameObject damagePopupPrefab;
    [SerializeField] private Slider enemyHPBar;
    [Tooltip("敌人立绘 Image（阶段切换时替换 Sprite）")]
    [SerializeField] private Image enemyPortraitImage;
    [Tooltip("凝视值文本（显示在敌人上方）")]
    [SerializeField] private TextMeshProUGUI gazeValueText;

    [Header("UI引用 - 敌人技能卡面")]
    [Tooltip("敌人出招时显示的技能卡面板（Image + 子 TextMeshProUGUI）")]
    [SerializeField] private GameObject enemySkillCard;
    [SerializeField] private Image enemySkillCardImage;
    [SerializeField] private TextMeshProUGUI enemySkillNameText;
    [SerializeField] private TextMeshProUGUI enemySkillDescText;
    [Tooltip("技能卡显示持续时间（秒）")]
    [SerializeField] private float enemySkillCardDuration = 2.5f;
    [Tooltip("技能卡淡入淡出时间（秒）")]
    [SerializeField] private float enemySkillCardFadeTime = 0.5f;

    [Header("UI引用 - 回合")]
    [SerializeField] private TextMeshProUGUI phaseHintText;
    [SerializeField] private Button endTurnButton;

    [Header("UI引用 - 角色切换")]
    [SerializeField] private Button switchCharacterButton;
    [SerializeField] private TextMeshProUGUI activeCharNameText;
    [SerializeField] private TextMeshProUGUI inactiveCharNameText;
    [SerializeField] private Image activeCharPortrait;
    [SerializeField] private Image inactiveCharPortrait;
    [SerializeField] private GameObject switchAvailableIndicator;
    [SerializeField] private GameObject switchUsedIndicator;

    [Header("UI引用 - 设置")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanelPrefab;

    [Header("UI引用 - 结果")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private Button quitButton;

    [Header("UI引用 - 黑暗模式（理智转阶段）")]
    [Tooltip("全屏暗色遮罩 Image，理智转阶段时淡入。留空则不显示遮罩。")]
    [SerializeField] private Image darkOverlay;
    [Tooltip("黑暗遮罩目标透明度（0-1）")]
    [SerializeField] private float darkOverlayAlpha = 0.3f;
    [Tooltip("黑暗遮罩淡入持续时间（秒）")]
    [SerializeField] private float darkOverlayFadeDuration = 1f;

    [Header("UI引用 - 战斗背景")]
    [Tooltip("战斗背景容器 GameObject；其下的 Image 组件用于显示战斗背景（正常 / 低理智）。留空则不切换背景。")]
    [SerializeField] private GameObject background;

    // ========================================================================
    // 运行时状态
    // ========================================================================

    private class CharBattleState
    {
        public CharacterData data;
        public List<CardData> drawPile = new();
        public List<CardData> discardPile = new();
        public List<CardData> consumedPile = new();
    }

    private CharBattleState[] _chars = new CharBattleState[2];
    private int _activeCharIdx = 0;
    private bool _hasSwitchedThisBattle = false;

    private readonly List<CardData> _hand = new();
    private int _playerHP;
    private int _playerArmor;
    private int _playerStrength;
    private int _playerDexterity;
    private int _playerAgility;
    private int _playerLifesteal;
    private int _playerCritRate;
    private int _playerCritDamage;
    private int _playerSanity;
    private int _playerMaxSanity;

    // === CardEntry 效果系统支持 ===
    private EffectExecutor _effectExecutor;
    private AbilitySystem _abilitySystem;
    private readonly Dictionary<string, int> _customData = new();
    private readonly HashSet<string> _eventsThisTurn = new();
    private readonly HashSet<string> _eventsThisBattle = new();
    private readonly Dictionary<string, int> _turnCounters = new();
    private readonly Dictionary<string, int> _battleCounters = new();
    private bool _sanityPhaseTriggered;  // 理智转阶段是否已触发（防止重复触发）
    private const int SanityPhaseThreshold = 4;  // 理智转阶段阈值
    private int _baseDrawPerTurn;   // 每场战斗前的抽牌基数（来自 Inspector 的 drawPerTurn，开局捕获一次）
    private int _actionPoints;
    // === 单敌人状态（当前每个 Battle 事件只有一个敌人）===
    private int _enemyHP;
    private int _enemyMaxHP;
    private int _enemyArmor;
    private int _enemyPhase = 1;
    private int _gazeValue = 0;
    private int _turnInCycle = 0;       // 阶段1的回合循环计数
    private int _lockedCharIdx = -1;    // 被光束扫描锁定的角色索引
    private int _turnCount = 1;
    private bool _isPlayerTurn = true;
    private bool _battleEnded = false;
    private bool _waitingEnemyConfirm = false;
    private bool _isDarkMode = false;
    private Coroutine _sanityTrembleRoutine;
    private Coroutine _damagePopupRoutine;
    private bool _listenersWired = false;
    private Image _backgroundImage;   // Background GameObject 下的背景 Image（按理智切换）

    private SettingsPanelUI _settingsPanel;

    public bool IsPlayerTurn => _isPlayerTurn && !_battleEnded;

    /// <summary>战斗结束（点击 QuitButton）后通知 ChapterManager 切回局外。</summary>
    public event Action OnBattleEnded;

    // ========================================================================
    // 公共属性（供 BattleCardContext / EffectExecutor 使用）
    // ========================================================================

    public int PlayerHP => _playerHP;
    public int PlayerMaxHP => playerMaxHP;
    public int PlayerStrength => _playerStrength;
    public int PlayerDexterity => _playerDexterity;
    public float PlayerCritRate => _playerCritRate / 100f;
    public float PlayerCritDamage => _playerCritDamage / 100f;
    public int PlayerSanity => _playerSanity;
    public int PlayerArmor => _playerArmor;
    public int PlayerBleed => 0;
    public int ActionPoints => _actionPoints;
    public int EnemyCount => 1; // 当前每个 Battle 事件只有一个敌人
    public int SelectedEnemyIndex => 0;
    public int HandCount => _hand.Count;
    public int DrawPileCount => ActiveChar?.drawPile.Count ?? 0;
    public int DiscardPileCount => ActiveChar?.discardPile.Count ?? 0;

    public int GetEnemyHP(int index) => _enemyHP;
    public int GetEnemyArmor(int index) => _enemyArmor;
    public int GetEnemyBleed(int index) => 0;
    public int GetEnemyArmorBreak(int index) => 0;

    /// <summary>局外（ChapterManager）在进入战斗前指定的敌人（单个）。
    /// 为空则回退到 Inspector 的默认 enemyConfig。</summary>
    public EnemyConfig StartEnemy { get; set; }

    /// <summary>战斗背景配置（由 Battle 事件的 PageEventData 注入）。
    /// StartNormalBattleBackground / StartLowSanityBattleBackground 分别为正常与低理智背景图，
    /// StartBackgroundSanityThreshold 为切换阈值（玩家理智 &lt;= 该值时用低理智背景）。</summary>
    public Sprite StartNormalBattleBackground { get; set; }
    public Sprite StartLowSanityBattleBackground { get; set; }
    public int StartBackgroundSanityThreshold { get; set; } = 4;

    public int GetTurnCounter(string name) => _turnCounters.TryGetValue(name, out var v) ? v : 0;
    public int GetBattleCounter(string name) => _battleCounters.TryGetValue(name, out var v) ? v : 0;

    public int GetCustomData(string key) => _customData.TryGetValue(key, out var v) ? v : 0;
    public void SetCustomData(string key, int value) => _customData[key] = value;
    public void ModifyCustomData(string key, int delta) => _customData[key] = GetCustomData(key) + delta;

    public bool HasEventOccurred(string eventName) => _eventsThisTurn.Contains(eventName) || _eventsThisBattle.Contains(eventName);
    public void RecordEvent(string eventName) { _eventsThisTurn.Add(eventName); _eventsThisBattle.Add(eventName); }

    public void DealDamageToEnemy(int index, int amount, bool ignoreArmor, bool isCrit = false, int armorBreak = 0)
    {
        int actual = DealDamageToEnemy(amount, ignoreArmor, armorBreak);
        if (actual > 0) ShowEnemyDamage(actual, isCrit);
    }

    public void DealDamageToAllEnemies(int amount, bool ignoreArmor, bool isCrit = false, int armorBreak = 0)
    {
        int actual = DealDamageToEnemy(amount, ignoreArmor, armorBreak);
        if (actual > 0) ShowEnemyDamage(actual, isCrit);
    }

    public void HealPlayer(int amount) => _playerHP = Mathf.Min(playerMaxHP, _playerHP + amount);
    public void AddPlayerArmor(int amount) => _playerArmor += amount;
    public void AddActionPoints(int amount) => _actionPoints = Mathf.Max(0, _actionPoints + amount);

    public void ModifyPlayerAttribute(ModifiableAttribute attr, ModifyMethod method, int amount)
    {
        int newVal = method switch
        {
            ModifyMethod.Add => GetModAttrValue(attr) + amount,
            ModifyMethod.Subtract => GetModAttrValue(attr) - amount,
            ModifyMethod.Multiply => GetModAttrValue(attr) * amount,
            ModifyMethod.Override => amount,
            _ => GetModAttrValue(attr)
        };
        SetModAttrValue(attr, newVal);
    }

    public void ApplyStatusToEnemy(int index, StatusType status, int stacks)
    {
        if (index != 0) return;
        // 破甲直接减少护甲；流血/力量等需要状态系统
        if (status == StatusType.ArmorBreak)
            _enemyArmor = Mathf.Max(0, _enemyArmor - stacks);
    }

    public void ApplyStatusToPlayer(StatusType status, int stacks)
    {
        switch (status)
        {
            case StatusType.Strength: _playerStrength += stacks; break;
            case StatusType.Dexterity: _playerDexterity += stacks; break;
            case StatusType.CritRateBoost: _playerCritRate += stacks; break;
            case StatusType.CritDamageBoost: _playerCritDamage += stacks; break;
        }
    }

    public int RequestSelectCardFromHand(string prompt) => -1; // 简化：暂不支持运行时选牌
    public void DiscardHandCard(int index)
    {
        if (index < 0 || index >= _hand.Count) return;
        ActiveChar.discardPile.Add(_hand[index]);
        _hand.RemoveAt(index);
        RefreshHandUI();
    }

    private int GetModAttrValue(ModifiableAttribute attr) => attr switch
    {
        ModifiableAttribute.Strength => _playerStrength,
        ModifiableAttribute.Dexterity => _playerDexterity,
        ModifiableAttribute.PlayerCritRate => _playerCritRate,
        ModifiableAttribute.PlayerCritDamage => _playerCritDamage,
        ModifiableAttribute.MaxHP => playerMaxHP,
        ModifiableAttribute.CurrentHP => _playerHP,
        ModifiableAttribute.DrawPerTurn => drawPerTurn,
        ModifiableAttribute.EnergyPerTurn => maxActionPoints,
        ModifiableAttribute.CurrentSanity => _playerSanity,
        ModifiableAttribute.MaxSanity => _playerMaxSanity,
        _ => 0
    };

    private void SetModAttrValue(ModifiableAttribute attr, int value)
    {
        switch (attr)
        {
            case ModifiableAttribute.Strength: _playerStrength = value; break;
            case ModifiableAttribute.Dexterity: _playerDexterity = value; break;
            case ModifiableAttribute.PlayerCritRate: _playerCritRate = value; break;
            case ModifiableAttribute.PlayerCritDamage: _playerCritDamage = value; break;
            case ModifiableAttribute.MaxHP: playerMaxHP = value; break;
            case ModifiableAttribute.CurrentHP: _playerHP = Mathf.Clamp(value, 0, playerMaxHP); break;
            case ModifiableAttribute.DrawPerTurn: drawPerTurn = value; break;
            case ModifiableAttribute.EnergyPerTurn: maxActionPoints = value; break;
            case ModifiableAttribute.CurrentSanity: ModifySanity(value - _playerSanity); break;
            case ModifiableAttribute.MaxSanity: _playerMaxSanity = value; break;
        }
    }

    private CharBattleState ActiveChar => _chars[_activeCharIdx];
    private CharBattleState InactiveChar => _chars[1 - _activeCharIdx];

    /// <summary>
    /// 局外（ChapterManager）在进入战斗前指定的【起始】激活/未激活角色（CharacterData）。
    /// 为空则按默认：characters[0] 激活、characters[1] 未激活。
    /// 注意：本类已有的 ActiveChar/InactiveChar（CharBattleState）是战斗中“当前”角色状态，
    /// 会随切换角色变化；此处 StartActiveChar/StartInactiveChar 仅作起始指派。
    /// </summary>
    public CharacterData StartActiveChar { get; set; }
    public CharacterData StartInactiveChar { get; set; }

    // ========================================================================
    // 生命周期
    // ========================================================================

    // 战斗由 ChapterManager 在进入战斗时显式调用 BeginBattle() 启动；
    // BattleCanvas 默认禁用，故场景加载时 Start() 不会自动开战（保持空实现）。
    private void Start()
    {
        // 空实现：真正初始化放在 BeginBattle()
    }

    /// <summary>绑定一次性 UI 监听（仅执行一次）。</summary>
    private void WireListeners()
    {
        if (handLayout != null)
        {
            handLayout.SetCardClickCallback(OnCardClicked);
            handLayout.SetCardPrefabs(attackCardPrefab, skillCardPrefab, abilityCardPrefab);
        }
        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (switchCharacterButton != null)
            switchCharacterButton.onClick.AddListener(OnSwitchCharacterClicked);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
    }

    /// <summary>
    /// 由 ChapterManager 在进入战斗后调用：绑定监听（一次性），启动战斗。
    /// 每次进入战斗都应调用一次（战斗结束后会重新启用 BattleCanvas 并再次 BeginBattle）。
    /// </summary>
    public void BeginBattle()
    {
        if (!_listenersWired) { WireListeners(); _listenersWired = true; }
        _baseDrawPerTurn = drawPerTurn;   // 捕获抽牌基数（Inspector 配置），避免逐场战斗累加
        StartBattle();
    }

    private void Update()
    {
        // 测试：按 1 降低 1 点理智
        if (Input.GetKeyDown(KeyCode.Alpha1))
            ModifySanity(-1);
    }

    // ========================================================================
    // 战斗初始化
    // ========================================================================

    /// <summary>外部接口：设置本次战斗的敌人配置（不重新加载场景）。
    /// 传入非 null 时覆盖 Inspector 的默认 enemyConfig；传入 null 时复位为默认 enemyConfig。</summary>
    public void SetEnemy(EnemyConfig config)
    {
        if (config != null) enemyConfig = config;
    }

    /// <summary>从当前敌人来源（StartEnemy 优先，否则 enemyConfig 默认）初始化单敌数据。</summary>
    private void InitEnemy()
    {
        EnemyConfig cfg = StartEnemy != null ? StartEnemy : enemyConfig;
        Debug.Log($"[BattleManager] InitEnemy: StartEnemy={StartEnemy?.enemyName ?? "NULL"}, enemyConfig={enemyConfig?.enemyName ?? "NULL"}, using={cfg?.enemyName ?? "NULL"}");
        if (cfg != null)
        {
            enemyConfig = cfg;   // 关键：让技能/阶段/立绘/名字等逻辑统一读取“当前出战敌人”（可能是事件指定的 StartEnemy）
            _enemyMaxHP = cfg.maxHP;
            _enemyHP = cfg.maxHP;
            _enemyArmor = cfg.armor;
            _enemyPhase = 1;
            _gazeValue = 0;
            _turnInCycle = 0;
            _lockedCharIdx = -1;
            UpdateEnemyPortrait();
        }
        else
        {
            _enemyMaxHP = 100;
            _enemyHP = 100;
            _enemyArmor = 0;
            _enemyPhase = 1;
            _gazeValue = 0;
            _turnInCycle = 0;
            _lockedCharIdx = -1;
            Debug.LogWarning("[BattleManager] 未配置任何敌人（StartEnemy 为空且默认 enemyConfig 为空），使用默认敌人 100HP");
        }
    }

    public void StartBattle()
    {
        // 解析战斗背景 Image（Background GameObject 下，含自身与子物体）
        _backgroundImage = background != null ? background.GetComponentInChildren<Image>() : null;

        if (gameConfig == null || gameConfig.characters == null || gameConfig.characters.Count < 2)
        {
            Debug.LogError("[BattleManager] GameConfig 未配置或角色不足2个，无法开始战斗");
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            _chars[i] = new CharBattleState { data = gameConfig.characters[i] };
            BuildStartingDeck(_chars[i]);
            ShuffleDrawPile(_chars[i]);
        }

        // 起始激活角色：优先用局外传入的 StartActiveChar，否则默认 characters[0]
        int startIdx = 0;
        if (StartActiveChar != null && gameConfig.characters != null)
        {
            int idx = gameConfig.characters.IndexOf(StartActiveChar);
            if (idx >= 0) startIdx = idx;
        }
        _activeCharIdx = startIdx;
        _hasSwitchedThisBattle = false;
        _turnCount = 1;
        _playerArmor = 0;
        _sanityPhaseTriggered = false;

        // 读入持久基础属性（单局内跨战斗保留，存于 ChapterManager 运行时副本；资产 PlayerConfig 仅作初始值）
        ChapterManager cm = chapterManager != null ? chapterManager : FindObjectOfType<ChapterManager>();
        if (cm != null)
        {
            playerMaxHP = cm.PlayerMaxHP;
            _playerHP = cm.PlayerHP;
            maxActionPoints = cm.PlayerMaxActionPoints;
            _baseDrawPerTurn = cm.PlayerDrawPerTurn;
            _playerMaxSanity = cm.PlayerMaxSanity;
            _playerSanity = cm.PlayerSanity;
            _playerStrength = cm.PlayerStrength;
            _playerAgility = cm.PlayerAgility;
            _playerLifesteal = cm.PlayerLifesteal;
            _playerCritRate = cm.PlayerCritRate;
            _playerCritDamage = cm.PlayerCritDamage;
            Debug.Log($"[BattleManager] 读入持久属性(来自ChapterManager) HP:{_playerHP}/{playerMaxHP} AP:{maxActionPoints} 抽牌:{_baseDrawPerTurn} 理智:{_playerSanity}/{_playerMaxSanity} 力量:{_playerStrength} 敏捷:{_playerAgility} 吸血:{_playerLifesteal} 暴击率:{_playerCritRate} 暴伤:{_playerCritDamage}");
        }
        else if (playerConfig != null)
        {
            // 回退：直接用资产初始值（不含事件累积，仅作安全网）
            playerMaxHP = playerConfig.maxHP;
            _playerHP = playerConfig.startHP;
            maxActionPoints = playerConfig.maxActionPoints;
            _baseDrawPerTurn = playerConfig.drawPerTurn;
            _playerMaxSanity = playerConfig.maxSanity;
            _playerSanity = playerConfig.startSanity;
            _playerStrength = playerConfig.strength;
            _playerAgility = playerConfig.agility;
            _playerLifesteal = playerConfig.lifesteal;
            _playerCritRate = playerConfig.critRate;
            _playerCritDamage = playerConfig.critDamage;
            Debug.LogWarning("[BattleManager] 未找到 ChapterManager，回退读入 PlayerConfig 初始值（无跨战斗累积）");
        }
        else
        {
            _playerHP = playerMaxHP;
            _playerMaxSanity = 10;
            _playerSanity = 10;
            Debug.LogWarning("[BattleManager] 未配置 ChapterManager / PlayerConfig，持久属性为 0");
        }

        // 灵巧：每回合额外抽牌 = 基础值 + 敏捷（赋值式，避免多场战斗逐场累加）
        drawPerTurn = _baseDrawPerTurn + _playerAgility;

        // 从敌人来源（StartEnemy 或默认 enemyConfig）初始化单个敌人
        InitEnemy();
        _battleEnded = false;
        _isPlayerTurn = true;

        // 重新进入战斗时复位上一场结束状态（胜利/失败面板、按钮可用性）
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (endTurnButton != null) endTurnButton.interactable = true;
        if (switchCharacterButton != null) switchCharacterButton.interactable = true;

        _hand.Clear();
        _actionPoints = maxActionPoints;

        // 初始化效果系统
        var ctx = new BattleCardContext(this);
        _effectExecutor = new EffectExecutor(ctx);
        _abilitySystem = new AbilitySystem(_effectExecutor, ctx);
        _effectExecutor._onAbilityTrigger = (trigger) => _abilitySystem?.OnTrigger(trigger);

        DrawCards(initialDraw);

        UpdateCharacterSwitchUI();
        UpdateUI();
        ApplyBackground();   // 按初始理智设置战斗背景

        Debug.Log($"[BattleManager] 战斗开始！角色1: {ActiveChar.data?.Label}, 角色2: {InactiveChar.data?.Label}");
    }

    private void BuildStartingDeck(CharBattleState state)
    {
        var charData = state.data;
        if (charData == null) return;

        // 优先使用运行时牌库（GlobalCardLibrary，含商店购买的卡），回退到 CardEntry / startingLibrary
        var globalLib = GlobalCardLibrary.Instance;
        if (globalLib != null && globalLib.IsRegistered(charData))
        {
            foreach (var inst in globalLib.GetCards(charData))
            {
                if (inst != null && inst.template != null)
                    state.drawPile.Add(inst.template);
            }
            if (state.drawPile.Count > 0)
            {
                Debug.Log($"[BattleManager] {charData.Label} 使用运行时牌库: {state.drawPile.Count} 张");
                return;
            }
        }

        // 其次：卡牌编辑器的 CardEntry 初始牌组
        List<CardEntry> entryCards = state == _chars[0] ? character1Cards : character2Cards;
        if (entryCards != null && entryCards.Count > 0)
        {
            var cardDataList = CardEntryAdapter.ConvertToCardData(entryCards);
            foreach (var cd in cardDataList)
                state.drawPile.Add(cd);
            Debug.Log($"[BattleManager] {charData?.Label} 初始牌组(CardEntry): {state.drawPile.Count} 张");
            return;
        }

        // 回退：使用旧 CardData 初始牌库
        if (charData.startingLibrary == null) return;

        foreach (var card in charData.startingLibrary.startingCards)
        {
            if (card != null)
                state.drawPile.Add(CardEntryAdapter.ConvertSingle(card));
        }

        Debug.Log($"[BattleManager] {charData.Label} 初始牌组(CardData): {state.drawPile.Count} 张");
    }

    // ========================================================================
    // 抽牌
    // ========================================================================

    public void DrawCards(int count)
    {
        var activeChar = ActiveChar;
        for (int i = 0; i < count; i++)
        {
            if (_hand.Count >= handLimit) break;

            if (activeChar.drawPile.Count == 0)
            {
                if (activeChar.discardPile.Count == 0) break;
                activeChar.drawPile = new List<CardData>(activeChar.discardPile);
                activeChar.discardPile.Clear();
                ShuffleDrawPile(activeChar);
            }

            _hand.Add(activeChar.drawPile[0]);
            activeChar.drawPile.RemoveAt(0);
        }
        RefreshHandUI();
    }

    // ========================================================================
    // 出牌
    // ========================================================================

    private void OnCardClicked(int handIndex)
    {
        if (!_isPlayerTurn || _battleEnded) return;
        PlayCard(handIndex);
    }

    public bool PlayCard(int handIndex)
    {
        if (handIndex < 0 || handIndex >= _hand.Count) return false;
        if (!_isPlayerTurn || _battleEnded) return false;

        var card = _hand[handIndex];
        int cost = card.GetEffectiveCost();

        if (_actionPoints < cost)
            return false;

        _actionPoints -= cost;

        ApplyCardEffects(card);
        HandleCardConsumption(card);

        _hand.RemoveAt(handIndex);
        RefreshHandUI();

        UpdateUI();
        CheckBattleEnd();
        return true;
    }

    private void ApplyCardEffects(CardData card)
    {
        // 如果有关联的 CardEntry，走统一效果执行器
        if (card.sourceEntry != null)
        {
            if (_effectExecutor == null)
            {
                var ctx = new BattleCardContext(this);
                _effectExecutor = new EffectExecutor(ctx);
                _abilitySystem = new AbilitySystem(_effectExecutor, ctx);
                _effectExecutor._onAbilityTrigger = (trigger) => _abilitySystem?.OnTrigger(trigger);
            }
            var entry = card.sourceEntry;

            // 能力卡：激活能力，不执行效果列表
            if (entry.cardType == LightMiniGame.CardEditor.CardType.Ability)
            {
                var ability = entry.GetAbility(card.isUpgraded);
                if (ability != null && _abilitySystem != null)
                    _abilitySystem.Activate(ability, entry, card.isUpgraded);
                UpdateUI();
                return;
            }

            // 普通卡：执行效果列表
            var effects = card.GetEffects(card.isUpgraded);
            _effectExecutor.ExecuteEffects(effects, entry, card.isUpgraded);
            UpdateUI();
            CheckBattleEnd();
            return;
        }

        // 回退：旧路径（无 CardEntry 的 CardData）
        switch (card.cardType)
        {
            case CardType.Attack: ApplyAttackCard(card); break;
            case CardType.Skill: ApplyArmorCard(card); break;
            case CardType.Ability: ApplyBuffCard(card); break;
        }
    }

    private void ApplyAttackCard(CardData card)
    {
        int baseDamage = card.attackValue;
        if (card.attackValueType == ValueType.AttributeBased)
            baseDamage += GetAttributeValue(card.attackAttribute);

        int attackCount = card.attackCount;
        bool ignoreArmor = card.ignoreArmor;

        int totalDamageDealt = 0;
        for (int i = 0; i < attackCount; i++)
        {
            totalDamageDealt += DealDamageToEnemy(baseDamage, ignoreArmor);
        }

        if (totalDamageDealt > 0)
            ShowEnemyDamage(totalDamageDealt);
    }

    private void ApplyArmorCard(CardData card)
    {
        int armor = card.armorValue;
        if (card.armorValueType == ValueType.AttributeBased)
            armor += GetAttributeValue(card.armorAttribute);

        _playerArmor += armor;
    }

    private void ApplyBuffCard(CardData card)
    {
        foreach (var effect in card.buffEffects)
        {
            int totalValue = effect.value * card.buffStacks;
            switch (effect.effectType)
            {
                case BuffEffectType.IncreaseAttribute:
                    IncreaseAttribute(effect.targetAttribute, totalValue);
                    break;
                case BuffEffectType.RestoreActionPoints:
                    _actionPoints += totalValue;
                    break;
                case BuffEffectType.DrawCards:
                    DrawCards(totalValue);
                    break;
                case BuffEffectType.GainArmor:
                    _playerArmor += totalValue;
                    break;
                case BuffEffectType.HealHP:
                    _playerHP = Mathf.Min(playerMaxHP, _playerHP + totalValue);
                    break;
            }
        }
    }

    private void HandleCardConsumption(CardData card)
    {
        switch (card.consumeType)
        {
            case ConsumeType.None:
                ActiveChar.discardPile.Add(card);
                break;
            case ConsumeType.ThisBattle:
            case ConsumeType.ThisRun:
                ActiveChar.consumedPile.Add(card);
                break;
        }
    }

    // ========================================================================
    // 伤害计算
    // ========================================================================

    private int DealDamageToEnemy(int damage, bool ignoreArmor, int armorBreak = 0)
    {
        int actualDamage = 0;

        // 破甲：额外X点伤害直接扣血，无视护甲
        int pierce = Mathf.Max(0, armorBreak);
        if (pierce > 0)
        {
            _enemyHP -= pierce;
            if (_enemyHP < 0) _enemyHP = 0;
            actualDamage += pierce;
        }

        // 基础伤害走护甲
        if (!ignoreArmor && _enemyArmor > 0 && damage > 0)
        {
            int absorbed = Mathf.Min(_enemyArmor, damage);
            _enemyArmor -= absorbed;
            damage -= absorbed;
        }

        if (damage > 0)
        {
            _enemyHP -= damage;
            if (_enemyHP < 0) _enemyHP = 0;
            actualDamage += damage;
        }

        return actualDamage;
    }

    private void DealDamageToPlayer(int damage)
    {
        int actualDamage = damage;
        if (_playerArmor > 0)
        {
            int absorbed = Mathf.Min(_playerArmor, damage);
            _playerArmor -= absorbed;
            actualDamage -= absorbed;
        }
        _playerHP -= actualDamage;
        if (_playerHP < 0) _playerHP = 0;
    }

    /// <summary>
    /// 修改理智值。delta 为正则恢复，为正则降低。降至 0 以下时置为 0。
    /// 当理智从 >阈值 降至 ≤阈值 时触发转阶段（每场战斗仅触发一次）。
    /// </summary>
    public void ModifySanity(int delta)
    {
        if (delta == 0) return;
        int prev = _playerSanity;
        _playerSanity = Mathf.Clamp(_playerSanity + delta, 0, _playerMaxSanity);

        // 降至阈值 → 触发黑暗阶段
        if (!_sanityPhaseTriggered && prev > SanityPhaseThreshold && _playerSanity <= SanityPhaseThreshold)
        {
            _sanityPhaseTriggered = true;
            OnSanityPhaseTransition();
        }

        // 理智变化时检查敌人阶段切换
        CheckEnemyPhaseSwitch();

        UpdateUI();
        ApplyBackground();   // 理智变化实时切换背景
    }

    /// <summary>理智转阶段钩子（理智降至阈值 4 时触发，每场战斗仅一次）</summary>
    protected virtual void OnSanityPhaseTransition()
    {
        Debug.Log($"[BattleManager] 理智转阶段触发！理智 {_playerSanity}/{_playerMaxSanity}（阈值 {SanityPhaseThreshold}）");
        _isDarkMode = true;

        // 1. 升级所有卡牌效果 + 施加侵蚀词条
        UpgradeAllCardsForDarkMode();

        // 2. 切换手牌为黑暗卡面
        if (handLayout != null)
            handLayout.SetDarkMode(true);

        // 3. 全屏暗色遮罩淡入
        if (darkOverlay != null)
            StartCoroutine(DarkOverlayFadeRoutine());

        // 4. 理智条颤抖效果
        if (sanityBar != null)
        {
            if (_sanityTrembleRoutine != null) StopCoroutine(_sanityTrembleRoutine);
            _sanityTrembleRoutine = StartCoroutine(SanityTrembleRoutine());
        }

        UpdateUI();
    }

    /// <summary>
    /// 升级所有牌堆中的卡牌效果（使用每张牌配置的升级数据），并施加灾厄词条。
    /// 涵盖手牌、两角色抽牌堆、弃牌堆、消耗堆。
    /// </summary>
    private void UpgradeAllCardsForDarkMode()
    {
        // 手牌
        foreach (var card in _hand)
            UpgradeSingleCard(card);

        // 双角色的牌堆
        for (int ci = 0; ci < 2; ci++)
        {
            if (_chars[ci] == null) continue;
            foreach (var card in _chars[ci].drawPile)
                UpgradeSingleCard(card);
            foreach (var card in _chars[ci].discardPile)
                UpgradeSingleCard(card);
            foreach (var card in _chars[ci].consumedPile)
                UpgradeSingleCard(card);
        }

        // 刷新手牌 UI（重新应用数据以显示升级后的描述）
        RefreshHandUI();
        Debug.Log("[BattleManager] 所有卡牌已升级并施加灾厄词条");
    }

    /// <summary>
    /// 单张卡牌升级：仅设置 isUpgraded 标记并施加灾厄词条。
    /// 升级后的效果由 EffectExecutor 通过 card.GetEffects(true) 自动读取 CardEntry.upgradeEffects，
    /// 无需写回 CardData flat fields。
    /// </summary>
    private void UpgradeSingleCard(CardData card)
    {
        if (card == null) return;
        if (card.isUpgraded) return;

        card.isUpgraded = true;
        card.keywords |= KeywordType.Calamity;
    }

    /// <summary>全屏暗色遮罩淡入协程</summary>
    private IEnumerator DarkOverlayFadeRoutine()
    {
        darkOverlay.gameObject.SetActive(true);
        Color target = new Color(0.05f, 0.02f, 0.08f, darkOverlayAlpha);
        Color start = darkOverlay.color;
        start.a = 0f;
        darkOverlay.color = start;

        float elapsed = 0f;
        while (elapsed < darkOverlayFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / darkOverlayFadeDuration;
            darkOverlay.color = Color.Lerp(start, target, t);
            yield return null;
        }
        darkOverlay.color = target;
    }

    /// <summary>理智条颤抖效果：持续在原始位置上做小幅随机偏移，理智恢复后停止</summary>
    private IEnumerator SanityTrembleRoutine()
    {
        var rt = sanityBar.GetComponent<RectTransform>();
        Vector2 basePos = rt.anchoredPosition;
        float intensity = 3f;
        var fill = sanityBar.fillRect?.GetComponent<UnityEngine.UI.Image>();

        while (_playerSanity <= SanityPhaseThreshold && !_battleEnded)
        {
            float ox = Random.Range(-intensity, intensity);
            float oy = Random.Range(-intensity, intensity);
            rt.anchoredPosition = basePos + new Vector2(ox, oy);

            if (fill != null)
            {
                Color darkTint = new Color(0.5f, 0.3f, 0.6f, 1f);
                fill.color = Color.Lerp(fill.color, darkTint, 0.1f);
            }

            yield return null;
        }

        rt.anchoredPosition = basePos;
        if (fill != null) fill.color = new Color(0.3f, 0.5f, 0.85f, 1f);
        _sanityTrembleRoutine = null;
    }

    /// <summary>
    /// 在敌人右侧显示伤害数字并飘起消失。支持多段同时显示（每次调用独立飘字）。
    /// </summary>
    private void ShowEnemyDamage(int amount, bool isCrit = false)
    {
        if (amount <= 0) return;

        GameObject popupObj = null;
        TextMeshProUGUI popupText = null;
        RectTransform popupRect = null;

        if (damagePopupPrefab != null)
        {
            popupObj = Instantiate(damagePopupPrefab, transform);
            popupText = popupObj.GetComponentInChildren<TextMeshProUGUI>();
            popupRect = popupObj.GetComponent<RectTransform>();
            if (popupRect == null && popupText != null)
                popupRect = popupText.GetComponent<RectTransform>();
        }
        else if (enemyDamageText != null)
        {
            popupObj = enemyDamageText.gameObject;
            popupText = enemyDamageText;
            popupRect = enemyDamageText.GetComponent<RectTransform>();
            if (_damagePopupRoutine != null) StopCoroutine(_damagePopupRoutine);
        }

        if (popupText == null || popupRect == null) return;

        float offsetX = Random.Range(-20f, 20f);
        Vector2 startPos = new Vector2(150 + offsetX, -20);

        popupObj.SetActive(true);
        popupText.text = isCrit ? $"{amount}!" : amount.ToString();
        popupText.color = isCrit ? new Color(1f, 0.85f, 0.1f, 1f) : new Color(1f, 0.3f, 0.2f, 1f);
        popupText.fontSize = isCrit ? 28 : 20;
        popupRect.anchoredPosition = startPos;

        _damagePopupRoutine = StartCoroutine(DamagePopupRoutine(popupRect, popupText, startPos, popupObj));
    }

    private IEnumerator DamagePopupRoutine(RectTransform rt, TextMeshProUGUI text, Vector2 startPos, GameObject obj)
    {
        float elapsed = 0f;
        float duration = 0.9f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            rt.anchoredPosition = startPos + new Vector2(0, 80f * t);
            text.color = new Color(1f, 0.3f, 0.2f, 1f - t);
            yield return null;
        }

        if (damagePopupPrefab != null && obj != enemyDamageText?.gameObject)
            Destroy(obj);
        else
            obj.SetActive(false);
    }

    // ========================================================================
    // 属性
    // ========================================================================

    private int GetAttributeValue(PlayerAttributeType attr) => attr switch
    {
        PlayerAttributeType.Strength => _playerStrength,
        PlayerAttributeType.Dexterity => _playerDexterity,
        _ => 0
    };

    private void IncreaseAttribute(PlayerAttributeType attr, int value)
    {
        switch (attr)
        {
            case PlayerAttributeType.Strength: _playerStrength += value; break;
            case PlayerAttributeType.Dexterity: _playerDexterity += value; break;
            case PlayerAttributeType.Vitality: playerMaxHP += value; _playerHP += value; break;
            case PlayerAttributeType.Agility: drawPerTurn += value; break;
        }
    }

    private bool IsCardPlayable(CardData card)
    {
        if (card == null) return false;
        return _actionPoints >= card.GetEffectiveCost();
    }

    // ========================================================================
    // 角色切换
    // ========================================================================

    private void OnSwitchCharacterClicked()
    {
        if (!_isPlayerTurn || _battleEnded) return;
        if (_hasSwitchedThisBattle)
        {
            Debug.Log("[BattleManager] 本场战斗已切换过角色");
            return;
        }
        SwitchCharacter();
    }

    private void SwitchCharacter()
    {
        var oldChar = ActiveChar;
        foreach (var card in _hand)
            oldChar.discardPile.Add(card);
        _hand.Clear();

        // 挂起当前角色的能力
        _abilitySystem?.SuspendAll();

        _activeCharIdx = 1 - _activeCharIdx;
        _hasSwitchedThisBattle = true;

        // 恢复切换后角色的能力
        _abilitySystem?.ResumeAll();

        DrawCards(drawPerTurn);

        UpdateCharacterSwitchUI();
        UpdateUI();

        Debug.Log($"[BattleManager] 切换到角色: {ActiveChar.data?.Label}，抽{drawPerTurn}张牌");
    }

    private void UpdateCharacterSwitchUI()
    {
        if (ActiveChar?.data != null)
        {
            if (activeCharNameText != null)
                activeCharNameText.text = ActiveChar.data.displayName;
            if (activeCharPortrait != null && ActiveChar.data.avatar != null)
                activeCharPortrait.sprite = ActiveChar.data.avatar;
        }
        if (InactiveChar?.data != null)
        {
            if (inactiveCharNameText != null)
                inactiveCharNameText.text = InactiveChar.data.displayName;
            if (inactiveCharPortrait != null && InactiveChar.data.avatar != null)
                inactiveCharPortrait.sprite = InactiveChar.data.avatar;
        }

        bool canSwitch = _isPlayerTurn && !_battleEnded && !_hasSwitchedThisBattle;
        if (switchCharacterButton != null)
            switchCharacterButton.interactable = canSwitch;
        if (switchAvailableIndicator != null)
            switchAvailableIndicator.SetActive(canSwitch);
        if (switchUsedIndicator != null)
            switchUsedIndicator.SetActive(_hasSwitchedThisBattle);
    }

    // ========================================================================
    // 设置面板
    // ========================================================================

    private void OnSettingsClicked()
    {
        if (_settingsPanel == null && settingsPanelPrefab != null)
        {
            var go = Instantiate(settingsPanelPrefab, transform.parent);
            _settingsPanel = go.GetComponent<SettingsPanelUI>();
        }
        _settingsPanel?.Show();
    }

    /// <summary>
    /// QuitButton 回调：把战斗后的玩家属性写回局外系统（ChapterManager），
    /// 并通知战斗结束（由 ChapterManager 切回 BookCanvas 并推进章节）。
    /// </summary>
    private void OnQuitClicked()
    {
        var cm = chapterManager != null ? chapterManager : FindObjectOfType<ChapterManager>();
        if (cm != null)
        {
            cm.ApplyBattleResult(
                _playerHP, playerMaxHP,
                _playerSanity, _playerMaxSanity,
                _playerStrength, _playerAgility, _playerLifesteal,
                _playerCritRate, _playerCritDamage,
                maxActionPoints, drawPerTurn,
                ActiveChar?.data, InactiveChar?.data);   // 把战斗结束时的激活/未激活角色同步回局外
        }
        OnBattleEnded?.Invoke();
    }

    // ========================================================================
    // 回合流程
    // ========================================================================

    private void OnEndTurnClicked()
    {
        if (!_isPlayerTurn || _battleEnded) return;
        EndPlayerTurn();
    }

    private void EndPlayerTurn()
    {
        _isPlayerTurn = false;

        var activeChar = ActiveChar;
        foreach (var card in _hand)
            activeChar.discardPile.Add(card);
        _hand.Clear();
        RefreshHandUI();

        _actionPoints = 0;

        if (endTurnButton != null) endTurnButton.interactable = false;
        if (switchCharacterButton != null) switchCharacterButton.interactable = false;
        UpdateCharacterSwitchUI();

        // 回合结束时触发能力
        _abilitySystem?.OnTrigger(AbilityTrigger.TurnEnd);

        StartEnemyTurn();
    }

    private void StartEnemyTurn()
    {
        _waitingEnemyConfirm = false;
        if (phaseHintText != null)
            phaseHintText.text = "敌人回合";
        if (enemyIntentText != null)
            enemyIntentText.text = "敌人正在行动...";
        UpdateUI();
        ExecuteEnemyAction();
    }

    private void ExecuteEnemyAction()
    {
        if (phaseHintText != null)
            phaseHintText.text = "";

        if (enemyIntentText != null)
            enemyIntentText.text = "敌人正在行动...";

        if (_enemyHP > 0 && !_battleEnded)
        {
            // 检查阶段切换
            CheckEnemyPhaseSwitch();

            EnemySkill skill = GetCurrentEnemySkill();
            if (skill != null)
            {
                StartCoroutine(ExecuteEnemySkillCoroutine(skill));
            }
            else
            {
                // 无技能配置，走旧逻辑（固定伤害）
                DealDamageToPlayer(5);
                UpdateUI();
                if (_playerHP <= 0) { EndBattle(false); return; }
                StartPlayerTurn();
            }
        }
        else
        {
            StartPlayerTurn();
        }
    }

    /// <summary>获取当前回合应执行的敌人技能</summary>
    private EnemySkill GetCurrentEnemySkill()
    {
        if (enemyConfig == null) return null;

        if (_enemyPhase == 1)
        {
            if (enemyConfig.phase1Skills == null || enemyConfig.phase1Skills.Count == 0) return null;
            return enemyConfig.phase1Skills[_turnInCycle % enemyConfig.phase1Skills.Count];
        }
        else
        {
            // 阶段2：凝视值满时触发特殊技能
            if (_gazeValue >= enemyConfig.gazeMaxValue && enemyConfig.phase2GazeSkill.skillName != null)
                return enemyConfig.phase2GazeSkill;

            // 否则执行常规技能（如果有多个则循环）
            if (enemyConfig.phase2Skills == null || enemyConfig.phase2Skills.Count == 0) return null;
            return enemyConfig.phase2Skills[_turnInCycle % enemyConfig.phase2Skills.Count];
        }
    }

    /// <summary>敌人技能执行协程：显示卡面 → 执行效果 → 隐藏卡面 → 检查战斗结束</summary>
    private IEnumerator ExecuteEnemySkillCoroutine(EnemySkill skill)
    {
        // 显示技能卡面
        if (enemySkillCard != null)
        {
            enemySkillCard.SetActive(true);
            if (enemySkillCardImage != null)
            {
                enemySkillCardImage.sprite = skill.skillCardArt;
                enemySkillCardImage.color = skill.skillCardArt != null
                    ? new Color(1, 1, 1, 1f)
                    : new Color(0.05f, 0.03f, 0.1f, 0.95f);
            }
            if (enemySkillNameText != null)
            {
                enemySkillNameText.text = skill.skillName;
                enemySkillNameText.color = new Color(1f, 0.95f, 0.7f, 1f);
            }
            if (enemySkillDescText != null)
            {
                enemySkillDescText.text = skill.description;
                enemySkillDescText.color = new Color(0.9f, 0.85f, 0.95f, 1f);
            }

            yield return StartCoroutine(FadeEnemySkillCard(0f, 1f, enemySkillCardFadeTime));
            yield return new WaitForSeconds(enemySkillCardDuration);
            yield return StartCoroutine(FadeEnemySkillCard(1f, 0f, enemySkillCardFadeTime));
            enemySkillCard.SetActive(false);
        }
        else
        {
            // 无技能卡面板时，用意图文本显示技能名并等待
            if (enemyIntentText != null)
                enemyIntentText.text = $"【{skill.skillName}】{skill.description}";
            yield return new WaitForSeconds(2f);
        }

        // 执行技能效果
        ExecuteEnemySkill(skill);

        UpdateUI();
        if (_playerHP <= 0) { EndBattle(false); yield break; }

        StartPlayerTurn();
    }

    /// <summary>执行单个敌人技能的实际效果</summary>
    private void ExecuteEnemySkill(EnemySkill skill)
    {
        if (skill == null) return;

        // 锁定角色
        if (skill.lockCharacter)
            _lockedCharIdx = _activeCharIdx;

        // 命中判定（光束命中：只有锁定角色未切换才生效）
        bool hitsResolved = true;
        if (skill.hitsLockedCharacter && _lockedCharIdx >= 0)
        {
            hitsResolved = (_lockedCharIdx == _activeCharIdx);
            _lockedCharIdx = -1; // 消耗锁定
        }

        if (hitsResolved)
        {
            if (skill.damage > 0)
                DealDamageToPlayer(skill.damage);
            if (skill.sanityReduction != 0)
                ModifySanity(skill.sanityReduction);
            if (skill.strengthReduction != 0)
                _playerStrength = Mathf.Max(0, _playerStrength + skill.strengthReduction);
        }

        // 凝视值变化
        if (skill.resetGaze)
            _gazeValue = 0;
        else if (skill.gazeChange != 0)
            _gazeValue = Mathf.Max(0, _gazeValue + skill.gazeChange);

        // 推进循环计数
        _turnInCycle++;
    }

    /// <summary>检查并执行敌人阶段切换</summary>
    private void CheckEnemyPhaseSwitch()
    {
        if (enemyConfig == null) return;

        bool sanityLow = _playerSanity <= enemyConfig.phase2SanityThreshold;

        // 阶段1→2：阶段1血量打完 或 理智低于阈值
        if (_enemyPhase == 1 && (_enemyHP <= 0 || sanityLow))
        {
            _enemyPhase = 2;
            _turnInCycle = 0;
            _enemyMaxHP = enemyConfig.phase2MaxHP > 0 ? enemyConfig.phase2MaxHP : _enemyMaxHP;
            _enemyHP = _enemyMaxHP;
            _enemyArmor = 0;
            UpdateEnemyPortrait();
            Debug.Log($"[BattleManager] 敌人进入阶段2「睁眼」 HP重置为 {_enemyHP}/{_enemyMaxHP}");
        }
        // 阶段2→1：理智恢复且阶段1还有血
        else if (_enemyPhase == 2 && !sanityLow && enemyConfig.phase2MaxHP > 0)
        {
            _enemyPhase = 1;
            _turnInCycle = 0;
            _enemyMaxHP = enemyConfig.maxHP;
            _enemyHP = enemyConfig.maxHP;
            UpdateEnemyPortrait();
            Debug.Log($"[BattleManager] 敌人回到阶段1「注视」 HP重置为 {_enemyHP}/{_enemyMaxHP}");
        }
    }

    /// <summary>根据当前阶段更新敌人立绘</summary>
    private void UpdateEnemyPortrait()
    {
        if (enemyPortraitImage == null || enemyConfig == null) return;
        var portrait = _enemyPhase == 1 ? enemyConfig.phase1Portrait : enemyConfig.phase2Portrait;
        if (portrait != null)
        {
            enemyPortraitImage.sprite = portrait;
            enemyPortraitImage.color = Color.white;
        }
        else
        {
            enemyPortraitImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        }
    }

    /// <summary>
    /// 根据当前玩家理智值切换战斗背景：
    /// 理智 &lt;= StartBackgroundSanityThreshold 且配置了低理智背景时，显示 lowSanityBattleBackground，
    /// 否则显示 normalBattleBackground。两者都为空则保持 Background 上既有 Sprite。
    /// </summary>
    private void ApplyBackground()
    {
        if (_backgroundImage == null) return;
        Sprite bg = (_playerSanity <= StartBackgroundSanityThreshold && StartLowSanityBattleBackground != null)
            ? StartLowSanityBattleBackground
            : StartNormalBattleBackground;
        if (bg != null)
            _backgroundImage.sprite = bg;
    }

    /// <summary>技能卡面淡入/淡出</summary>
    private IEnumerator FadeEnemySkillCard(float fromAlpha, float toAlpha, float duration)
    {
        if (enemySkillCardImage == null && enemySkillNameText == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float alpha = Mathf.Lerp(fromAlpha, toAlpha, t);

            if (enemySkillCardImage != null)
            {
                var c = enemySkillCardImage.color; c.a = alpha; enemySkillCardImage.color = c;
            }
            if (enemySkillNameText != null)
            {
                var c = enemySkillNameText.color; c.a = alpha; enemySkillNameText.color = c;
            }
            if (enemySkillDescText != null)
            {
                var c = enemySkillDescText.color; c.a = alpha; enemySkillDescText.color = c;
            }
            yield return null;
        }
    }

    private void StartPlayerTurn()
    {
        _turnCount++;
        _actionPoints = maxActionPoints;
        _playerArmor = 0;
        _eventsThisTurn.Clear(); // 清除本回合事件
        _abilitySystem?.OnTurnStart(); // 重置能力本回合触发计数
        _abilitySystem?.OnTrigger(AbilityTrigger.TurnStart); // 回合开始触发

        DrawCards(drawPerTurn);
        _isPlayerTurn = true;

        if (endTurnButton != null) endTurnButton.interactable = true;
        UpdateCharacterSwitchUI();
        UpdateUI();

        Debug.Log($"[BattleManager] 回合 {_turnCount} 开始，当前角色: {ActiveChar.data?.Label}");
    }

    // ========================================================================
    // 战斗结束
    // ========================================================================

    private void CheckBattleEnd()
    {
        if (_enemyHP <= 0)
        {
            // 阶段1血量打完：如果有阶段2，先切换而不是胜利
            if (_enemyPhase == 1 && enemyConfig != null && enemyConfig.phase2MaxHP > 0)
            {
                CheckEnemyPhaseSwitch();
            }
            else
            {
                EndBattle(true);
            }
        }
        else if (_playerHP <= 0) EndBattle(false);
    }

    private void EndBattle(bool victory)
    {
        _battleEnded = true;
        _isPlayerTurn = false;
        _waitingEnemyConfirm = false;
        if (_sanityTrembleRoutine != null) { StopCoroutine(_sanityTrembleRoutine); _sanityTrembleRoutine = null; }
        if (victoryPanel != null) victoryPanel.SetActive(victory);
        if (defeatPanel != null) defeatPanel.SetActive(!victory);
        if (endTurnButton != null) endTurnButton.interactable = false;
        if (switchCharacterButton != null) switchCharacterButton.interactable = false;
        if (phaseHintText != null) phaseHintText.text = "";
        Debug.Log(victory ? "[BattleManager] 战斗胜利！" : "[BattleManager] 战斗失败！");
    }

    // ========================================================================
    // 牌堆操作
    // ========================================================================

    private void ShuffleDrawPile(CharBattleState state)
    {
        for (int i = state.drawPile.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (state.drawPile[i], state.drawPile[j]) = (state.drawPile[j], state.drawPile[i]);
        }
    }

    // ========================================================================
    // UI 更新
    // ========================================================================

    private void RefreshHandUI()
    {
        if (handLayout != null)
            handLayout.UpdateHand(_hand, IsCardPlayable);
    }

    /// <summary>获取敌人意图文本（预览下一回合技能）</summary>
    private string GetEnemyIntentText()
    {
        if (enemyConfig == null) return "造成5伤害";
        var skill = GetCurrentEnemySkill();
        if (skill == null) return "...";
        return skill.skillName;
    }

    private void UpdateUI()
    {
        if (hpText != null) hpText.text = $"{_playerHP}/{playerMaxHP}";
        if (actionPointText != null) actionPointText.text = _actionPoints.ToString();
        if (armorText != null) armorText.text = _playerArmor > 0 ? $"护甲: {_playerArmor}" : "";
        if (enemyHPText != null) enemyHPText.text = $"{_enemyHP}/{_enemyMaxHP}";
        if (enemyArmorText != null) enemyArmorText.text = _enemyArmor > 0 ? $"护甲: {_enemyArmor}" : "";
        if (enemyNameText != null) enemyNameText.text = enemyConfig != null ? enemyConfig.enemyName : "敌人";
        if (enemyIntentText != null && !_battleEnded && !_waitingEnemyConfirm)
            enemyIntentText.text = _isPlayerTurn ? GetEnemyIntentText() : "";

        if (strengthText != null) strengthText.text = _playerStrength > 0 ? $"力量: {_playerStrength}" : "";
        if (dexterityText != null) dexterityText.text = _playerDexterity > 0 ? $"敏捷: {_playerDexterity}" : "";

        if (playerHPBar != null)
        {
            playerHPBar.maxValue = playerMaxHP;
            playerHPBar.value = _playerHP;
        }
        if (enemyHPBar != null)
        {
            enemyHPBar.maxValue = _enemyMaxHP;
            enemyHPBar.value = _enemyHP;
        }

        if (sanityText != null) sanityText.text = $"{_playerSanity}/{_playerMaxSanity}";
        if (sanityBar != null)
        {
            sanityBar.maxValue = _playerMaxSanity;
            sanityBar.value = _playerSanity;
        }

        if (gazeValueText != null)
        {
            if (enemyConfig != null && enemyConfig.gazeMaxValue > 0)
            {
                gazeValueText.gameObject.SetActive(true);
                gazeValueText.text = $"凝视 {_gazeValue}/{enemyConfig.gazeMaxValue}";
            }
            else
            {
                gazeValueText.gameObject.SetActive(false);
            }
        }

        if (handLayout != null)
            handLayout.RefreshPlayable(IsCardPlayable);
    }
}
