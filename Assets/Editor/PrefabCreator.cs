using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键重建被误删的 UI Prefab（PageCard、OptionButton）
/// 菜单：Tools > 重建 PageCard Prefab / 重建 OptionButton Prefab
/// </summary>
public static class PrefabCreator
{
    [MenuItem("Tools/重建 PageCard Prefab")]
    public static void CreatePageCardPrefab()
    {
        EnsureDirectory();

        var root = new GameObject("PageCard");
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(200, 280);

        var bgImage = root.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.12f, 0.15f, 0.95f);

        var button = root.AddComponent<Button>();
        button.targetGraphic = bgImage;

        var cardUI = root.AddComponent<PageCardUI>();

        // Frame（色调层，运行时按事件类型着色）
        var frameObj = CreateChild(rootRect, "Frame");
        StretchFill(frameObj);
        var frameImage = frameObj.AddComponent<Image>();
        frameImage.color = new Color(1, 1, 1, 0.08f);

        // Icon
        var iconObj = CreateChild(rootRect, "Icon");
        var iconRect = iconObj.GetComponent<RectTransform>();
        SetAnchors(iconRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        iconRect.anchoredPosition = new Vector2(0, -20);
        iconRect.sizeDelta = new Vector2(80, 80);
        var iconImage = iconObj.AddComponent<Image>();
        iconImage.color = new Color(1, 1, 1, 0.1f);

        // TypeBadge
        var badgeObj = CreateChild(rootRect, "TypeBadge");
        var badgeRect = badgeObj.GetComponent<RectTransform>();
        SetAnchors(badgeRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        badgeRect.anchoredPosition = new Vector2(-10, -10);
        badgeRect.sizeDelta = new Vector2(60, 24);
        var badgeText = badgeObj.AddComponent<Text>();
        SetupText(badgeText, "类型", 14, Color.white);

        // Title
        var titleObj = CreateChild(rootRect, "Title");
        var titleRect = titleObj.GetComponent<RectTransform>();
        SetAnchors(titleRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
        titleRect.anchoredPosition = new Vector2(0, 25);
        titleRect.sizeDelta = new Vector2(-20, 30);
        var titleText = titleObj.AddComponent<Text>();
        SetupText(titleText, "标题", 18, Color.white);

        // Desc
        var descObj = CreateChild(rootRect, "Desc");
        var descRect = descObj.GetComponent<RectTransform>();
        SetAnchors(descRect, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
        descRect.anchoredPosition = new Vector2(0, -15);
        descRect.sizeDelta = new Vector2(-20, -50);
        var descText = descObj.AddComponent<Text>();
        SetupText(descText, "描述", 14, new Color(0.8f, 0.8f, 0.8f, 1f));
        descText.alignment = TextAnchor.UpperCenter;
        descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descText.verticalOverflow = VerticalWrapMode.Truncate;

        // FinalIndicator
        var finalObj = CreateChild(rootRect, "FinalIndicator");
        var finalRect = finalObj.GetComponent<RectTransform>();
        SetAnchors(finalRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        finalRect.anchoredPosition = new Vector2(0, 8);
        finalRect.sizeDelta = new Vector2(120, 20);
        var finalText = finalObj.AddComponent<Text>();
        SetupText(finalText, "★ 最终节点", 12, new Color(1f, 0.85f, 0.2f, 1f));
        finalObj.SetActive(false);

        // 连接 PageCardUI 的 private [SerializeField] 字段
        var so = new SerializedObject(cardUI);
        so.FindProperty("iconImage").objectReferenceValue = iconImage;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("descText").objectReferenceValue = descText;
        so.FindProperty("typeBadgeText").objectReferenceValue = badgeText;
        so.FindProperty("cardButton").objectReferenceValue = button;
        so.FindProperty("cardFrameImage").objectReferenceValue = frameImage;
        so.FindProperty("finalNodeIndicator").objectReferenceValue = finalObj;
        so.ApplyModifiedProperties();

        string path = "Assets/Prefabs/UI/PageCard.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Debug.Log($"[PrefabCreator] PageCard Prefab 已创建: {path}");
    }

    [MenuItem("Tools/重建 OptionButton Prefab")]
    public static void CreateOptionButtonPrefab()
    {
        EnsureDirectory();

        var root = new GameObject("OptionButton");
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(400, 50);

        var bgImage = root.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.25f, 0.95f);

        var button = root.AddComponent<Button>();
        button.targetGraphic = bgImage;

        // Text 子对象
        var textObj = CreateChild(rootRect, "Text");
        StretchFill(textObj);
        textObj.GetComponent<RectTransform>().offsetMin = new Vector2(10, 2);
        textObj.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -2);
        var text = textObj.AddComponent<Text>();
        SetupText(text, "选项文本", 16, Color.white);
        text.alignment = TextAnchor.MiddleLeft;

        string path = "Assets/Prefabs/UI/OptionButton.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Debug.Log($"[PrefabCreator] OptionButton Prefab 已创建: {path}");
    }

    [MenuItem("Tools/重建全部 UI Prefab")]
    public static void CreateAllPrefabs()
    {
        CreatePageCardPrefab();
        CreateOptionButtonPrefab();
        Debug.Log("[PrefabCreator] 全部 UI Prefab 已重建，请在 BookUIController / OptionPanelUI 的 Inspector 中重新拖入引用。");
    }

    private static void EnsureDirectory()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        var obj = new GameObject(name);
        obj.AddComponent<RectTransform>();
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void StretchFill(GameObject obj)
    {
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
    }

    private static void SetupText(Text text, string content, int fontSize, Color color)
    {
        text.text = content;
        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private static Font GetDefaultFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/simhei.ttf");
        if (font != null) return font;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
