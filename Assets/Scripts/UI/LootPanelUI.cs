using System;
using System.Collections.Generic;
using LightMiniGame.Shop;
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
///   Relic 存在    → 启用 RelicA、RelicB（双角色各一次权柄选择）
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

    [Header("继续按钮")]
    [Tooltip("继续按钮：点击后触发 OnContinueClicked（BattleManager 订阅以回到局外）")]
    [SerializeField] private Button continueButton;

    /// <summary>继续按钮点击事件（BattleManager 订阅后走 OnQuitClicked 流程回到局外）。</summary>
    public event Action OnContinueClicked;

    // 当前一局结算中由 ShowForLootTable 缓存的可领取内容。
    private int _currencyAmount;
    private readonly List<CardGrade> _relicGrades = new List<CardGrade>();
    private bool _coinClaimed;
    private bool _relicAClaimed;
    private bool _relicBClaimed;

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
    }

    /// <summary>
    /// 按单个 LootTable 刷新按钮显示。
    /// </summary>
    public void ShowForLootTable(LootTable table)
    {
        bool hasCurrency = false, hasCard = false, hasRelic = false;
        Scan(table, ref hasCurrency, ref hasCard, ref hasRelic);
        CacheClaimableLoot(table);
        Apply(hasCurrency, hasCard, hasRelic);
    }

    /// <summary>
    /// 按多个 Battle 事件的掉落表汇总刷新按钮显示（任一事件掉某类型即显示该类型）。
    /// 掉落表配置已从 EnemyConfig 迁移到 PageEventData.lootTable（仅 Battle 类型事件可配置）。
    /// </summary>
    public void ShowForEvents(IEnumerable<PageEventData> events)
    {
        bool hasCurrency = false, hasCard = false, hasRelic = false;
        if (events != null)
        {
            foreach (var evt in events)
            {
                if (evt == null) continue;
                Scan(evt.lootTable, ref hasCurrency, ref hasCard, ref hasRelic);
            }
        }
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
        if (relicAButton != null) relicAButton.interactable = hasRelic && !_relicAClaimed;
        if (relicBButton != null) relicBButton.interactable = hasRelic && !_relicBClaimed;

        Debug.Log($"[LootPanelUI] 按钮显示刷新：货币={hasCurrency}, 卡牌={hasCard}, 权柄={hasRelic}");
    }

    /// <summary>隐藏全部奖励按钮（面板复用前的重置）。</summary>
    public void HideAll() => Apply(false, false, false);

    /// <summary>
    /// 从当前 Battle 事件的 LootTable 缓存可领取内容。
    /// 货币条目数量累加；所有 Relic 条目的可选品级合并去重，供 RelicA/B 各抽 1 件使用。
    /// </summary>
    private void CacheClaimableLoot(LootTable table)
    {
        _currencyAmount = 0;
        _relicGrades.Clear();
        _coinClaimed = false;
        _relicAClaimed = false;
        _relicBClaimed = false;

        var entries = table?.entries;
        if (entries == null) return;

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
    /// 领取 1 件遗物：characterIndex 0=玩家角色库第一个角色（RelicA），1=第二个角色（RelicB）。
    /// 具体抽取/品级筛选/库存写入交给 ChapterManager，确保使用其 MasterRelicLibrary 配置与 GlobalRelicInventory。
    /// </summary>
    private void ClaimRelic(int characterIndex)
    {
        bool alreadyClaimed = characterIndex == 0 ? _relicAClaimed : _relicBClaimed;
        if (alreadyClaimed || _relicGrades.Count == 0) return;

        var chapter = FindObjectOfType<ChapterManager>();
        if (chapter == null)
        {
            Debug.LogWarning("[LootPanelUI] 领取遗物失败：未找到 ChapterManager");
            return;
        }

        if (!chapter.TryGrantBattleLootRelic(characterIndex, _relicGrades, out var relic))
            return; // 保持可点击，以便配置修正后重试

        if (characterIndex == 0)
        {
            _relicAClaimed = true;
            // RelicA 是整个奖励节点，隐藏它会同时移除其 Button 与视觉内容。
            SetActiveSafe(relicA, false);
        }
        else
        {
            _relicBClaimed = true;
            // RelicB 是整个奖励节点，隐藏它会同时移除其 Button 与视觉内容。
            SetActiveSafe(relicB, false);
        }

        Debug.Log($"[LootPanelUI] 角色{characterIndex + 1}领取遗物：{relic.relicName}");
    }

    /// <summary>扫描一张掉落表，把出现过的类型标记为 true（只做或运算，不覆盖已有 true）。</summary>
    private static void Scan(LootTable table, ref bool hasCurrency, ref bool hasCard, ref bool hasRelic)
    {
        var entries = table?.entries;
        if (entries == null) return;

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

    /// <summary>空引用保护版 SetActive（未接线的按钮直接跳过，不报错）。</summary>
    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}
