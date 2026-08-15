using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 手牌扇形布局管理 —— 负责卡牌的弧形排列、悬浮放大、丝滑过渡动画。
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
    private System.Action<int> _onCardClicked;
    private System.Action<int, UnityEngine.Vector2> _onCardDrop;
    private System.Action<int, UnityEngine.Vector2> _onCardDragOver;
    private bool _isDarkMode = false;  // 黑暗卡面模式（理智转阶段时开启）

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

    /// <summary>当前是否处于黑暗卡面模式</summary>
    public bool IsDarkMode => _isDarkMode;

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
    /// 开启/关闭黑暗卡面模式。对所有当前手牌及后续新手牌生效。
    /// </summary>
    public void SetDarkMode(bool enabled)
    {
        _isDarkMode = enabled;
        foreach (var display in _cardDisplays)
        {
            if (display != null)
                display.SetDarkMode(enabled);
        }
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
            if (_isDarkMode)
                display.SetDarkMode(true);
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
        }

        _hoveredIndex = -1;
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

    public void SetHoveredIndex(int index)
    {
        if (_hoveredIndex == index) return;

        if (_hoveredIndex >= 0 && _hoveredIndex < _cardObjects.Count && _cardObjects[_hoveredIndex] != null)
            _cardObjects[_hoveredIndex].transform.SetSiblingIndex(_hoveredIndex);

        _hoveredIndex = index;

        if (_hoveredIndex >= 0 && _hoveredIndex < _cardObjects.Count && _cardObjects[_hoveredIndex] != null)
            _cardObjects[_hoveredIndex].transform.SetAsLastSibling();

        CalculateLayout();
    }

    private void CalculateLayout()
    {
        _targetPositions.Clear();
        _targetRotations.Clear();
        _targetScales.Clear();

        int count = _cardObjects.Count;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : (float)i / (count - 1);
            float angle = Mathf.Lerp(-maxFanAngle / 2f, maxFanAngle / 2f, t);
            float rad = angle * Mathf.Deg2Rad;

            float totalWidth = (count - 1) * cardSpacing;
            float x = -totalWidth / 2f + i * cardSpacing;
            float y = -fanRadius + Mathf.Cos(rad) * fanRadius;

            float scale = 1f;
            if (i == _hoveredIndex)
            {
                y += hoverYOffset;
                scale = hoverScale;
                angle = 0f;
            }

            _targetPositions.Add(new Vector3(x, y, 0));
            _targetRotations.Add(Quaternion.Euler(0, 0, -angle));
            _targetScales.Add(Vector3.one * scale);
        }
    }

    private void Update()
    {
        if (_cardObjects.Count == 0) return;
        float dt = Time.deltaTime * lerpSpeed;

        for (int i = 0; i < _cardObjects.Count; i++)
        {
            if (_cardObjects[i] == null) continue;
            // 拖拽中的卡牌位置由拖拽处理器控制，布局不与其抢位
            if (i == _draggedIndex)
            {
                _cardObjects[i].transform.localRotation = Quaternion.identity;
                continue;
            }
            var trans = _cardObjects[i].transform;
            trans.localPosition = Vector3.Lerp(trans.localPosition, _targetPositions[i], dt);
            trans.localRotation = Quaternion.Slerp(trans.localRotation, _targetRotations[i], dt);
            trans.localScale = Vector3.Lerp(trans.localScale, _targetScales[i], dt);
        }
    }
}
