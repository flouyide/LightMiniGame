using System;
using System.Collections.Generic;
using LightMiniGame.Card;
using LightMiniGame.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店面板 UI：进店时从 ShopManager 读取库存并动态渲染（卡牌 / 遗物 / 删牌服务），
/// 点击购买 / 删除按钮调用 ShopManager 完成交易。所有条目在运行时用 uGUI 动态构建，
/// 因此无需额外预制体即可跑通；后续若要做精美样式，可替换为卡牌/遗物项预制体（见 cardItemPrefab 等字段）。
/// </summary>
public class ShopPanelUI : MonoBehaviour
{
    [Header("UI引用（未绑定时按名字自动查找子对象）")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI hintText;

    [Header("可选：卡牌/遗物/删牌项预制体（留空则运行时用 uGUI 自动构建）")]
    [SerializeField] private GameObject cardItemPrefab;
    [SerializeField] private GameObject relicItemPrefab;
    [SerializeField] private GameObject removeItemPrefab;

    private Action _onClose;
    private bool _closeHooked;

    private ShopManager _shop;
    private List<CharacterData> _characters;
    private GameObject _scrollGO;
    private RectTransform _contentRoot;

    // ===== 生命周期 =====
    private void Start()
    {
        if (panel == null) panel = transform.Find("Panel")?.gameObject;
        if (closeButton == null) closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
        if (hintText == null) hintText = transform.Find("HintText")?.GetComponent<TextMeshProUGUI>();
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

    /// <summary>
    /// 显示商店面板。shop 为商店控制器，characters 为两个出战角色（用于按比例抽卡），
    /// onClose 为关闭回调。进店会重新随机一次库存。
    /// </summary>
    public void Show(ShopManager shop, List<CharacterData> characters, Action onClose)
    {
        _shop = shop;
        _characters = characters;
        _onClose = onClose;

        if (_shop != null)
        {
            _shop.OpenShop(_characters);
            _shop.OnStockChanged -= Render;
            _shop.OnStockChanged += Render;
        }

        if (panel != null) panel.SetActive(true);
        RebuildContent();
    }

    private void OnCloseClicked()
    {
        if (_shop != null)
            _shop.OnStockChanged -= Render;
        _onClose?.Invoke();
        Hide();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        _onClose = null;
    }

    // ===== 内容构建 =====
    private void RebuildContent()
    {
        if (panel == null) return;
        if (_scrollGO != null) Destroy(_scrollGO);
        BuildScrollArea();
        Render();
    }

    private void BuildScrollArea()
    {
        // ScrollRect（仅纵向滚动，删牌列表可能较长）
        _scrollGO = new GameObject("ShopScroll", typeof(RectTransform), typeof(ScrollRect));
        _scrollGO.transform.SetParent(panel.transform, false);
        var sr = _scrollGO.GetComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        var vpGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        vpGO.transform.SetParent(_scrollGO.transform, false);
        var vpRT = vpGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;

        // Content（纵向布局 + 自适应高度）
        var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGO.transform.SetParent(vpGO.transform, false);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.sizeDelta = new Vector2(0, 0);

        var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = contentGO.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vpRT;
        sr.content = contentRT;
        _contentRoot = contentRT;
    }

    /// <summary>库存 / 金币变化后刷新（保留 ScrollRect，仅重刷条目）。</summary>
    private void Render()
    {
        if (_contentRoot == null || _shop == null) return;

        // 清旧条目
        var children = new List<Transform>();
        foreach (Transform c in _contentRoot) children.Add(c);
        foreach (var c in children) Destroy(c.gameObject);

        // 金币抬头
        AddSectionTitle($"金币：{_shop.PlayerGold}");

        // 卡牌
        AddSectionTitle("卡牌");
        if (_shop.CardStock.Count == 0) AddInfo("（总牌库为空或尚未配置 MasterCardLibrary）");
        foreach (var e in _shop.CardStock) AddCardRow(e);

        // 遗物
        AddSectionTitle("遗物");
        if (_shop.RelicStock.Count == 0) AddInfo("（总遗物库为空或尚未配置 MasterRelicLibrary）");
        foreach (var e in _shop.RelicStock) AddRelicRow(e);

        // 删牌服务
        AddSectionTitle($"删牌服务（剩余 {_shop.RemovalsRemaining} 次，本次价格 {_shop.CurrentRemovalPrice}）");
        var removable = _shop.GetRemovableCards();
        if (removable.Count == 0) AddInfo("（当前牌库为空）");
        foreach (var (card, owner) in removable) AddRemoveRow(card, owner);
    }

    // ===== 行构建辅助 =====
    private GameObject AddRow()
    {
        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(_contentRoot, false);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var le = row.GetComponent<LayoutElement>();
        le.minHeight = 32;
        return row;
    }

    private Text AddLabel(Transform parent, string text, int fontSize, Color color, float minWidth)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = minWidth;
        le.flexibleWidth = 1;
        le.minHeight = 28;
        return t;
    }

    private Button AddButton(Transform parent, string label, Action onClick, bool enabled)
    {
        var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = enabled ? new Color(0.20f, 0.60f, 0.25f) : new Color(0.35f, 0.35f, 0.35f);
        var btn = go.GetComponent<Button>();
        btn.interactable = enabled;
        btn.onClick.AddListener(() => onClick?.Invoke());
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 110;
        le.minHeight = 28;

        var lbl = new GameObject("Text", typeof(RectTransform), typeof(Text));
        lbl.transform.SetParent(go.transform, false);
        var t = lbl.GetComponent<Text>();
        t.text = label;
        t.fontSize = 15;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        var rt = lbl.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return btn;
    }

    private void AddSectionTitle(string text)
    {
        var go = new GameObject("Title", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(_contentRoot, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = 18;
        t.fontStyle = FontStyle.Bold;
        t.color = new Color(1f, 0.85f, 0.4f);
        t.alignment = TextAnchor.MiddleLeft;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 28;
    }

    private void AddInfo(string text)
    {
        var go = new GameObject("Info", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(_contentRoot, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = 14;
        t.color = new Color(0.7f, 0.7f, 0.7f);
        t.alignment = TextAnchor.MiddleLeft;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 24;
    }

    // ===== 各类行 =====
    private void AddCardRow(ShopCardEntry e)
    {
        var row = AddRow();
        string grade = e.card != null ? CardData.GetGradeName(e.card.grade) : "";
        string name = e.card != null ? e.card.cardName : "(空)";
        AddLabel(row.transform, name, 16, Color.white, 170);
        AddLabel(row.transform, $"[{grade}]", 14, Color.cyan, 70);
        bool canBuy = !e.sold && _shop.CanAfford(e.price);
        string label = e.sold ? "已售" : $"购买 {e.price}";
        AddButton(row.transform, label, () => OnBuyCard(e), canBuy);
    }

    private void AddRelicRow(ShopRelicEntry e)
    {
        var row = AddRow();
        string name = e.relic != null ? e.relic.relicName : "(空)";
        AddLabel(row.transform, name, 16, Color.white, 240);
        bool canBuy = !e.sold && _shop.CanAfford(e.price);
        string label = e.sold ? "已售" : $"购买 {e.price}";
        AddButton(row.transform, label, () => OnBuyRelic(e), canBuy);
    }

    private void AddRemoveRow(CardInstance card, CharacterData owner)
    {
        var row = AddRow();
        string cname = card != null ? card.EffectiveName : "(?)";
        string ownerName = owner != null ? owner.displayName : "";
        AddLabel(row.transform, $"{ownerName}：{cname}", 15, Color.white, 230);
        bool canRemove = _shop.RemovalsRemaining > 0 && _shop.CanAfford(_shop.CurrentRemovalPrice);
        AddButton(row.transform, $"删除 {_shop.CurrentRemovalPrice}", () => OnRemoveCard(card, owner), canRemove);
    }

    // ===== 按钮回调 =====
    private void OnBuyCard(ShopCardEntry e)
    {
        var r = _shop.BuyCard(e);
        if (r == ShopResult.Success) Debug.Log($"[Shop] 购买卡牌：{e.card?.cardName}");
        else Debug.Log($"[Shop] 购买卡牌失败：{r}");
        Render();
    }

    private void OnBuyRelic(ShopRelicEntry e)
    {
        var r = _shop.BuyRelic(e);
        if (r == ShopResult.Success) Debug.Log($"[Shop] 购买遗物：{e.relic?.relicName}");
        else Debug.Log($"[Shop] 购买遗物失败：{r}");
        Render();
    }

    private void OnRemoveCard(CardInstance card, CharacterData owner)
    {
        var r = _shop.RemoveCard(card, owner);
        if (r == ShopResult.Success) Debug.Log($"[Shop] 删除卡牌：{card?.EffectiveName}");
        else Debug.Log($"[Shop] 删牌失败：{r}");
        Render();
    }
}
