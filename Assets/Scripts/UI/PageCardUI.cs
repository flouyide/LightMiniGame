using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 单个书页卡片UI
/// </summary>
public class PageCardUI : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private Button cardButton;            // 点击整张卡片直接进入事件
    [SerializeField] private GameObject finalNodeIndicator;

    [Header("右上角删除按钮")]
    [SerializeField] private Button deleteButton;          // 右上角删除按钮
    [SerializeField] private TextMeshProUGUI deleteButtonText; // 删除按钮文字

    [Header("删除按钮显示规则（按事件类型）")]
    [Tooltip("勾选表示该类型卡片显示删除按钮")]
    [SerializeField] private bool showDeleteOnBattle = false;   // 战斗：默认不显示
    [SerializeField] private bool showDeleteOnShop = true;      // 商店：默认显示
    [SerializeField] private bool showDeleteOnRest = true;      // 休整：默认显示
    [SerializeField] private bool showDeleteOnEvent = true;      // 事件：默认显示

    [Header("书页显示载体（prefab 上固定的两个 Image，按类型换 sprite）")]
    [Tooltip("书页背景显示位：整卡的背景 Image，按事件类型换 sprite")]
    [SerializeField] private Image pageImage;
    [Tooltip("书页 logo 显示位：左上角 logo Image，按事件类型换 sprite")]
    [SerializeField] private Image pageLogoImage;

    [Header("书页样式（按事件类型的背景与 logo sprite）")]
    [Tooltip("回复书页（Rest 类型）：卡片书页背景图")]
    [SerializeField] private Sprite restPage;
    [Tooltip("回复书页logo（Rest 类型）")]
    [SerializeField] private Sprite restPageLogo;
    [Tooltip("命运书页（Event 类型）：卡片书页背景图")]
    [SerializeField] private Sprite fatePage;
    [Tooltip("命运书页logo（Event 类型）")]
    [SerializeField] private Sprite fatePageLogo;
    [Tooltip("商店书页（Shop 类型）：卡片书页背景图")]
    [SerializeField] private Sprite shopPage;
    [Tooltip("商店书页logo（Shop 类型）")]
    [SerializeField] private Sprite shopPageLogo;
    [Tooltip("战斗书页（Battle 类型）：卡片书页背景图")]
    [SerializeField] private Sprite battlePage;
    [Tooltip("战斗书页logo（Battle 类型）")]
    [SerializeField] private Sprite battlePageLogo;

    private int _index;
    private Action<int> _onClick;
    private Action<int> _onDelete;

    private void Awake()
    {
        // 整张卡片是唯一的进入入口。
        if (cardButton != null)
            cardButton.onClick.AddListener(OnClick);
        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDelete);
    }

    private void OnClick()
    {
        _onClick?.Invoke(_index);
    }

    private void OnDelete()
    {
        _onDelete?.Invoke(_index);
    }

    public void Setup(PageEventData data, int index, Action<int> onClick, Action<int> onDelete = null)
    {
        _index = index;
        _onClick = onClick;
        _onDelete = onDelete;

        bool isMysteryEvent = data.eventType == PageEventType.Event;
        titleText.text = isMysteryEvent ? "神秘事件" : data.displayName;
        descText.text = isMysteryEvent ? string.Empty : data.description;

        if (data.icon != null)
        {
            iconImage.sprite = data.icon;
            iconImage.color = Color.white;
        }
        else
        {
            iconImage.color = new Color(1, 1, 1, 0.1f);
        }

        if (finalNodeIndicator != null)
            finalNodeIndicator.SetActive(data.isFinalNode);

        // 根据事件类型决定是否显示删除按钮
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(ShouldShowDelete(data.eventType));

        // 根据事件类型切换书页背景与 logo
        ApplyPageStyle(data.eventType);
    }

    /// <summary>
    /// 当其它卡片被消耗导致本卡片在列表中的位置前移时，重绑其索引，
    /// 否则 _index 会与 ChapterManager._currentPages 的位置错位，导致点击失效。
    /// </summary>
    public void SetIndex(int index)
    {
        _index = index;
    }

    // 根据事件类型判断是否显示删除按钮
    private bool ShouldShowDelete(PageEventType type) => type switch
    {
        PageEventType.Battle => showDeleteOnBattle,
        PageEventType.Shop => showDeleteOnShop,
        PageEventType.Rest => showDeleteOnRest,
        PageEventType.Event => showDeleteOnEvent,
        _ => true
    };

    /// <summary>
    /// 按事件类型给书页背景/logo 显示位换 sprite。
    /// Battle → 战斗书页；Shop → 商店书页；Rest → 回复书页；Event → 命运书页。
    /// 背景 sprite 未配置时以类型占位色显示；logo 未配置时隐藏 logo 节点。
    /// </summary>
    private void ApplyPageStyle(PageEventType type)
    {
        var (page, logo) = type switch
        {
            PageEventType.Battle => (battlePage, battlePageLogo),
            PageEventType.Shop   => (shopPage, shopPageLogo),
            PageEventType.Rest   => (restPage, restPageLogo),
            PageEventType.Event  => (fatePage, fatePageLogo),
            _ => (null, null)
        };

        if (pageImage != null)
        {
            pageImage.sprite = page;
            pageImage.color = page != null ? Color.white : FallbackColor(type);
        }

        if (pageLogoImage != null)
        {
            pageLogoImage.sprite = logo;
            pageLogoImage.color = Color.white;
            if (pageLogoImage.gameObject.activeSelf != (logo != null))
                pageLogoImage.gameObject.SetActive(logo != null);
        }
    }

    /// <summary>未配置书页 sprite 时的类型占位色（回复绿 / 命运紫 / 商店金 / 战斗红）</summary>
    private static Color FallbackColor(PageEventType type) => type switch
    {
        PageEventType.Rest   => new Color(0.16f, 0.38f, 0.22f, 0.92f),
        PageEventType.Event  => new Color(0.28f, 0.16f, 0.38f, 0.92f),
        PageEventType.Shop   => new Color(0.42f, 0.32f, 0.12f, 0.92f),
        PageEventType.Battle => new Color(0.42f, 0.14f, 0.14f, 0.92f),
        _ => new Color(0.2f, 0.2f, 0.2f, 0.92f)
    };
}
