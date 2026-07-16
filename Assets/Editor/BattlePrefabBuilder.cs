using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 卡牌Prefab模板构建器 —— 创建3种类型的卡牌模板
/// 菜单: Tools > 重建卡牌模板Prefabs
/// 攻击牌模板 / 护甲牌模板 / 增益牌模板
/// </summary>
public static class BattlePrefabBuilder
{
    private static readonly string PrefabDir = "Assets/Prefabs/Battle/Cards";

    [MenuItem("Tools/重建卡牌模板Prefabs")]
    public static void CreateAllCardPrefabs()
    {
        EnsureDirectory();
        CreateCardPrefab(CardType.Attack);
        CreateCardPrefab(CardType.Armor);
        CreateCardPrefab(CardType.Buff);
        Debug.Log("[BattlePrefabBuilder] 三种卡牌模板Prefab已创建");
    }

    private static void CreateCardPrefab(CardType cardType)
    {
        string typeName = cardType switch
        {
            CardType.Attack => "攻击牌",
            CardType.Armor => "护甲牌",
            CardType.Buff => "增益牌",
            _ => "卡牌"
        };

        var root = new GameObject(typeName);
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(180, 252);

        var bgImage = root.AddComponent<Image>();
        bgImage.color = new Color(0.10f, 0.10f, 0.13f, 1f);

        var cardDisplay = root.AddComponent<CardDisplay>();
        // 设置卡牌类型（不可更改，由模板决定）
        cardDisplay.cardType = cardType;
        cardDisplay.cardName = typeName + "模板";
        cardDisplay.grade = CardGrade.Common;
        cardDisplay.actionPointCost = 1;

        // 按类型设默认值
        switch (cardType)
        {
            case CardType.Attack:
                cardDisplay.attackValue = 5;
                cardDisplay.attackCount = 1;
                break;
            case CardType.Armor:
                cardDisplay.armorValue = 5;
                break;
            case CardType.Buff:
                cardDisplay.buffEffects = new System.Collections.Generic.List<BuffEffect>();
                break;
        }

        root.AddComponent<CardHoverEffect>();

        // === Border ===
        var borderObj = CreateChild(rootRect, "Frame");
        StretchFill(borderObj, 3, 3, 3, 3);
        var frameImage = borderObj.AddComponent<Image>();
        frameImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);

        // === Background ===
        var innerBgObj = CreateChild(borderObj.transform, "Background");
        StretchFill(innerBgObj, 3, 3, 3, 3);
        var backgroundImage = innerBgObj.AddComponent<Image>();
        backgroundImage.color = new Color(0.15f, 0.15f, 0.20f, 1f);

        // === NameText ===
        var nameObj = CreateChild(innerBgObj.transform, "NameText");
        SetupRect(nameObj, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(-10, 24));
        var nameText = nameObj.AddComponent<TextMeshProUGUI>();
        SetupText(nameText, typeName, 15, Color.white);

        // === CostBadge ===
        var costBadgeObj = CreateChild(innerBgObj.transform, "CostBadge");
        SetupRect(costBadgeObj, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(20, -26), new Vector2(32, 32));
        var costBadgeImage = costBadgeObj.AddComponent<Image>();
        costBadgeImage.color = new Color(0.8f, 0.6f, 0.15f, 1f);

        var costTextObj = CreateChild(costBadgeObj.transform, "CostText");
        StretchFill(costTextObj);
        var costText = costTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(costText, "1", 18, Color.white);
        costText.enableWordWrapping = false;

        // === TypeBadge ===
        var typeBadgeObj = CreateChild(innerBgObj.transform, "TypeBadge");
        SetupRect(typeBadgeObj, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-35, -22), new Vector2(50, 22));
        var typeBadgeImage = typeBadgeObj.AddComponent<Image>();
        typeBadgeImage.color = new Color(0.3f, 0.3f, 0.4f, 0.9f);

        var typeTextObj = CreateChild(typeBadgeObj.transform, "TypeText");
        StretchFill(typeTextObj);
        var typeText = typeTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(typeText, CardData.GetCardTypeName(cardType), 12, Color.white);

        // === GradeText ===
        var gradeObj = CreateChild(innerBgObj.transform, "GradeText");
        SetupRect(gradeObj, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(-10, 18));
        var gradeText = gradeObj.AddComponent<TextMeshProUGUI>();
        SetupText(gradeText, "普通", 11, new Color(0.7f, 0.7f, 0.7f, 1f));

        // === ArtArea ===
        var artObj = CreateChild(innerBgObj.transform, "ArtArea");
        SetupRect(artObj, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(148, 88));
        var artImage = artObj.AddComponent<Image>();
        artImage.color = new Color(1, 1, 1, 0.08f);

        // === DescText ===
        var descObj = CreateChild(innerBgObj.transform, "DescText");
        SetupRect(descObj, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(-12, 72));
        var descText = descObj.AddComponent<TextMeshProUGUI>();
        SetupText(descText, "描述", 12, new Color(0.85f, 0.85f, 0.85f, 1f));
        descText.alignment = TextAlignmentOptions.Top;

        // === KeywordText ===
        var kwObj = CreateChild(innerBgObj.transform, "KeywordText");
        SetupRect(kwObj, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(-12, 18));
        var keywordText = kwObj.AddComponent<TextMeshProUGUI>();
        SetupText(keywordText, "", 11, new Color(0.9f, 0.8f, 0.3f, 1f));
        keywordText.gameObject.SetActive(false);

        // === 连接 CardDisplay 字段 ===
        var so = new SerializedObject(cardDisplay);
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("descText").objectReferenceValue = descText;
        so.FindProperty("costText").objectReferenceValue = costText;
        so.FindProperty("typeText").objectReferenceValue = typeText;
        so.FindProperty("keywordText").objectReferenceValue = keywordText;
        so.FindProperty("gradeText").objectReferenceValue = gradeText;
        so.FindProperty("frameImage").objectReferenceValue = frameImage;
        so.FindProperty("backgroundImage").objectReferenceValue = backgroundImage;
        so.FindProperty("artImage").objectReferenceValue = artImage;
        so.FindProperty("typeBadgeImage").objectReferenceValue = typeBadgeImage;
        so.FindProperty("costBadgeImage").objectReferenceValue = costBadgeImage;
        so.ApplyModifiedProperties();

        cardDisplay.UpdateDisplay();

        string path = $"{PrefabDir}/{typeName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Debug.Log($"[BattlePrefabBuilder] {typeName}模板Prefab已创建: {path}");
    }

    // === 辅助 ===

    private static void EnsureDirectory()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Battle"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Battle");
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder("Assets/Prefabs/Battle", "Cards");
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        var obj = new GameObject(name);
        obj.AddComponent<RectTransform>();
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void StretchFill(GameObject obj, float left = 0, float right = 0, float top = 0, float bottom = 0)
    {
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(left, bottom);
        r.offsetMax = new Vector2(-right, -top);
    }

    private static void SetupRect(GameObject obj, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = pivot;
        r.anchoredPosition = anchoredPosition;
        r.sizeDelta = sizeDelta;
    }

    private static void SetupText(TextMeshProUGUI text, string content, int fontSize, Color color)
    {
        text.text = content;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/simhei Chinese.asset");
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }
}
