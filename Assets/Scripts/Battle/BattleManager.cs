using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 战斗管理器 —— 管理玩家/敌人状态、手牌、回合、卡牌效果
/// 卡牌以Prefab形式配置在Deck列表中，数据直接在CardDisplay组件上
/// </summary>
public class BattleManager : MonoBehaviour
{
    // === 玩家属性 ===
    [Header("玩家属性")]
    [SerializeField] private int playerHP = 50;
    [SerializeField] private int playerMaxHP = 50;
    [SerializeField] private int playerActionPoints = 3;
    [SerializeField] private int playerMaxActionPoints = 3;
    [SerializeField] private int playerArmor = 0;
    [SerializeField] private int playerStrength = 0;
    [SerializeField] private int playerDexterity = 0;

    // === 敌人属性 ===
    [Header("敌人属性")]
    [SerializeField] private int enemyHP = 80;
    [SerializeField] private int enemyMaxHP = 80;
    [SerializeField] private int enemyArmor = 0;
    [SerializeField] private int enemyAttackDamage = 8;
    [SerializeField] private string enemyName = "敌人";

    // === 手牌设置 ===
    [Header("手牌设置")]
    [SerializeField] private int handLimit = 10;
    [SerializeField] private int initialDraw = 5;
    [SerializeField] private int drawPerTurn = 5;

    // === 牌组（拖入卡牌Prefab，数据在CardDisplay组件上） ===
    [Header("牌组（卡牌Prefab）")]
    [SerializeField] private List<GameObject> deck = new List<GameObject>();

    // === UI引用 ===
    [Header("UI引用")]
    [SerializeField] private HandCardLayout handLayout;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI actionPointText;
    [SerializeField] private TextMeshProUGUI armorText;
    [SerializeField] private TextMeshProUGUI enemyHPText;
    [SerializeField] private TextMeshProUGUI enemyArmorText;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI enemyIntentText;
    [SerializeField] private TextMeshProUGUI drawPileText;
    [SerializeField] private TextMeshProUGUI discardPileText;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI strengthText;
    [SerializeField] private TextMeshProUGUI dexterityText;
    [SerializeField] private Image playerHPBarFill;
    [SerializeField] private Image enemyHPBarFill;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private Button endTurnButton;

    // === 运行时状态 ===
    private List<GameObject> _drawPile = new List<GameObject>();
    private List<GameObject> _hand = new List<GameObject>();
    private List<GameObject> _discardPile = new List<GameObject>();
    private List<GameObject> _consumedThisBattle = new List<GameObject>();
    private bool _isPlayerTurn = true;
    private bool _battleEnded = false;
    private int _turnCount = 1;
    private bool _hasSturdyArmor = false;

    public bool IsPlayerTurn => _isPlayerTurn && !_battleEnded;

    private void Start()
    {
        if (handLayout != null)
            handLayout.SetCardClickCallback(OnCardClicked);
        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        StartBattle();
    }

    public void StartBattle()
    {
        _drawPile = new List<GameObject>(deck);
        _hand.Clear();
        _discardPile.Clear();
        _consumedThisBattle.Clear();
        _battleEnded = false;
        _turnCount = 1;
        playerArmor = 0;
        playerActionPoints = playerMaxActionPoints;

        ShuffleDrawPile();
        DrawCards(initialDraw);
        UpdateUI();
    }

