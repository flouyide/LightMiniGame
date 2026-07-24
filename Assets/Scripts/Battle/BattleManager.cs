using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using LightMiniGame.Card;
using LightMiniGame.CardEditor;

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

    [Header("敌人属性")]
    [SerializeField] private int enemyMaxHP = 100;
    [SerializeField] private int enemyArmor = 0;
    [SerializeField] private int enemyAttackDamage = 5;
    [SerializeField] private string enemyName = "精英1";

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
    [SerializeField] private Image playerHPBarFill;

    [Header("UI引用 - 理智")]
    [SerializeField] private TextMeshProUGUI sanityText;
    [SerializeField] private Image sanityBarFill;

    [Header("UI引用 - 敌人")]
    [SerializeField] private TextMeshProUGUI enemyHPText;
    [SerializeField] private TextMeshProUGUI enemyArmorText;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI enemyIntentText;
    [SerializeField] private TextMeshProUGUI enemyDamageText;
    [SerializeField] private Image enemyHPBarFill;

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
    private bool _hasSwitchedThisTurn = false;

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
    private readonly Dictionary<string, int> _customData = new();
    private readonly HashSet<string> _eventsThisTurn = new();
    private readonly HashSet<string> _eventsThisBattle = new();
    private readonly Dictionary<string, int> _turnCounters = new();
    private readonly Dictionary<string, int> _battleCounters = new();
    private int _selectedEnemyIndex = 0;
    private bool _sanityPhaseTriggered;  // 理智转阶段是否已触发（防止重复触发）
    private const int SanityPhaseThreshold = 4;  // 理智转阶段阈值
    private int _baseDrawPerTurn;   // 每场战斗前的抽牌基数（来自 Inspector 的 drawPerTurn，开局捕获一次）
    private int _actionPoints;
    private int _enemyHP;
    private int _enemyArmor;
    private int _turnCount = 1;
    private bool _isPlayerTurn = true;
    private bool _battleEnded = false;
    private bool _waitingEnemyConfirm = false;
    private bool _isDarkMode = false;
    private Coroutine _sanityTrembleRoutine;

    private SettingsPanelUI _settingsPanel;

    public bool IsPlayerTurn => _isPlayerTurn && !_battleEnded;

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
    public int EnemyCount => 1; // 当前只有一个敌人
    public int SelectedEnemyIndex => _selectedEnemyIndex;
    public int HandCount => _hand.Count;
    public int DrawPileCount => ActiveChar?.drawPile.Count ?? 0;
    public int DiscardPileCount => ActiveChar?.discardPile.Count ?? 0;

    public int GetEnemyHP(int index) => index == 0 ? _enemyHP : 0;
    public int GetEnemyArmor(int index) => index == 0 ? _enemyArmor : 0;
    public int GetEnemyBleed(int index) => 0;
    public int GetEnemyArmorBreak(int index) => 0;

    public int GetTurnCounter(string name) => _turnCounters.TryGetValue(name, out var v) ? v : 0;
    public int GetBattleCounter(string name) => _battleCounters.TryGetValue(name, out var v) ? v : 0;

    public int GetCustomData(string key) => _customData.TryGetValue(key, out var v) ? v : 0;
    public void SetCustomData(string key, int value) => _customData[key] = value;
    public void ModifyCustomData(string key, int delta) => _customData[key] = GetCustomData(key) + delta;

    public bool HasEventOccurred(string eventName) => _eventsThisTurn.Contains(eventName) || _eventsThisBattle.Contains(eventName);
    public void RecordEvent(string eventName) { _eventsThisTurn.Add(eventName); _eventsThisBattle.Add(eventName); }

    public void DealDamageToEnemy(int index, int amount, bool ignoreArmor)
    {
        if (index < 0 || index >= EnemyCount) return;
        int actual = DealDamageToEnemy(amount, ignoreArmor);
        if (actual > 0) ShowEnemyDamage(actual);
    }

    public void DealDamageToAllEnemies(int amount, bool ignoreArmor)
    {
        int actual = DealDamageToEnemy(amount, ignoreArmor);
        if (actual > 0) ShowEnemyDamage(actual);
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
        }
    }

    private CharBattleState ActiveChar => _chars[_activeCharIdx];
    private CharBattleState InactiveChar => _chars[1 - _activeCharIdx];

    // ========================================================================
    // 生命周期
    // ========================================================================

    private void Start()
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

        _baseDrawPerTurn = drawPerTurn;   // 捕获抽牌基数（Inspector 配置），避免逐场战斗累加
        StartBattle();
    }

    private void Update()
    {
        if (_waitingEnemyConfirm && Input.GetKeyDown(KeyCode.P))
            ExecuteEnemyAction();

        // 测试：按 1 降低 1 点理智
        if (Input.GetKeyDown(KeyCode.Alpha1))
            ModifySanity(-1);
    }

    // ========================================================================
    // 战斗初始化
    // ========================================================================

    public void StartBattle()
    {
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

        _activeCharIdx = 0;
        _hasSwitchedThisTurn = false;
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

        _enemyHP = enemyMaxHP;
        _enemyArmor = enemyArmor;
        _battleEnded = false;
        _isPlayerTurn = true;

        _hand.Clear();
        _actionPoints = maxActionPoints;
        DrawCards(initialDraw);

        UpdateCharacterSwitchUI();
        UpdateUI();

        Debug.Log($"[BattleManager] 战斗开始！角色1: {ActiveChar.data?.Label}, 角色2: {InactiveChar.data?.Label}");
    }

    private void BuildStartingDeck(CharBattleState state)
    {
        var charData = state.data;

        // 优先使用卡牌编辑器的 CardEntry 初始牌组
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
        if (charData == null || charData.startingLibrary == null) return;

        foreach (var card in charData.startingLibrary.startingCards)
        {
            if (card != null)
                state.drawPile.Add(card);
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
                _effectExecutor = new EffectExecutor(new BattleCardContext(this));
            var effects = card.GetEffects(card.isUpgraded);
            _effectExecutor.ExecuteEffects(effects, card.sourceEntry, card.isUpgraded);
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

    private int DealDamageToEnemy(int damage, bool ignoreArmor)
    {
        int actualDamage = damage;
        if (!ignoreArmor && _enemyArmor > 0)
        {
            int absorbed = Mathf.Min(_enemyArmor, damage);
            _enemyArmor -= absorbed;
            actualDamage -= absorbed;
        }
        _enemyHP -= actualDamage;
        if (_enemyHP < 0) _enemyHP = 0;
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

        UpdateUI();
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
        if (sanityBarFill != null)
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
        var rt = sanityBarFill.GetComponent<RectTransform>();
        Vector2 basePos = rt.anchoredPosition;
        float intensity = 3f;  // 颤抖幅度（像素）

        while (_playerSanity <= SanityPhaseThreshold && !_battleEnded)
        {
            float ox = Random.Range(-intensity, intensity);
            float oy = Random.Range(-intensity, intensity);
            rt.anchoredPosition = basePos + new Vector2(ox, oy);

            // 变暗效果
            Color darkTint = new Color(0.5f, 0.3f, 0.6f, 1f);
            sanityBarFill.color = Color.Lerp(sanityBarFill.color, darkTint, 0.1f);

            yield return null;
        }

        // 恢复
        rt.anchoredPosition = basePos;
        sanityBarFill.color = new Color(0.3f, 0.5f, 0.85f, 1f);
        _sanityTrembleRoutine = null;
    }

    /// <summary>
    /// 在敌人右侧显示伤害数字并飘起消失
    /// </summary>
    private void ShowEnemyDamage(int amount)
    {
        if (enemyDamageText == null) return;
        enemyDamageText.gameObject.SetActive(true);
        enemyDamageText.text = amount.ToString();
        enemyDamageText.color = new Color(1f, 0.3f, 0.2f, 1f);
        var rt = enemyDamageText.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(150, -20);
        StopCoroutine(nameof(DamagePopupRoutine));
        StartCoroutine(DamagePopupRoutine());
    }

    private IEnumerator DamagePopupRoutine()
    {
        var rt = enemyDamageText.GetComponent<RectTransform>();
        float elapsed = 0f;
        float duration = 0.9f;
        Vector2 startPos = new Vector2(150, -20);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            rt.anchoredPosition = startPos + new Vector2(0, 80f * t);
            enemyDamageText.color = new Color(1f, 0.3f, 0.2f, 1f - t);
            yield return null;
        }
        enemyDamageText.gameObject.SetActive(false);
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
        if (_hasSwitchedThisTurn)
        {
            Debug.Log("[BattleManager] 本回合已切换过角色");
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

        _activeCharIdx = 1 - _activeCharIdx;
        _hasSwitchedThisTurn = true;

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

        bool canSwitch = _isPlayerTurn && !_battleEnded && !_hasSwitchedThisTurn;
        if (switchCharacterButton != null)
            switchCharacterButton.interactable = canSwitch;
        if (switchAvailableIndicator != null)
            switchAvailableIndicator.SetActive(canSwitch);
        if (switchUsedIndicator != null)
            switchUsedIndicator.SetActive(_hasSwitchedThisTurn);
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

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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

        StartEnemyTurn();
    }

    private void StartEnemyTurn()
    {
        _waitingEnemyConfirm = true;
        if (phaseHintText != null)
            phaseHintText.text = "按 P 键继续敌人行动";
        if (enemyIntentText != null)
            enemyIntentText.text = "敌人准备行动...";
        UpdateUI();
    }

    private void ExecuteEnemyAction()
    {
        _waitingEnemyConfirm = false;
        if (phaseHintText != null)
            phaseHintText.text = "";

        if (enemyIntentText != null)
            enemyIntentText.text = "敌人正在行动...";

        if (_enemyHP > 0 && !_battleEnded)
        {
            DealDamageToPlayer(enemyAttackDamage);
            UpdateUI();
            if (_playerHP <= 0) { EndBattle(false); return; }
        }

        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
        _turnCount++;
        _actionPoints = maxActionPoints;
        _playerArmor = 0;
        _hasSwitchedThisTurn = false;
        _eventsThisTurn.Clear(); // 清除本回合事件

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
        if (_enemyHP <= 0) EndBattle(true);
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

    private void UpdateUI()
    {
        if (hpText != null) hpText.text = $"{_playerHP}/{playerMaxHP}";
        if (actionPointText != null) actionPointText.text = _actionPoints.ToString();
        if (armorText != null) armorText.text = _playerArmor > 0 ? $"护甲: {_playerArmor}" : "";
        if (enemyHPText != null) enemyHPText.text = $"{_enemyHP}/{enemyMaxHP}";
        if (enemyArmorText != null) enemyArmorText.text = _enemyArmor > 0 ? $"护甲: {_enemyArmor}" : "";
        if (enemyNameText != null) enemyNameText.text = enemyName;
        if (enemyIntentText != null && !_battleEnded && !_waitingEnemyConfirm)
            enemyIntentText.text = _isPlayerTurn ? $"造成{enemyAttackDamage}伤害" : "";

        if (strengthText != null) strengthText.text = _playerStrength > 0 ? $"力量: {_playerStrength}" : "";
        if (dexterityText != null) dexterityText.text = _playerDexterity > 0 ? $"敏捷: {_playerDexterity}" : "";

        if (playerHPBarFill != null)
            playerHPBarFill.fillAmount = (float)_playerHP / playerMaxHP;
        if (enemyHPBarFill != null)
            enemyHPBarFill.fillAmount = (float)_enemyHP / enemyMaxHP;

        if (sanityText != null) sanityText.text = $"{_playerSanity}/{_playerMaxSanity}";
        if (sanityBarFill != null) sanityBarFill.fillAmount = (float)_playerSanity / _playerMaxSanity;

        if (handLayout != null)
            handLayout.RefreshPlayable(IsCardPlayable);
    }
}
