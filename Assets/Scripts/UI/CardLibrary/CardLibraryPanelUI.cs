using System.Collections.Generic;
using LightMiniGame.Card;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 牌库界面主控面板（基于 CardLibraryPanel.prefab）。
/// 功能：
///   - Show/Hide 时暂停/恢复游戏，屏蔽背后所有交互（同 SettingsPanelUI 模式）。
///   - 顶部显示当前角色名称（左），关闭按钮（右）。
///   - Excel 页签式角色切换栏：每个注册角色一个页签，点击切换该角色的牌库。
///   - 网格滚动区列出当前角色所有卡；每张卡用对应的 Battle 卡面预制体（攻击牌/护甲牌/增益牌）渲染。
///   - 数据来源：GlobalCardLibrary.Instance（按角色隔离的牌库）。
///
/// 用法：
///   1. 在 Inspector 把 CardLibraryPanel.prefab 赋给 BookUIController.cardLibraryPanelPrefab。
///   2. 把三张 Battle 卡面预制体（攻击牌/护甲牌/增益牌）赋给 attackCardPrefab/armorCardPrefab/buffCardPrefab。
///   3. 点击 DeckButton → BookUIController 实例化预制体并调用 Show()。
/// </summary>
public class CardLibraryPanelUI : MonoBehaviour
{
    [Header("=== 面板根节点（来自 CardLibraryPanel.prefab）===")]
    [Tooltip("面板根物体（含 Canvas）。由预制体注入，不要运行时自建。")]
    public GameObject panel;
    [Tooltip("角色头像 Image（预制体未包含，留空则只显示名字）")]
    public Image characterAvatar;
    [Tooltip("角色名称 TextMeshProUGUI")]
    public TextMeshProUGUI characterNameText;
    [Tooltip("右上角关闭按钮")]
    public Button closeButton;
    [Tooltip("切换角色按钮")]
    public Button CharacterButton1;
    public Button CharacterButton2;
    [Tooltip("网格内容容器（GridLayoutGroup 的父节点）")]
    public Transform gridContent;

    [Header("=== 卡面预制体（按类型，来自 Battle/Cards）===")]
    [Tooltip("攻击牌预制体（需挂有 CardDisplay 组件）")]
    public GameObject attackCardPrefab;
    [Tooltip("护甲牌预制体")]
    public GameObject armorCardPrefab;
    [Tooltip("增益牌预制体")]
    public GameObject buffCardPrefab;

    [Header("=== 布局 ===")]
    [Tooltip("网格每行最多几张卡（覆盖 GridContent 的 constraintCount）")]
    [SerializeField] private int cardsPerRow = 5;

    // ===== 内部状态 =====
    private readonly List<CharacterData> _registeredCharacters = new List<CharacterData>();
    private int _currentCharacterIndex = -1;     // 当前选中角色的索引（_registeredCharacters 中）
    private readonly List<GameObject> _entryObjects = new List<GameObject>(); // 当前展示的卡面对象

    // 暂停 & 背景屏蔽（与 SettingsPanelUI 同模式）
    private readonly List<Selectable> _disabledBackground = new List<Selectable>();

    #region 生命周期

    private void Awake()
    {
        if (panel == null)
        {
            Debug.LogError("[CardLibraryPanel] 未绑定面板根节点（panel）。请在 CardLibraryPanel.prefab 中给 CardLibraryPanelUI 的 panel 字段赋值。");
            return;
        }
        
        // 兜底：强制根缩放为 1（prefab 的 RectTransform 在某些环境下被序列化为 0 导致整块不可见）
        /*if (panel != null)
        {
            var prt = panel.transform as RectTransform;
            if (prt != null) prt.localScale = Vector3.one;
        }*/
        FixOverlay();       // 修复全屏遮罩 Overlay 拦截点击的问题（已删则自动跳过）
        FixInteraction();   // 关闭背景/容器 raycast 穿透，并将按钮栏置顶，确保按钮可点
        BindExistingUI();
        HideImmediate();
    }

    /// <summary>初始化：应用布局参数、刷新角色列表与页签。首次 Show 前调用一次。</summary>
    public void Init()
    {
        if (gridContent != null)
        {
            var gl = gridContent.GetComponent<GridLayoutGroup>();
            if (gl != null) gl.constraintCount = cardsPerRow;
        }
        RefreshCharacterList();
    }

    #endregion

    #region 显示 / 隐藏

