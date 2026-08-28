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

    [Header("抽牌飞入")]
    [Tooltip("从抽牌堆飞入时的起始缩放")]
    [SerializeField] private float drawStartScale = 0.38f;
    [Tooltip("多张连抽时，后一张相对前一张的延迟（秒）")]
    [SerializeField] private float drawStagger = 0.09f;
    [Tooltip("飞入持续时间（秒）")]
    [SerializeField] private float drawFlightDuration = 0.34f;
    [Tooltip("飞入抛物线高度（手牌本地像素）")]
    [SerializeField] private float drawArcHeight = 90f;

    [Header("弃牌 / 消耗")]
    [Tooltip("非消耗牌飞入弃牌堆的时长（秒）")]
    [SerializeField] private float discardFlightDuration = 0.32f;
    [Tooltip("飞入弃牌堆的抛物线高度")]
    [SerializeField] private float discardArcHeight = 70f;
    [Tooltip("消耗牌渐隐时长（秒）")]
    [SerializeField] private float exhaustFadeDuration = 0.28f;
    [Tooltip("回合结束多张弃牌时的错开间隔（秒）")]
    [SerializeField] private float discardStagger = 0.055f;
    [Tooltip("飞到弃牌堆时的结束缩放")]
    [SerializeField] private float discardEndScale = 0.32f;
    [Tooltip("飞入进度达到该值后，卡牌图层收到弃牌堆后面")]
    [SerializeField] [Range(0.2f, 0.95f)] private float tuckBehindDiscardAt = 0.55f;

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
    private readonly List<float> _drawHoldUntil = new List<float>();
    private readonly List<float> _drawFlightT = new List<float>();
    private readonly List<Vector3> _drawStartPos = new List<Vector3>();
    private readonly List<Vector3> _drawStartScale = new List<Vector3>();
    private readonly List<Quaternion> _drawStartRot = new List<Quaternion>();
    private RectTransform _drawPileOrigin;
    private RectTransform _discardPileOrigin;
    private int _activeDrawFlights;
    private readonly List<ExitingCard> _exiting = new List<ExitingCard>();
    private System.Action<int> _onCardClicked;
    private System.Action<int, UnityEngine.Vector2> _onCardDrop;
    private System.Action<int, UnityEngine.Vector2> _onCardDragOver;

    public int CardCount => _cardObjects.Count;
    public bool HasActiveDrawFlights => _activeDrawFlights > 0;
    public bool HasActiveCardExits => _exiting.Count > 0;
    public event System.Action OnDrawFlightsStarted;
    public event System.Action OnDrawFlightsFinished;
    public event System.Action OnCardExitsFinished;

    public void SetDrawPileOrigin(RectTransform pileIcon)
    {
        _drawPileOrigin = pileIcon;
    }

    public void SetDiscardPileOrigin(RectTransform pileIcon)
    {
        _discardPileOrigin = pileIcon;
    }

    private sealed class ExitingCard
    {
        public GameObject go;
        public CanvasGroup cg;
        public bool exhaust;
        public float holdUntil;
        public float t;
        public Vector3 startPos;
        public Vector3 startScale;
        public Quaternion startRot;
        public bool tuckedBehindPile;
    }

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

    private CardData IndexToData(int index)
    {
        if (index < 0 || index >= _cardDataRefs.Count) return null;
        return _cardDataRefs[index];
    }

    private int DataToIndex(CardData data)
    {
        if (data == null) return -1;
        for (int i = 0; i < _cardDataRefs.Count; i++)
            if (_cardDataRefs[i] == data) return i;
        return -1;
    }

    private Vector3 GetDrawStartLocal()
    {
        if (_drawPileOrigin == null)
            return new Vector3(-420f, -40f, 0f);
        return PileCenterToHandLocal(_drawPileOrigin);
    }

    private Vector3 GetDiscardEndLocal()
    {
        if (_discardPileOrigin == null)
            return new Vector3(420f, -40f, 0f);
        return PileCenterToHandLocal(_discardPileOrigin);
    }

    /// <summary>
    /// 把牌堆图标的矩形中心换到手牌层本地坐标。
    /// 抽牌堆锚点在左上、弃牌堆锚点在右下，直接用 position 会对准角落而不是图标中心。
    /// </summary>
    private Vector3 PileCenterToHandLocal(RectTransform pile)
    {
        Vector3 worldCenter = pile.TransformPoint(pile.rect.center);
        var hand = transform as RectTransform;
        if (hand == null)
            return transform.InverseTransformPoint(worldCenter);

        var canvas = GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(hand, screen, cam, out Vector2 local))
            return local;
        return transform.InverseTransformPoint(worldCenter);
    }

    private void TuckBehindDiscardPile(ExitingCard ex)
    {
        if (ex == null || ex.go == null || _discardPileOrigin == null) return;
        var pileParent = _discardPileOrigin.parent;
        if (pileParent == null) return;
        ex.tuckedBehindPile = true;
        ex.go.transform.SetParent(pileParent, true);
        PlaceBehindDiscardPile(ex.go.transform);
    }

    private void PlaceBehindDiscardPile(Transform card)
    {
        if (card == null || _discardPileOrigin == null) return;
        if (card.parent != _discardPileOrigin.parent) return;
        int pileIdx = _discardPileOrigin.GetSiblingIndex();
        card.SetSiblingIndex(pileIdx);
    }

    private void BeginExit(GameObject cardObj, CanvasGroup cg, bool exhaust, int seq)
    {
        if (cardObj == null) return;
        if (cg == null) cg = cardObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = cardObj.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        cg.alpha = 1f;

        var hover = cardObj.GetComponent<CardHoverEffect>();
        if (hover != null) hover.enabled = false;
        var drag = cardObj.GetComponent<CardDragHandler>();
        if (drag != null) drag.enabled = false;

        cardObj.transform.SetAsLastSibling();
        _exiting.Add(new ExitingCard
        {
            go = cardObj,
            cg = cg,
            exhaust = exhaust,
            holdUntil = Time.time + seq * Mathf.Max(0f, discardStagger),
            t = -1f,
            startPos = cardObj.transform.localPosition,
            startScale = cardObj.transform.localScale,
            startRot = cardObj.transform.localRotation
        });
    }

    private void UpdateExits()
    {
        if (_exiting.Count == 0) return;
        Vector3 discardEnd = GetDiscardEndLocal();
        Quaternion discardRot = Quaternion.Euler(0f, 0f, -10f);
        Vector3 discardScale = Vector3.one * Mathf.Max(0.12f, discardEndScale);

        for (int i = _exiting.Count - 1; i >= 0; i--)
        {
            var ex = _exiting[i];
            if (ex.go == null)
            {
                _exiting.RemoveAt(i);
                continue;
            }

            if (Time.time < ex.holdUntil)
                continue;

            if (ex.t < 0f)
            {
                ex.startPos = ex.go.transform.localPosition;
                ex.startScale = ex.go.transform.localScale;
                ex.startRot = ex.go.transform.localRotation;
                ex.t = 0f;
            }

            float dur = ex.exhaust
                ? Mathf.Max(0.08f, exhaustFadeDuration)
                : Mathf.Max(0.08f, discardFlightDuration);
            ex.t = Mathf.Min(1f, ex.t + Time.deltaTime / dur);
            float s = ex.t * ex.t * (3f - 2f * ex.t);
            var trans = ex.go.transform;

            if (ex.exhaust)
            {
                trans.SetAsLastSibling();
                trans.localPosition = ex.startPos + new Vector3(0f, 28f * s, 0f);
                trans.localScale = Vector3.Lerp(ex.startScale, ex.startScale * 0.82f, s);
                trans.localRotation = ex.startRot;
                if (ex.cg != null) ex.cg.alpha = 1f - s;
            }
            else
            {
                Vector3 p = Vector3.LerpUnclamped(ex.startPos, discardEnd, s);
                p.y += Mathf.Sin(s * Mathf.PI) * discardArcHeight;
                if (!ex.tuckedBehindPile && s >= tuckBehindDiscardAt)
                    TuckBehindDiscardPile(ex);
                if (ex.tuckedBehindPile)
                    PlaceBehindDiscardPile(trans);
                else
                    trans.SetAsLastSibling();

                trans.position = transform.TransformPoint(p);
                trans.localRotation = Quaternion.Slerp(ex.startRot, discardRot, s);
                trans.localScale = Vector3.Lerp(ex.startScale, discardScale, s);
                if (ex.cg != null) ex.cg.alpha = 1f - 0.35f * s;
            }

            if (ex.t >= 1f)
            {
                Destroy(ex.go);
                _exiting.RemoveAt(i);
            }
        }

        if (_exiting.Count == 0)
            OnCardExitsFinished?.Invoke();
    }

    private GameObject CreateHandCard(CardData data, int index, System.Func<CardData, bool> isPlayable)
    {
        var prefab = GetPrefabForType(data.cardType);
        if (prefab == null)
        {
            Debug.LogError($"[HandCardLayout] 未找到卡牌类型 {data.cardType} 对应的 Prefab");
            return null;
        }

        var cardObj = Instantiate(prefab, transform);
        var display = cardObj.GetComponent<CardDisplay>();
        if (display == null)
        {
            Debug.LogError($"[HandCardLayout] 卡牌Prefab缺少CardDisplay组件: {prefab.name}");
            Destroy(cardObj);
            return null;
        }

        BindHandCard(cardObj, display, data, index, isPlayable);
        return cardObj;
    }

    private void BindHandCard(GameObject cardObj, CardDisplay display, CardData data, int index, System.Func<CardData, bool> isPlayable)
    {
        if (display != null)
        {
            display.ApplyCardData(data);
            if (isPlayable != null)
                display.SetPlayable(isPlayable(data));
        }

        var hover = cardObj.GetComponent<CardHoverEffect>();
        if (hover != null)
            hover.Setup(index, this, _onCardClicked);

        var drag = cardObj.GetComponent<CardDragHandler>();
        if (drag != null)
            drag.Setup(index, this, _onCardDrop, _onCardDragOver);
    }

    private void RecountDrawFlights(bool fireEvents)
    {
        int prev = _activeDrawFlights;
        int n = 0;
        for (int i = 0; i < _drawFlightT.Count; i++)
        {
            if (i < _drawHoldUntil.Count && Time.time < _drawHoldUntil[i]) n++;
            else if (_drawFlightT[i] >= 0f && _drawFlightT[i] < 1f) n++;
        }
        _activeDrawFlights = n;
        if (!fireEvents) return;
        if (prev == 0 && n > 0) OnDrawFlightsStarted?.Invoke();
        if (prev > 0 && n == 0) OnDrawFlightsFinished?.Invoke();
    }

    /// <summary>
    /// 更新手牌显示。已存在的 CardData 实例会复用卡面，避免整手重建。
    /// flyFromDraw 中的新牌从抽牌堆飞入。
    /// </summary>
    public void UpdateHand(List<CardData> hand, System.Func<CardData, bool> isPlayable = null)
    {
        UpdateHand(hand, isPlayable, null, null, null);
    }

    public void UpdateHand(List<CardData> hand, System.Func<CardData, bool> isPlayable, HashSet<CardData> flyFromDraw)
    {
        UpdateHand(hand, isPlayable, flyFromDraw, null, null);
    }

    public void UpdateHand(
        List<CardData> hand,
        System.Func<CardData, bool> isPlayable,
        HashSet<CardData> flyFromDraw,
        HashSet<CardData> flyToDiscard,
        HashSet<CardData> fadeOut)
    {
        if (hand == null) hand = new List<CardData>();

        CardData hoveredData = IndexToData(_hoveredIndex);
        CardData draggedData = IndexToData(_draggedIndex);

        var oldObjects = new List<GameObject>(_cardObjects);
        var oldDisplays = new List<CardDisplay>(_cardDisplays);
        var oldData = new List<CardData>(_cardDataRefs);
        var oldGroups = new List<CanvasGroup>(_cardCanvasGroups);
        var oldHold = new List<float>(_drawHoldUntil);
        var oldFlightT = new List<float>(_drawFlightT);
        var oldStartPos = new List<Vector3>(_drawStartPos);
        var oldStartScale = new List<Vector3>(_drawStartScale);
        var oldStartRot = new List<Quaternion>(_drawStartRot);
        var usedOld = new bool[oldObjects.Count];

        _cardObjects.Clear();
        _cardDisplays.Clear();
        _cardDataRefs.Clear();
        _cardCanvasGroups.Clear();
        _drawHoldUntil.Clear();
        _drawFlightT.Clear();
        _drawStartPos.Clear();
        _drawStartScale.Clear();
        _drawStartRot.Clear();

        int flySeq = 0;
        int exitSeq = 0;
        Vector3 drawLocal = GetDrawStartLocal();
        Quaternion pileRot = Quaternion.Euler(0f, 0f, 8f);

        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] == null)
            {
                Debug.LogError($"[HandCardLayout] hand[{i}] 为 null，跳过实例化");
                continue;
            }

            int oldIndex = -1;
            for (int o = 0; o < oldData.Count; o++)
            {
                if (usedOld[o]) continue;
                if (oldData[o] == hand[i])
                {
                    oldIndex = o;
                    usedOld[o] = true;
                    break;
                }
            }

            GameObject cardObj;
            CardDisplay display;
            CanvasGroup cg;
            if (oldIndex >= 0 && oldObjects[oldIndex] != null)
            {
                cardObj = oldObjects[oldIndex];
                display = oldDisplays[oldIndex];
                cg = oldGroups[oldIndex];
                BindHandCard(cardObj, display, hand[i], i, isPlayable);
                _cardObjects.Add(cardObj);
                _cardDisplays.Add(display);
                _cardDataRefs.Add(hand[i]);
                _cardCanvasGroups.Add(cg);
                _drawHoldUntil.Add(oldHold[oldIndex]);
                _drawFlightT.Add(oldFlightT[oldIndex]);
                _drawStartPos.Add(oldStartPos[oldIndex]);
                _drawStartScale.Add(oldStartScale[oldIndex]);
                _drawStartRot.Add(oldStartRot[oldIndex]);
            }
            else
            {
                cardObj = CreateHandCard(hand[i], i, isPlayable);
                if (cardObj == null) continue;
                display = cardObj.GetComponent<CardDisplay>();
                cg = cardObj.GetComponent<CanvasGroup>();
                if (cg == null) cg = cardObj.AddComponent<CanvasGroup>();

                bool fly = flyFromDraw != null && flyFromDraw.Contains(hand[i]);
                if (fly)
                {
                    float hold = Time.time + flySeq * Mathf.Max(0f, drawStagger);
                    flySeq++;
                    cardObj.transform.localPosition = drawLocal;
                    cardObj.transform.localRotation = pileRot;
                    cardObj.transform.localScale = Vector3.one * drawStartScale;
                    cg.blocksRaycasts = false;
                    cg.interactable = false;
                    _drawHoldUntil.Add(hold);
                    _drawFlightT.Add(-1f);
                    _drawStartPos.Add(drawLocal);
                    _drawStartScale.Add(Vector3.one * drawStartScale);
                    _drawStartRot.Add(pileRot);
                }
                else
                {
                    cardObj.transform.localPosition = new Vector3(0, -200f, 0);
                    _drawHoldUntil.Add(0f);
                    _drawFlightT.Add(-1f);
                    _drawStartPos.Add(Vector3.zero);
                    _drawStartScale.Add(Vector3.one);
                    _drawStartRot.Add(Quaternion.identity);
                }

                _cardObjects.Add(cardObj);
                _cardDisplays.Add(display);
                _cardDataRefs.Add(hand[i]);
                _cardCanvasGroups.Add(cg);
            }
        }

        for (int o = 0; o < oldObjects.Count; o++)
        {
            if (usedOld[o] || oldObjects[o] == null) continue;
            var data = oldData[o];
            bool exhaust = fadeOut != null && fadeOut.Contains(data);
            bool discard = flyToDiscard != null && flyToDiscard.Contains(data);
            if (exhaust || discard)
                BeginExit(oldObjects[o], oldGroups[o], exhaust, exitSeq++);
            else
                Destroy(oldObjects[o]);
        }

        _hoveredIndex = DataToIndex(hoveredData);
        _draggedIndex = DataToIndex(draggedData);
        _siblingStamp = int.MinValue;
        ClampScroll();
        if (flySeq > 0 && MaxScroll > 0f)
            _scrollTarget = MaxScroll;
        CalculateLayout();
        RecountDrawFlights(true);
    }

    /// <summary>
    /// 刷新已存在卡牌的可打出状态
    /// </summary>
    public void RefreshPlayable(System.Func<CardData, bool> isPlayable)
    {
        for (int i = 0; i < _cardDisplays.Count; i++)
        {
            var display = _cardDisplays[i];
            if (display == null || isPlayable == null || i >= _cardDataRefs.Count) continue;
            display.SetPlayable(isPlayable(_cardDataRefs[i]));
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
            var display = _cardDisplays[i];
            if (display == null) continue;
            if (i < _cardDataRefs.Count && _cardDataRefs[i] != null)
                display.ApplyCardData(_cardDataRefs[i]);
            else
                display.UpdateDisplay();
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
        if (IsDrawFlightActive(index)) on = false;
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
            if (IsDrawFlightActive(i)) continue;
            var trans = _cardObjects[i].transform;
            trans.localPosition = _targetPositions[i];
            if (i < _targetRotations.Count) trans.localRotation = _targetRotations[i];
            if (i < _targetScales.Count) trans.localScale = _targetScales[i];
        }
    }

    private bool IsDrawFlightActive(int i)
    {
        if (i < 0 || i >= _drawFlightT.Count) return false;
        if (i < _drawHoldUntil.Count && Time.time < _drawHoldUntil[i]) return true;
        return _drawFlightT[i] >= 0f && _drawFlightT[i] < 1f;
    }

    private void UpdateDrawFlights()
    {
        int prev = _activeDrawFlights;
        _activeDrawFlights = 0;
        float dur = Mathf.Max(0.08f, drawFlightDuration);
        Vector3 livePile = GetDrawStartLocal();

        for (int i = 0; i < _cardObjects.Count; i++)
        {
            if (_cardObjects[i] == null) continue;
            if (i >= _drawFlightT.Count) continue;

            if (Time.time < _drawHoldUntil[i])
            {
                var trans = _cardObjects[i].transform;
                trans.localPosition = livePile;
                trans.localRotation = _drawStartRot[i];
                trans.localScale = _drawStartScale[i];
                trans.SetAsLastSibling();
                _activeDrawFlights++;
                continue;
            }

            if (_drawFlightT[i] < 0f && _drawHoldUntil[i] > 0f)
            {
                _drawStartPos[i] = _cardObjects[i].transform.localPosition;
                _drawStartScale[i] = _cardObjects[i].transform.localScale;
                _drawStartRot[i] = _cardObjects[i].transform.localRotation;
                _drawFlightT[i] = 0f;
                _drawHoldUntil[i] = 0f;
            }

            if (_drawFlightT[i] >= 0f && _drawFlightT[i] < 1f)
            {
                _drawFlightT[i] = Mathf.Min(1f, _drawFlightT[i] + Time.deltaTime / dur);
                float u = _drawFlightT[i];
                float s = u * u * (3f - 2f * u);
                if (i < _targetPositions.Count)
                {
                    Vector3 a = _drawStartPos[i];
                    Vector3 b = _targetPositions[i];
                    Vector3 p = Vector3.LerpUnclamped(a, b, s);
                    p.y += Mathf.Sin(s * Mathf.PI) * drawArcHeight;
                    var trans = _cardObjects[i].transform;
                    trans.localPosition = p;
                    if (i < _targetRotations.Count)
                        trans.localRotation = Quaternion.Slerp(_drawStartRot[i], _targetRotations[i], s);
                    if (i < _targetScales.Count)
                        trans.localScale = Vector3.Lerp(_drawStartScale[i], _targetScales[i], s);
                    trans.SetAsLastSibling();
                }

                if (_drawFlightT[i] >= 1f)
                {
                    _drawFlightT[i] = -1f;
                    SetCardRaycast(i, true);
                    if (i < _targetPositions.Count)
                    {
                        var trans = _cardObjects[i].transform;
                        trans.localPosition = _targetPositions[i];
                        if (i < _targetRotations.Count) trans.localRotation = _targetRotations[i];
                        if (i < _targetScales.Count) trans.localScale = _targetScales[i];
                    }
                }
                else
                {
                    _activeDrawFlights++;
                }
            }
        }

        if (prev == 0 && _activeDrawFlights > 0) OnDrawFlightsStarted?.Invoke();
        if (prev > 0 && _activeDrawFlights == 0) OnDrawFlightsFinished?.Invoke();
    }

    private void Update()
    {
        if (_cardObjects.Count == 0)
        {
            if (_activeDrawFlights > 0)
            {
                _activeDrawFlights = 0;
                OnDrawFlightsFinished?.Invoke();
            }
            UpdateExits();
            return;
        }

        UpdateOverflowScroll();
        CalculateLayout();
        UpdateExits();
        UpdateDrawFlights();

        float follow = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        for (int i = 0; i < _cardObjects.Count; i++)
        {
            if (_cardObjects[i] == null) continue;
            if (i >= _targetPositions.Count) continue;
            if (i == _draggedIndex)
            {
                _cardObjects[i].transform.localRotation = Quaternion.identity;
                continue;
            }
            if (IsDrawFlightActive(i)) continue;
            var trans = _cardObjects[i].transform;
            trans.localPosition = Vector3.Lerp(trans.localPosition, _targetPositions[i], follow);
            trans.localRotation = Quaternion.Slerp(trans.localRotation, _targetRotations[i], follow);
            trans.localScale = Vector3.Lerp(trans.localScale, _targetScales[i], follow);
        }
    }
}