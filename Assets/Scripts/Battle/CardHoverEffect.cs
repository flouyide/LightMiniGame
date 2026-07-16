using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 卡牌悬浮与点击交互 —— 实现Pointer Enter/Exit/Click
/// </summary>
public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private int _handIndex;
    private HandCardLayout _layout;
    private System.Action<int> _onCardClicked;

    /// <summary>
    /// 初始化交互回调
    /// </summary>
    public void Setup(int handIndex, HandCardLayout layout, System.Action<int> onCardClicked)
    {
        _handIndex = handIndex;
        _layout = layout;
        _onCardClicked = onCardClicked;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_layout != null)
            _layout.SetHoveredIndex(_handIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_layout != null)
            _layout.SetHoveredIndex(-1);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _onCardClicked?.Invoke(_handIndex);
    }
}
