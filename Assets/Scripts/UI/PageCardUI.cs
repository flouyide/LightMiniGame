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
    [SerializeField] private TextMeshProUGUI typeBadgeText;
    [SerializeField] private Button cardButton;            // 整卡点击（点击卡片其它区域触发）
    [SerializeField] private Image cardFrameImage;
    [SerializeField] private GameObject finalNodeIndicator;

    [Header("底部进入按钮")]
    [SerializeField] private Button enterButton;           // 底部"进入"按钮（独立 Button）
    [SerializeField] private TextMeshProUGUI enterButtonText; // 按钮文字

    [Header("右上角删除按钮")]
    [SerializeField] private Button deleteButton;          // 右上角删除按钮
    [SerializeField] private TextMeshProUGUI deleteButtonText; // 删除按钮文字

    [Header("事件类型颜色")]
    [SerializeField] private Color ColorBattle = new(0.8f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color ColorShop = new(0.9f, 0.75f, 0.2f, 1f);
    [SerializeField] private Color ColorRest = new(0.3f, 0.7f, 0.4f, 1f);
    [SerializeField] private Color ColorFate = new(0.6f, 0.3f, 0.8f, 1f);

    private int _index;
    private Action<int> _onClick;
    private Action<int> _onDelete;

    private void Awake()
    {
        // 整卡与底部按钮都触发同一个回调。
        // Unity UI 的 click 通过 ExecuteHierarchy 只命中最深的 IPointerClickHandler，
        // 点底部按钮只触发 enterButton，点卡片其它区域触发 cardButton，不会双重触发。
        if (cardButton != null)
            cardButton.onClick.AddListener(OnClick);
        if (enterButton != null)
            enterButton.onClick.AddListener(OnClick);
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

        titleText.text = data.displayName;
        descText.text = data.description;
        typeBadgeText.text = GetTypeName(data.eventType);

        if (data.icon != null)
        {
            iconImage.sprite = data.icon;
            iconImage.color = Color.white;
        }
        else
        {
            iconImage.color = new Color(1, 1, 1, 0.1f);
        }

        Color typeColor = GetColor(data.eventType);

        // 卡片边框按类型上色
        if (cardFrameImage != null)
            cardFrameImage.color = typeColor;

        // 底部"进入"按钮：按类型上色 + 文案（进入战斗/商店/休整/命运）
        if (enterButton != null && enterButton.image != null)
            enterButton.image.color = typeColor;
        if (enterButtonText != null)
            enterButtonText.text = GetEnterLabel(data.eventType);

        if (finalNodeIndicator != null)
            finalNodeIndicator.SetActive(data.isFinalNode);
    }

    private static string GetTypeName(PageEventType type) => type switch
    {
        PageEventType.Battle => "战斗",
        PageEventType.Shop => "商店",
        PageEventType.Rest => "休整",
        PageEventType.Fate => "命运",
        _ => "未知"
    };

    // 底部按钮文案：进入 + 类型
    private static string GetEnterLabel(PageEventType type) => type switch
    {
        PageEventType.Battle => "进入战斗",
        PageEventType.Shop => "进入商店",
        PageEventType.Rest => "进入休整",
        PageEventType.Fate => "进入命运",
        _ => "进入"
    };

    private Color GetColor(PageEventType type) => type switch
    {
        PageEventType.Battle => ColorBattle,
        PageEventType.Shop => ColorShop,
        PageEventType.Rest => ColorRest,
        PageEventType.Fate => ColorFate,
        _ => Color.white
    };
}
