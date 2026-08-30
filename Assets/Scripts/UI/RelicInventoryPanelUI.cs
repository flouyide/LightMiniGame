using System.Collections.Generic;
using LightMiniGame.Card;
using LightMiniGame.Shop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 遗物清单界面主控面板（基于 RelicInventoryPanel.prefab）。
/// 行为与 CardLibraryPanelUI 一致（每个角色有独立遗物库）：
///   - Show/Hide 时暂停/恢复游戏，屏蔽背后所有交互（同 SettingsPanelUI 模式）。
///   - 顶部显示当前角色名/遗物数量（左），关闭按钮（右）。
///   - 两个角色切换按钮 CharacterButton1 / CharacterButton2：点击切换该角色的遗物库，
///     按钮上显示角色头像（CharacterData.avatar）与名字。
///   - 滚动区列出当前角色拥有的全部遗物（来自 GlobalRelicInventory.Instance.GetRelics），
///     每张遗物显示图标（RelicData.icon）、名称、品级、描述（均为 TMP）。
///
/// 用法：
///   1. 在 Inspector 把 RelicInventoryPanel（已作为 BookCanvas 子物体）赋给
///      BookUIController.relicInventoryPanel；并给该面板挂上 RelicInventoryPanelUI 组件，
///      配置 panel / closeButton / content / CharacterButton1 / CharacterButton2 字段。
///   2. 把 RelicButton 赋给 BookUIController.relicButton。
///   3. 点击 RelicButton → BookUIController 调用本面板的 Show()。
/// </summary>
public class RelicInventoryPanelUI : MonoBehaviour
{
    [Header("=== 面板根节点（来自 RelicInventoryPanel.prefab）===")]
    [Tooltip("面板根物体（含 Canvas）。由预制体注入，不要运行时自建。留空则回退到本组件所在 GameObject。")]
    public GameObject panel;
    [Tooltip("标题文本")]
    public TextMeshProUGUI titleText;
    [Tooltip("右上角关闭按钮")]
    public Button closeButton;
    [Tooltip("切换角色按钮（对应角色1）")]
    public Button CharacterButton1;
    [Tooltip("切换角色按钮（对应角色2）")]
    public Button CharacterButton2;
    [Tooltip("遗物列表内容容器（VerticalLayoutGroup 的父节点，通常挂在一个 ScrollRect 的 Content 上）")]
    public Transform content;
    [Tooltip("遗物库使用的条目预制体，运行时会隐藏其中的 PriceRow。")]
    public GameObject relicItemPrefab;

    [Header("=== 单条遗物条目布局 ===")]
    [Tooltip("遗物库条目预制体的统一缩放倍率")]
    [SerializeField, Min(0.01f)] private float relicItemScale = 1f;

    [Header("=== 描述悬停（TooltipLayer）===")]
    [Tooltip("描述文本相对遗物条目底部中心的偏移（x 正向右、y 正向上；默认略微下移留出间隙）")]
    [SerializeField] private Vector2 tooltipOffset = new Vector2(0f, -8f);

    // ===== 内部状态 =====
    private readonly List<CharacterData> _registeredCharacters = new List<CharacterData>();
    private int _currentCharacterIndex = -1;     // 当前选中角色的索引（_registeredCharacters 中）
    private readonly List<GameObject> _entryObjects = new List<GameObject>();
    private readonly List<GameObject> _descTexts = new List<GameObject>();   // 被 reparent 到 TooltipLayer 的描述（需随条目一起清理）
    private Transform _tooltipLayer;            // 描述专用层：panel 的最后一个子物体，确保渲染在 Content 之上
    private GameObject _activeTooltipItem;      // 当前悬停的遗物条目（LateUpdate 跟随其位置）
    private GameObject _activeTooltipDesc;     // 当前显示的描述文本
    private readonly Dictionary<Button, ColorBlock> _characterButtonDefaultColors = new Dictionary<Button, ColorBlock>();
    private Vector2 _baseRelicGridCellSize;
    private bool _hasBaseRelicGridCellSize;

    // 暂停 & 背景屏蔽（与 CardLibraryPanelUI 同模式）
    private readonly List<Selectable> _disabledBackground = new List<Selectable>();

    #region 生命周期

    private void Awake()
    {
        // 便利：未显式绑定 panel 时，回退到本组件所在 GameObject 作为面板根
        if (panel == null) panel = gameObject;
        BindExistingUI();
        HideImmediate();
    }

    #endregion

    #region 显示 / 隐藏