    public void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_hand.Count >= handLimit) break;

            if (_drawPile.Count == 0)
            {
                if (_discardPile.Count == 0) break;
                _drawPile = new List<GameObject>(_discardPile);
                _discardPile.Clear();
                ShuffleDrawPile();
            }

            _hand.Add(_drawPile[0]);
            _drawPile.RemoveAt(0);
        }
        RefreshHandUI();
    }

    private void OnCardClicked(int handIndex)
    {
        if (!_isPlayerTurn || _battleEnded) return;
        PlayCard(handIndex);
    }

    public bool PlayCard(int handIndex)
    {
        if (handIndex < 0 || handIndex >= _hand.Count) return false;
        if (!_isPlayerTurn || _battleEnded) return false;

        var cardGO = _hand[handIndex];
        var card = cardGO.GetComponent<CardDisplay>();
        if (card == null) return false;

        bool freePlay = (card.keywords & KeywordType.FreePlay) != 0;

        if (!freePlay && playerActionPoints < card.actionPointCost)
            return false;

        if (!freePlay)
            playerActionPoints -= card.actionPointCost;

        ApplyCardEffects(card);
        HandleCardConsumption(cardGO, card);

        _hand.RemoveAt(handIndex);

        if ((card.keywords & KeywordType.Swift) != 0)
            DrawCards(1);
        else
            RefreshHandUI();

        UpdateUI();
        CheckBattleEnd();
        return true;
    }

    private void ApplyCardEffects(CardDisplay card)
    {
        switch (card.cardType)
        {
            case CardType.Attack:
                ApplyAttackCard(card);
                break;
            case CardType.Armor:
                ApplyArmorCard(card);
                break;
            case CardType.Buff:
                ApplyBuffCard(card);
                break;
        }
    }

    private void ApplyAttackCard(CardDisplay card)
    {
        int baseDamage = card.attackValue;
        if (card.attackValueType == ValueType.AttributeBased)
            baseDamage += GetAttributeValue(card.attackAttribute);

        int attackCount = card.attackCount;
        if ((card.keywords & KeywordType.Combo) != 0) attackCount += 1;

        bool ignoreArmor = card.ignoreArmor || (card.keywords & KeywordType.Pierce) != 0;
        bool isHeavy = (card.keywords & KeywordType.Heavy) != 0;
        bool hasLifesteal = (card.keywords & KeywordType.Lifesteal) != 0;

        int totalDamageDealt = 0;
        for (int i = 0; i < attackCount; i++)
        {
            int hitDamage = baseDamage;
            if (isHeavy && Random.value < 0.25f) hitDamage *= 2;
            totalDamageDealt += DealDamageToEnemy(hitDamage, ignoreArmor);
        }

        if ((card.keywords & KeywordType.Toxic) != 0)
            totalDamageDealt += DealDamageToEnemy(2, true);
        if ((card.keywords & KeywordType.Burning) != 0)
            totalDamageDealt += DealDamageToEnemy(3, true);

        if (hasLifesteal && totalDamageDealt > 0)
        {
            int heal = Mathf.FloorToInt(totalDamageDealt * 0.5f);
            playerHP = Mathf.Min(playerMaxHP, playerHP + heal);
        }
    }

    private void ApplyArmorCard(CardDisplay card)
    {
        int armor = card.armorValue;
        if (card.armorValueType == ValueType.AttributeBased)
            armor += GetAttributeValue(card.armorAttribute);

        playerArmor += armor;
        if ((card.keywords & KeywordType.Sturdy) != 0)
            _hasSturdyArmor = true;
    }

    private void ApplyBuffCard(CardDisplay card)
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
                    playerActionPoints += totalValue;
                    break;
                case BuffEffectType.DrawCards:
                    DrawCards(totalValue);
                    break;
                case BuffEffectType.GainArmor:
                    playerArmor += totalValue;
                    break;
                case BuffEffectType.HealHP:
                    playerHP = Mathf.Min(playerMaxHP, playerHP + totalValue);
                    break;
            }
        }
    }

    private int DealDamageToEnemy(int damage, bool ignoreArmor)
    {
        int actualDamage = damage;
        if (!ignoreArmor && enemyArmor > 0)
        {
            int absorbed = Mathf.Min(enemyArmor, damage);
            enemyArmor -= absorbed;
            actualDamage -= absorbed;
        }
        enemyHP -= actualDamage;
        if (enemyHP < 0) enemyHP = 0;
        return actualDamage;
    }

    private void HandleCardConsumption(GameObject cardGO, CardDisplay card)
    {
        switch (card.consumeType)
        {
            case ConsumeType.None:
                _discardPile.Add(cardGO);
                break;
            case ConsumeType.ThisBattle:
            case ConsumeType.ThisRun:
                _consumedThisBattle.Add(cardGO);
                break;
        }
    }

    private int GetAttributeValue(PlayerAttributeType attr) => attr switch
    {
        PlayerAttributeType.Strength => playerStrength,
        PlayerAttributeType.Dexterity => playerDexterity,
        PlayerAttributeType.Vitality => 0,
        PlayerAttributeType.Agility => 0,
        _ => 0
    };

    private void IncreaseAttribute(PlayerAttributeType attr, int value)
    {
        switch (attr)
        {
            case PlayerAttributeType.Strength: playerStrength += value; break;
            case PlayerAttributeType.Dexterity: playerDexterity += value; break;
            case PlayerAttributeType.Vitality: playerMaxHP += value; playerHP += value; break;
            case PlayerAttributeType.Agility: drawPerTurn += value; break;
        }
    }

    private bool IsCardPlayable(CardDisplay card)
    {
        if ((card.keywords & KeywordType.FreePlay) != 0) return true;
        return playerActionPoints >= card.actionPointCost;
    }

    private void OnEndTurnClicked()
    {
        if (!_isPlayerTurn || _battleEnded) return;
        EndTurn();
    }

    public void EndTurn()
    {
        if (!_isPlayerTurn || _battleEnded) return;
        _isPlayerTurn = false;

        foreach (var card in _hand)
            _discardPile.Add(card);
        _hand.Clear();
        RefreshHandUI();

        if (endTurnButton != null) endTurnButton.interactable = false;
        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        if (enemyIntentText != null)
            enemyIntentText.text = "敌人正在行动...";

        yield return new WaitForSeconds(1f);

        if (enemyHP > 0)
        {
            DealDamageToPlayer(enemyAttackDamage);
            UpdateUI();
            if (playerHP <= 0) { EndBattle(false); yield break; }
        }

        yield return new WaitForSeconds(0.5f);
        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
        _turnCount++;
        playerActionPoints = playerMaxActionPoints;
        if (!_hasSturdyArmor) playerArmor = 0;
        _hasSturdyArmor = false;

        DrawCards(drawPerTurn);
        _isPlayerTurn = true;
        if (endTurnButton != null) endTurnButton.interactable = true;
        UpdateUI();
    }

    private void DealDamageToPlayer(int damage)
    {
        int actualDamage = damage;
        if (playerArmor > 0)
        {
            int absorbed = Mathf.Min(playerArmor, damage);
            playerArmor -= absorbed;
            actualDamage -= absorbed;
        }
        playerHP -= actualDamage;
        if (playerHP < 0) playerHP = 0;
    }

    private void CheckBattleEnd()
    {
        if (enemyHP <= 0) EndBattle(true);
        else if (playerHP <= 0) EndBattle(false);
    }

    private void EndBattle(bool victory)
    {
        _battleEnded = true;
        _isPlayerTurn = false;
        if (victoryPanel != null) victoryPanel.SetActive(victory);
        if (defeatPanel != null) defeatPanel.SetActive(!victory);
        if (endTurnButton != null) endTurnButton.interactable = false;
        Debug.Log(victory ? "[BattleManager] 战斗胜利！" : "[BattleManager] 战斗失败！");
    }

    private void ShuffleDrawPile()
    {
        for (int i = _drawPile.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
        }
    }

    private void RefreshHandUI()
    {
        if (handLayout != null)
            handLayout.UpdateHand(_hand, IsCardPlayable);
    }

    private void UpdateUI()
    {
        if (hpText != null) hpText.text = $"HP: {playerHP}/{playerMaxHP}";
        if (actionPointText != null) actionPointText.text = playerActionPoints.ToString();
        if (armorText != null) armorText.text = $"护甲: {playerArmor}";
        if (enemyHPText != null) enemyHPText.text = $"{enemyHP}/{enemyMaxHP}";
        if (enemyArmorText != null) enemyArmorText.text = enemyArmor > 0 ? $"护甲: {enemyArmor}" : "";
        if (enemyNameText != null) enemyNameText.text = enemyName;
        if (enemyIntentText != null && !_battleEnded)
            enemyIntentText.text = _isPlayerTurn ? $"下回合敌人攻击: {enemyAttackDamage}" : "敌人正在行动...";
        if (drawPileText != null) drawPileText.text = _drawPile.Count.ToString();
        if (discardPileText != null) discardPileText.text = _discardPile.Count.ToString();
        if (turnText != null) turnText.text = $"第{_turnCount}回合";
        if (strengthText != null) strengthText.text = playerStrength > 0 ? $"力量: {playerStrength}" : "";
        if (dexterityText != null) dexterityText.text = playerDexterity > 0 ? $"敏捷: {playerDexterity}" : "";
        if (playerHPBarFill != null) playerHPBarFill.fillAmount = (float)playerHP / playerMaxHP;
        if (enemyHPBarFill != null) enemyHPBarFill.fillAmount = (float)enemyHP / enemyMaxHP;

        // 只刷新可打出状态，不重建手牌
        if (handLayout != null)
            handLayout.RefreshPlayable(IsCardPlayable);
    }
}
