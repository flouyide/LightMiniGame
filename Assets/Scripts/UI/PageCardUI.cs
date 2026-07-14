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
    [SerializeField] private Button cardButton;
    [SerializeField] private Image cardFrameImage;
    [SerializeField] private GameObject finalNodeIndicator;

    [Header("底部进入按钮(CTA)")]
    [SerializeField] private Image enterCtaImage;       // 底部"进入"按钮底图
    [SerializeField] private TextMeshProUGUI enterCtaText; // 底部"进入"按钮文字

    [Header("事件类型颜色")]
    [SerializeField] private Color ColorBattle = new(0.8f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color ColorShop = new(0.9f, 0.75f, 0.2f, 1f);
    [SerializeField] private Color ColorRest = new(0.3f, 0.7f, 0.4f, 1f);
    [SerializeField] private Color ColorFate = new(0.6f, 0.3f, 0.8f, 1f);

    private int _index;
    private Action<int> _onClick;

    private void Awake()
    {
        if (cardButton != null)
            cardButton.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        _onClick?.Invoke(_index);
    }

    public void Setup(PageEventData data, int index, Action<int> onClick)
    {
        _index = index;
        _onClick = onClick;

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

        // 底部"进入"按钮：按类型上色 + 文案（战斗/商店/休整/命运）
        if (enterCtaImage != null)
            enterCtaImage.color = typeColor;
        if (enterCtaText != null)
            enterCtaText.text = GetEnterLabel(data.eventType);

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
