using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 战斗场景构建器 —— 菜单: Tools > 构建战斗场景
/// 创建 Battle.scene + 每张卡牌单独的Prefab（数据直接在CardDisplay组件上）
/// </summary>
public static class BattleSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Battle.scene";
    private const string CardPrefabDir = "Assets/Prefabs/Battle/Cards";

    [MenuItem("Tools/构建战斗场景")]
    public static void BuildBattleScene()
    {
        // 加载3种模板Prefab
        var attackTemplate = AssetDatabase.LoadAssetAtPath<GameObject>($"{CardPrefabDir}/攻击牌.prefab");
        var armorTemplate = AssetDatabase.LoadAssetAtPath<GameObject>($"{CardPrefabDir}/护甲牌.prefab");
        var buffTemplate = AssetDatabase.LoadAssetAtPath<GameObject>($"{CardPrefabDir}/增益牌.prefab");

        if (attackTemplate == null || armorTemplate == null || buffTemplate == null)
        {
            Debug.LogError("[BattleSceneBuilder] 未找到卡牌模板Prefab，请先运行 Tools > 重建卡牌模板Prefabs");
            return;
        }

        EnsureDirectories();
        var cardPrefabs = CreateSampleCardPrefabs(attackTemplate, armorTemplate, buffTemplate);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateLight();
        var canvasGO = CreateCanvas();
        CreateBackground(canvasGO);

        var enemyRefs = CreateEnemyArea(canvasGO.transform);
        var playerRefs = CreatePlayerArea(canvasGO.transform);
        var handLayout = CreateHandArea(canvasGO.transform);
        var bottomRefs = CreateBottomRight(canvasGO.transform);
        var turnText = CreateTopBar(canvasGO.transform);
        var resultRefs = CreateResultPanels(canvasGO.transform);

        CreateEventSystem();

        // BattleManager
        var battleGO = new GameObject("BattleSystem");
        var battleManager = battleGO.AddComponent<BattleManager>();

        var so = new SerializedObject(battleManager);
        so.FindProperty("handLayout").objectReferenceValue = handLayout;
        so.FindProperty("hpText").objectReferenceValue = playerRefs.hpText;
        so.FindProperty("actionPointText").objectReferenceValue = playerRefs.apText;
        so.FindProperty("armorText").objectReferenceValue = playerRefs.armorText;
        so.FindProperty("enemyHPText").objectReferenceValue = enemyRefs.hpText;
        so.FindProperty("enemyArmorText").objectReferenceValue = enemyRefs.armorText;
        so.FindProperty("enemyNameText").objectReferenceValue = enemyRefs.nameText;
        so.FindProperty("enemyIntentText").objectReferenceValue = enemyRefs.intentText;
        so.FindProperty("drawPileText").objectReferenceValue = bottomRefs.drawPileText;
        so.FindProperty("discardPileText").objectReferenceValue = bottomRefs.discardPileText;
        so.FindProperty("turnText").objectReferenceValue = turnText;
        so.FindProperty("strengthText").objectReferenceValue = playerRefs.strengthText;
        so.FindProperty("dexterityText").objectReferenceValue = playerRefs.dexterityText;
        so.FindProperty("playerHPBarFill").objectReferenceValue = playerRefs.hpBarFill;
        so.FindProperty("enemyHPBarFill").objectReferenceValue = enemyRefs.hpBarFill;
        so.FindProperty("victoryPanel").objectReferenceValue = resultRefs.victoryPanel;
        so.FindProperty("defeatPanel").objectReferenceValue = resultRefs.defeatPanel;
        so.FindProperty("endTurnButton").objectReferenceValue = bottomRefs.endTurnButton;

        // 牌组：拖入卡牌Prefab
        var deckProp = so.FindProperty("deck");
        deckProp.arraySize = cardPrefabs.Count;
        for (int i = 0; i < cardPrefabs.Count; i++)
            deckProp.GetArrayElementAtIndex(i).objectReferenceValue = cardPrefabs[i];

        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[BattleSceneBuilder] 战斗场景已创建: {ScenePath}");
    }

    // ====================================================================
    // 示例卡牌Prefab创建（每张卡牌一个独立Prefab，数据在CardDisplay上）
    // ====================================================================

    private static List<GameObject> CreateSampleCardPrefabs(GameObject attackTpl, GameObject armorTpl, GameObject buffTpl)
    {
        var cards = new List<GameObject>();

        // 攻击牌
        cards.Add(SaveCardPrefab(attackTpl, "AK47", c => {
            c.grade = CardGrade.Fine; c.actionPointCost = 2; c.value = 35;
            c.attackValue = 10; c.keywords = KeywordType.Pierce;
        }));
        cards.Add(SaveCardPrefab(attackTpl, "棒球棒", c => {
            c.grade = CardGrade.Common; c.actionPointCost = 1; c.value = 15;
            c.attackValue = 4; c.attackCount = 2;
        }));
        cards.Add(SaveCardPrefab(attackTpl, "沙漠之鹰", c => {
            c.grade = CardGrade.Rare; c.actionPointCost = 1; c.value = 40;
            c.attackValue = 7; c.ignoreArmor = true;
        }));
        cards.Add(SaveCardPrefab(attackTpl, "照明弹", c => {
            c.grade = CardGrade.Common; c.actionPointCost = 0; c.value = 10;
            c.attackValue = 2; c.keywords = KeywordType.Swift;
        }));
        cards.Add(SaveCardPrefab(attackTpl, "军体拳", c => {
            c.grade = CardGrade.Common; c.actionPointCost = 1; c.value = 18;
            c.attackValue = 3; c.attackCount = 3; c.keywords = KeywordType.Combo;
        }));

        // 护甲牌
        cards.Add(SaveCardPrefab(armorTpl, "防弹衣", c => {
            c.grade = CardGrade.Fine; c.actionPointCost = 1; c.value = 30;
            c.armorValue = 6; c.keywords = KeywordType.Sturdy;
        }));
        cards.Add(SaveCardPrefab(armorTpl, "迷彩服", c => {
            c.grade = CardGrade.Common; c.actionPointCost = 1; c.value = 20;
            c.armorValue = 4; c.armorValueType = ValueType.AttributeBased;
            c.armorAttribute = PlayerAttributeType.Dexterity;
        }));

        // 增益牌
        cards.Add(SaveCardPrefab(buffTpl, "力量提升", c => {
            c.grade = CardGrade.Fine; c.actionPointCost = 1; c.value = 28;
            c.buffEffects = new List<BuffEffect> {
                new() { effectType = BuffEffectType.IncreaseAttribute, value = 2, targetAttribute = PlayerAttributeType.Strength }
            };
        }));
        cards.Add(SaveCardPrefab(buffTpl, "回复行动力", c => {
            c.grade = CardGrade.Common; c.actionPointCost = 0; c.value = 15;
            c.buffEffects = new List<BuffEffect> {
                new() { effectType = BuffEffectType.RestoreActionPoints, value = 2 }
            };
        }));
        cards.Add(SaveCardPrefab(buffTpl, "抽牌", c => {
            c.grade = CardGrade.Common; c.actionPointCost = 1; c.value = 12;
            c.buffEffects = new List<BuffEffect> {
                new() { effectType = BuffEffectType.DrawCards, value = 2 }
            };
        }));

        return cards;
    }

    /// <summary>
    /// 从类型模板Prefab创建一张卡牌Prefab，设置CardDisplay字段后保存
    /// </summary>
    private static GameObject SaveCardPrefab(GameObject templatePrefab, string cardName, System.Action<CardDisplay> setup)
    {
        string path = $"{CardPrefabDir}/{cardName}.prefab";

        // 删除已有
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);

        // 临时实例化
        var tempGO = (GameObject)PrefabUtility.InstantiatePrefab(templatePrefab);
        tempGO.name = cardName;

        var display = tempGO.GetComponent<CardDisplay>();
        if (display != null)
        {
            display.cardName = cardName;
            setup(display);
            display.UpdateDisplay();
        }

        // 保存为新Prefab
        var saved = PrefabUtility.SaveAsPrefabAsset(tempGO, path);
        Object.DestroyImmediate(tempGO);

        return saved;
    }

    // ====================================================================
    // 场景元素
    // ====================================================================

    private static void CreateCamera()
    {
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        cam.backgroundColor = new Color(0.06f, 0.06f, 0.10f, 1f);
        camGO.transform.position = new Vector3(0, 0, -10);
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
    }

    private static void CreateLight()
    {
        var lightGO = new GameObject("Global Light 2D");
        var light2D = lightGO.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light2D.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
        light2D.intensity = 1f;
        light2D.color = Color.white;
    }

    private static GameObject CreateCanvas()
    {
        var canvasGO = new GameObject("BattleCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        return canvasGO;
    }

    private static void CreateBackground(GameObject canvas)
    {
        var bg = CreateChild(canvas.transform, "Background");
        StretchFill(bg);
        bg.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 1f);
    }

    private static (TextMeshProUGUI nameText, TextMeshProUGUI hpText, TextMeshProUGUI armorText,
                     TextMeshProUGUI intentText, Image hpBarFill) CreateEnemyArea(Transform parent)
    {
        var area = CreateChild(parent, "EnemyArea");
        Rect(area, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, 80), V2(0, 0));

        var sprite = CreateChild(area.transform, "EnemySprite");
        Rect(sprite, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, 40), V2(200, 280));
        sprite.AddComponent<Image>().color = new Color(0.7f, 0.15f, 0.15f, 0.4f);

        var nameObj = CreateChild(area.transform, "EnemyNameText");
        Rect(nameObj, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, 200), V2(300, 30));
        var nameText = nameObj.AddComponent<TextMeshProUGUI>();
        SetupText(nameText, "敌人", 24, Color.white);

        var hpBar = CreateChild(area.transform, "EnemyHPBar");
        Rect(hpBar, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, 170), V2(320, 24));
        hpBar.AddComponent<Image>().color = new Color(0.2f, 0.08f, 0.08f, 1f);

        var hpFill = CreateChild(hpBar.transform, "EnemyHPBarFill");
        StretchFill(hpFill);
        var hpFillImg = hpFill.AddComponent<Image>();
        hpFillImg.color = new Color(0.2f, 0.8f, 0.2f, 1f);
        hpFillImg.type = Image.Type.Filled;
        hpFillImg.fillMethod = Image.FillMethod.Horizontal;

        var hpTextObj = CreateChild(hpBar.transform, "EnemyHPText");
        StretchFill(hpTextObj);
        var hpText = hpTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(hpText, "80/80", 14, Color.white);
        hpText.enableWordWrapping = false;

        var armorObj = CreateChild(area.transform, "EnemyArmorText");
        Rect(armorObj, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, 140), V2(200, 22));
        var armorText = armorObj.AddComponent<TextMeshProUGUI>();
        SetupText(armorText, "", 16, C(0.6f, 0.8f, 1f, 1f));

        var intentObj = CreateChild(area.transform, "EnemyIntentText");
        Rect(intentObj, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, -120), V2(300, 24));
        var intentText = intentObj.AddComponent<TextMeshProUGUI>();
        SetupText(intentText, "下回合敌人攻击: 8", 16, C(1f, 0.6f, 0.3f, 1f));

        return (nameText, hpText, armorText, intentText, hpFillImg);
    }

    private static (TextMeshProUGUI hpText, TextMeshProUGUI apText, TextMeshProUGUI armorText,
                     TextMeshProUGUI strengthText, TextMeshProUGUI dexterityText, Image hpBarFill)
        CreatePlayerArea(Transform parent)
    {
        var area = CreateChild(parent, "PlayerArea");
        Rect(area, C(0f, 0f), C(0f, 0f), C(0f, 0f), C(40, 40), V2(0, 0));

        var hpBar = CreateChild(area.transform, "PlayerHPBar");
        Rect(hpBar, C(0f, 0f), C(0f, 0f), C(0f, 0.5f), C(60, 40), V2(200, 22));
        hpBar.AddComponent<Image>().color = new Color(0.2f, 0.08f, 0.08f, 1f);

        var hpFill = CreateChild(hpBar.transform, "PlayerHPBarFill");
        StretchFill(hpFill);
        var hpFillImg = hpFill.AddComponent<Image>();
        hpFillImg.color = new Color(0.2f, 0.8f, 0.2f, 1f);
        hpFillImg.type = Image.Type.Filled;
        hpFillImg.fillMethod = Image.FillMethod.Horizontal;

        var hpTextObj = CreateChild(hpBar.transform, "HPText");
        StretchFill(hpTextObj);
        var hpText = hpTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(hpText, "HP: 50/50", 14, Color.white);
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.enableWordWrapping = false;

        var apBadge = CreateChild(area.transform, "ActionPointBadge");
        Rect(apBadge, C(0f, 0f), C(0f, 0f), C(0.5f, 0.5f), C(20, 51), V2(40, 40));
        apBadge.AddComponent<Image>().color = new Color(0.85f, 0.65f, 0.15f, 1f);

        var apTextObj = CreateChild(apBadge.transform, "ActionPointText");
        StretchFill(apTextObj);
        var apText = apTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(apText, "3", 22, Color.white);
        apText.enableWordWrapping = false;

        var armorObj = CreateChild(area.transform, "ArmorText");
        Rect(armorObj, C(0f, 0f), C(0f, 0f), C(0f, 0.5f), C(60, 14), V2(200, 22));
        var armorText = armorObj.AddComponent<TextMeshProUGUI>();
        SetupText(armorText, "护甲: 0", 16, C(0.6f, 0.8f, 1f, 1f));
        armorText.alignment = TextAlignmentOptions.Left;

        var strObj = CreateChild(area.transform, "StrengthText");
        Rect(strObj, C(0f, 0f), C(0f, 0f), C(0f, 0.5f), C(60, -10), V2(200, 22));
        var strengthText = strObj.AddComponent<TextMeshProUGUI>();
        SetupText(strengthText, "", 16, C(1f, 0.5f, 0.3f, 1f));
        strengthText.alignment = TextAlignmentOptions.Left;

        var dexObj = CreateChild(area.transform, "DexterityText");
        Rect(dexObj, C(0f, 0f), C(0f, 0f), C(0f, 0.5f), C(60, -34), V2(200, 22));
        var dexterityText = dexObj.AddComponent<TextMeshProUGUI>();
        SetupText(dexterityText, "", 16, C(0.3f, 0.7f, 1f, 1f));
        dexterityText.alignment = TextAlignmentOptions.Left;

        return (hpText, apText, armorText, strengthText, dexterityText, hpFillImg);
    }

    private static HandCardLayout CreateHandArea(Transform parent)
    {
        var handObj = CreateChild(parent, "HandArea");
        Rect(handObj, C(0.5f, 0f), C(0.5f, 0f), C(0.5f, 0f), C(0, 160), V2(1200, 400));

        var layout = handObj.AddComponent<HandCardLayout>();
        var so = new SerializedObject(layout);
        so.FindProperty("cardSpacing").floatValue = 140f;
        so.FindProperty("fanRadius").floatValue = 1000f;
        so.FindProperty("maxFanAngle").floatValue = 15f;
        so.FindProperty("hoverScale").floatValue = 1.4f;
        so.FindProperty("hoverYOffset").floatValue = 100f;
        so.FindProperty("lerpSpeed").floatValue = 20f;
        so.ApplyModifiedProperties();
        return layout;
    }

    private static (TextMeshProUGUI drawPileText, TextMeshProUGUI discardPileText, Button endTurnButton)
        CreateBottomRight(Transform parent)
    {
        var drawObj = CreateChild(parent, "DrawPileText");
        Rect(drawObj, C(1f, 0f), C(1f, 0f), C(1f, 0f), C(-120, 60), V2(100, 30));
        var drawPileText = drawObj.AddComponent<TextMeshProUGUI>();
        SetupText(drawPileText, "5", 18, C(0.7f, 0.7f, 0.8f, 1f));
        drawPileText.enableWordWrapping = false;

        var drawLabel = CreateChild(drawObj.transform, "Label");
        Rect(drawLabel, C(0.5f, 0f), C(0.5f, 0f), C(0.5f, 1f), C(0, -2), V2(100, 16));
        var drawLabelText = drawLabel.AddComponent<TextMeshProUGUI>();
        SetupText(drawLabelText, "抽牌堆", 12, C(0.6f, 0.6f, 0.7f, 1f));

        var discardObj = CreateChild(parent, "DiscardPileText");
        Rect(discardObj, C(1f, 0f), C(1f, 0f), C(1f, 0f), C(-120, 120), V2(100, 30));
        var discardPileText = discardObj.AddComponent<TextMeshProUGUI>();
        SetupText(discardPileText, "0", 18, C(0.7f, 0.7f, 0.8f, 1f));
        discardPileText.enableWordWrapping = false;

        var discardLabel = CreateChild(discardObj.transform, "Label");
        Rect(discardLabel, C(0.5f, 0f), C(0.5f, 0f), C(0.5f, 1f), C(0, -2), V2(100, 16));
        var discardLabelText = discardLabel.AddComponent<TextMeshProUGUI>();
        SetupText(discardLabelText, "弃牌堆", 12, C(0.6f, 0.6f, 0.7f, 1f));

        var btnObj = CreateChild(parent, "EndTurnButton");
        Rect(btnObj, C(1f, 0f), C(1f, 0f), C(1f, 0f), C(-110, 200), V2(180, 60));
        btnObj.AddComponent<Image>().color = new Color(0.85f, 0.45f, 0.1f, 1f);
        var endTurnButton = btnObj.AddComponent<Button>();

        var btnTextObj = CreateChild(btnObj.transform, "Text");
        StretchFill(btnTextObj);
        var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(btnText, "结束回合", 20, Color.white);

        return (drawPileText, discardPileText, endTurnButton);
    }

    private static TextMeshProUGUI CreateTopBar(Transform parent)
    {
        var turnObj = CreateChild(parent, "TurnText");
        Rect(turnObj, C(0.5f, 1f), C(0.5f, 1f), C(0.5f, 1f), C(0, -30), V2(200, 30));
        var turnText = turnObj.AddComponent<TextMeshProUGUI>();
        SetupText(turnText, "第1回合", 20, C(0.9f, 0.85f, 0.5f, 1f));
        return turnText;
    }

    private static (GameObject victoryPanel, GameObject defeatPanel) CreateResultPanels(Transform parent)
    {
        var victoryPanel = CreateChild(parent, "VictoryPanel");
        StretchFill(victoryPanel);
        victoryPanel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

        var vText = CreateChild(victoryPanel.transform, "VictoryText");
        Rect(vText, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, 0), V2(600, 80));
        var vTMP = vText.AddComponent<TextMeshProUGUI>();
        SetupText(vTMP, "战 斗 胜 利", 48, C(1f, 0.85f, 0.2f, 1f));
        victoryPanel.SetActive(false);

        var defeatPanel = CreateChild(parent, "DefeatPanel");
        StretchFill(defeatPanel);
        defeatPanel.AddComponent<Image>().color = new Color(0.08f, 0.03f, 0.03f, 0.85f);

        var dText = CreateChild(defeatPanel.transform, "DefeatText");
        Rect(dText, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, 0), V2(600, 80));
        var dTMP = dText.AddComponent<TextMeshProUGUI>();
        SetupText(dTMP, "战 斗 失 败", 48, C(0.9f, 0.3f, 0.3f, 1f));
        defeatPanel.SetActive(false);

        return (victoryPanel, defeatPanel);
    }

    private static void CreateEventSystem()
    {
        var esObj = new GameObject("EventSystem");
        esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    // ====================================================================
    // 辅助方法
    // ====================================================================

    private static void EnsureDirectories()
    {
        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Battle");
        EnsureFolder("Assets/Prefabs/Battle", "Cards");
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
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
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private static RectTransform Rect(GameObject obj, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = pivot;
        r.anchoredPosition = anchoredPosition;
        r.sizeDelta = sizeDelta;
        return r;
    }

    private static Vector2 C(float x, float y) => new Vector2(x, y);
    private static Vector2 V2(float x, float y) => new Vector2(x, y);
    private static Color C(float r, float g, float b, float a) => new Color(r, g, b, a);

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
