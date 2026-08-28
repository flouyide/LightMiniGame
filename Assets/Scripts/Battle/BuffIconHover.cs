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
        if (!buff.hideStacks && buff.totalStacks != 0 && !string.IsNullOrEmpty(title))
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

/// <summary>全屏画布上唯一的 Buff 说明框，始终画在最上层。</summary>
public static class BuffTooltipOverlay
{
    private static RectTransform _root;
    private static TextMeshProUGUI _title;
    private static TextMeshProUGUI _body;
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
        Vector3[] corners = new Vector3[4];
        icon.GetWorldCorners(corners);
        Vector3 bottomCenter = (corners[0] + corners[3]) * 0.5f;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, bottomCenter);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screen, cam, out Vector2 local))
            return;

        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 1f);
        _root.anchoredPosition = local + new Vector2(0f, -6f);
    }

    public static void Hide(RectTransform icon)
    {
        if (icon != null && _shownFor != icon) return;
        _shownFor = null;
        if (_root != null)
            _root.gameObject.SetActive(false);
    }

    private static void Ensure()
    {
        if (_root != null) return;

        var go = new GameObject("BuffTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _root = go.GetComponent<RectTransform>();
        _root.sizeDelta = new Vector2(220f, 40f);

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

        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_FontAsset font = null;
        var sample = Object.FindObjectOfType<TextMeshProUGUI>();
        if (sample != null) font = sample.font;

        _title = CreateLabel(go.transform, "Title", 16, FontStyles.Bold, font);
        _body = CreateLabel(go.transform, "Body", 14, FontStyles.Normal, font);
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
        le.preferredWidth = 200f;
        return tmp;
    }
}