    /// <summary>打开遗物清单：激活面板、暂停游戏、屏蔽背景交互、刷新角色与列表。</summary>
    public void Show()
    {
        if (panel == null) return;
        panel.SetActive(true);

        Time.timeScale = 0f;
        DisableBackgroundInteractables();
        RefreshCharacterList();
        if (_registeredCharacters.Count > 0)
            SwitchToCharacter(_currentCharacterIndex >= 0 && _currentCharacterIndex < _registeredCharacters.Count ? _currentCharacterIndex : 0);
        else
            RefreshList(null);
    }

    /// <summary>关闭遗物清单：隐藏面板、恢复游戏、恢复背景交互。</summary>
    public void Hide()
    {
        if (panel == null) return;
        Debug.Log("Close relic inventory");
        HideImmediate();
    }

    private void HideImmediate()
    {
        if (panel != null) panel.SetActive(false);
        Time.timeScale = 1f;
        EnableBackgroundInteractables();
    }

    #endregion

    #region 角色切换（仿 CardLibraryPanelUI 的 CharacterButton）

    /// <summary>从 GlobalRelicInventory 刷新已注册角色列表并重建切换按钮。</summary>
    public void RefreshCharacterList()
    {
        var gri = GlobalRelicInventory.Instance;
        if (gri == null) { Debug.LogWarning("[RelicInventoryPanel] GlobalRelicInventory.Instance 为空"); return; }

        _registeredCharacters.Clear();
        foreach (var lib in gri.AllLibraries)
            if (lib.owner != null && !_registeredCharacters.Contains(lib.owner))
                _registeredCharacters.Add(lib.owner);

        SetupCharacterButtons();
    }

    /// <summary>切换到指定索引的角色遗物库（刷新标题、按钮高亮与列表）。</summary>
    public void SwitchToCharacter(int index)
    {
        if (index < 0 || index >= _registeredCharacters.Count) return;
        _currentCharacterIndex = index;
        UpdateCharacterButtonHighlight(index);
        RefreshList(_registeredCharacters[index]);   // RefreshList 内部会更新 titleText
    }

    #endregion

    #region 遗物列表

    /// <summary>从 GlobalRelicInventory.Instance.GetRelics 刷新当前角色的遗物。</summary>
    public void RefreshList(CharacterData character)
    {
        if (content == null) return;

        // 清理旧条目
        foreach (var go in _entryObjects)
            if (go != null) Destroy(go);
        _entryObjects.Clear();
        // 旧条目 reparent 到 TooltipLayer 的描述也已脱离条目，需独立清理
        foreach (var go in _descTexts)
            if (go != null) Destroy(go);
        _descTexts.Clear();
        _activeTooltipItem = null;
        _activeTooltipDesc = null;

        var relics = character != null ? GlobalRelicInventory.Instance?.GetRelics(character) : null;

        if (titleText != null)
        {
            string title = character != null ? character.displayName : "遗物清单";
            titleText.SetText($"{title} ({(relics != null ? relics.Count : 0)})");
        }

        if (relics == null || relics.Count == 0)
        {
            AddNote("（暂无遗物）");
            Canvas.ForceUpdateCanvases();
            return;
        }

        ApplyRelicItemScaleToLayout();

        foreach (var relic in relics)
            AddRelicItem(relic);

        Canvas.ForceUpdateCanvases();
    }

