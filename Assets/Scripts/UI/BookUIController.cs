using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 书页界面总控
/// </summary>
public class BookUIController : MonoBehaviour
{
    [Header("系统引用")]
    [SerializeField] private ChapterManager chapterManager;

    [Header("卡片")]
    [SerializeField] private Transform cardContainer;
    [SerializeField] private PageCardUI cardPrefab;

    [Header("选项面板")]
    [SerializeField] private OptionPanelUI optionPanel;

    [Header("商店面板")]
    [SerializeField] private ShopPanelUI shopPanel;

    [Header("顶部栏")]
    [SerializeField] private TextMeshProUGUI chapterNameText;
    [SerializeField] private TextMeshProUGUI remainingPagesText;

    [Header("底部栏")]
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("章节完成面板")]
    [SerializeField] private GameObject chapterCompletePanel;
    [SerializeField] private Button nextChapterButton;
    [SerializeField] private Button settingsButton;   // 设置按钮（场景中已命名为 SettingsButton），目前无功能，预留打开设置菜单

    private readonly List<PageCardUI> _activeCards = new();
    private PageEventData _currentEventData;   // 当前事件面板正在显示的事件数据（供选项回调取 effects）

    private void OnEnable()
    {
        chapterManager.OnPagesRefreshed.AddListener(HandlePagesRefreshed);
        chapterManager.OnPageRefreshed.AddListener(HandlePageRefreshed);
        chapterManager.OnPageSelected.AddListener(HandlePageSelected);
        chapterManager.OnPageDeleted.AddListener(HandlePageDeleted);
        chapterManager.OnPageConsumed.AddListener(HandlePageConsumed);
        chapterManager.OnChapterComplete.AddListener(HandleChapterComplete);
        chapterManager.OnChapterInfoUpdated.AddListener(HandleChapterInfoUpdated);
        chapterManager.OnPlayerStatsUpdated.AddListener(HandlePlayerStatsUpdated);

        if (nextChapterButton != null)
            nextChapterButton.onClick.AddListener(OnNextChapterClicked);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
    }

    



















    private void OnDisable()
    {
        chapterManager.OnPagesRefreshed.RemoveListener(HandlePagesRefreshed);
        chapterManager.OnPageRefreshed.RemoveListener(HandlePageRefreshed);
        chapterManager.OnPageSelected.RemoveListener(HandlePageSelected);
        chapterManager.OnPageDeleted.RemoveListener(HandlePageDeleted);
        chapterManager.OnPageConsumed.RemoveListener(HandlePageConsumed);
        chapterManager.OnChapterComplete.RemoveListener(HandleChapterComplete);
        chapterManager.OnChapterInfoUpdated.RemoveListener(HandleChapterInfoUpdated);
        chapterManager.OnPlayerStatsUpdated.RemoveListener(HandlePlayerStatsUpdated);

        if (nextChapterButton != null)
            nextChapterButton.onClick.RemoveListener(OnNextChapterClicked);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
    }

    private void HandlePagesRefreshed(List<PageEventData> pages)
    {
        // 清除旧卡片
        foreach (var card in _activeCards)
        {
            if (card != null && card.gameObject != null)
                Destroy(card.gameObject);
        }
        _activeCards.Clear();

        // 实例化新卡片
        for (int i = 0; i < pages.Count; i++)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            card.Setup(pages[i], i, OnCardClicked, OnCardDeleted);
            _activeCards.Add(card);
        }
    }

    private void HandlePageRefreshed(PageEventData newData, int refreshedIndex)
    {
        if (refreshedIndex < 0 || refreshedIndex >= _activeCards.Count)
        {
            Debug.LogWarning($"[BookUIController] 无效的刷新索引: {refreshedIndex}");
            return;
        }

        // 只刷新指定位置的卡片
        var card = _activeCards[refreshedIndex];
        if (card != null)
        {
            card.Setup(newData, refreshedIndex, OnCardClicked, OnCardDeleted);
            Debug.Log($"[BookUIController] 卡片 {refreshedIndex} 已刷新为新事件「{newData.displayName}」");
        }
    }

    private void HandlePageDeleted(int deletedIndex)
    {
        Debug.Log($"[BookUIController] 卡片 {deletedIndex} 已删除，等待刷新");
    }

    /// <summary>
    /// 剩余页数为 0 时选中卡片 → 删除该卡片，不刷新新卡片
    /// </summary>
    private void HandlePageConsumed(int consumedIndex)
    {
        if (consumedIndex < 0 || consumedIndex >= _activeCards.Count)
            return;

        var card = _activeCards[consumedIndex];
        if (card != null && card.gameObject != null)
            Destroy(card.gameObject);
        _activeCards.RemoveAt(consumedIndex);

        // 列表位置发生变化，重绑剩余卡片的索引，使其与 ChapterManager._currentPages 对齐。
        // 否则剩余卡片仍持有旧的 _index（例如 2），而 _currentPages 已被 RemoveAt 缩短，
        // 再次点击会因 index 越界被 SelectPage/DeletePage 拒绝。
        for (int i = 0; i < _activeCards.Count; i++)
        {
            _activeCards[i].SetIndex(i);
        }

        Debug.Log($"[BookUIController] 卡片 {consumedIndex} 已消耗（剩余页数为0），剩余 {_activeCards.Count} 张");
    }

    public void OnCardClicked(int index)
    {
        chapterManager.SelectPage(index);
    }

    public void OnCardDeleted(int index)
    {
        chapterManager.DeletePage(index);
    }

    private void HandlePageSelected(PageEventData data, int selectedIndex)
    {
        if (data.eventType == PageEventType.Event && optionPanel != null)
        {
            // Event 类型：弹出选项面板让玩家选择
            _currentEventData = data;
            optionPanel.Show(data, OnOptionPanelResolved);
        }
        else if (data.eventType == PageEventType.Shop && shopPanel != null)
        {
            // Shop 类型：弹出商店面板（不应用 effects）
            shopPanel.Show(OnShopClosed);
        }
        else
        {
            // Battle/Rest 类型：直接应用 defaultEffects，不弹面板
            chapterManager.ApplyEffects(data.defaultEffects);
            chapterManager.OnOptionResolved();
        }
    }

    /// <summary>
    /// 商店关闭回调：刷新 3 个 PageCard（买卖功能待实现）
    /// </summary>
    private void OnShopClosed()
    {
        chapterManager.OnOptionResolved();
    }

    /// <summary>
    /// 事件面板关闭后的回调：应用选中选项的 effects，然后继续章节推进
    /// </summary>
    private void OnOptionPanelResolved(int optionIndex)
    {
        if (_currentEventData != null
            && optionIndex >= 0
            && optionIndex < _currentEventData.options.Count)
        {
            chapterManager.ApplyEffects(_currentEventData.options[optionIndex].effects);
        }
        _currentEventData = null;
        chapterManager.OnOptionResolved();
    }

    private void HandleChapterComplete()
    {
        if (chapterCompletePanel != null)
            chapterCompletePanel.SetActive(true);
    }

    private void OnNextChapterClicked()
    {
        if (chapterCompletePanel != null)
            chapterCompletePanel.SetActive(false);
        chapterManager.NextChapter();
    }

    /// <summary>
    /// 设置按钮点击回调。
    /// 目前设置菜单尚未实现，此函数暂时为空（不执行任何逻辑）。
    /// 后续若要做设置菜单，在此处打开对应面板即可（例如：settingsPanel.SetActive(true)）。
    /// </summary>
    private void OnSettingsClicked()
    {
        // TODO: 打开设置菜单（音量、画质、重开等）。当前无功能。
    }

    private void HandleChapterInfoUpdated(string name, int remaining)
    {
        if (chapterNameText != null)
            chapterNameText.text = name;
        if (remainingPagesText != null)
            remainingPagesText.text = $"剩余页数: {remaining}";
    }

    private void HandlePlayerStatsUpdated(int hp, int gold)
    {
        if (hpText != null)
            hpText.text = $"HP: {hp}";
        if (goldText != null)
            goldText.text = $"金币: {gold}";
    }
}
