using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 手牌扇形布局管理 —— 负责卡牌的弧形排列、悬浮放大、丝滑过渡动画。
/// 可见手牌默认最多 5 张；超出部分叠在两侧，鼠标移到扇形最左/最右时平滑滑入。
/// 接收 CardData 列表，根据卡牌类型自动选择对应 Prefab 实例化并填充数据。
/// </summary>
public class HandCardLayout : MonoBehaviour
{
    [Header("布局参数")]
    [SerializeField] private float cardWidth = 180f;
    [SerializeField] private float cardSpacing = 140f;
    [SerializeField] private float fanRadius = 1000f;
    [SerializeField] private float maxFanAngle = 15f;

    [Header("悬浮效果")]
    [SerializeField] private float hoverScale = 1.4f;
    [SerializeField] private float hoverYOffset = 100f;

    [Header("动画")]
    [SerializeField] private float lerpSpeed = 20f;

    [Header("溢出手牌（超过可见张数时靠边滑动）")]
    [SerializeField] private int maxVisibleCards = 5;
    [Tooltip("最左/最右触发滑动的宽度（像素，含扇形外侧空白）")]
    [SerializeField] private float edgeZoneWidth = 72f;
    [Tooltip("按住边缘时每秒滑过的卡牌数")]
    [SerializeField] private float scrollSpeed = 2.4f;
    [Tooltip("窗口滑动平滑时间")]
    [SerializeField] private float scrollSmoothTime = 0.14f;
    [Tooltip("未滑入窗口的牌从扇形外侧滑入/滑出的距离（槽位）")]
    [SerializeField] private float overflowPeekSlots = 0.7f;

    [Header("卡牌预制体（按类型）")]
    [SerializeField] private GameObject attackCardPrefab;
    [FormerlySerializedAs("armorCardPrefab")] [SerializeField] private GameObject skillCardPrefab;
    [FormerlySerializedAs("buffCardPrefab")] [SerializeField] private GameObject abilityCardPrefab;

    private readonly List<GameObject> _cardObjects = new List<GameObject>();
    private readonly List<CardDisplay> _cardDisplays = new List<CardDisplay>();
    private readonly List<CardData> _cardDataRefs = new List<CardData>();
    private readonly List<Vector3> _targetPositions = new List<Vector3>();
    private readonly List<Quaternion> _targetRotations = new List<Quaternion>();
    private readonly List<Vector3> _targetScales = new List<Vector3>();
    private int _hoveredIndex = -1;
    private int _draggedIndex = -1;
    private float _scrollOffset;
    private float _scrollTarget;
    private float _scrollVelocity;
    private int _siblingStamp = int.MinValue;
    private readonly List<CanvasGroup> _cardCanvasGroups = new List<CanvasGroup>();
    private System.Action<int> _onCardClicked;
    private System.Action<int, UnityEngine.Vector2> _onCardDrop;
    private System.Action<int, UnityEngine.Vector2> _onCardDragOver;

    public int CardCount => _cardObjects.Count;

    /// <summary>返回指定索引手牌卡面视图的 RectTransform（越界返回 null），用于原位徽章定位。</summary>
    public RectTransform GetCardViewTransform(int index)
    {
        if (index < 0 || index >= _cardObjects.Count || _cardObjects[index] == null)
            return null;
        return _cardObjects[index].transform as RectTransform;
    }

    /// <summary>返回指定索引手牌的 CardDisplay（越界返回 null），用于数字字符精确定位。</summary>
    public CardDisplay GetCardDisplay(int index)
    {
        if (index < 0 || index >= _cardDisplays.Count) return null;
        return _cardDisplays[index];
    }

    public void SetCardClickCallback(System.Action<int> callback)
    {
        _onCardClicked = callback;
    }

    /// <summary>设置拖拽出牌回调（handIndex, 释放时屏幕坐标）。未命中目标时由调用方自行处理。</summary>
    public void SetCardDropCallback(System.Action<int, UnityEngine.Vector2> callback)
    {
        _onCardDrop = callback;
    }

    /// <summary>设置拖拽过程中逐帧回调（handIndex, 拖拽时屏幕坐标），用于实时高亮悬停的敌人。未命中时由调用方自行处理。</summary>
    public void SetCardDragOverCallback(System.Action<int, UnityEngine.Vector2> callback)
    {
        _onCardDragOver = callback;
    }

