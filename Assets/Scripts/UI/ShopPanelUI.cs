using System;
using System.Collections.Generic;
using LightMiniGame.Card;
using LightMiniGame.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店面板 UI（仿《杀戮尖塔2》布局）：
///  - ShopBoard 分为上下两层：上层 CardLayer 水平排列卡牌，下层 RelicLayer 水平排列遗物；
///  - 右下角保留 prefab 里的 Service（删牌）按钮，可在 Inspector 的 serviceButton 字段配置（未配时按路径 _shopBoard/Service 查找），点击弹出牌库选择界面；
///  - 每次进店刷新一次卡牌 / 遗物（由 ShopManager.OpenShop 负责抽取）；
///  - 每张卡牌 / 遗物下方显示价格，点击即购买：卡牌进对应角色牌库（CharacterCardLibrary），
///    遗物进对应角色的遗物库（GlobalRelicInventory），并扣除对应货币。
/// 卡牌条目使用 Assets/Prefabs/Battle/Cards 下按类型（攻击/护甲/增益）细分的卡牌预制体，
/// 由牌库数据驱动绘制卡面；遗物条目在运行时动态生成。两者都挂到 CardLayer / RelicLayer 下。
/// </summary>
public class ShopPanelUI : MonoBehaviour
{
    [Header("UI引用（未绑定时按路径自动查找）")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private Button serviceButton;           // 删牌按钮（prefab 中的 Service），点击弹出牌库选择界面；未绑定时按路径查找

    [Header("卡牌预制体（按 CardType 细分，拖入对应 prefab）")]
    [Tooltip("攻击牌预制体（Assets/Prefabs/Battle/Cards/攻击牌.prefab），须含 CardDisplay 组件")]
    public GameObject attackCardPrefab;
    [Tooltip("护甲牌预制体（Assets/Prefabs/Battle/Cards/护甲牌.prefab），须含 CardDisplay 组件")]
    public GameObject armorCardPrefab;
    [Tooltip("增益牌预制体（Assets/Prefabs/Battle/Cards/增益牌.prefab），须含 CardDisplay 组件")]
    public GameObject buffCardPrefab;

    private Action _onClose;
    private bool _closeHooked;

    private ShopManager _shop;
    private List<CharacterData> _characters;

    private Transform _shopBoard;
    private Transform _cardLayer;
    private Transform _relicLayer;
    private Text _goldLabel;

    // ===== 删牌服务（点击 Service 按钮弹出 CardLibraryPanel，点卡即删）=====
    private Button _serviceButton;
    private bool _serviceHooked;
    private TextMeshProUGUI _removalPriceText;     // 删牌按钮（Service）下 Price 子物体里的价格标签（TMP）

    [Header("牌库界面（用于删牌选择；优先 Inspector 配置，未配时按场景查找）")]
    [Tooltip("CardLibraryPanel.prefab 的 UI 控件（场景实例，通常位于 BookCanvas 下）。点击删牌按钮后以此面板作为选择界面。")]
    public CardLibraryPanelUI cardLibraryPanel;

    // ===== 生命周期 =====
    private void Start()
    {
        if (panel == null) panel = transform.Find("Panel")?.gameObject;
        if (closeButton == null) closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
        if (hintText == null) hintText = transform.Find("HintText")?.GetComponent<TextMeshProUGUI>();

        _shopBoard = transform.Find("Panel/ShopBoard");
        EnsureLayers();
        HookServiceButton();
        EnsureRemovalPriceText();

        if (panel != null) panel.SetActive(false);
        HookCloseButton();
    }

    private void OnEnable() => HookCloseButton();

    private void OnDisable()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
        _closeHooked = false;
        if (_shop != null)
            _shop.OnStockChanged -= Render;
    }

