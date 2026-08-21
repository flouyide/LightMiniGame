using System;
using System.Collections.Generic;
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

    [Header("继续按钮")]
    [Tooltip("继续按钮：点击后触发 OnContinueClicked（BattleManager 订阅以回到局外）")]
    [SerializeField] private Button continueButton;

    /// <summary>继续按钮点击事件（BattleManager 订阅后走 OnQuitClicked 流程回到局外）。</summary>
    public event Action OnContinueClicked;

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(() => OnContinueClicked?.Invoke());
    }

    /// <summary>
    /// 按单个 LootTable 刷新按钮显示。
    /// </summary>
    public void ShowForLootTable(LootTable table)
    {
        bool hasCurrency = false, hasCard = false, hasRelic = false;
        Scan(table, ref hasCurrency, ref hasCard, ref hasRelic);
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

        Debug.Log($"[LootPanelUI] 按钮显示刷新：货币={hasCurrency}, 卡牌={hasCard}, 权柄={hasRelic}");
    }

    /// <summary>隐藏全部奖励按钮（面板复用前的重置）。</summary>
    public void HideAll() => Apply(false, false, false);

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
