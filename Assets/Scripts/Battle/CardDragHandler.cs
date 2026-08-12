using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 卡牌拖拽出牌 —— 实现 BeginDrag / Drag / EndDrag。
/// 拖拽时卡牌跟随指针并置顶，并逐帧回调拖拽位置（用于高亮悬停的敌人）；
/// 释放时回调 HandCardLayout 注册的 drop 回调，由 BattleManager 判断是否命中某个敌人区域决定出牌或弹回。
/// </summary>
public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private int _handIndex = -1;
    private HandCardLayout _layout;
    private System.Action<int, Vector2> _onCardDrop;
    private System.Action<int, Vector2> _onCardDragOver;
    private RectTransform _rect;
    private bool _dragging = false;

    /// <summary>是否正在拖拽中（供其它组件判断，如抑制点击）</summary>
    public bool IsDragging => _dragging;

    /// <summary>初始化拖拽回调（由 HandCardLayout 在实例化卡牌时调用）</summary>
    public void Setup(int handIndex, HandCardLayout layout, System.Action<int, Vector2> onCardDrop, System.Action<int, Vector2> onCardDragOver)
    {
        _handIndex = handIndex;
        _layout = layout;
        _onCardDrop = onCardDrop;
        _onCardDragOver = onCardDragOver;
        _rect = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_layout == null) return;
        _dragging = true;
        _layout.SetDraggedIndex(_handIndex);
        _layout.SetHoveredIndex(_handIndex);
        if (_rect != null) _rect.localScale = Vector3.one * 1.1f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging || _rect == null) return;
        // 屏幕坐标：BattleCanvas 为 Overlay 时即屏幕位置
        _rect.position = eventData.position;
        _rect.localRotation = Quaternion.identity;
        // 逐帧通知拖拽位置与卡牌索引，供 BattleManager 判断类型并实时高亮悬停的敌人
        _onCardDragOver?.Invoke(_handIndex, eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;

        // 释放：结束拖拽、清除悬浮，交由布局 lerp 回初始位置（若未被打出）
        _layout.SetDraggedIndex(-1);
        _layout.SetHoveredIndex(-1);
        if (_rect != null) _rect.localScale = Vector3.one;

        _onCardDrop?.Invoke(_handIndex, eventData.position);
    }
}