    private void AddRelicItem(RelicData relic)
    {
        if (relic == null || relicItemPrefab == null) return;

        // GridLayoutGroup 不会把 localScale 计入子节点的占位尺寸。
        // 用未缩放的占位容器参与网格排版，再将实际视觉预制体放入其中缩放，避免放大后跨出相邻格子或库边框。
        var slot = new GameObject($"{relic.relicName}_Slot", typeof(RectTransform));
        slot.transform.SetParent(content, false);

        var item = Instantiate(relicItemPrefab, slot.transform, false);
        item.name = relic.relicName;
        var itemRect = item.transform as RectTransform;
        if (itemRect != null)
        {
            itemRect.anchorMin = new Vector2(0.5f, 0.5f);
            itemRect.anchorMax = new Vector2(0.5f, 0.5f);
            itemRect.anchoredPosition = Vector2.zero;
        }
        item.transform.localScale = Vector3.one * relicItemScale;
        item.SetActive(true);

        var iconImage = item.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImage != null)
        {
            iconImage.sprite = relic.icon;
            iconImage.color = relic.icon != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);
        }

        var nameText = item.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.SetText(relic.relicName);

        // RelicItem 同时服务商店与遗物库：遗物库只展示已拥有遗物，因此不展示购买价格行。
        var priceRow = item.transform.Find("PriceRow");
        if (priceRow != null)
            priceRow.gameObject.SetActive(false);

        // 遗物库专属：描述文本默认隐藏，鼠标悬停时才显示（商店共用同一预制体，不走这里，故不受影响）。
        var descNode = item.transform.Find("DescText");
        if (descNode != null)
        {
            var descTmp = descNode.GetComponent<TextMeshProUGUI>();
            if (descTmp != null && !string.IsNullOrEmpty(relic.description))
                descTmp.SetText(relic.description);
            descNode.gameObject.SetActive(false);
            RaiseDescAboveSiblings(descNode.gameObject);
            BindDescHover(item, descNode.gameObject);
            _descTexts.Add(descNode.gameObject);
        }

        _entryObjects.Add(slot);
    }

    /// <summary>
    /// 遗物库条目悬停规则：鼠标移入条目 → 启用 DescText 并对齐到条目下方；鼠标移出 → 禁用 DescText。
    /// 用 EventTrigger 挂运行时监听，不改动预制体，因此商店里实例化的 RelicItem 不会有这条规则。
    /// </summary>
    private void BindDescHover(GameObject item, GameObject descGo)
    {
        var trigger = item.GetComponent<EventTrigger>();
        if (trigger == null) trigger = item.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            descGo.SetActive(true);
            _activeTooltipItem = item;
            _activeTooltipDesc = descGo;
            PositionTooltip(item, descGo);   // 显示时立即对齐一次
        });
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            descGo.SetActive(false);
            if (_activeTooltipDesc == descGo)
            {
                _activeTooltipItem = null;
                _activeTooltipDesc = null;
            }
        });
        trigger.triggers.Add(exit);
    }

    /// <summary>
    /// 悬停期间每帧跟随条目位置（ScrollRect 滚动时描述不会与条目脱节）。
    /// </summary>
    private void LateUpdate()
    {
        if (_activeTooltipItem != null && _activeTooltipDesc != null && _activeTooltipDesc.activeSelf)
            PositionTooltip(_activeTooltipItem, _activeTooltipDesc);
    }

    /// <summary>
    /// 把描述文本对齐到遗物条目正下方：
    /// 取条目底部中心的世界坐标，转换到 TooltipLayer 局部坐标，再叠加可调偏移 tooltipOffset。
    /// 描述锚点/轴心取顶部中心（pivot 上沿对齐条目底边），向下展开。
    /// </summary>
    private void PositionTooltip(GameObject item, GameObject descGo)
    {
        if (_tooltipLayer == null) return;
        var layerRT = (RectTransform)_tooltipLayer;
        var itemRT = item.transform as RectTransform;
        var descRT = descGo.transform as RectTransform;
        if (itemRT == null || descRT == null) return;

        // 条目底部中心（GetWorldCorners 顺序：左下、左上、右上、右下，已含缩放）
        var corners = new Vector3[4];
        itemRT.GetWorldCorners(corners);
        var bottomCenter = (corners[0] + corners[3]) * 0.5f;

        // 转换到 TooltipLayer 局部坐标（层 pivot=中心，锚点也取中心，直接可用）
        var local = layerRT.InverseTransformPoint(bottomCenter);

        descRT.anchorMin = descRT.anchorMax = new Vector2(0.5f, 0.5f);
        descRT.pivot = new Vector2(0.5f, 1f);   // 顶部中心为轴，从条目底边向下展开
        descRT.anchoredPosition = (Vector2)local + tooltipOffset;
    }

    /// <summary>
    /// 让描述文本渲染在所有遗物条目之上，解决被下一行条目遮挡的问题。
    ///
    /// DescText 位于条目 rect 下方、超出 GridLayoutGroup 单元格范围，最初的方案是给它挂
    /// overrideSorting 的嵌套 Canvas，但在本项目面板结构（panel 在 BookCanvas 下、未必
    /// overrideSorting）下经常与其它 UI 同批次绘制，仍按 hierarchy 顺序被下一行条目盖住。
    /// 这里改为更稳的做法：把 DescText 整个 reparent 到 panel 下的 TooltipLayer
    /// （作为 panel 的最后一个子物体），让 hierarchy 顺序直接保证它绘制在 Content 之上。
    /// worldPositionStays 已不再需要：位置由 PositionTooltip 在悬停时按条目当前位置
    /// 动态对齐到条目下方（LateUpdate 跟随，支持滚动），并用 tooltipOffset 字段微调。
    /// 同时关闭其射线检测：描述不接收指针，指针穿过它落到背后的条目上，
    /// 符合「移出条目即隐藏」的规则，也不会出现悬停闪烁。
    /// </summary>
    private void RaiseDescAboveSiblings(GameObject descGo)
    {
        if (panel == null) return;
        EnsureTooltipLayer();

        var descRT = descGo.transform as RectTransform;
        if (descRT == null) return;

        // 只负责脱离条目树；锚点/轴心/位置由 PositionTooltip 在显示时按条目位置动态设定。
        descRT.SetParent(_tooltipLayer, false);

        // 描述不参与点击：关闭射线检测，避免它截获指针事件。
        var tmp = descGo.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.raycastTarget = false;
    }

    /// <summary>
    /// 懒创建 TooltipLayer：作为 panel 的最后一个子物体（hierarchy 顺序最后，渲染最上层）。
    /// 全屏拉伸到 panel 内，使其中的描述可以基于 panel 坐标系自由定位。
    /// </summary>
    private void EnsureTooltipLayer()
    {
        if (_tooltipLayer != null) return;
        if (panel == null) return;

        var go = new GameObject("TooltipLayer", typeof(RectTransform));
        _tooltipLayer = go.transform;
        var rt = (RectTransform)_tooltipLayer;
        rt.SetParent(panel.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _tooltipLayer.SetAsLastSibling();
    }

    /// <summary>
    /// Transform.localScale 不参与 GridLayoutGroup 的排版计算。
    /// 缩放遗物条目后同步扩大单元格，并按 Viewport 宽度重新计算列数，
    /// 让超出可视区域的内容转为下一行并交由 ScrollRect 滚动，而非溢出遗物库边框。
    /// </summary>
    private void ApplyRelicItemScaleToLayout()
    {
        if (content == null || relicItemPrefab == null)
            return;

        var grid = content.GetComponent<GridLayoutGroup>();
        var prefabRect = relicItemPrefab.transform as RectTransform;
        if (grid == null || prefabRect == null)
            return;

        if (!_hasBaseRelicGridCellSize)
        {
            _baseRelicGridCellSize = grid.cellSize;
            _hasBaseRelicGridCellSize = true;
        }

        float scale = Mathf.Max(0.01f, relicItemScale);
        // 预留边距，保证缩放后的视觉边界仍完整落在 Grid 单元格内。
        Vector2 scaledItemSize = prefabRect.sizeDelta * scale;
        const float cellPadding = 16f;
        grid.cellSize = new Vector2(
            Mathf.Max(_baseRelicGridCellSize.x, scaledItemSize.x + cellPadding),
            Mathf.Max(_baseRelicGridCellSize.y, scaledItemSize.y + cellPadding));

        var scrollRect = content.GetComponentInParent<ScrollRect>();
        if (scrollRect != null && scrollRect.viewport != null)
        {
            float availableWidth = scrollRect.viewport.rect.width - grid.padding.left - grid.padding.right;
            float cellWidthWithSpacing = grid.cellSize.x + grid.spacing.x;
            int columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + grid.spacing.x) / cellWidthWithSpacing));
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform);
    }

    private void AddNote(string text)
    {
        var go = new GameObject("Note", typeof(RectTransform));
        go.transform.SetParent(content, false);
        AddTmpText(go.transform, text, 16, FontStyles.Normal, new Color(0.6f, 0.6f, 0.6f), TextAlignmentOptions.Center, false, false);
        _entryObjects.Add(go);
    }

    /// <summary>动态创建 TMP 文本。widthFit/heightFit 控制 ContentSizeFitter（便于在布局组内自动撑开）。</summary>
    private static void AddTmpText(Transform parent, string text, float size, FontStyles style, Color color, TextAlignmentOptions align, bool widthFit, bool heightFit)
    {
        var go = new GameObject("TMP", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = widthFit ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = heightFit ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
    }

    #endregion

    #region 角色切换按钮（仿 CardLibraryPanelUI）

    /// <summary>
    /// 绑定 CharacterButton1 / CharacterButton2 到前两个注册角色：
    /// 按钮头像设为角色头像、文字设为角色名；点击切换到对应角色遗物库；不足两个角色时隐藏多余按钮。
    /// 替代原 TabBar 页签切换机制。
    /// </summary>
    private void SetupCharacterButtons()
    {
        // 若 Inspector 未绑定，则按名称在面板层级中查找（避免 prefab 未赋值被 Unity 重导入回滚的问题）。
        // 预制体中的按钮物体名为 Character1 / Character2。
        if (CharacterButton1 == null && panel != null)
            CharacterButton1 = panel.transform.Find("Character1")?.GetComponent<Button>();
        if (CharacterButton2 == null && panel != null)
            CharacterButton2 = panel.transform.Find("Character2")?.GetComponent<Button>();

        BindCharacterButton(CharacterButton1, 0);
        BindCharacterButton(CharacterButton2, 1);
    }

    private void BindCharacterButton(Button btn, int index)
    {
        if (btn == null) return;

        CacheCharacterButtonColors(btn);

        bool hasChar = index < _registeredCharacters.Count;
        btn.gameObject.SetActive(hasChar);   // 角色不足两个时隐藏多余按钮
        if (!hasChar) return;

        btn.interactable = true;   // 保险：prefab 可能被序列化为 false 导致点击无反应

        var ch = _registeredCharacters[index];

        // 按钮图片 = 角色头像（CharacterData.avatar）：优先自身 Image，否则子物体 Image
        var btnImg = btn.GetComponent<Image>() ?? btn.GetComponentInChildren<Image>();
        if (btnImg != null && ch.avatar != null)
        {
            btnImg.sprite = ch.avatar;
            btnImg.preserveAspect = true;
        }

        // 按钮文字 = 角色名字
        var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.SetText(ch.displayName);
            txt.fontSize = 18;
        }

        // 清掉旧监听再绑定，避免重复打开时堆叠
        btn.onClick.RemoveAllListeners();
        int captured = index;
        btn.onClick.AddListener(() => SwitchToCharacter(captured));

        // 高亮当前选中角色
        UpdateCharacterButtonHighlight(_currentCharacterIndex);
    }

    private void UpdateCharacterButtonHighlight(int activeIndex)
    {
        UpdateCharacterButtonHighlight(CharacterButton1, 0, activeIndex);
        UpdateCharacterButtonHighlight(CharacterButton2, 1, activeIndex);
    }

    private void UpdateCharacterButtonHighlight(Button btn, int index, int activeIndex)
    {
        if (btn == null) return;

        CacheCharacterButtonColors(btn);
        bool active = (index == activeIndex);
        var colors = _characterButtonDefaultColors[btn];
        if (active)
        {
            // Button 的 Selected 状态会在点击列表或其它控件后丢失；
            // 将当前角色按钮的所有交互状态固定为选中颜色，使展示内容与高亮状态保持一致。
            colors.normalColor = colors.selectedColor;
            colors.highlightedColor = colors.selectedColor;
            colors.pressedColor = colors.selectedColor;
        }

        btn.colors = colors;
        if (btn.targetGraphic != null)
            btn.targetGraphic.color = colors.normalColor;

        var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt == null) return;

        txt.color = active ? new Color(0.95f, 0.85f, 0.45f) : new Color(0.65f, 0.6f, 0.55f);  // 亮黄 vs 灰
        txt.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
    }

    private void CacheCharacterButtonColors(Button btn)
    {
        if (btn != null && !_characterButtonDefaultColors.ContainsKey(btn))
            _characterButtonDefaultColors.Add(btn, btn.colors);
    }

    #endregion

    #region 背景交互屏蔽（与 CardLibraryPanelUI 同模式）

    private void DisableBackgroundInteractables()
    {
        _disabledBackground.Clear();
        var allSelectables = Selectable.allSelectables;
        foreach (var s in allSelectables)
        {
            if (s == null || !s.IsActive()) continue;
            // 跳过本面板内的所有可交互组件
            if (IsChildOf(s.transform, panel?.transform)) continue;
            if (s.interactable)
            {
                s.interactable = false;
                _disabledBackground.Add(s);
            }
        }
    }

    private void EnableBackgroundInteractables()
    {
        foreach (var s in _disabledBackground)
            if (s != null) s.interactable = true;
        _disabledBackground.Clear();
    }

    private static bool IsChildOf(Transform child, Transform parent)
    {
        if (parent == null || child == null) return false;
        var c = child;
        while (c != null)
        {
            if (c == parent) return true;
            c = c.parent;
        }
        return false;
    }

    #endregion

    #region 绑定 / 事件

    private void BindExistingUI()
    {
        if (closeButton != null)
        {
            closeButton.interactable = true;   // 保险：prefab 可能被序列化为 false 导致点击无反应
            closeButton.onClick.AddListener(Hide);
        }
    }

    #endregion
}
