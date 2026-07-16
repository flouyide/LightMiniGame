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

    [Header("删除按钮显示规则（按事件类型）")]
    [Tooltip("勾选表示该类型卡片显示删除按钮")]
    [SerializeField] private bool showDeleteOnBattle = false;   // 战斗：默认不显示
    [SerializeField] private bool showDeleteOnShop = true;      // 商店：默认显示
    [SerializeField] private bool showDeleteOnRest = true;      // 休整：默认显示
    [SerializeField] private bool showDeleteOnEvent = true;      // 事件：默认显示

    [Header("事件类型颜色")]
    [SerializeField] private Color ColorBattle = new(0.8f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color ColorShop = new(0.9f, 0.75f, 0.2f, 1f);
    [SerializeField] private Color ColorRest = new(0.3f, 0.7f, 0.4f, 1f);
    [SerializeField] private Color ColorEvent = new(0.6f, 0.3f, 0.8f, 1f);

    [Header("类型名称文案")]
    [SerializeField] private string typeNameBattle = "战斗";
    [SerializeField] private string typeNameShop = "商店";
    [SerializeField] private string typeNameRest = "休整";
    [SerializeField] private string typeNameEvent = "事件";

    [Header("进入按钮文案")]
    [SerializeField] private string enterLabelBattle = "进入战斗";
    [SerializeField] private string enterLabelShop = "进入商店";
    [SerializeField] private string enterLabelRest = "进入休整";
    [SerializeField] private string enterLabelEvent = "进入事件";

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

        // 底部"进入"按钮：按类型上色 + 文案（进入战斗/商店/休整/事件）
        if (enterButton != null && enterButton.image != null)
            enterButton.image.color = typeColor;
        if (enterButtonText != null)
            enterButtonText.text = GetEnterLabel(data.eventType);

        if (finalNodeIndicator != null)
            finalNodeIndicator.SetActive(data.isFinalNode);

        // 根据事件类型决定是否显示删除按钮
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(ShouldShowDelete(data.eventType));
    }

    private string GetTypeName(PageEventType type) => type switch
    {
        PageEventType.Battle => typeNameBattle,
        PageEventType.Shop => typeNameShop,
        PageEventType.Rest => typeNameRest,
        PageEventType.Event => typeNameEvent,
        _ => "未知"
    };

    // 底部按钮文案：进入 + 类型
    private string GetEnterLabel(PageEventType type) => type switch
    {
        PageEventType.Battle => enterLabelBattle,
        PageEventType.Shop => enterLabelShop,
        PageEventType.Rest => enterLabelRest,
        PageEventType.Event => enterLabelEvent,
        _ => "进入"
    };

    private Color GetColor(PageEventType type) => type switch
    {
        PageEventType.Battle => ColorBattle,
        PageEventType.Shop => ColorShop,
        PageEventType.Rest => ColorRest,
        PageEventType.Event => ColorEvent,
        _ => Color.white
    };

    // 根据事件类型判断是否显示删除按钮
    private bool ShouldShowDelete(PageEventType type) => type switch
    {
        PageEventType.Battle => showDeleteOnBattle,
        PageEventType.Shop => showDeleteOnShop,
        PageEventType.Rest => showDeleteOnRest,
        PageEventType.Event => showDeleteOnEvent,
        _ => true
    };
}