    private void HookCloseButton()
    {
        if (closeButton != null && !_closeHooked)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
            _closeHooked = true;
        }
    }

    // ===== 在 ShopBoard 下创建上下两层（若 prefab 未提供则运行时创建）=====
    private void EnsureLayers()
    {
        if (_shopBoard == null) return;

        _cardLayer = _shopBoard.Find("CardLayer");
        if (_cardLayer == null)
            _cardLayer = CreateLayer("CardLayer", new Vector2(0f, 0.52f), new Vector2(0.82f, 0.98f));
        else
            SetupHorizontal(_cardLayer);

        _relicLayer = _shopBoard.Find("RelicLayer");
        if (_relicLayer == null)
            _relicLayer = CreateLayer("RelicLayer", new Vector2(0f, 0.02f), new Vector2(0.82f, 0.48f));
        else
            SetupHorizontal(_relicLayer);

        // 顺手隐藏 prefab 里遗留的单个 Card / Relic 占位（若有），避免与动态行重复
        var legacy = _shopBoard.Find("Card");
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
        var hlg = layer.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = layer.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 100;
        hlg.padding = new RectOffset(16, 16, 16, 16);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
    }

    // ===== 对外：显示商店 =====
    public void Show(ShopManager shop, List<CharacterData> characters, Action onClose)
    {
        _shop = shop;
        _characters = characters;
        _onClose = onClose;

        if (_shop != null)
        {
            _shop.OpenShop(_characters);   // 每次进店重抽一次卡牌与遗物
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
        if (_shop == null || _cardLayer == null || _relicLayer == null) return;

        UpdateGoldLabel();

        ClearLayer(_cardLayer);
        ClearLayer(_relicLayer);

        if (_shop.CardStock.Count == 0)
            AddNote(_cardLayer, "（总牌库为空或未配置 MasterCardLibrary）");
        foreach (var e in _shop.CardStock)
            BuildCardItem(_cardLayer, e);

        if (_shop.RelicStock.Count == 0)
            AddNote(_relicLayer, "（总遗物库为空或未配置 MasterRelicLibrary）");
        foreach (var e in _shop.RelicStock)
            BuildRelicItem(_relicLayer, e);

        // 同步删牌价格标签
        UpdateRemovalPriceText();
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

    private void ClearLayer(Transform layer)
    {
        var children = new List<Transform>();
        foreach (Transform c in layer) children.Add(c);
        foreach (var c in children) Destroy(c.gameObject);
    }

    private void AddNote(Transform parent, string text)
    {
        var go = new GameObject("Note", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = 14;
        t.color = new Color(0.7f, 0.7f, 0.7f);
        t.alignment = TextAnchor.MiddleCenter;
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 160;
        le.minHeight = 160;
    }

    // ===== 商品条目 =====
    private GameObject GetCardPrefab(CardType type) => type switch
    {
        CardType.Attack => attackCardPrefab,
        CardType.Armor => armorCardPrefab,
        CardType.Buff => buffCardPrefab,
        _ => attackCardPrefab
    };

    /// <summary>
    /// 用对应类型的卡牌预制体渲染一张待售卡牌：
    ///  - 竖向容器 = 角色名（上，TMP）+ 卡牌（中，含 CardDisplay）+ 价格（下，TMP）；
    ///  - 卡牌用 CardDisplay.Apply(CardData) 由数据驱动绘制；
    ///  - 点击卡牌即购买。
    /// </summary>
    private void BuildCardItem(Transform parent, ShopCardEntry e)
    {
        if (e.card == null) { AddNote(parent, "(空卡牌)"); return; }

        bool sold = e.sold;
        bool affordable = !sold && _shop.CanAfford(e.price);

        var prefab = GetCardPrefab(e.card.cardType);
        if (prefab == null)
        {
            AddNote(parent, $"[缺少 {CardData.GetCardTypeName(e.card.cardType)} 预制体]");
            return;
        }

        // 竖向容器：角色名（上）→ 卡牌（中）→ 价格（下）
        var wrapper = new GameObject("CardItem", typeof(RectTransform), typeof(VerticalLayoutGroup));
        wrapper.transform.SetParent(parent, false);
        var vlg = wrapper.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        // 角色名（卡牌上方，TMP）—— 此卡牌属于哪个角色的牌库
        string ownerName = e.ownerCharacter != null ? e.ownerCharacter.displayName : "通用";
        AddTmpText(wrapper.transform, ownerName, 18, new Color(0.85f, 0.9f, 1f), FontStyles.Bold);

        // 实例化卡牌预制体
        var cardGO = Instantiate(prefab, wrapper.transform, false);
        var cardRT = cardGO.GetComponent<RectTransform>();
        cardRT.localScale = Vector3.one;   // 防止父级缩放影响卡面

        // 用牌库数据驱动绘制卡面
        var display = cardGO.GetComponent<CardDisplay>();
        if (display != null) display.ApplyCardData(e.card);

        // 价格（卡牌下方，TMP）
        AddTmpText(wrapper.transform, sold ? "已售" : $"价格 {e.price}", 18,
            sold ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.85f, 0.3f), FontStyles.Bold);

        // 点击卡牌即购买（整张卡牌作为按钮）
        var btn = cardGO.GetComponent<Button>() ?? cardGO.AddComponent<Button>();
        btn.targetGraphic = cardGO.GetComponent<Image>();
        btn.interactable = affordable;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnBuyCard(e));

        if (sold)
        {
            var img = cardGO.GetComponent<Image>();
            if (img != null) img.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        }
    }

    private void BuildRelicItem(Transform parent, ShopRelicEntry e)
    {
        if (e.relic == null) { AddNote(parent, "(空遗物)"); return; }

        bool sold = e.sold;
        bool affordable = !sold && _shop.CanAfford(e.price);
        string name = e.relic.relicName;
        // 遗物上方显示所属角色名（每个角色有独立遗物池）
        string ownerName = e.ownerCharacter != null ? e.ownerCharacter.displayName : "";
        string price = sold ? "已售" : $"{e.price}";
        Sprite icon = e.relic.icon;   // RelicData.icon：遗物图标
        BuildItem(parent, ownerName, name, price, icon, new Color(0.45f, 0.76f, 0.44f), affordable,
            () => OnBuyRelic(e));
    }

    /// <summary>
    /// 生成一个遗物商品条目：竖向布局 = [所属角色名(TMP)] + [图标(RelicData.icon)] + [名称(TMP)] + [价格(TMP, 下方)]，
    /// 整块可点击购买。图标缺失时仅显示纯色块背景。
    /// </summary>
    private void BuildItem(Transform parent, string ownerName, string name, string price, Sprite icon,
        Color bgColor, bool affordable, Action onClick)
    {
        var go = new GameObject("RelicItem", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = affordable ? bgColor : new Color(0.45f, 0.45f, 0.45f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = affordable;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        // 所属角色名（TMP，置顶，金色）—— 每个角色有独立遗物池
        if (!string.IsNullOrEmpty(ownerName))
            AddTmpText(go.transform, ownerName, 13, new Color(0.95f, 0.85f, 0.45f), FontStyles.Bold);

        // 遗物图标：使用 RelicData.icon（缺失则跳过，仅留背景色块）
        if (icon != null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(go.transform, false);
            var iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.sizeDelta = new Vector2(96, 96);
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = icon;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
        }

        // 名称（TMP）
        AddTmpText(go.transform, name, 16, Color.white, FontStyles.Bold);

        // 价格（TMP，显示在图标下方）
        AddTmpText(go.transform, price, 18,
            affordable ? new Color(1f, 0.85f, 0.3f) : new Color(0.6f, 0.6f, 0.6f), FontStyles.Bold);
    }

    /// <summary>动态创建一个 TextMeshProUGUI 文本（用于卡牌的角色名 / 价格，避免依赖 prefab）。</summary>
    private void AddTmpText(Transform parent, string text, float fontSize, Color color, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("TmpTxt", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(180f, 24f);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = style;
        t.raycastTarget = false;
    }

    // ===== 购买回调 =====
    private void OnBuyCard(ShopCardEntry e)
    {
        var r = _shop.BuyCard(e);
        if (r == ShopResult.Success) Debug.Log($"[Shop] 购买卡牌：{e.card?.cardName} → {e.ownerCharacter?.displayName} 牌库");
        else Debug.Log($"[Shop] 购买卡牌失败：{r}");
        // BuyCard 内部会广播 OnStockChanged → Render 自动刷新
    }

    private void OnBuyRelic(ShopRelicEntry e)
    {
        var r = _shop.BuyRelic(e);
        if (r == ShopResult.Success) Debug.Log($"[Shop] 购买遗物：{e.relic?.relicName} → {e.ownerCharacter?.displayName} 遗物库");
        else Debug.Log($"[Shop] 购买遗物失败：{r}");
    }

    // ===== 删牌服务：点击 Service 按钮 → 弹出牌库选择界面，点卡即删 =====

    /// <summary>绑定「删牌」按钮（Service）到 OnServiceClicked：优先用 Inspector 配置的 serviceButton，
    /// 未配置时回退到 prefab 路径 _shopBoard/Service 查找。</summary>
    private void HookServiceButton()
    {
        Button btn = null;
        if (serviceButton != null)
        {
            btn = serviceButton;
        }
        else if (_shopBoard != null)
        {
            var svc = _shopBoard.Find("Service");
            btn = svc != null ? svc.GetComponent<Button>() : null;
        }
        _serviceButton = btn;
        if (_serviceButton != null && !_serviceHooked)
        {
            _serviceButton.onClick.AddListener(OnServiceClicked);
            _serviceHooked = true;
        }
    }

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

    /// <summary>解析删牌按钮（Service）下 Price 子物体中的 TMP：作为删牌价格标签（取代单独创建/查找 _shopBoard 下的 RemovalPriceText）。</summary>
    private void EnsureRemovalPriceText()
    {
        if (_removalPriceText != null) return;
        if (_serviceButton != null)
        {
            var priceGO = _serviceButton.transform.Find("Price");
            if (priceGO != null)
                _removalPriceText = priceGO.GetComponent<TextMeshProUGUI>();
        }
        if (_removalPriceText == null)
            Debug.LogWarning("[ShopPanelUI] 未在 serviceButton 下找到 Price 子物体的 TMP，删牌价格标签不可用");
        UpdateRemovalPriceText();
    }

    private void UpdateRemovalPriceText()
    {
        if (_removalPriceText == null) return;
        if (_shop == null || _shop.RemovalsRemaining <= 0)
            _removalPriceText.text = "已删牌";
        else
            _removalPriceText.text = $"删牌价格：{_shop.CurrentRemovalPrice}";
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
            UpdateRemovalPriceText();   // 价格随 removalPriceStep 上涨
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
