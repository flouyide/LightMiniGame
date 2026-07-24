using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] private GameObject skillCardPrefab;
    [SerializeField] private GameObject abilityCardPrefab;

    private readonly List<GameObject> _cardObjects = new List<GameObject>();
    private readonly List<CardDisplay> _cardDisplays = new List<CardDisplay>();
    private readonly List<CardData> _cardDataRefs = new List<CardData>();
    private readonly List<Vector3> _targetPositions = new List<Vector3>();
    private readonly List<Quaternion> _targetRotations = new List<Quaternion>();
    private readonly List<Vector3> _targetScales = new List<Vector3>();
    private int _hoveredIndex = -1;
    private System.Action<int> _onCardClicked;
    private bool _isDarkMode = false;  // 黑暗卡面模式（理智转阶段时开启）

    public int CardCount => _cardObjects.Count;

    /// <summary>当前是否处于黑暗卡面模式</summary>
    public bool IsDarkMode => _isDarkMode;

    public void SetCardClickCallback(System.Action<int> callback)
    {
        _onCardClicked = callback;
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
            var trans = _cardObjects[i].transform;
            trans.localPosition = Vector3.Lerp(trans.localPosition, _targetPositions[i], dt);
            trans.localRotation = Quaternion.Slerp(trans.localRotation, _targetRotations[i], dt);
            trans.localScale = Vector3.Lerp(trans.localScale, _targetScales[i], dt);
        }
    }
}
