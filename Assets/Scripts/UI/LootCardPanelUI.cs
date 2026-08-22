using System;
using System.Collections.Generic;
using LightMiniGame.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 卡牌掉落选择面板控制器。
/// 由 LootPanelUI 在点击 CardA / CardB 时实例化 LootCardPanel.prefab 并 AddComponent 本脚本，
/// 再调用 Open(...) 抽取并展示候选卡牌。
///
/// 展示规则：按 LootEntry.cardDrawCount（n）从指定角色 MasterCardLibrary 中按品级概率抽 n 张，
/// 放入 CardLayer 的 HorizontalLayoutGroup；点击某张卡即发放给该角色并关闭面板，点击 Skip 直接关闭。
///
/// 容器与按钮优先使用 Inspector 字段，未配置时按 prefab 固定结构自动查找，免去手工接线：
///   - CardLayer：transform.Find("Background/CardLayer")
///   - Skip 按钮：transform.Find("Background/Skip")（回退 GetComponentInChildren）
/// </summary>
public class LootCardPanelUI : MonoBehaviour
{
    [Header("运行时解析（也可在 Inspector 预接）")]
    [SerializeField] private Transform cardLayer;        // 候选卡牌容器（HorizontalLayoutGroup）
    [SerializeField] private Button skipButton;          // 跳过按钮

    private ChapterManager _chapter;
    private int _characterIndex;
    private Action<int> _onPicked;
    private bool _closed;

    private void Awake()
    {
        // 优先用 Inspector 字段，否则按固定结构路径查找（路径比 GetComponentInChildren 更可靠，
        // 可避免 Instantiate+AddComponent 时序下 GetComponentInChildren 返回 null 导致 Skip 按钮监听挂不上）。
        if (cardLayer == null)
            cardLayer = transform.Find("Background/CardLayer");
        EnsureSkipButton();

        // 设为屏幕空间覆盖层，确保弹在最上层
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
        }

        // 关键修复：prefab 的 Canvas 上缺少 GraphicRaycaster。
        // Canvas 没有它时其下所有 Button（Skip、候选卡/遗物）都收不到点击事件，
        // 监听虽已挂上但点击永远到不了按钮。运行时补挂，避免手写 prefab YAML 的 GUID 风险。
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    /// <summary>按固定路径解析 Skip 按钮（优先），失败时回退 GetComponentInChildren。</summary>
    private void EnsureSkipButton()
    {
        if (skipButton != null) return;
        var skipGo = transform.Find("Background/Skip");
        if (skipGo != null) skipButton = skipGo.GetComponent<Button>();
        if (skipButton == null) skipButton = GetComponentInChildren<Button>();
    }

    /// <summary>
    /// 打开卡牌选择面板：抽取并展示候选卡牌。
    /// characterIndex：0=角色1，1=角色2。rarities：允许品级。drawCount：展示张数（=LootEntry.cardDrawCount）。
    /// cardPrefab：卡牌预制体（卡面 CardDisplay）。onPicked：玩家选中一张后回调（参数为 characterIndex）。
    /// </summary>
    public void Open(int characterIndex, List<CardGrade> rarities, int drawCount,
                     GameObject cardPrefab, Action<int> onPicked)
    {
        _characterIndex = characterIndex;
        _onPicked = onPicked;
        _chapter = FindObjectOfType<ChapterManager>();

        // 清空旧候选卡牌，并复位跳过按钮监听
        ClearCards();
        EnsureSkipButton();
        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(Close);
        }

        // 抽取候选卡牌
        if (_chapter == null)
        {
            Debug.LogWarning("[LootCardPanelUI] 未找到 ChapterManager，无法抽取卡牌");
            return;
        }

        if (!_chapter.DrawBattleLootCards(characterIndex, rarities, drawCount, out var cards) || cards.Count == 0)
        {
            Debug.LogWarning("[LootCardPanelUI] 暂无符合品级的可获取卡牌");
            return;
        }

        if (cardPrefab == null)
        {
            Debug.LogWarning("[LootCardPanelUI] 未配置卡牌预制体，无法展示候选");
            return;
        }

        // 自适应容器尺寸，确保 n 张卡完整显示
        FitBackgroundToCards(cards.Count, cardPrefab);

        // 实例化候选卡牌
        foreach (var data in cards)
        {
            var go = Instantiate(cardPrefab, cardLayer);
            var display = go.GetComponent<CardDisplay>();
            if (display != null) display.ApplyCardData(data);

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            int capturedIndex = _characterIndex;
            var capturedData = data;
            btn.onClick.AddListener(() => OnCardPicked(capturedIndex, capturedData));
        }

