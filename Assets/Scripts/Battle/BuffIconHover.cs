using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 挂在单个 Buff 图标上：鼠标移入在图标正下方弹出说明，移出关闭。
/// </summary>
public class BuffIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private string _title;
    private string _body;

    public void Setup(string title, string body)
    {
        _title = title ?? "";
        _body = body ?? "";
        var img = GetComponent<Image>();
        if (img == null) img = GetComponentInChildren<Image>(true);
        if (img != null) img.raycastTarget = true;
    }

    public static void Bind(GameObject iconGo, DisplayedBuff buff, BattleManager battle)
    {
        if (iconGo == null) return;
        var hover = iconGo.GetComponent<BuffIconHover>();
        if (hover == null) hover = iconGo.AddComponent<BuffIconHover>();

        string title = buff.tooltipTitle;
        string body = buff.tooltipBody;
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
        {
            var data = battle != null ? battle.GetBuffData(buff.attributeType) : null;
            if (data != null)
            {
                title = data.GetDisplayName();
                body = data.GetDescription();
            }
            else
            {
                title = BuffData.DefaultName(buff.attributeType);
                body = BuffData.DefaultDescription(buff.attributeType);
            }
        }
        if (!buff.hideStacks && !string.IsNullOrEmpty(title))
            title = $"{title} {FormatStacks(buff.totalStacks)}";
        hover.Setup(title, body);
    }

    private static string FormatStacks(int stacks)
        => stacks > 0 ? $"+{stacks}" : stacks.ToString();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(_title) && string.IsNullOrEmpty(_body)) return;
        BuffTooltipOverlay.Show(transform as RectTransform, _title, _body);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        BuffTooltipOverlay.Hide(transform as RectTransform);
    }

    private void OnDisable()
    {
        BuffTooltipOverlay.Hide(transform as RectTransform);
    }
}

/// <summary>
/// Buff 说明框样式设置（可选）。
///
/// 用法：把本组件挂到场景中任意常驻 GameObject 上（如 BattleManager 或 UI 根节点），
/// 即可在 Inspector 里实时调节说明框的字号、宽度、内边距与贴边行为；不挂则使用下面这些默认值。
/// 每次弹出都会重新读取，运行模式下改动即时生效，无需重新编译。
/// </summary>
public class BuffTooltipSettings : MonoBehaviour
{
    [Header("字号")]
    [Tooltip("标题（Buff 名 + 层数）字号")]
    public float titleFontSize = 22f;
    [Tooltip("正文（描述）字号")]
    public float bodyFontSize = 18f;

    [Header("尺寸")]
    [Tooltip("文本期望宽度，决定说明框的宽（实际框宽 ≈ 该值 + 左右内边距）")]
    public float preferredWidth = 340f;

    [Header("内边距")]
    public int paddingLeft = 16;
    public int paddingRight = 16;
    public int paddingTop = 12;
    public int paddingBottom = 12;

    [Header("标题与正文的间距")]
    public float spacing = 6f;

    [Header("摆放")]
    [Tooltip("说明框与图标之间的间距")]
    public float gap = 6f;
    [Tooltip("说明框与屏幕边缘的最小留白，用于自动贴边/翻转判定")]
    public float screenMargin = 8f;

    public static BuffTooltipSettings Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

/// <summary>全屏画布上唯一的 Buff 说明框，始终画在最上层。</summary>
public static class BuffTooltipOverlay
{
    // 未挂 BuffTooltipSettings 时使用的兜底样式
    private const float DefaultTitleFontSize = 22f;
    private const float DefaultBodyFontSize = 18f;
    private const float DefaultPreferredWidth = 340f;
    private const int DefaultPaddingLR = 16;
    private const int DefaultPaddingTB = 12;
    private const float DefaultSpacing = 6f;
    private const float DefaultGap = 6f;
    private const float DefaultScreenMargin = 8f;

    private static RectTransform _root;
    private static TextMeshProUGUI _title;
    private static TextMeshProUGUI _body;
    private static VerticalLayoutGroup _layout;
    private static RectTransform _shownFor;

    public static void Show(RectTransform icon, string title, string body)
    {
        if (icon == null) return;
        Ensure();
        if (_root == null) return;

        _shownFor = icon;
        _title.text = title ?? "";
        _body.text = body ?? "";
        _title.gameObject.SetActive(!string.IsNullOrEmpty(title));
        _body.gameObject.SetActive(!string.IsNullOrEmpty(body));

        var sample = icon.GetComponentInChildren<TextMeshProUGUI>(true);
        if (sample != null && sample.font != null)
        {
            _title.font = sample.font;
            _body.font = sample.font;
        }

        // 每次弹出都重新套用样式：运行模式下改 BuffTooltipSettings 立刻生效。
        ApplyStyle();

        _root.gameObject.SetActive(true);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

        var canvas = icon.GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas == null) return;
        var canvasRT = canvas.transform as RectTransform;
        _root.SetParent(canvasRT, false);
        _root.SetAsLastSibling();

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        // 图标四角：0=左下 1=左上 2=右上 3=右下
        Vector3[] corners = new Vector3[4];
        icon.GetWorldCorners(corners);
        Vector3 bottomCenter = (corners[0] + corners[3]) * 0.5f;
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;