    /// <summary>打开牌库界面：激活面板、暂停游戏、屏蔽背景交互。</summary>
    public void Show()
    {
        if (panel == null) return;
        panel.SetActive(true);

        // 确保 Overlay 不会阻挡按钮点击
        /*var overlayTransform = panel.transform.Find("Overlay");
        if (overlayTransform != null)
        {
            var overlayImg = overlayTransform.GetComponent<UnityEngine.UI.Image>();
            if (overlayImg != null) overlayImg.raycastTarget = false;
        }*/

        Time.timeScale = 0f;
        DisableBackgroundInteractables();
        RefreshCharacterList();
        if (_registeredCharacters.Count > 0)
            SwitchToCharacter(_currentCharacterIndex >= 0 && _currentCharacterIndex < _registeredCharacters.Count ? _currentCharacterIndex : 0);
    }

    /// <summary>关闭牌库界面：隐藏面板、恢复游戏、恢复背景交互。</summary>
    public void Hide()
    {
        if (panel == null) return;
        Debug.Log("Close card library");
        HideImmediate();
    }

    private void HideImmediate()
    {
        if (panel != null) panel.SetActive(false);
        Time.timeScale = 1f;
        EnableBackgroundInteractables();
    }

    #endregion

    #region 角色切换（Excel 页签）

    /// <summary>从 GlobalCardLibrary 刷新已注册角色列表并重建页签。</summary>
    public void RefreshCharacterList()
    {
        var lib = GlobalCardLibrary.Instance;
        if (lib == null) { Debug.LogWarning("[CardLibraryPanel] GlobalCardLibrary.Instance 为空"); return; }

        _registeredCharacters.Clear();
        foreach (var clib in lib.AllLibraries)
            if (clib.owner != null && !_registeredCharacters.Contains(clib.owner))
                _registeredCharacters.Add(clib.owner);

        SetupCharacterButtons();
    }

    /// <summary>切换到指定索引的角色牌库（刷新网格内容）。</summary>
    public void SwitchToCharacter(int index)
    {
        if (index < 0 || index >= _registeredCharacters.Count) return;
        _currentCharacterIndex = index;
        UpdateHeader(_registeredCharacters[index]);
        UpdateCharacterButtonHighlight(index);
        RefreshGrid(_registeredCharacters[index]);
    }

    #endregion

    #region 网格内容（使用 Battle 卡牌预制体渲染牌面）

