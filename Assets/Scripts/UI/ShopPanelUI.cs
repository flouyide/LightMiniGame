using System;
using System.Collections;
using System.Collections.Generic;
using LightMiniGame.Card;
using LightMiniGame.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 商店面板 UI（仿《杀戮尖塔2》布局）：
///  - GoodsLayer 单排显示所有货物，每页最多5个，货物包含卡牌、遗物和删牌服务；
///  - ArrowLeft / ArrowRight 控制 GoodsLayer 左右翻页；
///  - 每次进店刷新一次卡牌 / 遗物（由 ShopManager.OpenShop 负责抽取）；
///  - 每张卡牌 / 遗物下方显示价格，点击即购买：卡牌进对应角色牌库（CharacterCardLibrary），
///    遗物进对应角色的遗物库（GlobalRelicInventory），并扣除对应货币。
/// 所有货物均直接实例化现有 UI 预制体：CardItem.prefab、RelicItem.prefab、DeleteCard.prefab。
/// ShopPanelUI 只负责向预制体中的既有节点写入数据并绑定交互，不在运行时创建货物层级。
/// 卡牌、遗物和删牌服务统一挂到 GoodsLayer，由 GoodsLayer 的 HorizontalLayoutGroup 负责单排排列。
/// </summary>
public class ShopPanelUI : MonoBehaviour
{
    [Header("UI引用（未绑定时按路径自动查找）")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private Button arrowLeft;               // GoodsLayer 左翻页
    [SerializeField] private Button arrowRight;              // GoodsLayer 右翻页
    [SerializeField] private Transform goodsLayer;            // 一排货物容器，每页最多显示5个

    [Header("GoodsLayer 位置与尺寸（运行时可调）")]
    [Tooltip("GoodsLayer 相对锚点的位置")]
    [SerializeField] private Vector2 goodsLayerPosition = new Vector2(4.6325f, -24.7071f);
    [Tooltip("GoodsLayer 尺寸；HorizontalLayoutGroup 会在此区域内排列货物")]
    [SerializeField] private Vector2 goodsLayerSize = new Vector2(1631.685f, 483.6244f);
    [Tooltip("GoodsLayer 缩放")]
    [SerializeField] private Vector3 goodsLayerScale = Vector3.one;

    [Header("货物预制体")]
    [Tooltip("卡牌货物预制体：Assets/Prefabs/UI/局外/CardItem.prefab")]
    [SerializeField] private GameObject cardItemPrefab;
    [Tooltip("遗物货物预制体：Assets/Prefabs/UI/局外/RelicItem.prefab")]
    [SerializeField] private GameObject relicItemPrefab;
    [Tooltip("删牌货物预制体：Assets/Prefabs/UI/局外/DeleteCard.prefab")]
    [SerializeField] private GameObject deleteCardPrefab;

    [Header("货物布局（相对 HorizontalLayoutGroup 的排列结果）")]
    [Tooltip("卡牌货物在 HorizontalLayoutGroup 自动排列位置基础上的偏移")]
    [SerializeField] private Vector2 cardItemPositionOffset;
    [Tooltip("卡牌货物尺寸。X/Y 必须大于 0；默认使用 CardItem.prefab 的 100 × 100")]
    [SerializeField] private Vector2 cardItemSize = new Vector2(100f, 100f);
    [Tooltip("遗物货物在 HorizontalLayoutGroup 自动排列位置基础上的偏移")]
    [SerializeField] private Vector2 relicItemPositionOffset;
    [Tooltip("遗物货物尺寸。X/Y 必须大于 0；默认使用 RelicItem.prefab 的 100 × 132.998")]
    [SerializeField] private Vector2 relicItemSize = new Vector2(100f, 132.998f);
    [Tooltip("删牌货物在 HorizontalLayoutGroup 自动排列位置基础上的偏移")]
    [SerializeField] private Vector2 deleteCardPositionOffset;
    [Tooltip("删牌货物尺寸。X/Y 必须大于 0；默认使用 DeleteCard.prefab 的 420 × 520")]
    [SerializeField] private Vector2 deleteCardSize = new Vector2(420f, 520f);

    [Header("货物槽位高亮（当前页从左到右 1-5）")]
    [Tooltip("第1个货物下方的红色椭圆高亮；该货物购买成功后隐藏")]
    [SerializeField] private GameObject highlight1;
    [Tooltip("第2个货物下方的红色椭圆高亮；该货物购买成功后隐藏")]
    [SerializeField] private GameObject highlight2;
    [Tooltip("第3个货物下方的红色椭圆高亮；该货物购买成功后隐藏")]
    [SerializeField] private GameObject highlight3;
    [Tooltip("第4个货物下方的红色椭圆高亮；该货物购买成功后隐藏")]
    [SerializeField] private GameObject highlight4;
    [Tooltip("第5个货物下方的红色椭圆高亮；该货物购买成功后隐藏")]
    [SerializeField] private GameObject highlight5;

    private Action _onClose;
    private bool _closeHooked;
    private bool _pageButtonsHooked;

    private ShopManager _shop;
    private List<CharacterData> _characters;

    private Transform _shopBoard;
    private Transform _goodsLayer;
    private Text _goldLabel;

    private const int GoodsPerPage = 5;
    private int _goodsPage;
    private int _goodsOnCurrentPage;

    private enum GoodsKind { Card, Relic, Service }

    private sealed class GoodsEntry
    {
        public GoodsKind kind;
        public ShopCardEntry card;
        public ShopRelicEntry relic;
    }

    private readonly List<GoodsEntry> _goods = new List<GoodsEntry>();
    private Coroutine _goodsItemLayoutOverrideRoutine;

#if UNITY_EDITOR
    private static bool _savePrefabBindingsQueued;
#endif

    // ===== 特价商店角标（运行时动态创建，避免改动 prefab）=====
    private float _discountRatio = 1f;   // 本店折扣比例：1=不打折；<1=特价商店
    private Text _discountBadge;            // 「特价商店（X折）」角标（动态创建，复用即显隐）

    // ===== 删牌货物（DeleteCard.prefab 的 Frame/Button 进入删牌流程）=====
    private GameObject _deleteCardInstance;
    private Button _deleteCardButton;

    [Header("牌库界面（用于删牌选择；优先 Inspector 配置，未配时按场景查找）")]
    [Tooltip("CardLibraryPanel.prefab 的 UI 控件（场景实例，通常位于 BookCanvas 下）。点击删牌按钮后以此面板作为选择界面。")]
    public CardLibraryPanelUI cardLibraryPanel;

    // ===== 生命周期 =====
#if UNITY_EDITOR
    [ContextMenu("自动绑定商店货物预制体")]
    private void AssignGoodsPrefabsInEditor()
    {
        cardItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/局外/CardItem.prefab");
        relicItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/局外/RelicItem.prefab");
        deleteCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/局外/DeleteCard.prefab");
        EditorUtility.SetDirty(this);

        if (_savePrefabBindingsQueued) return;
        _savePrefabBindingsQueued = true;
        EditorApplication.delayCall += () =>
        {
            _savePrefabBindingsQueued = false;
            AssetDatabase.SaveAssets();
        };
    }
#endif

    private void Start()
    {
        if (panel == null) panel = transform.Find("Panel")?.gameObject;
        if (closeButton == null) closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
        if (hintText == null) hintText = transform.Find("HintText")?.GetComponent<TextMeshProUGUI>();

        _shopBoard = transform.Find("Panel/ShopBoard");
        EnsureLayers();
        HookPageButtons();

        if (panel != null) panel.SetActive(false);
        HookCloseButton();
    }

    private void Awake()
    {
        // ShopPanel 作为嵌套 prefab 默认可能处于未激活状态；提前解析当前运行时实例并绑定按钮。
        ResolveRuntimeReferences();
        HookCloseButton();
        HookPageButtons();
        Debug.Log($"[ShopPanelUI] Awake：实例={name}，activeInHierarchy={gameObject.activeInHierarchy}，enabled={enabled}");
    }

    private void OnEnable()
    {
        ResolveRuntimeReferences();
        HookCloseButton();
        HookPageButtons();
        Debug.Log($"[ShopPanelUI] OnEnable：实例={name}，activeInHierarchy={gameObject.activeInHierarchy}");
    }

    private void ResolveRuntimeReferences()
    {
        if (panel == null)
            panel = transform.Find("Panel")?.gameObject;
        if (_shopBoard == null)
            _shopBoard = transform.Find("Panel/ShopBoard");
        if (closeButton == null && panel != null)
            closeButton = panel.transform.Find("CloseButton")?.GetComponent<Button>();
        if (hintText == null && panel != null)
            hintText = panel.transform.Find("HintText")?.GetComponent<TextMeshProUGUI>();
        if (arrowLeft == null && panel != null)
            arrowLeft = panel.transform.Find("ArrowLeft")?.GetComponent<Button>();
        if (arrowRight == null && panel != null)
            arrowRight = panel.transform.Find("ArrowRight")?.GetComponent<Button>();
        if (_goodsLayer == null)
            _goodsLayer = goodsLayer != null ? goodsLayer : _shopBoard?.Find("GoodsLayer");

        // Inspector 未接线时按 prefab 节点名兜底：Highlight / Highlight (1) ... Highlight (4)。
        if (highlight1 == null && panel != null) highlight1 = panel.transform.Find("Highlight")?.gameObject;
        if (highlight2 == null && panel != null) highlight2 = panel.transform.Find("Highlight (1)")?.gameObject;
        if (highlight3 == null && panel != null) highlight3 = panel.transform.Find("Highlight (2)")?.gameObject;
        if (highlight4 == null && panel != null) highlight4 = panel.transform.Find("Highlight (3)")?.gameObject;
        if (highlight5 == null && panel != null) highlight5 = panel.transform.Find("Highlight (4)")?.gameObject;
    }

    /// <summary>
    /// Inspector 修改 GoodsLayer 位置或尺寸后立即应用。
    /// OnValidate 在编辑器和运行时 Inspector 调整时都会触发。
    /// </summary>
    private void OnValidate()
    {
#if UNITY_EDITOR
        if (cardItemPrefab == null || relicItemPrefab == null || deleteCardPrefab == null)
            AssignGoodsPrefabsInEditor();
#endif
        if (goodsLayer == null)
            goodsLayer = transform.Find("Panel/ShopBoard/GoodsLayer");
        ApplyGoodsLayerLayout();

        // Play Mode 中 Inspector 改偏移值时，不重新打开商店也能在当前帧末生效。
        if (Application.isPlaying)
            QueueGoodsItemLayoutOverrides();
    }

    private void OnDisable()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
        _closeHooked = false;
        if (_shop != null)
            _shop.OnStockChanged -= Render;
        if (_goodsItemLayoutOverrideRoutine != null)
        {
            StopCoroutine(_goodsItemLayoutOverrideRoutine);
            _goodsItemLayoutOverrideRoutine = null;
        }
        UnhookPageButtons();
    }

