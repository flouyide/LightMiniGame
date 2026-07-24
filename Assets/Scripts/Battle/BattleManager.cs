using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightMiniGame.Card;

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

    [Header("运行时属性来源（持久基础属性运行时副本）")]
    [Tooltip("ChapterManager 持有持久基础属性（力量/敏捷/吸血/暴击率/暴伤）的运行时副本，单局内跨战斗保留。战斗开始时从此读取。留空则回退到 PlayerConfig（仅初始值，不含事件累积）")]
    [SerializeField] private ChapterManager chapterManager;

    [Header("卡牌预制体（按类型）")]
    [SerializeField] private GameObject attackCardPrefab;
    [SerializeField] private GameObject armorCardPrefab;
    [SerializeField] private GameObject buffCardPrefab;

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
    private int _baseDrawPerTurn;   // 每场战斗前的抽牌基数（来自 Inspector 的 drawPerTurn，开局捕获一次）
    private int _actionPoints;
    private int _enemyHP;
    private int _enemyArmor;
    private int _turnCount = 1;
    private bool _isPlayerTurn = true;
    private bool _battleEnded = false;
    private bool _hasSturdyArmor = false;
    private bool _waitingEnemyConfirm = false;

    private SettingsPanelUI _settingsPanel;

    public bool IsPlayerTurn => _isPlayerTurn && !_battleEnded;

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
            handLayout.SetCardPrefabs(attackCardPrefab, armorCardPrefab, buffCardPrefab);
        }
        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (switchCharacterButton != null)
            switchCharacterButton.onClick.AddListener(OnSwitchCharacterClicked);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);

        _baseDrawPerTurn = drawPerTurn;   // 捕获抽牌基数（Inspector 配置），避免逐场战斗累加
        StartBattle();
    }

    private void Update()
    {
        if (_waitingEnemyConfirm && Input.GetKeyDown(KeyCode.P))
            ExecuteEnemyAction();
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
        _playerHP = playerMaxHP;
        _playerArmor = 0;
        _playerStrength = playerStrength;
        _playerDexterity = playerDexterity;

        // 读入持久基础属性（单局内跨战斗保留，存于 ChapterManager 运行时副本；资产 PlayerConfig 仅作初始值）
        ChapterManager cm = chapterManager != null ? chapterManager : FindObjectOfType<ChapterManager>();
        if (cm != null)
        {
            _playerStrength = cm.PlayerStrength;
            _playerAgility = cm.PlayerAgility;
            _playerLifesteal = cm.PlayerLifesteal;
            _playerCritRate = cm.PlayerCritRate;
            _playerCritDamage = cm.PlayerCritDamage;
            Debug.Log($"[BattleManager] 读入持久属性(来自ChapterManager) 力量:{_playerStrength} 敏捷:{_playerAgility} 吸血:{_playerLifesteal} 暴击率:{_playerCritRate} 暴伤:{_playerCritDamage}");
        }
        else if (playerConfig != null)
        {
            // 回退：直接用资产初始值（不含事件累积，仅作安全网）
            _playerStrength = playerConfig.strength;
            _playerAgility = playerConfig.agility;
            _playerLifesteal = playerConfig.lifesteal;
            _playerCritRate = playerConfig.critRate;
            _playerCritDamage = playerConfig.critDamage;
            Debug.LogWarning("[BattleManager] 未找到 ChapterManager，回退读入 PlayerConfig 初始值（无跨战斗累积）");
        }
        else
        {
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
        if (charData == null || charData.startingLibrary == null) return;

        foreach (var card in charData.startingLibrary.startingCards)
        {
            if (card != null)
                state.drawPile.Add(card);
        }

        Debug.Log($"[BattleManager] {charData.Label} 初始牌组: {state.drawPile.Count} 张");
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
        bool freePlay = (card.keywords & KeywordType.FreePlay) != 0;

        if (!freePlay && _actionPoints < card.actionPointCost)
            return false;

        if (!freePlay)
            _actionPoints -= card.actionPointCost;

        ApplyCardEffects(card);
        HandleCardConsumption(card);

        _hand.RemoveAt(handIndex);

        if ((card.keywords & KeywordType.Swift) != 0)
            DrawCards(1);
        else
            RefreshHandUI();

        UpdateUI();
        CheckBattleEnd();
        return true;
    }

    private void ApplyCardEffects(CardData card)
    {
        switch (card.cardType)
        {
            case CardType.Attack: ApplyAttackCard(card); break;
            case CardType.Armor: ApplyArmorCard(card); break;
            case CardType.Buff: ApplyBuffCard(card); break;
        }
    }

    private void ApplyAttackCard(CardData card)
    {
        int baseDamage = card.attackValue;
        if (card.attackValueType == ValueType.AttributeBased)
            baseDamage += GetAttributeValue(card.attackAttribute);

        int attackCount = card.attackCount;
        if ((card.keywords & KeywordType.Combo) != 0) attackCount += 1;

        bool ignoreArmor = card.ignoreArmor || (card.keywords & KeywordType.Pierce) != 0;
        bool isHeavy = (card.keywords & KeywordType.Heavy) != 0;
        bool hasLifesteal = (card.keywords & KeywordType.Lifesteal) != 0;

        // 重击暴击：默认 25% 概率 / 2 倍；配置了暴击率/暴伤后以其为准（百分比）
        float critChance = isHeavy ? (_playerCritRate > 0 ? _playerCritRate * 0.01f : 0.25f) : 0f;
        float critMult   = isHeavy ? (_playerCritDamage > 0 ? _playerCritDamage * 0.01f : 2f) : 1f;

        int totalDamageDealt = 0;
        for (int i = 0; i < attackCount; i++)
        {
            int hitDamage = baseDamage;
            if (isHeavy && Random.value < critChance) hitDamage = Mathf.RoundToInt(hitDamage * critMult);
            totalDamageDealt += DealDamageToEnemy(hitDamage, ignoreArmor);
        }

        if ((card.keywords & KeywordType.Toxic) != 0)
            totalDamageDealt += DealDamageToEnemy(2, true);
        if ((card.keywords & KeywordType.Burning) != 0)
            totalDamageDealt += DealDamageToEnemy(3, true);

        if (totalDamageDealt > 0)
            ShowEnemyDamage(totalDamageDealt);

        if (hasLifesteal && totalDamageDealt > 0)
        {
            float ratio = 0.5f + _playerLifesteal * 0.01f;   // 基础 50%，吸血属性每点 +1%
            int heal = Mathf.FloorToInt(totalDamageDealt * ratio);
            _playerHP = Mathf.Min(playerMaxHP, _playerHP + heal);
        }
    }

    private void ApplyArmorCard(CardData card)
    {
        int armor = card.armorValue;
        if (card.armorValueType == ValueType.AttributeBased)
            armor += GetAttributeValue(card.armorAttribute);

        _playerArmor += armor;
        if ((card.keywords & KeywordType.Sturdy) != 0)
            _hasSturdyArmor = true;
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
        if ((card.keywords & KeywordType.FreePlay) != 0) return true;
        return _actionPoints >= card.actionPointCost;
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
        if (!_hasSturdyArmor) _playerArmor = 0;
        _hasSturdyArmor = false;
        _hasSwitchedThisTurn = false;

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

        if (handLayout != null)
            handLayout.RefreshPlayable(IsCardPlayable);
    }
}
