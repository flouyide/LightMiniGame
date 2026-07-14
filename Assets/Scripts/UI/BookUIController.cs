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

    [Header("顶部栏")]
    [SerializeField] private TextMeshProUGUI chapterNameText;
    [SerializeField] private TextMeshProUGUI remainingPagesText;

    [Header("底部栏")]
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("章节完成面板")]
    [SerializeField] private GameObject chapterCompletePanel;
    [SerializeField] private Button nextChapterButton;
    [SerializeField] private Button backButton;

    private readonly List<PageCardUI> _activeCards = new();

    private void OnEnable()
    {
        chapterManager.OnPagesRefreshed.AddListener(HandlePagesRefreshed);
        chapterManager.OnPageSelected.AddListener(HandlePageSelected);
        chapterManager.OnChapterComplete.AddListener(HandleChapterComplete);
        chapterManager.OnChapterInfoUpdated.AddListener(HandleChapterInfoUpdated);
        chapterManager.OnPlayerStatsUpdated.AddListener(HandlePlayerStatsUpdated);

        if (nextChapterButton != null)
            nextChapterButton.onClick.AddListener(OnNextChapterClicked);
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnDisable()
    {
        chapterManager.OnPagesRefreshed.RemoveListener(HandlePagesRefreshed);
        chapterManager.OnPageSelected.RemoveListener(HandlePageSelected);
        chapterManager.OnChapterComplete.RemoveListener(HandleChapterComplete);
        chapterManager.OnChapterInfoUpdated.RemoveListener(HandleChapterInfoUpdated);
        chapterManager.OnPlayerStatsUpdated.RemoveListener(HandlePlayerStatsUpdated);

        if (nextChapterButton != null)
            nextChapterButton.onClick.RemoveListener(OnNextChapterClicked);
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);
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
            card.Setup(pages[i], i, OnCardClicked);
            _activeCards.Add(card);
        }
    }

    public void OnCardClicked(int index)
    {
        chapterManager.SelectPage(index);
    }

    private void HandlePageSelected(PageEventData data, int selectedIndex)
    {
        // 占位阶段：不弹选项面板，直接刷新3个新页面。
        // z-1 已在 ChapterManager.SelectPage 完成；OnOptionResolved 内部会判断
        // 剩余次数 >0 则 RefreshPages()，否则触发章节完成。
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

    private void OnBackClicked()
    {
        chapterManager.StartGame();
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
