using System;
using System.Collections.Generic;
using LightMiniGame.Card;
using LightMiniGame.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战利品面板显示控制器（挂在 LootPanel.prefab 根节点上）。
///
/// 职责单一：按 Battle 事件 LootTable 里出现的掉落物类型，决定 ButtonContainer 下
/// 五个奖励按钮（Coin / CardA / CardB / RelicA / RelicB）的显示与隐藏。
/// 不负责抽取、发放、点击处理——那些由掉落结算系统与各按钮自己的逻辑负责。
///
/// 显示规则（按 LootEntry.kind 统计）：
///   Currency 存在 → 启用 Coin
///   Card 存在     → 启用 CardA、CardB（双角色各一次卡牌选择）
///   Relic 存在    → 启用 RelicA、RelicB（点击直接领取各自预抽取的遗物，按钮随即消失）
///   某类型不存在  → 对应按钮 SetActive(false)
///
/// 例：只掉金币 → 只有 Coin 显示；金币+卡牌 → Coin、CardA、CardB 显示。
///
/// 用法（战斗胜利结算时，掉落表配置在 PageEventData.lootTable）：
///   lootPanelUI.ShowForEvents(battleEvents);            // 汇总多个 Battle 事件的掉落表
///   lootPanelUI.ShowForLootTable(battleEvent.lootTable); // 单个 Battle 事件
/// </summary>
public class LootPanelUI : MonoBehaviour
{
    [Header("奖励按钮（ButtonContainer 下的五个节点）")]
    [Tooltip("货币奖励按钮：LootTable 含 Currency 条目时显示")]
    [SerializeField] private GameObject coin;

    [Tooltip("卡牌奖励按钮 A（a 角色）：LootTable 含 Card 条目时显示")]
    [SerializeField] private GameObject cardA;

    [Tooltip("卡牌奖励按钮 B（b 角色）：LootTable 含 Card 条目时显示")]
    [SerializeField] private GameObject cardB;

    [Tooltip("权柄奖励按钮 A（a 角色）：LootTable 含 Relic 条目时显示")]
    [SerializeField] private GameObject relicA;

    [Tooltip("权柄奖励按钮 B（b 角色）：LootTable 含 Relic 条目时显示")]
    [SerializeField] private GameObject relicB;

    [Header("可领取按钮（与上方奖励节点一一对应）")]
    [SerializeField] private Button coinButton;
    [SerializeField] private Button relicAButton;
    [SerializeField] private Button relicBButton;
    [SerializeField] private Button cardAButton;   // 卡牌A按钮（a 角色），未接线时回退取 cardA 下 Button
    [SerializeField] private Button cardBButton;   // 卡牌B按钮（b 角色），未接线时回退取 cardB 下 Button

    [Header("卡牌选择面板")]
    [Tooltip("卡牌选择面板预制体（LootCardPanel.prefab）；点击 CardA/CardB 时实例化")]
    [SerializeField] private GameObject lootCardPanelPrefab;
    [Tooltip("卡牌预制体（卡面 CardDisplay），用于在面板中实例化候选卡")]
    [SerializeField] private GameObject cardPrefab;

    [Header("继续按钮")]
    [Tooltip("继续按钮：点击后触发 OnContinueClicked（BattleManager 订阅以回到局外）")]
    [SerializeField] private Button continueButton;

    [Header("图标 / 文本引用")]
    [Tooltip("Coin 按钮内的 TMP 组件，显示掉落的货币数量")]
    [SerializeField] private TextMeshProUGUI coinText;

    [Header("RelicA/RelicB 子节点（未接线时按名称自动查找）")]
    [Tooltip("RelicA/RelicImage：显示该角色将掉落的遗物原画")]
    [SerializeField] private Image relicAImage;

    [Tooltip("RelicA/Text：显示该角色将掉落的遗物名")]
    [SerializeField] private TextMeshProUGUI relicAText;

    [Tooltip("RelicB/RelicImage：显示该角色将掉落的遗物原画")]
    [SerializeField] private Image relicBImage;

    [Tooltip("RelicB/Text：显示该角色将掉落的遗物名")]
    [SerializeField] private TextMeshProUGUI relicBText;

    [Header("CharacterImage 头像引用（未接线时按名称自动查找）")]
    [Tooltip("CardA/CharacterImage：角色1头像")]
    [SerializeField] private Image cardACharImage;

    [Tooltip("CardB/CharacterImage：角色2头像")]
    [SerializeField] private Image cardBCharImage;

