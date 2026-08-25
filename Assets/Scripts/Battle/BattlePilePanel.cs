using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LightMiniGame.Card;

/// <summary>
/// 战斗中抽牌堆 / 弃牌堆：替换为局内美术图标，点击后弹出只读卡面列表。
/// 抽牌堆图标随当前角色切换文件夹颜色；弃牌堆用回收桶图。
/// </summary>
public class BattlePilePanel : MonoBehaviour
{
    private const string ArtDir = "Assets/Art/局内/";
    private const string PriestFolderPath = ArtDir + "牧师文件夹.png";
    private const string GunnerFolderPath = ArtDir + "枪械师文件夹.png";
    private const string ScavengerFolderPath = ArtDir + "拾荒者文件夹.png";
    private const string DiscardBinPath = ArtDir + "弃牌堆.png";
    private const float IconSize = 88f;

    private BattleManager _battle;
    private GameObject _attackPrefab;
    private GameObject _skillPrefab;
    private GameObject _abilityPrefab;

    private Image _drawIconImage;
    private GameObject _panelRoot;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _emptyText;
    private Transform _gridContent;
    private readonly List<GameObject> _cardViews = new();
    private TMP_FontAsset _font;
    private float _savedTimeScale = 1f;
    private bool _bound;

    private static Sprite _priestFolder;
    private static Sprite _gunnerFolder;
    private static Sprite _scavengerFolder;
    private static Sprite _discardBin;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    public void Bind(BattleManager battle, GameObject attackPrefab, GameObject skillPrefab, GameObject abilityPrefab)
    {
        _battle = battle;
        _attackPrefab = attackPrefab;
        _skillPrefab = skillPrefab;
        _abilityPrefab = abilityPrefab;
        if (_bound) return;
        _bound = true;
        CacheFont();
        WirePileIcons();
        BuildPanel();
        RefreshDrawIcon();
    }

    public void RefreshDrawIcon()
    {
        if (_drawIconImage == null) return;
        _drawIconImage.sprite = FolderSpriteFor(_battle != null ? _battle.ActiveCharacterData : null);
        _drawIconImage.color = Color.white;
        _drawIconImage.preserveAspect = true;
    }

    private void OnDestroy()
    {
        if (_panelRoot != null) Destroy(_panelRoot);
    }

    // ========================================================================
    // 图标
    // ========================================================================

    private void WirePileIcons()
    {
        var canvas = FindBattleCanvas();
        if (canvas == null) return;

        var drawIcon = FindNamed(canvas.transform, "DrawPileIcon");
        var discardIcon = FindNamed(canvas.transform, "DiscardPileIcon");

        if (drawIcon != null)
        {
            HideDecorations(drawIcon);
            _drawIconImage = EnsureIconImage(drawIcon, FolderSpriteFor(_battle != null ? _battle.ActiveCharacterData : null));
            WireClick(drawIcon, OnDrawPileClicked);
        }

        if (discardIcon != null)
        {
            HideDecorations(discardIcon);
            EnsureIconImage(discardIcon, LoadSprite(ref _discardBin, DiscardBinPath));
            WireClick(discardIcon, OnDiscardPileClicked);
        }
    }