    private void HookCloseButton()
    {
        if (closeButton != null && !_closeHooked)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
            _closeHooked = true;
        }
    }

    private void HookPageButtons()
    {
        if (_pageButtonsHooked) return;

        ResolveRuntimeReferences();

        if (arrowLeft != null)
            arrowLeft.onClick.AddListener(ShowPreviousGoodsPage);
        if (arrowRight != null)
            arrowRight.onClick.AddListener(ShowNextGoodsPage);

        _pageButtonsHooked = arrowLeft != null || arrowRight != null;
        Debug.Log($"[ShopPanelUI] 翻页按钮绑定：Left={(arrowLeft != null ? arrowLeft.name : "未找到")}，Right={(arrowRight != null ? arrowRight.name : "未找到")}");
    }

    private void UnhookPageButtons()
    {
        if (arrowLeft != null)
            arrowLeft.onClick.RemoveListener(ShowPreviousGoodsPage);
        if (arrowRight != null)
            arrowRight.onClick.RemoveListener(ShowNextGoodsPage);
        _pageButtonsHooked = false;
    }

    private void ShowPreviousGoodsPage()
    {
        int oldPage = _goodsPage;
        if (_goodsPage > 0)
            _goodsPage--;

        Debug.Log($"[ShopPanelUI] 点击 ArrowLeft：{oldPage + 1} → {_goodsPage + 1} 页，货物总数={_goods.Count}");
        RenderGoodsPage();
    }

    private void ShowNextGoodsPage()
    {
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(_goods.Count / (float)GoodsPerPage));
        int oldPage = _goodsPage;
        if (_goodsPage < pageCount - 1)
            _goodsPage++;

        Debug.Log($"[ShopPanelUI] 点击 ArrowRight：{oldPage + 1} → {_goodsPage + 1}/{pageCount} 页，货物总数={_goods.Count}");
        RenderGoodsPage();
    }

    // ===== 在 ShopBoard 下创建一排 GoodsLayer（每页最多5个） =====
    private void EnsureLayers()
    {
        if (_shopBoard == null) return;

        _goodsLayer = goodsLayer != null ? goodsLayer : _shopBoard.Find("GoodsLayer");
        if (_goodsLayer == null)
            _goodsLayer = CreateLayer("GoodsLayer", new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.82f));
        else
            SetupHorizontal(_goodsLayer);

        // 兼容旧 prefab：如果残留 CardLayer / RelicLayer，隐藏它们，商品统一进入 GoodsLayer。
        var legacy = _shopBoard.Find("CardLayer");
        if (legacy != null) legacy.gameObject.SetActive(false);
        legacy = _shopBoard.Find("RelicLayer");
        if (legacy != null) legacy.gameObject.SetActive(false);
        legacy = _shopBoard.Find("Card");
        if (legacy != null) legacy.gameObject.SetActive(false);
        legacy = _shopBoard.Find("Relic");
        if (legacy != null) legacy.gameObject.SetActive(false);
    }

    private Transform CreateLayer(string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_shopBoard, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        SetupHorizontal(rt);
        return rt;
    }

    private void SetupHorizontal(Transform layer)
    {
        ApplyGoodsLayerLayout(layer);
    }

    /// <summary>
    /// 把 Inspector 暴露的 GoodsLayer 位置、尺寸和 HorizontalLayoutGroup 全部参数应用到运行时组件。
    /// </summary>
    private void ApplyGoodsLayerLayout(Transform layer = null)
    {
        layer ??= goodsLayer;
        if (layer == null) return;

        var rt = layer as RectTransform;
        if (rt != null)
        {
            rt.anchoredPosition = goodsLayerPosition;
            rt.sizeDelta = goodsLayerSize;
            rt.localScale = goodsLayerScale;
        }

        var hlg = layer.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
            hlg = layer.gameObject.AddComponent<HorizontalLayoutGroup>();

        // GoodsLayer 的 HorizontalLayoutGroup 使用固定布局参数；不再暴露为 ShopPanelUI 字段。
        //hlg.spacing = 300f;
        //hlg.padding = new RectOffset(16, 16, 16, 16);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childScaleWidth = false;
        hlg.childScaleHeight = false;
        hlg.reverseArrangement = false;

        if (rt != null)
            LayoutRebuilder.MarkLayoutForRebuild(rt);
    }

    // ===== 对外：显示商店 =====
    public void Show(ShopManager shop, List<CharacterData> characters, Action onClose, float discountRatio = 1f, bool isRegularShop = true)
    {
        _shop = shop;
        _characters = characters;
        _onClose = onClose;
        _discountRatio = discountRatio;   // 记录本次开店的基础折扣；内部福利的普通商店折扣由 ShopManager 叠加。
        _goodsPage = 0;                  // 每次重新进店从第一页开始
        HookPageButtons();                // 面板重开后确保箭头监听已恢复

        if (_shop != null)
        {
            _shop.OpenShop(_characters, discountRatio, isRegularShop); // 每次进店重抽一次卡牌与遗物；普通商店会消费内部福利折扣。
            _discountRatio = _shop.CurrentDiscountRatio;
            _shop.OnStockChanged -= Render;
            _shop.OnStockChanged += Render;
        }

        if (panel != null) panel.SetActive(true);
        Render();
    }

    private void OnCloseClicked()
    {
        if (_shop != null)
            _shop.OnStockChanged -= Render;
        CloseRemoval();   // 离开商店时确保关闭删牌选择界面
        _onClose?.Invoke();
        Hide();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        _onClose = null;
    }

    // ===== 渲染 =====
    private void Render()
    {
        if (_shop == null || _goodsLayer == null) return;

        UpdateGoldLabel();
        UpdateDiscountBadge();

        _goods.Clear();
        foreach (var card in _shop.CardStock)
            _goods.Add(new GoodsEntry { kind = GoodsKind.Card, card = card });
        foreach (var relic in _shop.RelicStock)
            _goods.Add(new GoodsEntry { kind = GoodsKind.Relic, relic = relic });

        // 每个商店固定提供一个删牌货物，与卡牌/遗物一起分页。
        if (deleteCardPrefab != null)
            _goods.Add(new GoodsEntry { kind = GoodsKind.Service });
        else
            Debug.LogWarning("[ShopPanelUI] 未配置 DeleteCard.prefab，当前商店不会显示删牌货物");

        _goodsPage = Mathf.Clamp(_goodsPage, 0, Mathf.Max(0, Mathf.CeilToInt(_goods.Count / (float)GoodsPerPage) - 1));
        Debug.Log($"[ShopPanelUI] 货物分页数据已生成：卡牌={_shop.CardStock.Count}，遗物={_shop.RelicStock.Count}，删牌货物={(deleteCardPrefab != null ? 1 : 0)}，总数={_goods.Count}，每页={GoodsPerPage}");
        RenderGoodsPage();
    }

    private void RenderGoodsPage()
    {
        if (_goodsLayer == null) return;

        ClearGoodsLayer();
        int start = _goodsPage * GoodsPerPage;
        int end = Mathf.Min(start + GoodsPerPage, _goods.Count);

        for (int i = start; i < end; i++)
        {
            var entry = _goods[i];
            switch (entry.kind)
            {
                case GoodsKind.Card:
                    BuildCardItem(_goodsLayer, entry.card);
                    break;
                case GoodsKind.Relic:
                    BuildRelicItem(_goodsLayer, entry.relic);
                    break;
                case GoodsKind.Service:
                    BuildDeleteCardItem();
                    break;
            }
        }

        // HorizontalLayoutGroup 在本帧后续的 Canvas 布局阶段会回写子节点位置，
        // 因此将偏移安排到布局完成后的帧末再叠加。
        QueueGoodsItemLayoutOverrides();

        _goodsOnCurrentPage = end - start;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(_goods.Count / (float)GoodsPerPage));
        RefreshCurrentPageHighlights(_goodsOnCurrentPage);
        if (arrowLeft != null)
            arrowLeft.interactable = _goodsPage > 0;
        if (arrowRight != null)
            arrowRight.interactable = _goodsPage < pageCount - 1;

        Debug.Log($"[ShopPanelUI] 渲染货物第 {_goodsPage + 1}/{pageCount} 页：索引 [{start}, {end})，本页 {end - start} 件，左箭头={(_goodsPage > 0)}，右箭头={(_goodsPage < pageCount - 1)}");
    }

    /// <summary>刷新当前页5个槽位高亮；每次翻页默认显示本页实际存在的货物槽位。</summary>
    private void RefreshCurrentPageHighlights(int goodsOnPage)
    {
        var highlights = new[] { highlight1, highlight2, highlight3, highlight4, highlight5 };
        int start = _goodsPage * GoodsPerPage;
        for (int slotIndex = 0; slotIndex < highlights.Length; slotIndex++)
        {
            if (highlights[slotIndex] == null) continue;

            int goodsIndex = start + slotIndex;
            bool hasGood = slotIndex < goodsOnPage && goodsIndex < _goods.Count;
            bool purchased = hasGood && IsPurchased(_goods[goodsIndex]);
            highlights[slotIndex].SetActive(hasGood && !purchased);
        }
    }

    private static bool IsPurchased(GoodsEntry entry)
        => entry != null && ((entry.kind == GoodsKind.Card && entry.card != null && entry.card.sold)
                          || (entry.kind == GoodsKind.Relic && entry.relic != null && entry.relic.sold));

    /// <summary>关闭当前页中指定已购买货物对应槽位的红色高亮。</summary>
    private void DisableHighlightForGoodsEntry(GoodsEntry entry)
    {
        int entryIndex = _goods.IndexOf(entry);
        if (entryIndex < 0 || entryIndex / GoodsPerPage != _goodsPage) return;

        int slotIndex = entryIndex % GoodsPerPage;
        GameObject highlight = slotIndex switch
        {
            0 => highlight1,
            1 => highlight2,
            2 => highlight3,
            3 => highlight4,
            4 => highlight5,
            _ => null
        };
        if (highlight != null)
        {
            highlight.SetActive(false);
            Debug.Log($"[ShopPanelUI] 货物购买成功，关闭第 {slotIndex + 1} 槽高亮：{highlight.name}");
        }
    }

    private void UpdateGoldLabel()
    {
        if (_shopBoard == null) return;
        if (_goldLabel == null)
        {
            var go = new GameObject("GoldLabel", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_shopBoard, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.985f);
            rt.anchorMax = new Vector2(0.6f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _goldLabel = go.GetComponent<Text>();
            _goldLabel.alignment = TextAnchor.MiddleLeft;
            _goldLabel.fontSize = 20;
            _goldLabel.fontStyle = FontStyle.Bold;
            _goldLabel.color = new Color(1f, 0.85f, 0.3f);
        }
        _goldLabel.text = $"金币：{_shop.PlayerGold}";
    }

    /// <summary>
    /// 特价商店角标：折扣比例 &lt; 1 时显示「特价商店（X折）」，正常商店（=1）则隐藏。
    /// 复用 _discountBadge，运行时动态创建为 ShopBoard 的子物体，不依赖 prefab 自带节点。
    /// </summary>
    private void UpdateDiscountBadge()
    {
        if (_shopBoard == null) return;

        // 正常商店：确保角标隐藏（已创建则只关不删，复用）
        if (_discountRatio >= 1f)
        {
            if (_discountBadge != null) _discountBadge.gameObject.SetActive(false);
            return;
        }

        if (_discountBadge == null)
        {
            var go = new GameObject("DiscountBadge", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_shopBoard, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.3f, 0.955f);
            rt.anchorMax = new Vector2(0.7f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _discountBadge = go.GetComponent<Text>();
            _discountBadge.alignment = TextAnchor.MiddleCenter;
            _discountBadge.fontSize = 22;
            _discountBadge.fontStyle = FontStyle.Bold;
            _discountBadge.color = new Color(1f, 0.45f, 0.3f);
        }

        int zhe = Mathf.RoundToInt(_discountRatio * 10f);   // 0.6 -> 6折
        _discountBadge.text = $"特价商店（{zhe}折）";
        _discountBadge.gameObject.SetActive(true);
    }

    private void ClearGoodsLayer()
    {
        if (_goodsLayer == null) return;
        var children = new List<Transform>();
        foreach (Transform child in _goodsLayer)
            children.Add(child);
        foreach (var child in children)
            Destroy(child.gameObject);

        _deleteCardInstance = null;
        _deleteCardButton = null;
    }

    /// <summary>
    /// 将 DeleteCard.prefab 实例化到 GoodsLayer。只使用它的 Frame 下 Button 进入原有删牌流程。
    /// 每次切页重建当前页货物，因此不存在旧 Service 节点复用或层级移动。
    /// </summary>
    private void BuildDeleteCardItem()
    {
        if (deleteCardPrefab == null || _goodsLayer == null) return;

        _deleteCardInstance = Instantiate(deleteCardPrefab, _goodsLayer, false);
        // 与 CardItem / RelicItem 一样固定实例名，供类型布局覆盖准确匹配。
        _deleteCardInstance.name = "DeleteCard";
        ApplyGoodsItemSize(_deleteCardInstance, deleteCardSize);
        SetTmpText(_deleteCardInstance.transform, "Price", _shop != null ? _shop.CurrentRemovalPrice.ToString() : "0");

        var frame = _deleteCardInstance.transform.Find("Frame");
        _deleteCardButton = frame != null ? frame.GetComponent<Button>() : null;
        if (_deleteCardButton == null)
        {
            Debug.LogError("[ShopPanelUI] DeleteCard.prefab 缺少 Frame/Button，无法进入删牌流程");
            return;
        }

        _deleteCardButton.onClick.RemoveAllListeners();
        _deleteCardButton.onClick.AddListener(OnServiceClicked);
        _deleteCardButton.interactable = _shop != null && _shop.RemovalsRemaining > 0;
        Debug.Log($"[ShopPanelUI] 已创建删牌货物：Frame/Button={_deleteCardButton.name}，可删次数={_shop?.RemovalsRemaining ?? 0}");
    }

    // ===== 商品条目 =====
    /// <summary>
    /// 实例化 CardItem.prefab，并只刷新其既有的角色名、卡牌、价格和货币图标节点。
    /// </summary>
    private void BuildCardItem(Transform parent, ShopCardEntry e)
    {
        if (e.card == null || cardItemPrefab == null)
        {
            Debug.LogWarning("[ShopPanelUI] CardItem.prefab 未配置或卡牌数据为空，无法创建卡牌货物");
            return;
        }

        bool sold = e.sold;
        bool affordable = !sold && _shop.CanAfford(e.price);
        var item = Instantiate(cardItemPrefab, parent, false);
        item.name = "CardItem";
        ApplyGoodsItemSize(item, cardItemSize);

        SetTmpText(item.transform, "CharacterName", e.ownerCharacter != null ? e.ownerCharacter.displayName : "通用");
        var display = item.GetComponentInChildren<CardDisplay>(true);
        if (display != null)
            display.ApplyCardData(e.card);

        var cardButton = display != null ? display.GetComponent<Button>() : null;
        if (cardButton != null)
        {
            cardButton.interactable = affordable;
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => OnBuyCard(e));
        }
        else
        {
            Debug.LogWarning("[ShopPanelUI] CardItem.prefab 内的卡牌缺少 Button 组件");
        }

        SetPrice(item.transform, sold ? "已售" : e.price.ToString(), !sold,
            sold ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.85f, 0.3f));
    }

    /// <summary>
    /// 实例化 RelicItem.prefab，并刷新其既有的角色名、图标、名称、价格和货币图标节点。
    /// </summary>
    private void BuildRelicItem(Transform parent, ShopRelicEntry e)
    {
        if (e.relic == null || relicItemPrefab == null)
        {
            Debug.LogWarning("[ShopPanelUI] RelicItem.prefab 未配置或遗物数据为空，无法创建遗物货物");
            return;
        }

        bool sold = e.sold;
        bool affordable = !sold && _shop.CanAfford(e.price);
        var item = Instantiate(relicItemPrefab, parent, false);
        item.name = "RelicItem";
        ApplyGoodsItemSize(item, relicItemSize);

        SetTmpText(item.transform, "CharacterName", e.ownerCharacter != null ? e.ownerCharacter.displayName : string.Empty);
        SetTmpText(item.transform, "Name", e.relic.relicName);

        var icon = item.transform.Find("Icon")?.GetComponent<Image>();
        if (icon != null)
        {
            icon.sprite = e.relic.icon;
            icon.gameObject.SetActive(e.relic.icon != null);
        }

        // RelicItem.prefab 的根 Image 颜色完全由预制体自身控制；商店不再根据售价/购买状态覆盖它。

        var button = item.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = affordable;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnBuyRelic(e));
        }

        SetPrice(item.transform, sold ? "已售" : e.price.ToString(), !sold,
            affordable ? new Color(1f, 0.85f, 0.3f) : new Color(0.6f, 0.6f, 0.6f));

        // 商店专属：鼠标移入 RelicItem 时启用 DescImage（含遗物描述），移出时禁用。
        // 遗物库内的悬停规则在 RelicInventoryPanelUI 中单独处理，互不影响。
        AttachRelicHoverTooltip(item, e);
    }

    /// <summary>
    /// 商店遗物悬停提示：鼠标移入 RelicItem 时启用 DescImage（含遗物描述），移出时禁用。
    /// 复用 RelicItem.prefab 内既有的 DescImage/DescText 节点，不改动预制体、不新增层级。
    /// 商店为单行 HorizontalLayoutGroup 布局，DescImage 向下展开，不会被相邻货物遮挡，
    /// 因此这里仅用 SetAsLastSibling 保证其在自身条目内绘制在最上层。
    /// </summary>
    private static void AttachRelicHoverTooltip(GameObject item, ShopRelicEntry e)
    {
        var descImage = item.transform.Find("DescImage")?.gameObject;
        if (descImage == null) return;

        var descText = descImage.transform.Find("DescText")?.GetComponent<TextMeshProUGUI>();
        if (descText != null && e.relic != null)
            descText.text = e.relic.description;

        descImage.SetActive(false);

        var trigger = item.GetComponent<EventTrigger>();
        if (trigger == null) trigger = item.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            descImage.SetActive(true);
            descImage.transform.SetAsLastSibling();
        });
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => descImage.SetActive(false));
        trigger.triggers.Add(exit);
    }

    /// <summary>
    /// 将尺寸写入货物根节点。GoodsLayer 禁用 childControlWidth/Height，因此不会覆盖这里的尺寸。
    /// </summary>
    private static void ApplyGoodsItemSize(GameObject item, Vector2 size)
    {
        if (item == null || size.x <= 0f || size.y <= 0f) return;

        var rect = item.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = size;
    }

    /// <summary>
    /// 将位置覆盖延后到当前帧的 Canvas 布局完成后执行。
    /// HorizontalLayoutGroup 会在 Canvas 更新阶段强制回写子节点位置，若提前赋值会被覆盖。
    /// </summary>
    private void QueueGoodsItemLayoutOverrides()
    {
        if (!Application.isPlaying || _goodsLayer == null) return;

        if (_goodsItemLayoutOverrideRoutine != null)
            StopCoroutine(_goodsItemLayoutOverrideRoutine);
        _goodsItemLayoutOverrideRoutine = StartCoroutine(ApplyGoodsItemLayoutOverridesAtEndOfFrame());
    }

    private IEnumerator ApplyGoodsItemLayoutOverridesAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        _goodsItemLayoutOverrideRoutine = null;
        ApplyGoodsItemLayoutOverrides();
        AlignCurrentPageGoodsToHighlights();
    }

    /// <summary>
    /// HorizontalLayoutGroup 负责货物的基础横向排列；布局完成后再叠加各类型的偏移，
    /// 从而在不新增包装节点、不脱离 HorizontalLayoutGroup 的前提下分别微调三类货物。
    /// </summary>
    private void ApplyGoodsItemLayoutOverrides()
    {
        var layerRect = _goodsLayer as RectTransform;
        if (layerRect == null) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(layerRect);
        foreach (Transform child in _goodsLayer)
        {
            var itemRect = child as RectTransform;
            if (itemRect == null) continue;

            Vector2 offset = child.name switch
            {
                "CardItem" => cardItemPositionOffset,
                "RelicItem" => relicItemPositionOffset,
                "DeleteCard" => deleteCardPositionOffset,
                _ => Vector2.zero
            };
            itemRect.anchoredPosition += offset;
        }
    }

    /// <summary>
    /// 货物不足一页时，HorizontalLayoutGroup 会将现有货物居中；这里保持高亮图片不动，
    /// 将第 N 个货物的中心沿 X 轴对齐到第 N 个高亮图片的中心（即落在该高亮的 Y 轴上）。
    /// </summary>
    private void AlignCurrentPageGoodsToHighlights()
    {
        if (_goodsLayer == null || _goodsOnCurrentPage >= GoodsPerPage) return;

        var highlights = new[] { highlight1, highlight2, highlight3, highlight4, highlight5 };
        int goodsIndex = 0;
        foreach (Transform child in _goodsLayer)
        {
            if (goodsIndex >= _goodsOnCurrentPage || goodsIndex >= highlights.Length) break;

            var itemRect = child as RectTransform;
            var highlightRect = highlights[goodsIndex] != null
                ? highlights[goodsIndex].transform as RectTransform
                : null;
            if (itemRect != null && highlightRect != null)
            {
                // 两者父级与锚点可能不同，以世界坐标计算目标中心，再只修正货物的 X 坐标。
                // Y 坐标（包括 Inspector 的各类货物 Position Offset.y）保持不变。
                float targetWorldX = highlightRect.TransformPoint(highlightRect.rect.center).x;
                float itemWorldX = itemRect.TransformPoint(itemRect.rect.center).x;
                itemRect.position += new Vector3(targetWorldX - itemWorldX, 0f, 0f);
            }
            goodsIndex++;
        }
    }

    /// <summary>按明确节点名写入其 TMP，避免依赖 prefab 子节点顺序。</summary>
    private static void SetTmpText(Transform parent, string nodeName, string value)
    {
        var text = parent.Find(nodeName)?.GetComponent<TextMeshProUGUI>();
        if (text == null)
        {
            Debug.LogWarning($"[ShopPanelUI] 未找到 {parent.name}/{nodeName} 的 TextMeshProUGUI 组件");
            return;
        }

        text.text = value ?? string.Empty;
    }

    /// <summary>复用货物 prefab 内既有 PriceRow/CurrencyIcon/Price 节点，不运行时创建节点。</summary>
    private static void SetPrice(Transform item, string priceText, bool showCurrencyIcon, Color color)
    {
        var priceRow = item.Find("PriceRow");
        if (priceRow == null) return;

        var currencyIcon = priceRow.Find("CurrencyIcon");
        if (currencyIcon != null)
            currencyIcon.gameObject.SetActive(showCurrencyIcon);

        var price = priceRow.Find("Price")?.GetComponent<TextMeshProUGUI>();
        if (price != null)
        {
            price.text = priceText;
            price.color = color;
        }
    }

    // ===== 购买回调 =====
    private void OnBuyCard(ShopCardEntry e)
    {
        var r = _shop.BuyCard(e);
        if (r == ShopResult.Success)
        {
            DisableHighlightForGoodsEntry(_goods.Find(entry => entry.kind == GoodsKind.Card && entry.card == e));
            Debug.Log($"[Shop] 购买卡牌：{e.card?.cardName} → {e.ownerCharacter?.displayName} 牌库");
        }
        else Debug.Log($"[Shop] 购买卡牌失败：{r}");
        // BuyCard 内部会广播 OnStockChanged → Render 自动刷新；RefreshCurrentPageHighlights 会保持已售槽位关闭。
    }

    private void OnBuyRelic(ShopRelicEntry e)
    {
        var r = _shop.BuyRelic(e);
        if (r == ShopResult.Success)
        {
            DisableHighlightForGoodsEntry(_goods.Find(entry => entry.kind == GoodsKind.Relic && entry.relic == e));
            Debug.Log($"[Shop] 购买遗物：{e.relic?.relicName} → {e.ownerCharacter?.displayName} 遗物库");
        }
        else Debug.Log($"[Shop] 购买遗物失败：{r}");
    }

    // ===== 删牌服务：点击 Service 按钮 → 弹出牌库选择界面，点卡即删 =====

    /// <summary>删牌按钮点击：校验剩余次数后，弹出 CardLibraryPanel 并进入删牌（删除）模式。</summary>
    private void OnServiceClicked()
    {
        if (_shop == null) return;
        if (_shop.RemovalsRemaining <= 0)
        {
            Debug.Log("[Shop] 删牌次数已用完（或本次商店未提供删牌服务）");
            return;
        }
        var lib = ResolveCardLibraryPanel();
        if (lib == null)
        {
            Debug.LogError("[Shop] 未配置 / 未找到 CardLibraryPanel（请在 Inspector 的 cardLibraryPanel 字段赋值）");
            return;
        }
        // 进入删除模式：网格中的卡变为可点击按钮，点卡即删
        lib.Init();   // 确保布局/角色列表已初始化（牌库面板与牌库浏览共用同一实例）
        lib.ShowRemovalMode(OnRemoveCard, () => _shop.RemovalsRemaining);
    }

    /// <summary>取得 CardLibraryPanelUI：优先用 Inspector 配置的 cardLibraryPanel，未配置时按场景查找（含未激活）。</summary>
    private CardLibraryPanelUI ResolveCardLibraryPanel()
    {
        if (cardLibraryPanel != null) return cardLibraryPanel;
        return UnityEngine.Object.FindObjectOfType<CardLibraryPanelUI>(true);
    }

    // —— 删牌：使用 CardLibraryPanel 作为选择界面（卡牌按钮化，点卡即删）——

    /// <summary>删牌回调：由 CardLibraryPanel 删除模式下点击卡牌触发。</summary>
    private void OnRemoveCard(CardInstance card, CharacterData owner)
    {
        if (_shop == null) return;
        var r = _shop.RemoveCard(card, owner);
        if (r == ShopResult.Success)
        {
            Debug.Log($"[Shop] 删除卡牌：{card.EffectiveName}（来自 {owner?.displayName} 牌库）");
            // 每删一次牌即关闭牌库界面，回到商店（可再次点删牌按钮继续删下一张）
            CloseRemoval();
        }
        else
        {
            Debug.Log($"[Shop] 删除卡牌失败：{r}");
            if (r == ShopResult.NoRemovalsLeft) CloseRemoval();
        }
        // RemoveCard 内部会广播 OnStockChanged → Render 自动刷新商店价格标签
    }

    /// <summary>关闭删牌选择界面：退出删除模式并隐藏 CardLibraryPanel。</summary>
    private void CloseRemoval()
    {
        var lib = ResolveCardLibraryPanel();
        if (lib == null) return;
        lib.EndRemovalMode();
        lib.Hide();
    }
}
