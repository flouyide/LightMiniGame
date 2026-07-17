using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 选项面板UI（事件类型专用）
/// </summary>
public class OptionPanelUI : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform optionContainer;
    [SerializeField] private Button optionButtonPrefab;
    [SerializeField] private TextMeshProUGUI eventTitleText;
    [SerializeField] private TextMeshProUGUI eventDescText;

    [Header("原画")]
    [Tooltip("事件大图（未绑定时跳过；sprite 为空时显示半透明占位）")]
    [SerializeField] private Image eventImage;

    private Action<int> _onOptionSelected;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    /// <summary>
    /// 显示事件面板，根据 data.options 动态生成选项按钮。
    /// 回调参数 = 选中的选项索引。
    /// </summary>
    public void Show(PageEventData data, Action<int> onOptionSelected)
    {
        _onOptionSelected = onOptionSelected;

        if (panel != null)
            panel.SetActive(true);

        if (eventTitleText != null)
            eventTitleText.text = data.displayName;

        if (eventDescText != null)
            eventDescText.text = data.description;

        // 原画：复用 PageEventData.icon，没有时显示半透明占位
        if (eventImage != null)
        {
            if (data.icon != null)
            {
                eventImage.sprite = data.icon;
                eventImage.color = Color.white;
            }
            else
            {
                eventImage.sprite = null;
                eventImage.color = new Color(1, 1, 1, 0.1f);
            }
        }

        // 清除旧选项按钮
        foreach (Transform child in optionContainer)
            Destroy(child.gameObject);

        if (optionContainer != null)
            optionContainer.gameObject.SetActive(true);

        // 实例化选项按钮
        if (data.options != null)
        {
            for (int i = 0; i < data.options.Count; i++)
            {
                var option = data.options[i];
                var btn = Instantiate(optionButtonPrefab, optionContainer);
                var text = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                    text.text = option.optionText;

                int capturedIndex = i;
                btn.onClick.AddListener(() => OnOptionClicked(capturedIndex));
            }
        }
    }

    private void OnOptionClicked(int index)
    {
        _onOptionSelected?.Invoke(index);
        Hide();
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }
}
