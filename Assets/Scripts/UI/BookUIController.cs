using System.Collections.Generic;
using LightMiniGame.Card;
using LightMiniGame.Shop;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 书页界面总控
/// </summary>
public class BookUIController : MonoBehaviour
{
    [Header("系统引用")]
    [SerializeField] private ChapterManager chapterManager;

    [Header("卡片")]
    [SerializeField] private Transform cardContainer;
    [SerializeField] private PageCardUI cardPrefab;

    [Header("选项面板")]
    [SerializeField] private OptionPanelUI optionPanel;

    [Header("商店面板")]
    [SerializeField] private ShopPanelUI shopPanel;

    [Header("顶部栏")]
    [SerializeField] private TextMeshProUGUI chapterNameText;
    [SerializeField] private TextMeshProUGUI remainingPagesText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI sanText;       // 理智文本（显示玩家 Sanity）

    [Header("章节完成面板")]
    [SerializeField] private GameObject chapterCompletePanel;
    [SerializeField] private Button nextChapterButton;
    [SerializeField] private Button settingsButton;   // 设置按钮（场景中已命名为 SettingsButton），点击弹出选项面板

    [Header("选项界面")]
    [SerializeField] private GameObject settingsPanelPrefab;   // 选项界面预制体（SettingsPanel），在 Inspector 中配置；留空则回退到 Resources/UI/SettingsPanel

    [Header("牌库界面")]
    [SerializeField] private Button deckButton;               // 牌库按钮（BookCanvas 中的 DeckButton），点击打开牌库面板（CardLibraryPanel 已在场景中作为 BookCanvas 的子物体存在）
    [SerializeField] private GameObject cardLibraryPanel;     // 牌库面板 GameObject（CardLibraryPanel），在 Inspector 中直接拖入配置；留空则回退到 GetComponentInChildren 自动查找
    [Header("卡面预制体（按类型，来自 Battle/Cards）")]
    [SerializeField] private GameObject attackCardPrefab;    // 攻击牌
    [SerializeField] private GameObject armorCardPrefab;     // 护甲牌
    [SerializeField] private GameObject buffCardPrefab;      // 增益牌

    [Header("遗物清单界面")]
    [SerializeField] private Button relicButton;             // 遗物按钮（BookCanvas 中的 RelicButton），点击弹出遗物清单面板（RelicInventoryPanel）
    [SerializeField] private GameObject relicInventoryPanel; // 遗物清单面板 GameObject（RelicInventoryPanel），Inspector 直接拖入；留空则回退 GetComponentInChildren 自动查找

    [Header("角色头像")]
    [SerializeField] private GameConfig gameConfig;          // 角色数据源（含 2 个 CharacterData）；需在 Inspector 配置
    [SerializeField] private GameObject character1Icon;          // 角色1 头像载体（其下 Image 显示角色1头像）
    [SerializeField] private GameObject character2Icon;          // 角色2 头像载体（其下 Image 显示角色2头像）

    [Header("背景")]
    [SerializeField] private GameObject background;         // BookCanvas 下的 Background 物体，其下 Image 显示背景图（按 Sanity 切换 NormalBG / AbnormalBG）
    private Image _backgroundImage;                        // Background 下的 Image 组件缓存

    private SettingsPanelUI _settingsPanel;            // 选项面板（运行时按需创建）
    private CardLibraryPanelUI _cardLibraryPanel;      // 牌库面板（运行时按需创建）
    private RelicInventoryPanelUI _relicInventoryPanel; // 遗物清单面板（运行时按需创建）

    private readonly List<PageCardUI> _activeCards = new();
    private PageEventData _currentEventData;   // 当前事件面板正在显示的事件数据（供选项回调取 effects）