    /// <summary>刷新指定角色的卡牌网格：按类型实例化对应 Battle 卡面预制体，并写入有效值。</summary>
    private void RefreshGrid(CharacterData character)
    {
        var lib = GlobalCardLibrary.Instance?.GetLibrary(character);
        if (lib == null) return;

        var cards = lib.All;  // IReadOnlyList<CardInstance>

        // 清理旧卡面
        foreach (var go in _entryObjects)
            if (go != null) Destroy(go);
        _entryObjects.Clear();

        if (gridContent == null) return;

        foreach (var inst in cards)
        {
            var ct = inst.template != null ? inst.template.cardType : CardType.Attack;
            var prefab = PrefabForType(ct);
            if (prefab == null)
            {
                Debug.LogWarning($"[CardLibraryPanel] 缺少类型为 {ct} 的卡面预制体，已跳过一张卡");
                continue;
            }

            var go = Instantiate(prefab, gridContent, false);
            go.SetActive(true);
            ApplyCardData(go, inst);
            _entryObjects.Add(go);
        }

        // 强制刷新布局
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>按卡牌类型选择对应的 Battle 卡面预制体。</summary>
    private GameObject PrefabForType(CardType type)
    {
        switch (type)
        {
            case CardType.Armor: return armorCardPrefab;
            case CardType.Buff:  return buffCardPrefab;
            default:             return attackCardPrefab;
        }
    }

    /// <summary>
    /// 将一张 CardInstance 的有效值写入 Battle 卡面预制体的 CardDisplay 并刷新显示。
    /// 尊重覆盖层（改过的属性优先），模板缺失时降级为孤儿卡显示。
    /// </summary>
    private void ApplyCardData(GameObject cardGo, CardInstance inst)
    {
        var d = cardGo.GetComponent<CardDisplay>();
        if (d == null)
        {
            Debug.LogWarning("[CardLibraryPanel] 卡面预制体缺少 CardDisplay 组件，无法填充数据");
            return;
        }

        var tpl = inst.template;
        d.cardName = inst.EffectiveName;
        d.cardType = tpl != null ? tpl.cardType : CardType.Attack;
        d.cardArt  = tpl != null ? tpl.cardArt : null;
        d.value = inst.EffectiveValue;
        d.grade = inst.EffectiveGrade;
        d.actionPointCost = inst.EffectiveCost;
        d.consumeType = inst.EffectiveConsume;
        d.keywords = inst.EffectiveKeywords;
        d.attackCount = inst.EffectiveAttackCount;
        d.attackValueType = inst.EffectiveAttackValType;
        d.attackValue = inst.EffectiveAttackValue;
        d.attackAttribute = inst.EffectiveAttackAttr;
        d.ignoreArmor = inst.EffectiveIgnoreArmor;
        d.armorValueType = inst.EffectiveArmorValType;
        d.armorValue = inst.EffectiveArmorValue;
        d.armorAttribute = inst.EffectiveArmorAttr;
        d.buffDuration = inst.EffectiveBuffDuration;
        d.buffDurationTurns = inst.EffectiveBuffTurns;
        d.buffStacks = inst.EffectiveBuffStacks;
        d.buffEffects = inst.EffectiveBuffEffects != null
            ? new List<BuffEffect>(inst.EffectiveBuffEffects)
            : new List<BuffEffect>();
        d.description = inst.EffectiveDescription;

        d.UpdateDisplay();
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 修复 Overlay 全屏遮罩遮挡点击的问题：
    /// 预制体里的 Overlay 是一个铺满 Canvas 的半透明黑 Image（RaycastTarget=true），
    /// 本意是“点空白关闭”的模态背景，但它未绑定任何点击事件，且在某些情况下会渲染在按钮之上，
    /// 从而拦截所有点击（Character1/Character2/CloseButton 全部点不了）。
    /// 这里把它强制压到 Canvas 子节点的最底层（作为背景），并关闭其射线拦截，
    /// 既保留背景视觉，又绝不挡住面板内按钮的交互。
    /// </summary>
    private void FixOverlay()
    {
        if (panel == null) return;
        var overlay = panel.transform.Find("Overlay");
        if (overlay == null) return;

        overlay.SetAsFirstSibling();   // 成为 Canvas 下第一个子物体 = 最底层背景

        var img = overlay.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;   // 纯背景遮罩，未绑定点击事件，关闭拦截最稳妥
    }

    /// <summary>
    /// 防御性修复：确保面板内按钮一定可点（最常见的“看得见点不动”根因是
    /// 某个大尺寸容器/背景的 Image 在按钮之上拦截了射线）。
    /// 1) 关闭所有“非按钮”Image 的 raycastTarget（背景 MainPanel、Viewport 遮罩、Scroll 容器等），
    ///    只保留带 Button 组件的 Image 的射线检测，彻底消除任何遮挡。
    /// 2) 将装有按钮的 Header 提到 MainPanel 的最上层，确保渲染层级上不被全屏 Scroll 等覆盖。
    /// 注意：关闭 Image.raycastTarget 不影响显示（显示由 Image 组件渲染，与 raycast 无关），
    /// 也不影响 Mask 遮罩功能（遮罩由 Mask 组件负责，不依赖 raycastTarget）。
    /// </summary>
    private void FixInteraction()
    {
        if (panel == null) return;

        // 1) 仅保留带 Button 组件的 Image 的射线检测，其余全部关闭
        var images = panel.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            if (img.GetComponent<Button>() != null) continue;  // 按钮自身需要 raycast
            img.raycastTarget = false;
        }

        // 1b) 关闭所有文字（TMP / 传统 Text）的射线检测，避免文字压在按钮上拦截点击
        var tmpTexts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in tmpTexts) t.raycastTarget = false;
        var uiTexts = panel.GetComponentsInChildren<Text>(true);
        foreach (var t in uiTexts) t.raycastTarget = false;

        // 2) 把 Header（含 CharacterButton1/2、CloseButton）提到 MainPanel 的最上层
        var header = panel.transform.Find("Header");
        if (header != null) header.SetAsLastSibling();
    }

    private void UpdateHeader(CharacterData character)
    {
        if (characterNameText != null)
            characterNameText.SetText(character != null ? character.displayName : "未选择");
        // TODO: 若 CharacterData 之后加了 avatar 字段，在此赋值 characterAvatar.sprite
    }

    /// <summary>
    /// 绑定 CharacterButton1 / CharacterButton2 到前两个注册角色：
    /// 按钮文字设为角色名字；点击切换到对应角色牌库；不足两个角色时隐藏多余按钮。
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

        var ch = _registeredCharacters[index];

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

    #region 背景交互屏蔽（与 SettingsPanelUI 同模式）

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

    /// <summary>Inspector 已绑定字段时，补齐缺失引用并绑定事件；隐藏不再使用的 TabBar。</summary>
    private void BindExistingUI()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    #endregion
}
