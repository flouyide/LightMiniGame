using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 商店面板UI（仅做 UI 显示，暂不实现购买/出售逻辑）
/// </summary>
public class ShopPanelUI : MonoBehaviour
{
    [Header("UI引用（未绑定时按名字自动查找子对象）")]
    [SerializeField] private GameObject panel;
    [Tooltip("右上角 X 按钮，点击关闭商店")]
    [SerializeField] private Button closeButton;
    [Tooltip("提示文字（\"点击卡牌/遗物...\"）")]
    [SerializeField] private TextMeshProUGUI hintText;

    private Action _onClose;
    private bool _closeHooked;

    private void Start()
    {
        // 兜底：未绑定时按名字查找子对象（用户可省略 Inspector 拖拽）
        if (panel == null) panel = transform.Find("Panel")?.gameObject;
        if (closeButton == null) closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
        if (hintText == null) hintText = transform.Find("HintText")?.GetComponent<TextMeshProUGUI>();

        if (panel != null) panel.SetActive(false);
        HookCloseButton();
    }

    private void OnEnable()
    {
        HookCloseButton();
    }

    private void OnDisable()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
        _closeHooked = false;
    }

    private void HookCloseButton()
    {
        if (closeButton != null && !_closeHooked)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
            _closeHooked = true;
        }
    }

    /// <summary>
    /// 显示商店面板，关闭时调用 onClose
    /// </summary>
    public void Show(Action onClose)
    {
        _onClose = onClose;
        if (panel != null) panel.SetActive(true);
    }

    private void OnCloseClicked()
    {
        _onClose?.Invoke();
        Hide();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        _onClose = null;
    }
}
