using UnityEditor;
using UnityEngine;

public static class EnemyEditorStyles
{
    private static Color _bgPanel, _cardBg, _cardBgHover, _cardBgSelected;
    private static Color _textPrimary, _textSecondary, _categoryActive;

    public static Color DifficultyColor(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Weak:   return new Color(0.54f, 0.54f, 0.54f);
            case Difficulty.Strong: return new Color(0.35f, 0.60f, 0.35f);
            case Difficulty.Elite:  return new Color(0.78f, 0.48f, 1.00f);
            case Difficulty.Boss:   return new Color(1.00f, 0.42f, 0.23f);
        }
        return Color.gray;
    }

    public static GUIStyle CardStyle { get; private set; }
    public static GUIStyle CardSelectedStyle { get; private set; }
    public static GUIStyle CategoryItemStyle { get; private set; }
    public static GUIStyle CategoryItemSelectedStyle { get; private set; }
    public static GUIStyle ToolbarStyle { get; private set; }
    public static GUIStyle ToolbarButtonStyle { get; private set; }
    public static GUIStyle SearchFieldStyle { get; private set; }

    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        bool isPro = EditorGUIUtility.isProSkin;
        if (isPro)
        {
            _bgPanel            = new Color(0.18f, 0.18f, 0.18f);
            _cardBg             = new Color(0.24f, 0.24f, 0.26f);
            _cardBgHover        = new Color(0.32f, 0.32f, 0.32f);
            _cardBgSelected     = new Color(0.40f, 0.50f, 0.72f);
            _textPrimary        = new Color(0.92f, 0.92f, 0.92f);
            _textSecondary      = new Color(0.65f, 0.66f, 0.70f);
            _categoryActive     = new Color(0.30f, 0.45f, 0.85f);
        }
        else
        {
            _bgPanel            = new Color(0.80f, 0.80f, 0.80f);
            _cardBg             = new Color(0.95f, 0.95f, 0.95f);
            _cardBgHover        = new Color(0.88f, 0.90f, 0.92f);
            _cardBgSelected     = new Color(0.55f, 0.70f, 0.95f);
            _textPrimary        = new Color(0.10f, 0.10f, 0.10f);
            _textSecondary      = new Color(0.40f, 0.40f, 0.40f);
            _categoryActive     = new Color(0.30f, 0.55f, 0.85f);
        }

        var baseBox = new GUIStyle("box");
        var baseButton = new GUIStyle("button");

        CardStyle = new GUIStyle(baseBox) { padding = new RectOffset(10, 10, 8, 8), margin = new RectOffset(6, 6, 6, 6) };
        CardStyle.normal.textColor = _textPrimary;
        CardStyle.normal.background = MakeSolidTexture(_cardBg);

        CardSelectedStyle = new GUIStyle(CardStyle);
        CardSelectedStyle.normal.background = MakeSolidTexture(_cardBgSelected);

        CategoryItemStyle = new GUIStyle() { padding = new RectOffset(8, 8, 4, 4), margin = new RectOffset(2, 2, 1, 1), alignment = TextAnchor.MiddleLeft, fontSize = 12 };
        CategoryItemStyle.normal.textColor = _textSecondary;
        CategoryItemStyle.normal.background = MakeSolidTexture(new Color(0, 0, 0, 0));

        CategoryItemSelectedStyle = new GUIStyle(CategoryItemStyle) { fontStyle = FontStyle.Bold };
        CategoryItemSelectedStyle.normal.textColor = Color.white;
        CategoryItemSelectedStyle.normal.background = MakeSolidTexture(_categoryActive);

        ToolbarStyle = new GUIStyle(EditorStyles.toolbar);
        ToolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
        SearchFieldStyle = new GUIStyle(EditorStyles.toolbarSearchField);
    }

    private static Texture2D MakeSolidTexture(Color c)
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, c);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    public static Color TextPrimary   => _textPrimary;
    public static Color TextSecondary => _textSecondary;
    public static Color CardBg        => _cardBg;
    public static Color CardBgHover   => _cardBgHover;
}