    /// <summary>
    /// 标记正在被拖拽的卡牌索引；拖拽期间该卡不参与布局 lerp（位置由拖拽控制），
    /// 并置顶。传入 -1 表示结束拖拽。
    /// </summary>
    public void SetDraggedIndex(int index)
    {
        if (_draggedIndex == index) return;
        _draggedIndex = index;
        if (index >= 0 && index < _cardObjects.Count && _cardObjects[index] != null)
            _cardObjects[index].transform.SetAsLastSibling();
    }

    /// <summary>
    /// 设置卡牌预制体（可由外部注入）
    /// </summary>
    public void SetCardPrefabs(GameObject attack, GameObject skill, GameObject ability)
    {
        attackCardPrefab = attack;
        skillCardPrefab = skill;
        abilityCardPrefab = ability;
    }

    private GameObject GetPrefabForType(CardType type)
    {
        return type switch
        {
            CardType.Attack => attackCardPrefab,
            CardType.Skill => skillCardPrefab,
            CardType.Ability => abilityCardPrefab,
            _ => attackCardPrefab
        };
    }

    /// <summary>
    /// 更新手牌显示 —— 传入 CardData 列表，自动实例化对应类型 Prefab 并填充数据
    /// </summary>
    public void UpdateHand(List<CardData> hand, System.Func<CardData, bool> isPlayable = null)
    {
        // 销毁旧卡牌
        foreach (var obj in _cardObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _cardObjects.Clear();
        _cardDisplays.Clear();
        _cardDataRefs.Clear();
        _cardCanvasGroups.Clear();

        // 创建新卡牌
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] == null)
            {
                Debug.LogError($"[HandCardLayout] hand[{i}] 为 null，跳过实例化");
                continue;
            }

            var prefab = GetPrefabForType(hand[i].cardType);
            if (prefab == null)
            {
                Debug.LogError($"[HandCardLayout] 未找到卡牌类型 {hand[i].cardType} 对应的 Prefab");
                continue;
            }

            var cardObj = Instantiate(prefab, transform);
            var display = cardObj.GetComponent<CardDisplay>();
            if (display == null)
            {
                Debug.LogError($"[HandCardLayout] 卡牌Prefab缺少CardDisplay组件: {prefab.name}");
                Destroy(cardObj);
                continue;
            }

            display.ApplyCardData(hand[i]);
            if (isPlayable != null)
                display.SetPlayable(isPlayable(hand[i]));

            var hover = cardObj.GetComponent<CardHoverEffect>();
            if (hover != null)
                hover.Setup(i, this, _onCardClicked);

            var drag = cardObj.GetComponent<CardDragHandler>();
            if (drag != null)
                drag.Setup(i, this, _onCardDrop, _onCardDragOver);

            cardObj.transform.localPosition = new Vector3(0, -200f, 0);
            _cardObjects.Add(cardObj);
            _cardDisplays.Add(display);
            _cardDataRefs.Add(hand[i]);