    [Tooltip("RelicA/CharacterImage：角色1头像")]
    [SerializeField] private Image relicACharImage;

    [Tooltip("RelicB/CharacterImage：角色2头像")]
    [SerializeField] private Image relicBCharImage;

    [Header("CardA/CardB 文本引用（未接线时按名称自动查找）")]
    [Tooltip("CardA/Text：显示 角色1名字 + 卡牌")]
    [SerializeField] private TextMeshProUGUI cardAText;

    [Tooltip("CardB/Text：显示 角色2名字 + 卡牌")]
    [SerializeField] private TextMeshProUGUI cardBText;

    /// <summary>继续按钮点击事件（BattleManager 订阅后走 OnQuitClicked 流程回到局外）。</summary>
    public event Action OnContinueClicked;

    // 当前一局结算中由 ShowForLootTable 缓存的可领取内容。
    private int _currencyAmount;
    private readonly List<CardGrade> _relicGrades = new List<CardGrade>();
    private RelicData _relicAData;   // 预抽取的角色A掉落遗物（仅预览图标，未写入库存）
    private RelicData _relicBData;   // 预抽取的角色B掉落遗物（仅预览图标，未写入库存）
    private bool _coinClaimed;
    private bool _relicAClaimed;
    private bool _relicBClaimed;

    // 卡牌掉落：合并自全部 Card 条目的允许品级与最大抽取数（供 CardA/CardB 弹出面板时使用）
    private readonly List<CardGrade> _cardRarities = new List<CardGrade>();
    private int _cardDrawCount = 3;
    private bool _cardAClaimed;
    private bool _cardBClaimed;
    private bool _avatarsApplied;   // 角色头像是否已写入（幂等标记）

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(() => OnContinueClicked?.Invoke());
        if (coinButton != null)
            coinButton.onClick.AddListener(ClaimCurrency);
        if (relicAButton != null)
            relicAButton.onClick.AddListener(() => ClaimRelic(0));
        if (relicBButton != null)
            relicBButton.onClick.AddListener(() => ClaimRelic(1));

        // 卡牌A/卡牌B按钮：优先用 Inspector 接线，未接线则回退到 cardA/cardB 节点下的 Button
        if (cardAButton == null && cardA != null)
            cardAButton = cardA.GetComponentInChildren<Button>();
        if (cardBButton == null && cardB != null)
            cardBButton = cardB.GetComponentInChildren<Button>();
        if (cardAButton != null)
            cardAButton.onClick.AddListener(() => OpenLootCardPanel(0));
        if (cardBButton != null)
            cardBButton.onClick.AddListener(() => OpenLootCardPanel(1));

