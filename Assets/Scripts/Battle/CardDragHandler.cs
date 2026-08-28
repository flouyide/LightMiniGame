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

    // 拖拽时卡牌留在原父级（手牌布局）内，仅把其所在手牌层临时提升到画布顶层，
    // 避免移动到屏幕上方敌人区域时被其它 UI 遮挡而“消失”。不再把卡牌 reparent 到根画布，
    // 以免父级切换导致坐标被重新解释（Overlay 画布下该假设脆弱，分辨率变化时会让卡牌偏移/消失）。
    private RectTransform _parentRect;
    private Vector3 _originalLocalPos;
    private Vector3 _originalLocalScale;
    private Quaternion _originalLocalRot;
    private int _originalHandSiblingIndex = -1;   // HandArea 在画布中的兄弟顺序（拖拽结束恢复）

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
        _parentRect = _rect != null ? _rect.parent as RectTransform : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_layout == null || _rect == null) return;
        _dragging = true;

        _originalLocalPos = _rect.localPosition;
        _originalLocalScale = _rect.localScale;
        _originalLocalRot = _rect.localRotation;
        if (_layout != null)
            _originalHandSiblingIndex = _layout.transform.GetSiblingIndex();

        // 把整张手牌层置顶（最后兄弟 = 最后绘制），保证被拖卡牌在拖拽期间不被其它 UI 遮挡。
        // 卡牌本身由 SetDraggedIndex 在其手牌层内置顶。
        if (_layout != null)
            _layout.transform.SetAsLastSibling();

        _layout.SetDraggedIndex(_handIndex);
        _layout.SetHoveredIndex(_handIndex);
        _rect.localScale = Vector3.one * 1.1f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging || _rect == null) return;

        // 把屏幕坐标换算为手牌层（父级）本地坐标再赋值，兼容 Overlay / Camera / 任意分辨率缩放，
        // 避免直接给 .position 传屏幕像素而当画布缩放因子非 1 时出现的偏移/位移。
        if (_parentRect != null)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                _rect.localPosition = localPoint;
            }
        }

        _rect.localRotation = Quaternion.identity;
        // 逐帧通知拖拽位置与卡牌索引，供 BattleManager 判断类型并实时高亮悬停的敌人
        _onCardDragOver?.Invoke(_handIndex, eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;

        // 恢复手牌层在容器中的层级，卡牌仍留在手牌布局内，由布局 lerp 回初始位置（若未被打出）
        if (_layout != null && _originalHandSiblingIndex >= 0)
            _layout.transform.SetSiblingIndex(_originalHandSiblingIndex);

        _layout.SetDraggedIndex(-1);
        _layout.SetHoveredIndex(-1);

        _onCardDrop?.Invoke(_handIndex, eventData.position);
    }
}