    private void OnEnable()
    {
        chapterManager.OnPagesRefreshed.AddListener(HandlePagesRefreshed);
        chapterManager.OnPageRefreshed.AddListener(HandlePageRefreshed);
        chapterManager.OnPageSelected.AddListener(HandlePageSelected);
        chapterManager.OnPageDeleted.AddListener(HandlePageDeleted);
        chapterManager.OnPageConsumed.AddListener(HandlePageConsumed);
        chapterManager.OnChapterComplete.AddListener(HandleChapterComplete);
        chapterManager.OnChapterInfoUpdated.AddListener(HandleChapterInfoUpdated);
        chapterManager.OnPlayerStatsUpdated.AddListener(HandlePlayerStatsUpdated);

        if (nextChapterButton != null)
            nextChapterButton.onClick.AddListener(OnNextChapterClicked);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
        if (deckButton != null)
            deckButton.onClick.AddListener(OnDeckClicked);
        if (relicButton != null)
            relicButton.onClick.AddListener(OnRelicButtonClicked);

        // 初始化商店控制器（绑定金币来源 ChapterManager）
        ShopManager.EnsureInstance()?.Init(chapterManager);

        // 游戏开始时，把 GameConfig 中前两个角色的头像应用到角色栏
        ApplyCharacterAvatars();

        // 缓存背景 Image 初始应用
        if (background != null)
            _backgroundImage = background.GetComponent<Image>() ?? background.GetComponentInChildren<Image>();
        UpdateBackground();
    }

    private void OnDisable()
    {
        chapterManager.OnPagesRefreshed.RemoveListener(HandlePagesRefreshed);
        chapterManager.OnPageRefreshed.RemoveListener(HandlePageRefreshed);
        chapterManager.OnPageSelected.RemoveListener(HandlePageSelected);
        chapterManager.OnPageDeleted.RemoveListener(HandlePageDeleted);
        chapterManager.OnPageConsumed.RemoveListener(HandlePageConsumed);
        chapterManager.OnChapterComplete.RemoveListener(HandleChapterComplete);
        chapterManager.OnChapterInfoUpdated.RemoveListener(HandleChapterInfoUpdated);
        chapterManager.OnPlayerStatsUpdated.RemoveListener(HandlePlayerStatsUpdated);

        if (nextChapterButton != null)
            nextChapterButton.onClick.RemoveListener(OnNextChapterClicked);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
        if (deckButton != null)
            deckButton.onClick.RemoveListener(OnDeckClicked);
        if (relicButton != null)
            relicButton.onClick.RemoveListener(OnRelicButtonClicked);
    }

