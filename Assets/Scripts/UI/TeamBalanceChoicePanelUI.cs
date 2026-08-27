using System;
using System.Collections.Generic;
using LightMiniGame.CardEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 站队天平专用的双卡选择面板。
/// 预制体内固定使用名为“卡牌”和“卡牌2”的两个节点；各节点须包含 CardDisplay 与 Button。
/// 引用在运行时按节点名解析，因此不需要在预制体中配置持久化 Button.onClick。
/// </summary>
public class TeamBalanceChoicePanelUI : MonoBehaviour
{
    private const string FirstCardNodeName = "卡牌";
    private const string SecondCardNodeName = "卡牌2";

    private GameObject _firstCardRoot;
    private GameObject _secondCardRoot;
    private Button _firstButton;
    private Button _secondButton;
    private CardDisplay _firstDisplay;
    private CardDisplay _secondDisplay;
    private readonly List<CardEntry> _choices = new List<CardEntry>(2);
    private Action<CardEntry> _onSelected;
    private bool _resolved;

    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 展示当前宿主敌人的候选技能。返回 false 表示预制体节点或组件不完整，调用方应做安全兜底。
    /// </summary>
    public bool Show(EnemyInstance host, IList<CardEntry> candidates, bool lowSanity, Action<CardEntry> onSelected)
    {
        if (!ResolveReferences() || host == null || candidates == null)
            return false;

        _choices.Clear();
        for (int i = 0; i < candidates.Count && _choices.Count < 2; i++)
        {
            if (candidates[i] != null)
                _choices.Add(candidates[i]);
        }
        if (_choices.Count == 0)
            return false;

        _resolved = false;
        _onSelected = onSelected;
        ConfigureSlot(_firstCardRoot, _firstButton, _firstDisplay, _choices[0], host, lowSanity, 0);

        bool hasSecond = _choices.Count > 1;
        if (_secondCardRoot != null)
            _secondCardRoot.SetActive(hasSecond);
        if (hasSecond)
            ConfigureSlot(_secondCardRoot, _secondButton, _secondDisplay, _choices[1], host, lowSanity, 1);

        gameObject.SetActive(true);
        return true;
    }

    public void Hide()
    {
        ClearButtonListeners();
        _choices.Clear();
        _onSelected = null;
        _resolved = false;
        gameObject.SetActive(false);
    }

    private void ConfigureSlot(
        GameObject root,
        Button button,
        CardDisplay display,
        CardEntry entry,
        EnemyInstance host,
        bool lowSanity,
        int choiceIndex)
    {
        if (root != null)
            root.SetActive(true);

        if (display != null)
        {
            display.ApplyCardEntry(entry, lowSanity);
            display.SetEnemyAttributeContext(host.EffectiveStrength, host.EffectiveDexterity);
            display.SetPlayable(true);

            // 候选卡只承担“点击选择”职责；禁止模板自带的拖拽处理抢占 Pointer 事件。
            CardDragHandler dragHandler = display.GetComponent<CardDragHandler>();
            if (dragHandler != null)
                dragHandler.enabled = false;
        }

        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.interactable = true;
        button.onClick.AddListener(() => Select(choiceIndex));
    }

    private void Select(int choiceIndex)
    {
        if (_resolved || choiceIndex < 0 || choiceIndex >= _choices.Count)
            return;

        _resolved = true;
        CardEntry selected = _choices[choiceIndex];
        Action<CardEntry> callback = _onSelected;
        Hide();
        callback?.Invoke(selected);
    }

    private bool ResolveReferences()
    {
        if (_firstCardRoot == null)
            _firstCardRoot = FindDirectChild(FirstCardNodeName);
        if (_secondCardRoot == null)
            _secondCardRoot = FindDirectChild(SecondCardNodeName);

        if (_firstCardRoot != null)
        {
            _firstButton ??= _firstCardRoot.GetComponent<Button>();
            _firstDisplay ??= _firstCardRoot.GetComponent<CardDisplay>();
        }
        if (_secondCardRoot != null)
        {
            _secondButton ??= _secondCardRoot.GetComponent<Button>();
            _secondDisplay ??= _secondCardRoot.GetComponent<CardDisplay>();
        }

        bool valid = _firstCardRoot != null && _firstButton != null && _firstDisplay != null
            && _secondCardRoot != null && _secondButton != null && _secondDisplay != null;
        if (!valid)
            Debug.LogError("[TeamBalanceChoicePanelUI] 预制体必须包含“卡牌”和“卡牌2”节点，且每个节点均需有 CardDisplay 与 Button。");
        return valid;
    }

    private GameObject FindDirectChild(string nodeName)
    {
        Transform child = transform.Find(nodeName);
        return child != null ? child.gameObject : null;
    }

    private void ClearButtonListeners()
    {
        _firstButton?.onClick.RemoveAllListeners();
        _secondButton?.onClick.RemoveAllListeners();
    }
}
