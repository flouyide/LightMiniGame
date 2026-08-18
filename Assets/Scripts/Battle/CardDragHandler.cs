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

    // 拖拽期间将卡牌临时提升到根画布顶层，避免在移动到屏幕上方敌人区域时被其它 UI 遮挡而“消失”。
    // 记录原父节点，拖拽结束后恢复，交回手牌布局做弹回/平滑归位。
    private Transform _originalParent;
    private Vector3 _originalLocalPos;
    private Vector3 _originalLocalScale;
    private Quaternion _originalLocalRot;

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

        // 记录原父节点与本地变换，供结束后恢复
        _originalParent = transform.parent;
        _originalLocalPos = transform.localPosition;
        _originalLocalScale = transform.localScale;
        _originalLocalRot = transform.localRotation;

        // 临时提升到根画布顶层：Overlay 画布下世界坐标==屏幕坐标，跟随指针不受父级布局/遮挡影响
        Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas != null)
        {
            transform.SetParent(rootCanvas.transform, false);
            transform.SetAsLastSibling();
        }

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

        // 恢复父节点，交由布局 lerp 回初始位置（若未被打出）
        if (_originalParent != null)
            transform.SetParent(_originalParent, true);

        // 恢复本地变换基准（相对手牌父节点重新解释），使布局能平滑归位
        if (_rect != null)
        {
            _rect.localScale = _originalLocalScale;
            _rect.localRotation = _originalLocalRot;
        }

        _layout.SetDraggedIndex(-1);
        _layout.SetHoveredIndex(-1);
        if (_rect != null) _rect.localScale = Vector3.one;

        _onCardDrop?.Invoke(_handIndex, eventData.position);
    }
}