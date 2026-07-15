using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 一键重建被误删的 UI Prefab（PageCard、OptionButton），使用 TMP + simhei Chinese 字体
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
        rootRect.sizeDelta = new Vector2(240, 320);

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
        badgeRect.anchoredPosition = new Vector2(-143, -10);
        badgeRect.sizeDelta = new Vector2(60, 24);
        var badgeText = badgeObj.AddComponent<TextMeshProUGUI>();
        SetupText(badgeText, "类型", 14, Color.white);
        
        // 添加 LayoutElement 组件，避免受父对象 LayoutGroup 影响
        var badgeLayoutElement = badgeObj.AddComponent<UnityEngine.UI.LayoutElement>();
        badgeLayoutElement.ignoreLayout = true;

        // DeleteButton（右上角删除按钮）
        var deleteObj = CreateChild(rootRect, "DeleteButton");
        var deleteRect = deleteObj.GetComponent<RectTransform>();
        SetAnchors(deleteRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        deleteRect.anchoredPosition = new Vector2(-20, -20);
        deleteRect.sizeDelta = new Vector2(30, 30);
        var deleteImage = deleteObj.AddComponent<Image>();
        deleteImage.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        var deleteBtn = deleteObj.AddComponent<Button>();
        deleteBtn.targetGraphic = deleteImage;

        var deleteTextObj = CreateChild(deleteRect, "Text");
        StretchFill(deleteTextObj);
        var deleteText = deleteTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(deleteText, "X", 18, Color.white);
        deleteText.alignment = TextAlignmentOptions.Center;
        
        // 添加 LayoutElement 组件，避免受父对象 LayoutGroup 影响
        var deleteLayoutElement = deleteObj.AddComponent<UnityEngine.UI.LayoutElement>();
        deleteLayoutElement.ignoreLayout = true;

        // Title
        var titleObj = CreateChild(rootRect, "Title");
        var titleRect = titleObj.GetComponent<RectTransform>();
        SetAnchors(titleRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
        titleRect.anchoredPosition = new Vector2(0, 25);
        titleRect.sizeDelta = new Vector2(-20, 30);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        SetupText(titleText, "标题", 18, Color.white);

        // Desc
        var descObj = CreateChild(rootRect, "Desc");
        var descRect = descObj.GetComponent<RectTransform>();
        SetAnchors(descRect, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
        descRect.anchoredPosition = new Vector2(0, -15);
        descRect.sizeDelta = new Vector2(-20, -50);
        var descText = descObj.AddComponent<TextMeshProUGUI>();
        SetupText(descText, "描述", 14, new Color(0.8f, 0.8f, 0.8f, 1f));
        descText.alignment = TextAlignmentOptions.Top;
        descText.enableWordWrapping = true;
        descText.overflowMode = TextOverflowModes.Ellipsis;

        // FinalIndicator
        var finalObj = CreateChild(rootRect, "FinalIndicator");
        var finalRect = finalObj.GetComponent<RectTransform>();
        SetAnchors(finalRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        finalRect.anchoredPosition = new Vector2(0, 8);
        finalRect.sizeDelta = new Vector2(120, 20);
        var finalText = finalObj.AddComponent<TextMeshProUGUI>();
        SetupText(finalText, "★ 最终节点", 12, new Color(1f, 0.85f, 0.2f, 1f));
        finalObj.SetActive(false);

        // EnterButton（底部"进入"按钮，独立 Button 组件，按下即进入事件）
        var ctaObj = CreateChild(rootRect, "EnterButton");
        var ctaRect = ctaObj.GetComponent<RectTransform>();
        SetAnchors(ctaRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        ctaRect.anchoredPosition = new Vector2(0, 8);
        ctaRect.sizeDelta = new Vector2(-20, 40);
        var ctaImage = ctaObj.AddComponent<Image>();
        ctaImage.color = new Color(0.3f, 0.3f, 0.4f, 1f);
        var enterBtn = ctaObj.AddComponent<Button>();
        enterBtn.targetGraphic = ctaImage;

        var ctaTextObj = CreateChild(ctaRect, "Text");
        StretchFill(ctaTextObj);
        ctaTextObj.GetComponent<RectTransform>().offsetMin = new Vector2(4, 2);
        ctaTextObj.GetComponent<RectTransform>().offsetMax = new Vector2(-4, -2);
        var ctaText = ctaTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(ctaText, "进入", 16, Color.white);

        // 连接 PageCardUI 的 private [SerializeField] 字段
        var so = new SerializedObject(cardUI);
        so.FindProperty("iconImage").objectReferenceValue = iconImage;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("descText").objectReferenceValue = descText;
        so.FindProperty("typeBadgeText").objectReferenceValue = badgeText;
        so.FindProperty("cardButton").objectReferenceValue = button;
        































































































































































































































        so.FindProperty("cardFrameImage").objectReferenceValue = frameImage;
        so.FindProperty("finalNodeIndicator").objectReferenceValue = finalObj;
        so.FindProperty("enterButton").objectReferenceValue = enterBtn;
        so.FindProperty("enterButtonText").objectReferenceValue = ctaText;
        so.FindProperty("deleteButton").objectReferenceValue = deleteBtn;
        so.FindProperty("deleteButtonText").objectReferenceValue = deleteText;
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
        var text = textObj.AddComponent<TextMeshProUGUI>();
        SetupText(text, "选项文本", 16, Color.white);
        text.alignment = TextAlignmentOptions.Left;

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
        Debug.Log("[PrefabCreator] 全部 UI Prefab 已重建（TMP + simhei Chinese），请在 BookUIController / OptionPanelUI 的 Inspector 中重新拖入引用。");
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

    private static void SetupText(TextMeshProUGUI text, string content, int fontSize, Color color)
    {
        text.text = content;
        text.font = GetTMPFont();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static TMP_FontAsset GetTMPFont()
    {
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/simhei Chinese.asset");
    }
}
