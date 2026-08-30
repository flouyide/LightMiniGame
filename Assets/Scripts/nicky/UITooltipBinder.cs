using UnityEngine;

/// <summary>
/// 运行时绑定器：目标对象由代码在运行时生成（无法在编辑器里拖组件）时使用。
///
/// 用法：挂到场景任意常驻对象上，填 targetObjectName（Hierarchy 里看到的目标对象名字，
/// 如融合魔方按钮）与说明文案；运行后找到该对象就自动挂上 UITooltipHover 并套用这里的配置。
///
/// 配置是持续同步的：运行模式下直接改本组件上的文案/字号/宽度，
/// 鼠标重新划过目标即可看到效果，方便边看边调。
/// </summary>
[AddComponentMenu("UI/UI Tooltip Binder")]
public class UITooltipBinder : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("目标 GameObject 名称（在 Hierarchy 里查看，需完全一致；GameObject.Find 只找激活对象）")]
    public string targetObjectName = "";
    [Tooltip("找不到目标时是否持续重试（目标由其他脚本运行时创建）")]
    public bool retryUntilFound = true;
    [Tooltip("重试间隔（秒）")]
    public float retryInterval = 0.5f;

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

    private float _timer;
    private UITooltipHover _hover;

    private void Update()
    {
        if (string.IsNullOrEmpty(targetObjectName)) return;

        if (_hover == null)
        {
            // 尚未挂载，或目标已随场景切换销毁：按间隔查找
            if (!retryUntilFound) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = Mathf.Max(0.05f, retryInterval);

            var go = GameObject.Find(targetObjectName);
            if (go == null) return;

            _hover = go.GetComponent<UITooltipHover>();
            if (_hover == null) _hover = go.AddComponent<UITooltipHover>();
            Debug.Log($"[UITooltipBinder] 已为 '{go.name}' 挂载悬停说明框");
        }

        // 持续同步配置：运行模式下直接改 Binder 上的文案/字号/宽度即可即时看到效果
        ApplyConfig(_hover);
    }

    private void ApplyConfig(UITooltipHover hover)
    {
        if (hover == null) return;

        hover.title = title;
        hover.body = body;
        hover.preferredWidth = preferredWidth;
        hover.titleFontSize = titleFontSize;
        hover.bodyFontSize = bodyFontSize;
        hover.padding = padding;
        hover.spacing = spacing;
        hover.side = side;
        hover.gap = gap;
        hover.screenMargin = screenMargin;
        hover.backgroundColor = backgroundColor;
        hover.bodyTextColor = bodyTextColor;
    }
}
