using System.Collections.Generic;
using LightMiniGame.Card;
using LightMiniGame.Shop;
using UnityEngine;
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

    [Header("=== 单条遗物条目布局 ===")]
    [Tooltip("条目最小高度（垂直布局中每行的基础高度）")]
    [SerializeField] private float itemMinHeight = 76f;

    // ===== 内部状态 =====
    private readonly List<CharacterData> _registeredCharacters = new List<CharacterData>();
    private int _currentCharacterIndex = -1;     // 当前选中角色的索引（_registeredCharacters 中）
    private readonly List<GameObject> _entryObjects = new List<GameObject>();

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

        foreach (var relic in relics)
            AddRelicItem(relic);

        Canvas.ForceUpdateCanvases();
    }

    private void AddRelicItem(RelicData relic)
    {
        if (relic == null) return;

        // 条目根：竖向布局 = [图标] + [名称+品级行] + [描述]
        var row = new GameObject("RelicItem", typeof(RectTransform), typeof(VerticalLayoutGroup));
        row.transform.SetParent(content, false);
        var rt = row.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 0);
        var vlg = row.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        // 行最小高度（在非网格布局容器下也能占位）
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = itemMinHeight;

        // 图标（来自 RelicData.icon）
        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(row.transform, false);
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(80, 80);
        var iconImg = iconGO.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        if (relic.icon != null)
        {
            iconImg.sprite = relic.icon;
            iconImg.color = Color.white;
        }
        else
        {
            // 占位底色：未配置 icon 时仍可见一个空槽
            iconImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        }
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.minWidth = 80;
        iconLE.minHeight = 80;
        iconLE.preferredWidth = 80;
        iconLE.preferredHeight = 80;

        // 名称 + 品级（横向一行，居中）
        var nameRow = new GameObject("NameRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        nameRow.transform.SetParent(row.transform, false);
        var hlg = nameRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        AddTmpText(nameRow.transform, relic.relicName, 16, FontStyles.Bold, new Color(0.95f, 0.85f, 0.45f), TextAlignmentOptions.MidlineLeft, true, false);
        AddTmpText(nameRow.transform, CardData.GetGradeName(relic.grade), 12, FontStyles.Normal, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.MidlineLeft, true, false);

        // 描述（TMP，自动撑高）
        AddTmpText(row.transform, string.IsNullOrEmpty(relic.description) ? "（无描述）" : relic.description,
            12, FontStyles.Normal, new Color(0.85f, 0.85f, 0.85f), TextAlignmentOptions.TopLeft, false, true);

        _entryObjects.Add(row);
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
        var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt == null) return;

        bool active = (index == activeIndex);
        txt.color = active ? new Color(0.95f, 0.85f, 0.45f) : new Color(0.65f, 0.6f, 0.55f);  // 亮黄 vs 灰
        txt.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
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
