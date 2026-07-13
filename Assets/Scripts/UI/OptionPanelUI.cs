using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选项面板UI（当事件有多选项时弹出）
/// </summary>
public class OptionPanelUI : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform optionContainer;
    [SerializeField] private Button optionButtonPrefab;
    [SerializeField] private Text eventTitleText;
    [SerializeField] private Text eventDescText;

    private Action<int> _onOptionSelected;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void Show(PageEventData data, Action<int> onOptionSelected)
    {
        _onOptionSelected = onOptionSelected;

        if (panel != null)
            panel.SetActive(true);

        if (eventTitleText != null)
            eventTitleText.text = data.displayName;

        if (eventDescText != null)
            eventDescText.text = data.description;

        // 清除旧选项按钮
        foreach (Transform child in optionContainer)
            Destroy(child.gameObject);

        // 实例化新选项按钮
        for (int i = 0; i < data.options.Count; i++)
        {
            var option = data.options[i];
            var btn = Instantiate(optionButtonPrefab, optionContainer);
            var text = btn.GetComponentInChildren<Text>();
            if (text != null)
                text.text = option.optionText;

            int capturedIndex = i;
            btn.onClick.AddListener(() => OnOptionClicked(capturedIndex));
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
