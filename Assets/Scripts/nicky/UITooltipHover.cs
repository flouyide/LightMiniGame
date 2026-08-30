using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>说明框相对目标的摆放方向。</summary>
public enum UITooltipSide
{
    /// <summary>自动：右 → 左 → 上 → 下，取第一个放得下的方向。</summary>
    Auto,
    Right,
    Left,
    Top,
    Bottom,
}

/// <summary>
/// 通用 UI 悬停说明框触发器（视觉风格与 Buff 说明框一致）。
///
/// 用法：挂到任意 UI 对象（如融合魔方按钮）上，在 Inspector 里填标题/正文并调节字号与宽度；
/// 鼠标移入即在指定方向弹出说明框，移出关闭。
///
/// 摆放：默认出现在目标右侧；右侧放不下自动翻到左侧，再不行则上/下，
/// 全放不下时钳制进屏幕边界，保证内容始终完整可见。
///
/// 注意：目标上需要有可接收射线的 Graphic（Button / Image 的 Raycast Target 需开启），
/// 本组件会在 Awake 时自动开启自身或子级 Image 的 Raycast Target。
/// </summary>
[AddComponentMenu("UI/UI Tooltip Hover")]
public class UITooltipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("文案")]
    [TextArea(1, 3)]
    public string title = "标题";
    [TextArea(2, 8)]
    public string body = "说明内容";

    [Header("尺寸与字号")]
    [Tooltip("文本期望宽度，决定说明框的宽（实际框宽 ≈ 该值 + 左右内边距）")]
    public float preferredWidth = 300f;
    public float titleFontSize = 22f;
    public float bodyFontSize = 18f;
    public int padding = 14;
    public float spacing = 6f;

    [Header("摆放")]
    [Tooltip("说明框出现的方向；放不下会自动翻转/钳制")]
    public UITooltipSide side = UITooltipSide.Right;
    [Tooltip("说明框与目标之间的间距")]
    public float gap = 10f;
    [Tooltip("距屏幕边缘的最小留白")]
    public float screenMargin = 8f;

    [Header("外观")]
    public Color backgroundColor = new Color(0.08f, 0.07f, 0.12f, 0.94f);
    public Color bodyTextColor = new Color(0.88f, 0.86f, 0.9f, 1f);

    private void Awake() => EnsureRaycastable();

    /// <summary>确保目标能接收鼠标射线（否则 OnPointerEnter 不会触发）。</summary>
    private void EnsureRaycastable()
    {
        var img = GetComponent<Image>();
        if (img == null) img = GetComponentInChildren<Image>(true);
        if (img != null) img.raycastTarget = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
        => UITooltipOverlay.Show(transform as RectTransform, this);

    public void OnPointerExit(PointerEventData eventData)
        => UITooltipOverlay.Hide(transform as RectTransform);

    private void OnDisable() => UITooltipOverlay.Hide(transform as RectTransform);
    private void OnDestroy() => UITooltipOverlay.Hide(transform as RectTransform);
}

/// <summary>全屏画布上唯一的通用 UI 说明框，始终画在最上层。</summary>
public static class UITooltipOverlay
{
    private static RectTransform _root;
    private static TextMeshProUGUI _title;
    private static TextMeshProUGUI _body;
    private static VerticalLayoutGroup _layout;
    private static Image _bg;
    private static RectTransform _shownFor;