            var cg = cardObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = cardObj.AddComponent<CanvasGroup>();
            _cardCanvasGroups.Add(cg);
        }

        _hoveredIndex = -1;
        _siblingStamp = int.MinValue;
        ClampScroll();
        CalculateLayout();
    }

    /// <summary>
    /// 刷新已存在卡牌的可打出状态
    /// </summary>
    public void RefreshPlayable(System.Func<CardData, bool> isPlayable)
    {
        for (int i = 0; i < _cardDisplays.Count; i++)
        {
            if (_cardDisplays[i] != null && isPlayable != null && i < _cardDataRefs.Count)
                _cardDisplays[i].SetPlayable(isPlayable(_cardDataRefs[i]));
        }
    }

    /// <summary>
    /// 刷新所有手牌的描述显示（力量/敏捷变化后调用，使卡面数值实时更新）。
    /// 不重建卡牌对象，仅调用 UpdateDisplay 重刷文本。
    /// </summary>
    public void RefreshCardDisplays()
    {
        for (int i = 0; i < _cardDisplays.Count; i++)
        {
            if (_cardDisplays[i] != null)
                _cardDisplays[i].UpdateDisplay();
        }
    }

    public void SetHoveredIndex(int index)
    {
        if (_hoveredIndex == index) return;
        _hoveredIndex = index;
        CalculateLayout();
    }

    private int VisibleSlotCount
    {
        get
        {
            int cap = Mathf.Max(1, maxVisibleCards);
            return Mathf.Min(_cardObjects.Count, cap);
        }
    }

    private float MaxScroll => Mathf.Max(0f, _cardObjects.Count - VisibleSlotCount);

    private void ClampScroll()
    {
        float max = MaxScroll;
        _scrollTarget = Mathf.Clamp(_scrollTarget, 0f, max);
        _scrollOffset = Mathf.Clamp(_scrollOffset, 0f, max);
        if (max <= 0f)
            _scrollVelocity = 0f;
    }

    private void CalculateLayout()
    {
        _targetPositions.Clear();
        _targetRotations.Clear();
        _targetScales.Clear();

        int count = _cardObjects.Count;
        if (count == 0) return;

        int vis = VisibleSlotCount;
        bool windowed = count > vis;
        float totalWidth = vis <= 1 ? 0f : (vis - 1) * cardSpacing;
        float fanLeft = -totalWidth / 2f;
        float peek = Mathf.Max(0.05f, overflowPeekSlots);

        for (int i = 0; i < count; i++)
        {
            float slot = windowed ? i - _scrollOffset : i;
            float displaySlot = windowed ? Mathf.Clamp(slot, -peek, vis - 1 + peek) : slot;

            float t = vis <= 1 ? 0.5f : displaySlot / (vis - 1);
            float angle = Mathf.Lerp(-maxFanAngle / 2f, maxFanAngle / 2f, t);
            float rad = angle * Mathf.Deg2Rad;
            float x = fanLeft + displaySlot * cardSpacing;
            float y = -fanRadius + Mathf.Cos(rad) * fanRadius;

            bool overflowHidden = windowed && (slot < -peek || slot > vis - 1 + peek);
            float scale = overflowHidden ? 0.9f : 1f;
            if (overflowHidden)
                y -= 12f;

            bool hoveredInFan = i == _hoveredIndex && !overflowHidden;
            if (hoveredInFan)
            {
                y += hoverYOffset;
                scale = hoverScale;
                angle = 0f;
            }

            _targetPositions.Add(new Vector3(x, y, 0));
            _targetRotations.Add(Quaternion.Euler(0, 0, -angle));
            _targetScales.Add(Vector3.one * scale);

            bool interactable = !windowed || (slot >= -peek - 0.15f && slot <= vis - 1 + peek + 0.15f);
            SetCardRaycast(i, interactable);
        }

        RefreshSiblingOrder(count, vis, windowed);
    }

    private void SetCardRaycast(int index, bool on)
    {
        if (index < 0 || index >= _cardCanvasGroups.Count) return;
        var cg = _cardCanvasGroups[index];
        if (cg == null) return;
        cg.blocksRaycasts = on;
        cg.interactable = on;
    }

    /// <summary>
    /// 溢出牌叠在两侧时：深处的牌在后，窗口内的牌在前，悬停/拖拽的牌置顶。
    /// </summary>
    private void RefreshSiblingOrder(int count, int vis, bool windowed)
    {
        float peek = Mathf.Max(0.05f, overflowPeekSlots);
        int windowStart = windowed ? Mathf.FloorToInt(_scrollOffset + peek) : 0;
        int stamp = count * 397 ^ vis * 17 ^ windowStart ^ (_hoveredIndex + 3) * 31 ^ (_draggedIndex + 5);
        if (stamp == _siblingStamp) return;
        _siblingStamp = stamp;

        int sibling = 0;
        if (windowed)
        {
            for (int i = 0; i < count; i++)
            {
                if (_cardObjects[i] == null) continue;
                float slot = i - _scrollOffset;
                if (slot < -peek)
                    _cardObjects[i].transform.SetSiblingIndex(sibling++);
            }

            for (int i = count - 1; i >= 0; i--)
            {
                if (_cardObjects[i] == null) continue;
                float slot = i - _scrollOffset;
                if (slot > vis - 1 + peek)
                    _cardObjects[i].transform.SetSiblingIndex(sibling++);
            }

            for (int i = 0; i < count; i++)
            {
                if (_cardObjects[i] == null) continue;
                float slot = i - _scrollOffset;
                if (slot >= -peek && slot <= vis - 1 + peek)
                    _cardObjects[i].transform.SetSiblingIndex(sibling++);
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                if (_cardObjects[i] != null)
                    _cardObjects[i].transform.SetSiblingIndex(sibling++);
            }
        }

        if (_hoveredIndex >= 0 && _hoveredIndex < count && _cardObjects[_hoveredIndex] != null)
            _cardObjects[_hoveredIndex].transform.SetAsLastSibling();
        if (_draggedIndex >= 0 && _draggedIndex < count && _cardObjects[_draggedIndex] != null)
            _cardObjects[_draggedIndex].transform.SetAsLastSibling();
    }

    private bool TryGetLocalMouse(RectTransform rt, out Vector2 local)
    {
        local = default;
        var canvas = GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Input.mousePosition, cam, out local);
    }

    private void UpdateOverflowScroll()
    {
        ClampScroll();
        float maxScroll = MaxScroll;
        if (maxScroll <= 0f)
            return;

        if (_draggedIndex < 0 && transform is RectTransform rt && TryGetLocalMouse(rt, out Vector2 local))
        {
            Rect r = rt.rect;
            bool inHand =
                local.x >= r.xMin && local.x <= r.xMax &&
                local.y >= r.yMin - 80f && local.y <= r.yMax;

            if (inHand)
            {
                int vis = VisibleSlotCount;
                float totalWidth = vis <= 1 ? 0f : (vis - 1) * cardSpacing;
                float fanLeft = -totalWidth / 2f;
                float fanRight = totalWidth / 2f;
                float zone = Mathf.Max(24f, edgeZoneWidth);

                if (local.x <= fanLeft + zone)
                    _scrollTarget -= scrollSpeed * Time.deltaTime;
                else if (local.x >= fanRight - zone)
                    _scrollTarget += scrollSpeed * Time.deltaTime;

                _scrollTarget = Mathf.Clamp(_scrollTarget, 0f, maxScroll);
            }
        }

        _scrollOffset = Mathf.SmoothDamp(
            _scrollOffset, _scrollTarget, ref _scrollVelocity, Mathf.Max(0.04f, scrollSmoothTime));
        if (Mathf.Abs(_scrollOffset - _scrollTarget) < 0.001f)
        {
            _scrollOffset = _scrollTarget;
            _scrollVelocity = 0f;
        }
    }

    /// <summary>
    /// 立即把手牌摆到目标布局位置（跳过 lerp 动画）。
    /// 融合高亮读取卡面坐标时需要卡牌处于最终位置，否则会读到动画中途坐标造成错位。
    /// </summary>
    public void SnapToTarget()
    {
        for (int i = 0; i < _cardObjects.Count; i++)
        {
            if (_cardObjects[i] == null || i >= _targetPositions.Count) continue;
            var trans = _cardObjects[i].transform;
            trans.localPosition = _targetPositions[i];
            if (i < _targetRotations.Count) trans.localRotation = _targetRotations[i];
            if (i < _targetScales.Count) trans.localScale = _targetScales[i];
        }
    }

    private void Update()
    {
        if (_cardObjects.Count == 0) return;

        UpdateOverflowScroll();
        CalculateLayout();

        float follow = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        for (int i = 0; i < _cardObjects.Count; i++)
        {
            if (_cardObjects[i] == null) continue;
            if (i >= _targetPositions.Count) continue;
            // 拖拽中的卡牌位置由拖拽处理器控制，布局不与其抢位
            if (i == _draggedIndex)
            {
                _cardObjects[i].transform.localRotation = Quaternion.identity;
                continue;
            }
            var trans = _cardObjects[i].transform;
            trans.localPosition = Vector3.Lerp(trans.localPosition, _targetPositions[i], follow);
            trans.localRotation = Quaternion.Slerp(trans.localRotation, _targetRotations[i], follow);
            trans.localScale = Vector3.Lerp(trans.localScale, _targetScales[i], follow);
        }
    }
}