        // 解析各奖励按钮下的子节点（RelicImage / Text / CharacterImage），未接线时按名称自动查找
        ResolveChildRefs();
    }

    /// <summary>解析各奖励按钮下的子节点引用；Inspector 已接线的优先，缺的按固定子节点名查找。</summary>
    private void ResolveChildRefs()
    {
        if (relicAImage == null) relicAImage = FindChildImage(relicA, "RelicImage");
        if (relicAText == null) relicAText = FindChildText(relicA, "Text");
        if (relicBImage == null) relicBImage = FindChildImage(relicB, "RelicImage");
        if (relicBText == null) relicBText = FindChildText(relicB, "Text");
        if (cardACharImage == null) cardACharImage = FindChildImage(cardA, "CharacterImage");
        if (cardBCharImage == null) cardBCharImage = FindChildImage(cardB, "CharacterImage");
        if (relicACharImage == null) relicACharImage = FindChildImage(relicA, "CharacterImage");
        if (relicBCharImage == null) relicBCharImage = FindChildImage(relicB, "CharacterImage");
        if (cardAText == null) cardAText = FindChildText(cardA, "Text");
        if (cardBText == null) cardBText = FindChildText(cardB, "Text");
    }

    private static Image FindChildImage(GameObject parent, string childName)
    {
        if (parent == null) return null;
        var t = parent.transform.Find(childName);
        return t != null ? t.GetComponent<Image>() : null;
    }

    private static TextMeshProUGUI FindChildText(GameObject parent, string childName)
    {
        if (parent == null) return null;
        var t = parent.transform.Find(childName);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    /// <summary>
    /// 按单个 LootTable 刷新按钮显示。
    /// </summary>
    public void ShowForLootTable(LootTable table)
    {
        var tables = table != null ? new List<LootTable> { table } : new List<LootTable>();
        Refresh(tables);
    }

    /// <summary>
    /// 按多个 Battle 事件的掉落表汇总刷新按钮显示（任一事件掉某类型即显示该类型；货币累加）。
    /// 掉落表配置已从 EnemyConfig 迁移到 PageEventData.lootTable（仅 Battle 类型事件可配置）。
    /// </summary>
    public void ShowForEvents(IEnumerable<PageEventData> events)
    {
        var tables = new List<LootTable>();
        if (events != null)
        {
            foreach (var evt in events)
            {
                if (evt == null || evt.lootTable == null) continue;
                tables.Add(evt.lootTable);
            }
        }
        Refresh(tables);
    }

    /// <summary>汇总多张掉落表，刷新按钮显示、货币文本与遗物图标。</summary>
    private void Refresh(List<LootTable> tables)
    {
        bool hasCurrency = false, hasCard = false, hasRelic = false;
        ScanAll(tables, ref hasCurrency, ref hasCard, ref hasRelic);
        CacheClaimableLoot(tables);
        Apply(hasCurrency, hasCard, hasRelic);
    }

    /// <summary>
    /// 直接按三个类型开关刷新（供掉落结算系统已自行统计好类型时调用）。
    /// </summary>
    public void Apply(bool hasCurrency, bool hasCard, bool hasRelic)
    {
        SetActiveSafe(coin, hasCurrency);
        SetActiveSafe(cardA, hasCard);
        SetActiveSafe(cardB, hasCard);
        SetActiveSafe(relicA, hasRelic);
        SetActiveSafe(relicB, hasRelic);

        // 每次显示一张新的掉落表时重新允许领取；具体领取后会将各自按钮禁用，防止重复点。
        if (coinButton != null) coinButton.interactable = hasCurrency && !_coinClaimed;
        if (relicAButton != null) relicAButton.interactable = hasRelic && !_relicAClaimed && _relicAData != null;
        if (relicBButton != null) relicBButton.interactable = hasRelic && !_relicBClaimed && _relicBData != null;
        if (cardAButton != null) cardAButton.interactable = hasCard && !_cardAClaimed;
        if (cardBButton != null) cardBButton.interactable = hasCard && !_cardBClaimed;

        ApplyDisplay(hasCurrency, hasRelic);
        Debug.Log($"[LootPanelUI] 按钮显示刷新：货币={hasCurrency}, 卡牌={hasCard}, 权柄={hasRelic}");
    }

    /// <summary>
    /// 刷新 Coin 的货币数量文本、RelicA/RelicB 的遗物原画与名字（按预抽取结果），
    /// 以及四个奖励按钮 CharacterImage 的角色头像。
    /// </summary>
    private void ApplyDisplay(bool hasCurrency, bool hasRelic)
    {
        if (coinText != null)
            coinText.text = hasCurrency ? $"货币：{_currencyAmount}" : string.Empty;

        // RelicA/RelicB：RelicImage 显示预抽取遗物原画，Text 显示遗物名（点击按钮即领取这件）
        UpdateRelicPreview(relicAImage, relicAText, hasRelic ? _relicAData : null);
        UpdateRelicPreview(relicBImage, relicBText, hasRelic ? _relicBData : null);

        ApplyCharacterAvatars();
        ApplyCardLabels();
    }

    /// <summary>刷新 CardA/CardB 文本：所属角色名字 + "卡牌"（CardA=角色1，CardB=角色2）。</summary>
    private void ApplyCardLabels()
    {
        var chapter = FindObjectOfType<ChapterManager>();
        if (chapter == null) return;   // ChapterManager 尚未就绪时下次刷新重试
        UpdateCardLabel(cardAText, chapter.GetCharacter(0));
        UpdateCardLabel(cardBText, chapter.GetCharacter(1));
    }

    /// <summary>把卡牌按钮文本设为「角色名字 + 卡牌」。character 为空时清空。</summary>
    private static void UpdateCardLabel(TextMeshProUGUI label, CharacterData character)
    {
        if (label == null) return;
        string name = character != null ? character.Label : string.Empty;
        label.text = name + "卡牌";
    }

    /// <summary>刷新单个遗物按钮的预览：RelicImage 显示原画，Text 显示遗物名；data 为空时清空。</summary>
    private static void UpdateRelicPreview(Image icon, TextMeshProUGUI label, RelicData data)
    {
        if (icon != null)
        {
            if (data != null && data.icon != null)
            {
                icon.sprite = data.icon;
                icon.color = Color.white;
                icon.preserveAspect = true;
                icon.enabled = true;
            }
            else
            {
                icon.sprite = null;
                icon.enabled = false;
            }
        }
        if (label != null)
            label.text = data != null ? data.relicName : string.Empty;
    }

    /// <summary>把四个奖励按钮的 CharacterImage 设为所属角色头像（CardA/RelicA=角色1，CardB/RelicB=角色2）。幂等。</summary>
    private void ApplyCharacterAvatars()
    {
        if (_avatarsApplied) return;
        var chapter = FindObjectOfType<ChapterManager>();
        if (chapter == null) return;   // ChapterManager 尚未就绪时下次刷新重试

        ApplyAvatar(cardACharImage, chapter.GetCharacter(0));
        ApplyAvatar(relicACharImage, chapter.GetCharacter(0));
        ApplyAvatar(cardBCharImage, chapter.GetCharacter(1));
        ApplyAvatar(relicBCharImage, chapter.GetCharacter(1));
        _avatarsApplied = true;
    }

    private static void ApplyAvatar(Image target, CharacterData character)
    {
        if (target == null || character == null || character.avatar == null) return;
        target.sprite = character.avatar;
        target.color = Color.white;
        target.preserveAspect = true;
    }

    /// <summary>隐藏全部奖励按钮（面板复用前的重置）。</summary>
    public void HideAll() => Apply(false, false, false);

    /// <summary>
    /// 从掉落表列表缓存可领取内容：货币条目数量累加；所有 Relic 条目的可选品级合并去重；
    /// 并按角色预抽取将掉落的遗物（仅预览图标，不写入库存，点击领取时再真正写入）。
    /// </summary>
    private void CacheClaimableLoot(List<LootTable> tables)
    {
        _currencyAmount = 0;
        _relicGrades.Clear();
        _relicAData = null;
        _relicBData = null;
        _coinClaimed = false;
        _relicAClaimed = false;
        _relicBClaimed = false;
        _cardRarities.Clear();
        _cardDrawCount = 3;
        _cardAClaimed = false;
        _cardBClaimed = false;

        if (tables != null)
        {
            foreach (var table in tables)
            {
                var entries = table?.entries;
                if (entries == null) continue;
                foreach (var entry in entries)
                {
                    if (entry == null) continue;
                    if (entry.kind == LootEntry.LootKind.Currency)
                    {
                        _currencyAmount += Mathf.Max(0, entry.currencyAmount);
                    }
                    else if (entry.kind == LootEntry.LootKind.Relic && entry.relicRarities != null)
                    {
                        foreach (var grade in entry.relicRarities)
                        {
                            if (!_relicGrades.Contains(grade))
                                _relicGrades.Add(grade);
                        }
                    }
                    else if (entry.kind == LootEntry.LootKind.Card)
                    {
                        if (entry.cardRarities != null)
                        {
                            foreach (var grade in entry.cardRarities)
                            {
                                if (!_cardRarities.Contains(grade))
                                    _cardRarities.Add(grade);
                            }
                        }
                        // 多张 Card 条目时取最大抽取数，保证所有候选都能展示
                        if (entry.cardDrawCount > _cardDrawCount)
                            _cardDrawCount = entry.cardDrawCount;
                    }
                }
            }
        }

        // 每个角色预抽取一件将掉落的遗物（仅用于面板预览图标，不写入库存）。
        if (_relicGrades.Count > 0)
        {
            var chapter = FindObjectOfType<ChapterManager>();
            if (chapter != null)
            {
                if (!chapter.PeekBattleLootRelic(0, _relicGrades, out _relicAData)) _relicAData = null;
                if (!chapter.PeekBattleLootRelic(1, _relicGrades, out _relicBData)) _relicBData = null;
            }
        }
    }

    /// <summary>领取全部货币条目的总额；同一结算只能领取一次。</summary>
    private void ClaimCurrency()
    {
        if (_coinClaimed || _currencyAmount <= 0) return;

        var chapter = FindObjectOfType<ChapterManager>();
        if (chapter == null)
        {
            Debug.LogWarning("[LootPanelUI] 领取货币失败：未找到 ChapterManager");
            return;
        }

        chapter.AddGold(_currencyAmount);
        _coinClaimed = true;
        // 领取后直接隐藏对应奖励节点（而非仅置灰），已领内容从结算界面移除。
        SetActiveSafe(coin, false);
        Debug.Log($"[LootPanelUI] 领取货币 +{_currencyAmount}");
    }

    /// <summary>
    /// 直接领取该角色预抽取的遗物（不弹选择面板）：characterIndex 0=角色1（RelicA），1=角色2（RelicB）。
    /// 领取的就是按钮预览原画/名字显示的那件（PeekBattleLootRelic 预抽取结果），写入库存走
    /// ChapterManager.CommitBattleLootRelic；领取后整个 RelicA/RelicB 奖励节点消失。
    /// </summary>
    private void ClaimRelic(int characterIndex)
    {
        bool alreadyClaimed = characterIndex == 0 ? _relicAClaimed : _relicBClaimed;
        var data = characterIndex == 0 ? _relicAData : _relicBData;
        if (alreadyClaimed || data == null) return;

        var chapter = FindObjectOfType<ChapterManager>();
        if (chapter == null)
        {
            Debug.LogWarning("[LootPanelUI] 领取遗物失败：未找到 ChapterManager");
            return;
        }

        // 把预览阶段已确定的遗物真正写入对应角色库存（与按钮上显示的图标/名字一致，不重复随机）
        if (!chapter.CommitBattleLootRelic(characterIndex, data))
        {
            Debug.LogWarning($"[LootPanelUI] 领取遗物失败：{data.relicName} 已拥有或写入库存失败");
            return; // 保持可点击，以便配置修正后重试
        }

        if (characterIndex == 0)
        {
            _relicAClaimed = true;
            SetActiveSafe(relicA, false);
        }
        else
        {
            _relicBClaimed = true;
            SetActiveSafe(relicB, false);
        }

        Debug.Log($"[LootPanelUI] 角色{characterIndex + 1}领取遗物：{data.relicName}");
    }

    /// <summary>
    /// 打开卡牌选择面板（LootCardPanel.prefab）：实例化面板、挂载 LootCardPanelUI 控制器并抽取候选卡。
    /// characterIndex：0=角色1（CardA），1=角色2（CardB）。
    /// 抽取的品级与张数来自当前结算缓存的 Card 条目（_cardRarities / _cardDrawCount）。
    /// </summary>
    private void OpenLootCardPanel(int characterIndex)
    {
        if (lootCardPanelPrefab == null || cardPrefab == null)
        {
            Debug.LogWarning("[LootPanelUI] 无法打开卡牌选择面板：未配置 lootCardPanelPrefab 或 cardPrefab（请在 Inspector 拖入 LootCardPanel.prefab 与卡牌预制体）");
            return;
        }
        if (_cardRarities.Count == 0) _cardRarities.Add(CardGrade.Bronze);

        var panel = Instantiate(lootCardPanelPrefab);
        var ui = panel.AddComponent<LootCardPanelUI>();
        ui.Open(characterIndex, new List<CardGrade>(_cardRarities), _cardDrawCount, cardPrefab, OnCardPicked);
        Debug.Log($"[LootPanelUI] 打开角色{characterIndex + 1} 卡牌选择面板（品级：{string.Join(",", _cardRarities)}，张数：{_cardDrawCount}）");
    }

    /// <summary>玩家在卡牌选择面板选中一张并发放后回调：标记该角色卡牌已领取并隐藏对应按钮。</summary>
    private void OnCardPicked(int characterIndex)
    {
        if (characterIndex == 0)
        {
            _cardAClaimed = true;
            SetActiveSafe(cardA, false);
        }
        else
        {
            _cardBClaimed = true;
            SetActiveSafe(cardB, false);
        }
        Debug.Log($"[LootPanelUI] 角色{characterIndex + 1}已领取卡牌掉落");
    }

    /// <summary>扫描多张掉落表，把出现过的类型标记为 true（只做或运算，不覆盖已有 true）。</summary>
    private static void ScanAll(IEnumerable<LootTable> tables, ref bool hasCurrency, ref bool hasCard, ref bool hasRelic)
    {
        if (tables == null) return;
        foreach (var table in tables)
        {
            var entries = table?.entries;
            if (entries == null) continue;
            foreach (var e in entries)
            {
                if (e == null) continue;
                switch (e.kind)
                {
                    case LootEntry.LootKind.Currency: hasCurrency = true; break;
                    case LootEntry.LootKind.Card:     hasCard = true;     break;
                    case LootEntry.LootKind.Relic:    hasRelic = true;    break;
                }
            }
        }
    }

    /// <summary>空引用保护版 SetActive（未接线的按钮直接跳过，不报错）。</summary>
    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}