    public static void Show(RectTransform target, UITooltipHover cfg)
    {
        if (target == null || cfg == null) return;
        Ensure();
        if (_root == null) return;

        _shownFor = target;
        _title.text = cfg.title ?? "";
        _body.text = cfg.body ?? "";
        _title.gameObject.SetActive(!string.IsNullOrEmpty(cfg.title));
        _body.gameObject.SetActive(!string.IsNullOrEmpty(cfg.body));

        // 字体：优先取目标自身的 TMP 字体，回退到场景里任意 TMP 文本
        TMP_FontAsset font = null;
        var sampleTmp = target.GetComponentInChildren<TextMeshProUGUI>(true);
        if (sampleTmp != null) font = sampleTmp.font;
        if (font == null)
        {
            var anyTmp = Object.FindObjectOfType<TextMeshProUGUI>();
            if (anyTmp != null) font = anyTmp.font;
        }
        if (font != null)
        {
            _title.font = font;
            _body.font = font;
        }

        // 每次弹出都套用配置：运行模式下改数值即时生效
        _title.fontSize = cfg.titleFontSize;
        _body.fontSize = cfg.bodyFontSize;
        _body.color = cfg.bodyTextColor;

        SetPreferredWidth(_title, cfg.preferredWidth);
        SetPreferredWidth(_body, cfg.preferredWidth);

        _layout.padding = new RectOffset(cfg.padding, cfg.padding, cfg.padding, cfg.padding);
        _layout.spacing = cfg.spacing;
        _bg.color = cfg.backgroundColor;

        _root.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

        var canvas = target.GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas == null) return;
        var canvasRT = canvas.transform as RectTransform;
        _root.SetParent(canvasRT, false);
        _root.SetAsLastSibling();

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        // 目标四角转换到画布局部坐标（0=左下 1=左上 2=右上 3=右下）
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2[] p = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, sp, cam, out p[i])) return;
        }

        float iL = Mathf.Min(p[0].x, Mathf.Min(p[1].x, Mathf.Min(p[2].x, p[3].x)));
        float iR = Mathf.Max(p[0].x, Mathf.Max(p[1].x, Mathf.Max(p[2].x, p[3].x)));
        float iB = Mathf.Min(p[0].y, Mathf.Min(p[1].y, Mathf.Min(p[2].y, p[3].y)));
        float iT = Mathf.Max(p[0].y, Mathf.Max(p[1].y, Mathf.Max(p[2].y, p[3].y)));

        Rect cr = canvasRT.rect;
        Position(
            iL, iR, iB, iT,
            (iL + iR) * 0.5f, (iB + iT) * 0.5f,
            _root.rect.width, _root.rect.height,
            cfg.gap, cfg.side,
            cr.xMin + cfg.screenMargin, cr.xMax - cfg.screenMargin,
            cr.yMin + cfg.screenMargin, cr.yMax - cfg.screenMargin);
    }

    private static void SetPreferredWidth(TextMeshProUGUI label, float width)
    {
        if (label == null) return;
        var le = label.GetComponent<LayoutElement>();
        if (le != null) le.preferredWidth = width;
    }

    /// <summary>
    /// 摆放：按 首选方向 → 反向 → 上/下 的顺序取第一个完整放得下的位置；
    /// 都放不下则用首选方向并钳制进屏幕，保证内容完整可见。
    /// 统一用 pivot=(0,0) 摆放，框占据 [l, l+w] × [b, b+h]。
    /// </summary>
    private static void Position(float iL, float iR, float iB, float iT, float cx, float cy,
                                 float w, float h, float gap, UITooltipSide side,
                                 float minX, float maxX, float minY, float maxY)
    {
        UITooltipSide[] order;
        if (side == UITooltipSide.Auto)
        {
            order = new[] { UITooltipSide.Right, UITooltipSide.Left, UITooltipSide.Top, UITooltipSide.Bottom };
        }
        else
        {
            UITooltipSide opp = Opposite(side);
            UITooltipSide a = side == UITooltipSide.Top || side == UITooltipSide.Bottom ? UITooltipSide.Right : UITooltipSide.Top;
            UITooltipSide b = a == UITooltipSide.Right ? UITooltipSide.Left : UITooltipSide.Bottom;
            order = new[] { side, opp, a, b };
        }

        foreach (var s in order)
        {
            RectFor(s, iL, iR, iB, iT, cx, cy, w, h, gap, out float l, out float bb);
            if (l >= minX && l + w <= maxX && bb >= minY && bb + h <= maxY)
            {
                Apply(l, bb);
                return;
            }
        }

        // 都放不下：用首选方向并钳制
        RectFor(order[0], iL, iR, iB, iT, cx, cy, w, h, gap, out float fl, out float fb);
        float cl = (maxX - minX >= w) ? Mathf.Clamp(fl, minX, maxX - w) : (minX + maxX - w) * 0.5f;
        float cb = (maxY - minY >= h) ? Mathf.Clamp(fb, minY, maxY - h) : (minY + maxY - h) * 0.5f;
        Apply(cl, cb);
    }

    private static void RectFor(UITooltipSide s, float iL, float iR, float iB, float iT, float cx, float cy,
                                float w, float h, float gap, out float l, out float b)
    {
        switch (s)
        {
            case UITooltipSide.Right:
                l = iR + gap;
                b = cy - h * 0.5f;
                break;
            case UITooltipSide.Left:
                l = iL - gap - w;
                b = cy - h * 0.5f;
                break;
            case UITooltipSide.Top:
                l = cx - w * 0.5f;
                b = iT + gap;
                break;
            default: // Bottom
                l = cx - w * 0.5f;
                b = iB - gap - h;
                break;
        }
    }

    private static void Apply(float l, float b)
    {
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0f, 0f);   // 左下角为锚点
        _root.anchoredPosition = new Vector2(l, b);
    }

    private static UITooltipSide Opposite(UITooltipSide s)
    {
        switch (s)
        {
            case UITooltipSide.Right: return UITooltipSide.Left;
            case UITooltipSide.Left: return UITooltipSide.Right;
            case UITooltipSide.Top: return UITooltipSide.Bottom;
            default: return UITooltipSide.Top;
        }
    }

    public static void Hide(RectTransform target)
    {
        if (target != null && _shownFor != target) return;
        _shownFor = null;
        if (_root != null)
            _root.gameObject.SetActive(false);
    }

    private static void Ensure()
    {
        if (_root != null) return;

        var go = new GameObject("UITooltip", typeof(RectTransform), typeof(CanvasRenderer),
                                typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _root = go.GetComponent<RectTransform>();
        _root.sizeDelta = new Vector2(320f, 40f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.07f, 0.12f, 0.94f);
        bg.raycastTarget = false;
        _bg = bg;

        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 14, 14);
        vlg.spacing = 6f;
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
        var anyTmp = Object.FindObjectOfType<TextMeshProUGUI>();
        if (anyTmp != null) font = anyTmp.font;

        _title = CreateLabel(go.transform, "Title", 22f, FontStyles.Bold, font);
        _body = CreateLabel(go.transform, "Body", 18f, FontStyles.Normal, font);
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
        le.preferredWidth = 300f;
        return tmp;
    }
}