        Debug.Log($"[LootCardPanelUI] 角色{characterIndex + 1} 卡牌选择面板：展示 {cards.Count} 张候选");
    }

    private void OnCardPicked(int characterIndex, CardData data)
    {
        if (_chapter != null)
            _chapter.GrantBattleLootCard(characterIndex, data);
        _onPicked?.Invoke(characterIndex);
        Close();
    }

    private void Close()
    {
        if (_closed) return;
        _closed = true;
        Destroy(gameObject);
    }

    private void ClearCards()
    {
        if (cardLayer == null) return;
        for (int i = cardLayer.childCount - 1; i >= 0; i--)
        {
            var child = cardLayer.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
    }

    private void FitBackgroundToCards(int count, GameObject cardPrefab)
    {
        var bg = transform.Find("Background");
        if (bg == null) return;
        var cardRT = cardPrefab != null ? cardPrefab.GetComponent<RectTransform>() : null;
        float cardW = cardRT != null ? cardRT.rect.width : 180f;
        float cardH = cardRT != null ? cardRT.rect.height : 252f;
        var hlg = cardLayer != null ? cardLayer.GetComponent<HorizontalLayoutGroup>() : null;
        float padX = hlg != null ? (hlg.padding.left + hlg.padding.right) : 32f;
        float padY = hlg != null ? (hlg.padding.top + hlg.padding.bottom) : 32f;
        float spacing = hlg != null ? hlg.spacing : 16f;

        float width = count * cardW + (count - 1) * spacing + padX;
        float height = cardH + padY;
        var bgRT = bg.GetComponent<RectTransform>();
        if (bgRT != null)
            bgRT.sizeDelta = new Vector2(Mathf.Max(width, bgRT.sizeDelta.x), Mathf.Max(height, bgRT.sizeDelta.y));
    }

    /// <summary>
    /// 打开遗物选择面板：从指定角色 MasterRelicLibrary 按品级概率抽取候选遗物并展示。
    /// 与卡牌面板共用同一 prefab（CardLayer + Skip），但候选以运行时构建的遗物单元格呈现。
    /// characterIndex：0=角色1，1=角色2。rarities：允许品级。drawCount：展示个数。
    /// </summary>
    public void OpenRelics(int characterIndex, List<CardGrade> rarities, int drawCount, Action<int> onPicked)
    {
        _characterIndex = characterIndex;
        _onPicked = onPicked;
        _chapter = FindObjectOfType<ChapterManager>();

        ClearCards();
        EnsureSkipButton();
        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(Close);
        }

        if (_chapter == null)
        {
            Debug.LogWarning("[LootCardPanelUI] 未找到 ChapterManager，无法抽取遗物");
            return;
        }

        if (!_chapter.DrawBattleLootRelics(characterIndex, rarities, drawCount, out var relics) || relics.Count == 0)
        {
            Debug.LogWarning("[LootCardPanelUI] 暂无符合品级且未拥有的遗物");
            return;
        }

        FitBackgroundToRelics(relics.Count);

        foreach (var relic in relics)
            BuildRelicCell(relic, characterIndex);

        Debug.Log($"[LootCardPanelUI] 角色{characterIndex + 1} 遗物选择面板：展示 {relics.Count} 个候选");
    }

    private void OnRelicPicked(int characterIndex, RelicData relic)
    {
        if (_chapter != null)
            _chapter.GrantBattleLootRelic(characterIndex, relic);
        _onPicked?.Invoke(characterIndex);
        Close();
    }

    /// <summary>运行时构建单个遗物候选单元格（图标 + 名称/品级），并挂 Button 用于选中发放。</summary>
    private void BuildRelicCell(RelicData relic, int characterIndex)
    {
        if (cardLayer == null || relic == null) return;

        var cell = new GameObject("RelicCell", typeof(RectTransform));
        cell.transform.SetParent(cardLayer, false);
        var rt = cell.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(180f, 252f);

        var bg = cell.AddComponent<Image>();
        bg.color = new Color(0.13f, 0.13f, 0.18f, 1f);

        if (relic.icon != null)
        {
            var iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(cell.transform, false);
            var iconRT = iconGo.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.sizeDelta = new Vector2(120f, 120f);
            iconRT.anchoredPosition = new Vector2(0f, 36f);
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = relic.icon;
            icon.preserveAspect = true;
            icon.color = Color.white;
        }

        var labelGo = new GameObject("Label", typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(cell.transform, false);
        var labelRT = labelGo.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.5f, 0.5f);
        labelRT.anchorMax = new Vector2(0.5f, 0.5f);
        labelRT.pivot = new Vector2(0.5f, 0.5f);
        labelRT.sizeDelta = new Vector2(164f, 90f);
        labelRT.anchoredPosition = new Vector2(0f, -64f);
        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text = $"{relic.relicName}\n<size=18>{relic.grade}</size>";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontSize = 22;

        var btn = cell.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        int ci = characterIndex;
        var captured = relic;
        btn.onClick.AddListener(() => OnRelicPicked(ci, captured));
    }

    private void FitBackgroundToRelics(int count)
    {
        var bg = transform.Find("Background");
        if (bg == null) return;
        float cardW = 180f, cardH = 252f;
        var hlg = cardLayer != null ? cardLayer.GetComponent<HorizontalLayoutGroup>() : null;
        float padX = hlg != null ? (hlg.padding.left + hlg.padding.right) : 32f;
        float padY = hlg != null ? (hlg.padding.top + hlg.padding.bottom) : 32f;
        float spacing = hlg != null ? hlg.spacing : 16f;

        float width = count * cardW + (count - 1) * spacing + padX;
        float height = cardH + padY;
        var bgRT = bg.GetComponent<RectTransform>();
        if (bgRT != null)
            bgRT.sizeDelta = new Vector2(Mathf.Max(width, bgRT.sizeDelta.x), Mathf.Max(height, bgRT.sizeDelta.y));
    }
}