        Vector2 screenBottom = RectTransformUtility.WorldToScreenPoint(cam, bottomCenter);
        Vector2 screenTop = RectTransformUtility.WorldToScreenPoint(cam, topCenter);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenBottom, cam, out Vector2 localBottom)) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenTop, cam, out Vector2 localTop)) return;

        var s = BuffTooltipSettings.Instance;
        float gap = s != null ? s.gap : DefaultGap;
        float margin = s != null ? s.screenMargin : DefaultScreenMargin;

        PositionTooltip(canvasRT, localBottom, localTop, gap, margin);
    }

    /// <summary>
    /// 摆放说明框：水平居中于图标并自动贴左右边界；垂直优先放图标下方，
    /// 下方放不下则翻到图标上方；两边都放不下时贴到空间较大的一侧的边界，确保内容完整可见。
    /// </summary>
    private static void PositionTooltip(RectTransform canvasRT, Vector2 localBottom, Vector2 localTop, float gap, float margin)
    {
        // 布局已重建，此处 rect 为最终尺寸
        float w = _root.rect.width;
        float h = _root.rect.height;

        Rect canvasRect = canvasRT.rect;
        float minX = canvasRect.xMin + margin;
        float maxX = canvasRect.xMax - margin;
        float minY = canvasRect.yMin + margin;
        float maxY = canvasRect.yMax - margin;

        // ---- 水平：居中于图标，超出画布则贴边 ----
        float halfW = w * 0.5f;
        float posX;
        if (maxX - minX >= w)
            posX = Mathf.Clamp(localBottom.x, minX + halfW, maxX - halfW);
        else
            posX = (minX + maxX) * 0.5f;   // 比可用区域还宽：居中

        // ---- 垂直：优先下方，放不下翻上方，都放不下则贴边 ----
        float belowTop = localBottom.y - gap;   // 放下方时“框顶”所在位置（pivot.y=1）
        float aboveBottom = localTop.y + gap;   // 放上方时“框底”所在位置（pivot.y=0）
        float spaceBelow = belowTop - minY;
        float spaceAbove = maxY - aboveBottom;

        float pivotY, posY;
        if (h <= spaceBelow)
        {
            pivotY = 1f;                            // 框顶为锚点，向下展开
            posY = belowTop;
        }
        else if (h <= spaceAbove)
        {
            pivotY = 0f;                            // 框底为锚点，向上展开
            posY = aboveBottom;
        }
        else if (spaceBelow >= spaceAbove)
        {
            pivotY = 1f;
            posY = Mathf.Max(belowTop, minY + h);   // 下方空间更大：贴下边界，保证底部完整
        }
        else
        {
            pivotY = 0f;
            posY = Mathf.Min(aboveBottom, maxY - h); // 上方空间更大：贴上边界，保证顶部完整
        }

        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, pivotY);
        _root.anchoredPosition = new Vector2(posX, posY);
    }

    public static void Hide(RectTransform icon)
    {
        if (icon != null && _shownFor != icon) return;
        _shownFor = null;
        if (_root != null)
            _root.gameObject.SetActive(false);
    }

    /// <summary>把 BuffTooltipSettings（或兜底值）套用到说明框。</summary>
    private static void ApplyStyle()
    {
        var s = BuffTooltipSettings.Instance;

        float titleSize = s != null ? s.titleFontSize : DefaultTitleFontSize;
        float bodySize = s != null ? s.bodyFontSize : DefaultBodyFontSize;
        float width = s != null ? s.preferredWidth : DefaultPreferredWidth;
        int padL = s != null ? s.paddingLeft : DefaultPaddingLR;
        int padR = s != null ? s.paddingRight : DefaultPaddingLR;
        int padT = s != null ? s.paddingTop : DefaultPaddingTB;
        int padB = s != null ? s.paddingBottom : DefaultPaddingTB;
        float spacing = s != null ? s.spacing : DefaultSpacing;

        if (_title != null)
        {
            _title.fontSize = titleSize;
            var le = _title.GetComponent<LayoutElement>();
            if (le != null) le.preferredWidth = width;
        }

        if (_body != null)
        {
            _body.fontSize = bodySize;
            var le = _body.GetComponent<LayoutElement>();
            if (le != null) le.preferredWidth = width;
        }

        if (_layout != null)
        {
            _layout.padding = new RectOffset(padL, padR, padT, padB);
            _layout.spacing = spacing;
        }
    }

    private static void Ensure()
    {
        if (_root != null) return;

        var go = new GameObject("BuffTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _root = go.GetComponent<RectTransform>();
        _root.sizeDelta = new Vector2(360f, 40f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.07f, 0.12f, 0.94f);
        bg.raycastTarget = false;

        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 8, 8);
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        _layout = vlg;

        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_FontAsset font = null;
        var sample = Object.FindObjectOfType<TextMeshProUGUI>();
        if (sample != null) font = sample.font;

        _title = CreateLabel(go.transform, "Title", DefaultTitleFontSize, FontStyles.Bold, font);
        _body = CreateLabel(go.transform, "Body", DefaultBodyFontSize, FontStyles.Normal, font);
        _body.color = new Color(0.88f, 0.86f, 0.9f, 1f);

        go.SetActive(false);
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, float size, FontStyles style, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Top;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = DefaultPreferredWidth;
        return tmp;
    }
}