    private void HandlePagesRefreshed(List<PageEventData> pages)
    {
        // 清除旧卡片
        foreach (var card in _activeCards)
        {
            if (card != null && card.gameObject != null)
                Destroy(card.gameObject);
        }
        _activeCards.Clear();

        // 实例化新卡片
        for (int i = 0; i < pages.Count; i++)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            card.Setup(pages[i], i, OnCardClicked, OnCardDeleted);
            _activeCards.Add(card);
        }
    }

    private void HandlePageRefreshed(PageEventData newData, int refreshedIndex)
    {
        if (refreshedIndex < 0 || refreshedIndex >= _activeCards.Count)
        {
            Debug.LogWarning($"[BookUIController] 无效的刷新索引: {refreshedIndex}");
            return;
        }

        // 只刷新指定位置的卡片
        var card = _activeCards[refreshedIndex];
        if (card != null)
        {
            card.Setup(newData, refreshedIndex, OnCardClicked, OnCardDeleted);
            Debug.Log($"[BookUIController] 卡片 {refreshedIndex} 已刷新为新事件「{newData.displayName}」");
        }
    }

    private void HandlePageDeleted(int deletedIndex)
    {
        Debug.Log($"[BookUIController] 卡片 {deletedIndex} 已删除，等待刷新");
    }

    /// <summary>
    /// 剩余页数为 0 时选中卡片 → 删除该卡片，不刷新新卡片
    /// </summary>
    private void HandlePageConsumed(int consumedIndex)
    {
        if (consumedIndex < 0 || consumedIndex >= _activeCards.Count)
            return;

        var card = _activeCards[consumedIndex];
        if (card != null && card.gameObject != null)
            Destroy(card.gameObject);
        _activeCards.RemoveAt(consumedIndex);

        // 列表位置发生变化，重绑剩余卡片的索引，使其与 ChapterManager._currentPages 对齐。
        // 否则剩余卡片仍持有旧的 _index（例如 2），而 _currentPages 已被 RemoveAt 缩短，
        // 再次点击会因 index 越界被 SelectPage/DeletePage 拒绝。
        for (int i = 0; i < _activeCards.Count; i++)
        {
            _activeCards[i].SetIndex(i);
        }

        Debug.Log($"[BookUIController] 卡片 {consumedIndex} 已消耗（剩余页数为0），剩余 {_activeCards.Count} 张");
    }

    public void OnCardClicked(int index)
    {
        chapterManager.SelectPage(index);
    }

    public void OnCardDeleted(int index)
    {
        chapterManager.DeletePage(index);
    }

    private void HandlePageSelected(PageEventData data, int selectedIndex)
    {
        if (data.eventType == PageEventType.Event && optionPanel != null)
        {
            // Event 类型：弹出选项面板让玩家选择
            _currentEventData = data;
            optionPanel.Show(data, OnOptionPanelResolved);
        }
        else if (data.eventType == PageEventType.Shop && shopPanel != null)
        {
            // Shop 类型：弹出商店面板（不应用 effects），进店时重新随机库存
            var mgr = ShopManager.EnsureInstance();
            if (mgr != null) mgr.Init(chapterManager);
            shopPanel.Show(mgr, gameConfig != null ? gameConfig.characters : null, OnShopClosed);
        }
        else
        {
            // Battle/Rest 类型：直接应用 defaultEffects，不弹面板
            chapterManager.ApplyEffects(data.defaultEffects);
            chapterManager.OnOptionResolved();
        }
    }

    /// <summary>
    /// 商店关闭回调：刷新 3 个 PageCard（买卖功能待实现）
    /// </summary>
    private void OnShopClosed()
    {
        chapterManager.OnOptionResolved();
    }

    /// <summary>
    /// 事件面板关闭后的回调：应用选中选项的 effects，然后继续章节推进
    /// </summary>
    private void OnOptionPanelResolved(int optionIndex)
    {
        if (_currentEventData != null
            && optionIndex >= 0
            && optionIndex < _currentEventData.options.Count)
        {
            chapterManager.ApplyEffects(_currentEventData.options[optionIndex].effects);
        }
        _currentEventData = null;
        chapterManager.OnOptionResolved();
    }

    private void HandleChapterComplete()
    {
        if (chapterCompletePanel != null)
            chapterCompletePanel.SetActive(true);
    }

    private void OnNextChapterClicked()
    {
        if (chapterCompletePanel != null)
            chapterCompletePanel.SetActive(false);
        chapterManager.NextChapter();
    }

    /// <summary>
    /// 设置按钮点击回调：首次点击时实例化选项界面预制体（settingsPanelPrefab，可在 Inspector 配置），
    /// 之后复用同一实例。面板打开时会暂停游戏，关闭时恢复（见 SettingsPanelUI）。
    /// 若 settingsPanelPrefab 未配置，则回退到 Resources/UI/SettingsPanel 并打印提示。
    /// </summary>
    private void OnSettingsClicked()
    {
        if (_settingsPanel == null)
        {
            // 优先使用 Inspector 中配置的预制体；未配置则回退到 Resources
            GameObject prefab = settingsPanelPrefab;
            if (prefab == null)
            {
                Debug.LogError("[BookUIController] 选项界面预制体未配置");
                return;
            }

            var go = Instantiate(prefab, transform, false);
            go.name = "SettingsPanel";
            _settingsPanel = go.GetComponent<SettingsPanelUI>();
            if (_settingsPanel != null)
                _settingsPanel.Init(chapterManager);
        }
        _settingsPanel.Show();
    }

    /// <summary>
    /// 牌库按钮点击回调：CardLibraryPanel 已存在于场景中（作为 BookCanvas 的子物体），
    /// 首次点击时查找并缓存该实例，注入三张 Battle 卡面预制体（攻击牌/护甲牌/增益牌）。
    /// 之后点击只调用 Show()：启用牌库面板（GameObject.SetActive(true)）、暂停游戏、屏蔽背景交互。
    /// 关闭由面板自身关闭按钮触发 Hide()：停用牌库面板（GameObject.SetActive(false)）、恢复游戏。
    /// </summary>
    private void OnDeckClicked()
    {
        if (_cardLibraryPanel == null)
        {
            // 优先使用 Inspector 中配置的 cardLibraryPanel；未配置则回退到子物体自动查找
            if (cardLibraryPanel != null)
                _cardLibraryPanel = cardLibraryPanel.GetComponent<CardLibraryPanelUI>();
            if (_cardLibraryPanel == null)
                _cardLibraryPanel = GetComponentInChildren<CardLibraryPanelUI>(true);
            if (_cardLibraryPanel == null)
            {
                Debug.LogError("[BookUIController] 未找到 CardLibraryPanelUI（请在 Inspector 配置 cardLibraryPanel，或确保其作为 BookCanvas 的子物体存在）");
                return;
            }

            // 注入三张卡面预制体（按类型），需在 Inspector 中配置
            _cardLibraryPanel.attackCardPrefab = attackCardPrefab;
            _cardLibraryPanel.armorCardPrefab  = armorCardPrefab;
            _cardLibraryPanel.buffCardPrefab   = buffCardPrefab;

            _cardLibraryPanel.Init();
        }
        _cardLibraryPanel.Show();
    }

    /// <summary>
    /// 遗物按钮点击回调：RelicInventoryPanel 已存在于场景中（作为 BookCanvas 的子物体），
    /// 首次点击时查找并缓存该实例。之后点击只调用 Show()：启用遗物清单面板、暂停游戏、屏蔽背景交互。
    /// 关闭由面板自身关闭按钮触发 Hide()：停用面板、恢复游戏。逻辑与 OnDeckClicked 一致。
    /// </summary>
    private void OnRelicButtonClicked()
    {
        if (_relicInventoryPanel == null)
        {
            // 优先使用 Inspector 中配置的 relicInventoryPanel；未配置则回退到子物体自动查找
            if (relicInventoryPanel != null)
                _relicInventoryPanel = relicInventoryPanel.GetComponent<RelicInventoryPanelUI>();
            if (_relicInventoryPanel == null)
                _relicInventoryPanel = GetComponentInChildren<RelicInventoryPanelUI>(true);
            if (_relicInventoryPanel == null)
            {
                Debug.LogError("[BookUIController] 未找到 RelicInventoryPanelUI（请在 Inspector 配置 relicInventoryPanel，或确保其作为 BookCanvas 的子物体存在）");
                return;
            }
        }
        _relicInventoryPanel.Show();
    }

    /// <summary>
    /// 游戏开始时从 GameConfig.characters 读取前两个角色，把各自的头像应用到
    /// character1 / character2 物体下的 Image 组件，并把 displayName 应用到其下的 TMP 文本
    /// （头像优先自身 Image、否则取子物体 Image；角色名优先自身 TMP、否则取子物体 TMP）。
    /// </summary>
    private void ApplyCharacterAvatars()
    {
        if (gameConfig == null || gameConfig.characters == null) return;
        var chars = gameConfig.characters;
        ApplyCharacterTo(character1Icon, chars.Count > 0 ? chars[0] : null);
        ApplyCharacterTo(character2Icon, chars.Count > 1 ? chars[1] : null);
    }

    private void ApplyCharacterTo(GameObject go, CharacterData data)
    {
        if (go == null || data == null) return;

        // 头像：优先自身 Image，否则取子物体 Image
        var img = go.GetComponent<Image>() ?? go.GetComponentInChildren<Image>();
        if (img != null && data.avatar != null)
            img.sprite = data.avatar;

        // 角色名：优先自身 TMP，否则取子物体 TMP
        var tmp = go.GetComponent<TextMeshProUGUI>() ?? go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
            tmp.text = data.displayName;
    }

    private void HandleChapterInfoUpdated(string name, int remaining)
    {
        if (chapterNameText != null)
            chapterNameText.text = name;
        if (remainingPagesText != null)
            remainingPagesText.text = $"剩余页数: {remaining}";
        // 章节切换 → 背景图与阈值可能变化
        UpdateBackground();
    }

    private void HandlePlayerStatsUpdated(int hp, int gold, int sanity)
    {
        if (hpText != null)
            hpText.text = $"HP: {hp}";
        if (goldText != null)
            goldText.text = $"金币: {gold}";
        if (sanText != null)
            sanText.text = $"理智: {sanity}";
        // 玩家属性变化（含 Sanity）→ 背景图可能需切换
        UpdateBackground();
    }

    /// <summary>
    /// 依据当前章节配置与玩家 Sanity 切换 Background 的图片：
    /// Sanity &gt;= sanityThreshold（阈值）→ NormalBG；Sanity &lt; threshold → AbnormalBG。
    /// 若未配置对应 Sprite 或 Background 缺失则跳过。
    /// </summary>
    private void UpdateBackground()
    {
        if (_backgroundImage == null || chapterManager == null) return;
        var chapter = chapterManager.CurrentChapter;
        if (chapter == null) return;

        Sprite target = chapterManager.PlayerSanity >= chapter.sanityThreshold
            ? chapter.NormalBG
            : chapter.AbnormalBG;

        if (target != null)
            _backgroundImage.sprite = target;
    }
}
