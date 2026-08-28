using System;
using LightMiniGame.Card;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 选人界面中的单张角色卡。
/// 角色卡预制体只负责自身的悬停说明和选中标记；人数限制与开局逻辑由
/// CharacterSelectionPanelUI 统一管理。
/// </summary>
public class CharacterSelectionCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("角色数据")]
    [Tooltip("该角色卡对应的固定角色数据。优先使用此引用，避免仅依赖头像 Sprite 推断角色。")]
    [SerializeField] private CharacterData characterData;

    private CharacterData _character;
    private GameObject _descriptionRoot;
    private TextMeshProUGUI _descriptionText;
    private GameObject _markRoot;
    private Button _button;
    private Action<CharacterSelectionCardUI> _onClicked;

    public CharacterData Character => _character != null ? _character : characterData;
    public bool IsSelected { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        EnsurePanelRegistration();
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleClick);
    }

    /// <summary>由选人面板注入角色数据和点击回调。</summary>
    public void Configure(CharacterData character, Action<CharacterSelectionCardUI> onClicked)
    {
        _character = character != null ? character : characterData;
        _onClicked = onClicked;

        ResolveReferences();
        if (_descriptionText != null)
            _descriptionText.text = Character != null ? Character.description : string.Empty;

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (_markRoot != null)
            _markRoot.SetActive(selected);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_descriptionRoot != null && _character != null)
            _descriptionRoot.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_descriptionRoot != null)
            _descriptionRoot.SetActive(false);
    }

    private void HandleClick()
    {
        _onClicked?.Invoke(this);
    }

    private void ResolveReferences()
    {
        if (_descriptionRoot == null)
        {
            var desc = transform.Find("Desc");
            if (desc != null)
            {
                _descriptionRoot = desc.gameObject;
                _descriptionText = desc.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_markRoot == null)
        {
            var mark = transform.Find("Mark");
            if (mark != null)
                _markRoot = mark.gameObject;
        }

        if (_button == null)
        {
            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
                _button.onClick.AddListener(HandleClick);
            }
        }

        if (_descriptionRoot != null)
            _descriptionRoot.SetActive(false);
        if (_markRoot != null)
            _markRoot.SetActive(false);
    }

    private void EnsurePanelRegistration()
    {
        // 选人界面可能嵌在场景 Canvas 或其他外层预制体下，不能依赖 transform.root 的名称。
        // 优先复用最近的面板组件；首次加载时再沿父级链定位“选人界面”并挂载面板。
        var panel = GetComponentInParent<CharacterSelectionPanelUI>(true);
        if (panel == null)
        {
            var selectionRoot = FindSelectionPanelRoot();
            if (selectionRoot == null)
                return;

            panel = selectionRoot.GetComponent<CharacterSelectionPanelUI>();
            if (panel == null)
                panel = selectionRoot.gameObject.AddComponent<CharacterSelectionPanelUI>();
        }

        panel.RegisterCard(this);
    }

    private Transform FindSelectionPanelRoot()
    {
        for (var current = transform; current != null; current = current.parent)
        {
            if (current.name == "选人界面")
                return current;
        }

        return null;
    }
}