    private static void HideDecorations(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            string n = child.name;
            if (n.Contains("Label") || n.StartsWith("Card"))
                child.gameObject.SetActive(false);
        }
    }

    private static Image EnsureIconImage(Transform root, Sprite sprite)
    {
        var rt = root as RectTransform;
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(IconSize, IconSize);
        }

        Image img = null;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (!child.gameObject.activeSelf) continue;
            img = child.GetComponent<Image>();
            if (img != null) break;
        }
        if (img == null) img = root.GetComponent<Image>();
        if (img == null)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root, false);
            var childRT = go.GetComponent<RectTransform>();
            childRT.anchorMin = Vector2.zero;
            childRT.anchorMax = Vector2.one;
            childRT.offsetMin = Vector2.zero;
            childRT.offsetMax = Vector2.zero;
            img = go.GetComponent<Image>();
        }
        else
        {
            var childRT = img.transform as RectTransform;
            if (childRT != null && childRT != rt)
            {
                childRT.anchorMin = Vector2.zero;
                childRT.anchorMax = Vector2.one;
                childRT.offsetMin = Vector2.zero;
                childRT.offsetMax = Vector2.zero;
                childRT.localScale = Vector3.one;
            }
        }

        img.sprite = sprite;
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = true;
        img.maskable = false;
        return img;
    }

    private static void WireClick(Transform root, UnityEngine.Events.UnityAction handler)
    {
        var graphic = root.GetComponentInChildren<Image>(false);
        var btn = root.GetComponent<Button>() ?? root.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        if (graphic != null) btn.targetGraphic = graphic;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(handler);
    }

    private void OnDrawPileClicked()
    {
        if (_battle == null || _battle.IsBattleEnded) return;
        var cards = _battle.GetActiveDrawPile();
        var shuffled = ShuffleCopy(cards);
        string name = _battle.ActiveCharacterData != null ? _battle.ActiveCharacterData.Label : "";
        string title = string.IsNullOrEmpty(name) ? "抽牌堆" : $"抽牌堆（{name}）";
        Show(title, shuffled, cards.Count == 0 ? "抽牌堆是空的" : null);
    }

    private void OnDiscardPileClicked()
    {
        if (_battle == null || _battle.IsBattleEnded) return;
        var cards = _battle.GetActiveDiscardPile();
        Show("弃牌堆", cards, cards.Count == 0 ? "弃牌堆是空的" : null);
    }

    // ========================================================================
    // 面板
    // ========================================================================

    private void BuildPanel()
    {
        var canvas = FindBattleCanvas();
        if (canvas == null) return;

        _panelRoot = new GameObject("BattlePilePanel");
        _panelRoot.transform.SetParent(canvas.transform, false);
        var rootRT = _panelRoot.AddComponent<RectTransform>();
        Stretch(rootRT);

        var overlay = _panelRoot.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.72f);
        overlay.raycastTarget = true;
        var overlayBtn = _panelRoot.AddComponent<Button>();
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.targetGraphic = overlay;
        overlayBtn.onClick.AddListener(Hide);

        var window = CreateUI("Window", _panelRoot.transform);
        var windowRT = window.GetComponent<RectTransform>();
        windowRT.anchorMin = new Vector2(0.08f, 0.12f);
        windowRT.anchorMax = new Vector2(0.92f, 0.88f);
        windowRT.offsetMin = Vector2.zero;
        windowRT.offsetMax = Vector2.zero;
        var windowImg = window.AddComponent<Image>();
        windowImg.color = new Color(0.10f, 0.08f, 0.14f, 0.96f);
        windowImg.raycastTarget = true;

        var titleGO = CreateUI("Title", window.transform);
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.04f, 0.88f);
        titleRT.anchorMax = new Vector2(0.8f, 0.98f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;
        _titleText = titleGO.AddComponent<TextMeshProUGUI>();
        ApplyFont(_titleText);
        _titleText.fontSize = 28;
        _titleText.fontStyle = FontStyles.Bold;
        _titleText.alignment = TextAlignmentOptions.MidlineLeft;
        _titleText.color = Color.white;
        _titleText.raycastTarget = false;

        var closeGO = CreateUI("Close", window.transform);
        var closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(0.86f, 0.88f);
        closeRT.anchorMax = new Vector2(0.98f, 0.98f);
        closeRT.offsetMin = Vector2.zero;
        closeRT.offsetMax = Vector2.zero;
        var closeImg = closeGO.AddComponent<Image>();
        closeImg.color = new Color(0.35f, 0.18f, 0.22f, 1f);
        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(Hide);
        var closeLabel = CreateUI("Label", closeGO.transform);
        Stretch(closeLabel.GetComponent<RectTransform>());
        var closeTxt = closeLabel.AddComponent<TextMeshProUGUI>();
        ApplyFont(closeTxt);
        closeTxt.text = "关闭";
        closeTxt.fontSize = 22;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.color = Color.white;
        closeTxt.raycastTarget = false;

        var scrollGO = CreateUI("Scroll", window.transform);
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.03f, 0.04f);
        scrollRT.anchorMax = new Vector2(0.97f, 0.86f);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;
        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var viewport = CreateUI("Viewport", scrollGO.transform);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<Image>().color = new Color(1, 1, 1, 0.02f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = viewport.GetComponent<RectTransform>();

        var content = CreateUI("Content", viewport.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, 0f);
        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(180f, 260f);
        grid.spacing = new Vector2(16f, 16f);
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.UpperLeft;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRT;
        _gridContent = content.transform;

        var emptyGO = CreateUI("Empty", window.transform);
        Stretch(emptyGO.GetComponent<RectTransform>());
        _emptyText = emptyGO.AddComponent<TextMeshProUGUI>();
        ApplyFont(_emptyText);
        _emptyText.fontSize = 24;
        _emptyText.alignment = TextAlignmentOptions.Center;
        _emptyText.color = new Color(0.75f, 0.72f, 0.8f, 1f);
        _emptyText.raycastTarget = false;

        _panelRoot.SetActive(false);
    }

    private void Show(string title, IReadOnlyList<CardData> cards, string emptyHint)
    {
        if (_panelRoot == null) BuildPanel();
        if (_panelRoot == null) return;

        _titleText.text = title;
        ClearCards();

        bool empty = cards == null || cards.Count == 0;
        if (_emptyText != null)
        {
            _emptyText.gameObject.SetActive(empty);
            _emptyText.text = emptyHint ?? "空";
        }

        if (!empty)
        {
            foreach (var card in cards)
                SpawnCard(card);
        }

        if (!_panelRoot.activeSelf)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        _panelRoot.SetActive(true);
        _panelRoot.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (_panelRoot == null || !_panelRoot.activeSelf) return;
        ClearCards();
        _panelRoot.SetActive(false);
        Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
    }

    private void SpawnCard(CardData card)
    {
        if (card == null || _gridContent == null) return;
        var prefab = PrefabFor(card.cardType);
        if (prefab == null) return;

        var go = Instantiate(prefab, _gridContent, false);
        go.SetActive(true);
        var hover = go.GetComponent<CardHoverEffect>();
        if (hover != null)
            hover.Setup(-1, null, null);
        var drag = go.GetComponent<CardDragHandler>();
        if (drag != null) drag.enabled = false;

        var display = go.GetComponent<CardDisplay>();
        if (display != null)
            display.ApplyCardData(card);

        _cardViews.Add(go);
    }

    private void ClearCards()
    {
        foreach (var go in _cardViews)
            if (go != null) Destroy(go);
        _cardViews.Clear();
    }

    private GameObject PrefabFor(CardType type)
    {
        return type switch
        {
            CardType.Skill => _skillPrefab != null ? _skillPrefab : _attackPrefab,
            CardType.Ability => _abilityPrefab != null ? _abilityPrefab : _attackPrefab,
            _ => _attackPrefab
        };
    }

    // ========================================================================
    // 资源 / 工具
    // ========================================================================

    private static Sprite FolderSpriteFor(CharacterData character)
    {
        string key = character == null
            ? ""
            : $"{character.characterId} {character.displayName} {character.name}";
        if (ContainsAny(key, "枪械", "gunner", "defect", "silent"))
            return LoadSprite(ref _gunnerFolder, GunnerFolderPath);
        if (ContainsAny(key, "拾荒", "scavenge", "watcher"))
            return LoadSprite(ref _scavengerFolder, ScavengerFolderPath);
        return LoadSprite(ref _priestFolder, PriestFolderPath);
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        if (string.IsNullOrEmpty(haystack)) return false;
        foreach (var n in needles)
            if (haystack.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }

    private static Sprite LoadSprite(ref Sprite cache, string path)
    {
        if (cache != null) return cache;
#if UNITY_EDITOR
        cache = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#endif
        return cache;
    }

    private static List<CardData> ShuffleCopy(IReadOnlyList<CardData> src)
    {
        var list = new List<CardData>();
        if (src != null)
        {
            for (int i = 0; i < src.Count; i++)
                list.Add(src[i]);
        }
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    private void CacheFont()
    {
        var tmp = FindObjectOfType<TextMeshProUGUI>();
        if (tmp != null) _font = tmp.font;
    }

    private void ApplyFont(TextMeshProUGUI tmp)
    {
        if (_font != null) tmp.font = _font;
    }

    private static GameObject CreateUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Transform FindNamed(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindNamed(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static Canvas FindBattleCanvas()
    {
        var canvases = FindObjectsOfType<Canvas>();
        foreach (var c in canvases)
        {
            if (c != null && c.name == "BattleCanvas" && c.gameObject.activeSelf)
                return c;
        }
        return Object.FindObjectOfType<Canvas>();
    }
}
