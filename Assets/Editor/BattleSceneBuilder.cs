using System.Collections.Generic;
using LightMiniGame.Card;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 战斗场景构建器 —— 菜单: Tools > 构建战斗场景
/// UI 布局参考杀戮尖塔风格战斗界面。
/// </summary>
public static class BattleSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Battle.scene";
    private const string CardPrefabDir = "Assets/Prefabs/Battle/Cards";

    [MenuItem("Tools/构建战斗场景")]
    public static void BuildBattleScene()
    {
        var attackTpl = LoadPrefab($"{CardPrefabDir}/攻击牌.prefab");
        var armorTpl = LoadPrefab($"{CardPrefabDir}/护甲牌.prefab");
        var buffTpl = LoadPrefab($"{CardPrefabDir}/增益牌.prefab");

        if (attackTpl == null || armorTpl == null || buffTpl == null)
        {
            Debug.LogError("[BattleSceneBuilder] 未找到卡牌模板Prefab，请先运行 Tools > 重建卡牌模板Prefabs");
            return;
        }

        var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/ScriptableObjects/GameConfig.asset");
        if (gameConfig == null)
        {
            Debug.LogError("[BattleSceneBuilder] 未找到 GameConfig.asset");
            return;
        }

        var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SettingsPanel.prefab");
        if (settingsPrefab == null)
        {
            Debug.LogError("[BattleSceneBuilder] 未找到 SettingsPanel.prefab");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateLight();
        var canvasGO = CreateCanvas();
        CreateBackground(canvasGO);

        var enemyRefs = CreateEnemyArea(canvasGO.transform);
        var playerRefs = CreatePlayerArea(canvasGO.transform);
        var handLayout = CreateHandArea(canvasGO.transform, attackTpl, armorTpl, buffTpl);
        var bottomRefs = CreateBottomRight(canvasGO.transform);
        var topRefs = CreateTopBar(canvasGO.transform);
        var charRefs = CreateCharacterSwitchArea(canvasGO.transform);
        var settingsRefs = CreateSettingsButton(canvasGO.transform);
        var resultRefs = CreateResultPanels(canvasGO.transform);

        CreateEventSystem();

        // BattleManager
        var battleGO = new GameObject("BattleSystem");
        var battleManager = battleGO.AddComponent<BattleManager>();

        var so = new SerializedObject(battleManager);

        so.FindProperty("gameConfig").objectReferenceValue = gameConfig;
        so.FindProperty("attackCardPrefab").objectReferenceValue = attackTpl;
        so.FindProperty("armorCardPrefab").objectReferenceValue = armorTpl;
        so.FindProperty("buffCardPrefab").objectReferenceValue = buffTpl;
        so.FindProperty("handLayout").objectReferenceValue = handLayout;

        // 玩家 UI
        so.FindProperty("hpText").objectReferenceValue = playerRefs.hpText;
        so.FindProperty("actionPointText").objectReferenceValue = playerRefs.apText;
        so.FindProperty("armorText").objectReferenceValue = playerRefs.armorText;
        so.FindProperty("strengthText").objectReferenceValue = playerRefs.strengthText;
        so.FindProperty("dexterityText").objectReferenceValue = playerRefs.dexterityText;
        so.FindProperty("playerHPBarFill").objectReferenceValue = playerRefs.hpBarFill;

        // 敌人 UI
        so.FindProperty("enemyHPText").objectReferenceValue = enemyRefs.hpText;
        so.FindProperty("enemyArmorText").objectReferenceValue = enemyRefs.armorText;
        so.FindProperty("enemyNameText").objectReferenceValue = enemyRefs.nameText;
        so.FindProperty("enemyIntentText").objectReferenceValue = enemyRefs.intentText;
        so.FindProperty("enemyHPBarFill").objectReferenceValue = enemyRefs.hpBarFill;
        so.FindProperty("enemyDamageText").objectReferenceValue = enemyRefs.damageText;

        // 回合 UI (no turnText)
        so.FindProperty("phaseHintText").objectReferenceValue = topRefs.phaseHintText;
        so.FindProperty("endTurnButton").objectReferenceValue = bottomRefs.endTurnButton;

        // 角色切换 UI
        so.FindProperty("switchCharacterButton").objectReferenceValue = charRefs.switchButton;
        so.FindProperty("activeCharNameText").objectReferenceValue = charRefs.activeNameText;
        so.FindProperty("inactiveCharNameText").objectReferenceValue = charRefs.inactiveNameText;
        so.FindProperty("activeCharPortrait").objectReferenceValue = charRefs.activePortrait;
        so.FindProperty("inactiveCharPortrait").objectReferenceValue = charRefs.inactivePortrait;
        so.FindProperty("switchAvailableIndicator").objectReferenceValue = charRefs.switchAvailableObj;
        so.FindProperty("switchUsedIndicator").objectReferenceValue = charRefs.switchUsedObj;

        // 设置
        so.FindProperty("settingsButton").objectReferenceValue = settingsRefs;
        so.FindProperty("settingsPanelPrefab").objectReferenceValue = settingsPrefab;

        // 结果面板
        so.FindProperty("victoryPanel").objectReferenceValue = resultRefs.victoryPanel;
        so.FindProperty("defeatPanel").objectReferenceValue = resultRefs.defeatPanel;

        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[BattleSceneBuilder] 战斗场景已创建: {ScenePath}");
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
        bg.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 1f);
    }

    // ====================================================================
    // 角色切换区（左上）
    // ====================================================================

    private static (Button switchButton, TextMeshProUGUI activeNameText, TextMeshProUGUI inactiveNameText,
        Image activePortrait, Image inactivePortrait,
        GameObject switchAvailableObj, GameObject switchUsedObj)
        CreateCharacterSwitchArea(Transform parent)
    {
        var area = CreateChild(parent, "CharacterSwitchArea");
        Rect(area, C(0, 1), C(0, 1), C(0, 1), C(30, -30), V2(420, 120));

        var activeFrame = CreateChild(area.transform, "ActiveCharFrame");
        Rect(activeFrame, C(0, 0.5f), C(0, 0.5f), C(0, 0.5f), C(10, 0), V2(80, 80));
        activeFrame.AddComponent<Image>().color = new Color(0.2f, 0.5f, 0.8f, 0.9f);

        var activePortraitObj = CreateChild(activeFrame.transform, "Portrait");
        StretchFill(activePortraitObj, 4, 4, 4, 4);
        var activePortrait = activePortraitObj.AddComponent<Image>();
        activePortrait.color = new Color(0.3f, 0.3f, 0.35f, 1f);

        var activeNameObj = CreateChild(activeFrame.transform, "Name");
        Rect(activeNameObj, C(0.5f, 0), C(0.5f, 0), C(0.5f, 1), C(0, -2), V2(76, 16));
        var activeNameText = activeNameObj.AddComponent<TextMeshProUGUI>();
        SetupText(activeNameText, "角色1", 11, Color.white);

        var switchBtnObj = CreateChild(area.transform, "SwitchButton");
        Rect(switchBtnObj, C(0, 0.5f), C(0, 0.5f), C(0.5f, 0.5f), C(100, 0), V2(60, 40));
        switchBtnObj.AddComponent<Image>().color = new Color(0.15f, 0.3f, 0.5f, 0.9f);
        var switchButton = switchBtnObj.AddComponent<Button>();

        var switchTextObj = CreateChild(switchBtnObj.transform, "Text");
        StretchFill(switchTextObj);
        var switchText = switchTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(switchText, "切换", 16, Color.white);

        var switchAvailObj = CreateChild(switchBtnObj.transform, "AvailableIndicator");
        Rect(switchAvailObj, C(0.5f, 0), C(0.5f, 0), C(0.5f, 0), C(0, -4), V2(8, 8));
        switchAvailObj.AddComponent<Image>().color = new Color(0.2f, 0.9f, 0.3f, 1f);

        var switchUsedObj = CreateChild(switchBtnObj.transform, "UsedIndicator");
        Rect(switchUsedObj, C(0.5f, 0), C(0.5f, 0), C(0.5f, 0), C(0, -4), V2(8, 8));
        switchUsedObj.AddComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        switchUsedObj.SetActive(false);

        var inactiveFrame = CreateChild(area.transform, "InactiveCharFrame");
        Rect(inactiveFrame, C(0, 0.5f), C(0, 0.5f), C(0, 0.5f), C(180, 0), V2(64, 64));
        inactiveFrame.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f, 0.6f);

        var inactivePortraitObj = CreateChild(inactiveFrame.transform, "Portrait");
        StretchFill(inactivePortraitObj, 4, 4, 4, 4);
        var inactivePortrait = inactivePortraitObj.AddComponent<Image>();
        inactivePortrait.color = new Color(0.25f, 0.25f, 0.3f, 0.8f);

        var inactiveNameObj = CreateChild(inactiveFrame.transform, "Name");
        Rect(inactiveNameObj, C(0.5f, 0), C(0.5f, 0), C(0.5f, 1), C(0, -2), V2(60, 14));
        var inactiveNameText = inactiveNameObj.AddComponent<TextMeshProUGUI>();
        SetupText(inactiveNameText, "角色2", 10, new Color(0.7f, 0.7f, 0.7f, 1f));

        return (switchButton, activeNameText, inactiveNameText, activePortrait, inactivePortrait,
            switchAvailObj, switchUsedObj);
    }

    // ====================================================================
    // 敌人区（中上）— HP条在敌人图片正上方，不重叠；伤害数字在右侧
    // ====================================================================

    private static (TextMeshProUGUI nameText, TextMeshProUGUI hpText, TextMeshProUGUI armorText,
        TextMeshProUGUI intentText, TextMeshProUGUI damageText, Image hpBarFill) CreateEnemyArea(Transform parent)
    {
        var area = CreateChild(parent, "EnemyArea");
        Rect(area, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, 120), V2(600, 400));

        // 敌人占位图（居中偏下）
        var sprite = CreateChild(area.transform, "EnemySprite");
        Rect(sprite, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, -20), V2(200, 260));
        sprite.AddComponent<Image>().color = new Color(0.7f, 0.15f, 0.15f, 0.3f);

        // 敌人名（在HP条上方）
        var nameObj = CreateChild(area.transform, "EnemyNameText");
        Rect(nameObj, C(0.5f, 1f), C(0.5f, 1f), C(0.5f, 1f), C(0, -10), V2(300, 28));
        var nameText = nameObj.AddComponent<TextMeshProUGUI>();
        SetupText(nameText, "精英1", 22, Color.white);

        // HP条（在敌人图片正上方，留有间距不重叠）
        var hpBar = CreateChild(area.transform, "EnemyHPBar");
        Rect(hpBar, C(0.5f, 1f), C(0.5f, 1f), C(0.5f, 1f), C(0, -44), V2(340, 28));
        hpBar.AddComponent<Image>().color = new Color(0.2f, 0.08f, 0.08f, 1f);

        var hpFill = CreateChild(hpBar.transform, "EnemyHPBarFill");
        StretchFill(hpFill);
        var hpFillImg = hpFill.AddComponent<Image>();
        hpFillImg.color = new Color(0.85f, 0.3f, 0.3f, 1f);
        hpFillImg.type = Image.Type.Filled;
        hpFillImg.fillMethod = Image.FillMethod.Horizontal;

        var hpTextObj = CreateChild(hpBar.transform, "EnemyHPText");
        StretchFill(hpTextObj);
        var hpText = hpTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(hpText, "100/100", 14, Color.white);
        hpText.enableWordWrapping = false;

        // 护甲
        var armorObj = CreateChild(area.transform, "EnemyArmorText");
        Rect(armorObj, C(0.5f, 1f), C(0.5f, 1f), C(0.5f, 1f), C(0, -80), V2(200, 22));
        var armorText = armorObj.AddComponent<TextMeshProUGUI>();
        SetupText(armorText, "", 16, C(0.6f, 0.8f, 1f, 1f));

        // 伤害数字（敌人右侧）
        var damageObj = CreateChild(area.transform, "EnemyDamageText");
        Rect(damageObj, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(150, -20), V2(120, 50));
        var damageText = damageObj.AddComponent<TextMeshProUGUI>();
        SetupText(damageText, "", 36, new Color(1f, 0.3f, 0.2f, 1f));
        damageText.enableWordWrapping = false;
        damageText.gameObject.SetActive(false);

        // 意图
        var intentBg = CreateChild(area.transform, "EnemyIntentBg");
        Rect(intentBg, C(0.5f, 0), C(0.5f, 0), C(0.5f, 0), C(0, 10), V2(220, 30));
        intentBg.AddComponent<Image>().color = new Color(0.2f, 0.35f, 0.6f, 0.8f);

        var intentObj = CreateChild(intentBg.transform, "EnemyIntentText");
        StretchFill(intentObj);
        var intentText = intentObj.AddComponent<TextMeshProUGUI>();
        SetupText(intentText, "造成5伤害", 14, new Color(0.9f, 0.9f, 1f, 1f));

        return (nameText, hpText, armorText, intentText, damageText, hpFillImg);
    }

    // ====================================================================
    // 玩家区（左下）— 含抽牌堆图标（无数字）
    // ====================================================================

    private static (TextMeshProUGUI hpText, TextMeshProUGUI apText, TextMeshProUGUI armorText,
        TextMeshProUGUI strengthText, TextMeshProUGUI dexterityText, Image hpBarFill)
        CreatePlayerArea(Transform parent)
    {
        var area = CreateChild(parent, "PlayerArea");
        Rect(area, C(0, 0), C(0, 0), C(0, 0), C(30, 30), V2(400, 140));

        // HP条
        var hpBar = CreateChild(area.transform, "PlayerHPBar");
        Rect(hpBar, C(0, 1f), C(0, 1f), C(0, 1f), C(0, -10), V2(260, 28));
        hpBar.AddComponent<Image>().color = new Color(0.2f, 0.08f, 0.08f, 1f);

        var hpFill = CreateChild(hpBar.transform, "PlayerHPBarFill");
        StretchFill(hpFill);
        var hpFillImg = hpFill.AddComponent<Image>();
        hpFillImg.color = new Color(0.85f, 0.3f, 0.3f, 1f);
        hpFillImg.type = Image.Type.Filled;
        hpFillImg.fillMethod = Image.FillMethod.Horizontal;

        var hpTextObj = CreateChild(hpBar.transform, "HPText");
        StretchFill(hpTextObj);
        var hpText = hpTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(hpText, "100/100", 14, Color.white);
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.enableWordWrapping = false;

        // 行动点徽章
        var apBadge = CreateChild(area.transform, "ActionPointBadge");
        Rect(apBadge, C(0, 1f), C(0, 1f), C(0.5f, 0.5f), C(290, -24), V2(44, 44));
        apBadge.AddComponent<Image>().color = new Color(0.9f, 0.4f, 0.4f, 1f);

        var apTextObj = CreateChild(apBadge.transform, "ActionPointText");
        StretchFill(apTextObj);
        var apText = apTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(apText, "3", 22, Color.white);
        apText.enableWordWrapping = false;

        // 护甲
        var armorObj = CreateChild(area.transform, "ArmorText");
        Rect(armorObj, C(0, 1f), C(0, 1f), C(0, 1f), C(0, -44), V2(260, 22));
        var armorText = armorObj.AddComponent<TextMeshProUGUI>();
        SetupText(armorText, "", 16, C(0.6f, 0.8f, 1f, 1f));
        armorText.alignment = TextAlignmentOptions.Left;

        // 力量
        var strObj = CreateChild(area.transform, "StrengthText");
        Rect(strObj, C(0, 1f), C(0, 1f), C(0, 1f), C(0, -68), V2(260, 22));
        var strengthText = strObj.AddComponent<TextMeshProUGUI>();
        SetupText(strengthText, "", 14, C(1f, 0.5f, 0.3f, 1f));
        strengthText.alignment = TextAlignmentOptions.Left;

        // 敏捷
        var dexObj = CreateChild(area.transform, "DexterityText");
        Rect(dexObj, C(0, 1f), C(0, 1f), C(0, 1f), C(0, -90), V2(260, 22));
        var dexterityText = dexObj.AddComponent<TextMeshProUGUI>();
        SetupText(dexterityText, "", 14, C(0.3f, 0.7f, 1f, 1f));
        dexterityText.alignment = TextAlignmentOptions.Left;

        // 抽牌堆图标（图片，无数字）
        CreatePileIcon(area.transform, "DrawPileIcon", C(0, 1f), C(0, 1f), C(0, 1f), C(0, -114), new Color(0.3f, 0.5f, 0.8f, 0.7f));
        var drawLabel = CreateChild(area.transform, "DrawPileLabel");
        Rect(drawLabel, C(0, 1f), C(0, 1f), C(0, 1f), C(40, -118), V2(60, 22));
        var drawLabelText = drawLabel.AddComponent<TextMeshProUGUI>();
        SetupText(drawLabelText, "抽牌堆", 12, C(0.6f, 0.6f, 0.7f, 1f));

        return (hpText, apText, armorText, strengthText, dexterityText, hpFillImg);
    }

    // ====================================================================
    // 牌堆图标（重叠卡片视觉）
    // ====================================================================

    private static void CreatePileIcon(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 pos, Color color)
    {
        var icon = CreateChild(parent, name);
        Rect(icon, anchorMin, anchorMax, pivot, pos, V2(40, 50));

        // 后面的卡片（偏移）
        var card2 = CreateChild(icon.transform, "Card2");
        Rect(card2, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(6, -4), V2(28, 38));
        var c2Img = card2.AddComponent<Image>();
        c2Img.color = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, color.a * 0.5f);

        // 前面的卡片
        var card1 = CreateChild(icon.transform, "Card1");
        Rect(card1, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, 0), V2(28, 38));
        var c1Img = card1.AddComponent<Image>();
        c1Img.color = color;
    }

    // ====================================================================
    // 手牌区
    // ====================================================================

    private static HandCardLayout CreateHandArea(Transform parent, GameObject atk, GameObject arm, GameObject buf)
    {
        var handObj = CreateChild(parent, "HandArea");
        Rect(handObj, C(0.5f, 0), C(0.5f, 0), C(0.5f, 0), C(0, 160), V2(1200, 400));

        var layout = handObj.AddComponent<HandCardLayout>();
        var so = new SerializedObject(layout);
        so.FindProperty("cardSpacing").floatValue = 140f;
        so.FindProperty("fanRadius").floatValue = 1000f;
        so.FindProperty("maxFanAngle").floatValue = 15f;
        so.FindProperty("hoverScale").floatValue = 1.4f;
        so.FindProperty("hoverYOffset").floatValue = 100f;
        so.FindProperty("lerpSpeed").floatValue = 20f;
        so.FindProperty("attackCardPrefab").objectReferenceValue = atk;
        so.FindProperty("armorCardPrefab").objectReferenceValue = arm;
        so.FindProperty("buffCardPrefab").objectReferenceValue = buf;
        so.ApplyModifiedProperties();
        return layout;
    }

    // ====================================================================
    // 右下角（结束按钮 + 弃牌堆图标，无消耗堆）
    // ====================================================================

    private static (Button endTurnButton, TextMeshProUGUI discardPileText, TextMeshProUGUI consumedPileText)
        CreateBottomRight(Transform parent)
    {
        // 结束回合按钮
        var btnObj = CreateChild(parent, "EndTurnButton");
        Rect(btnObj, C(1f, 0), C(1f, 0), C(1f, 0), C(-110, 180), V2(200, 60));
        btnObj.AddComponent<Image>().color = new Color(0.25f, 0.45f, 0.75f, 0.95f);
        var endTurnButton = btnObj.AddComponent<Button>();

        var btnTextObj = CreateChild(btnObj.transform, "Text");
        StretchFill(btnTextObj);
        var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        SetupText(btnText, "结束行动", 20, Color.white);

        // 弃牌堆图标（图片，无数字）
        CreatePileIcon(parent, "DiscardPileIcon", C(1f, 0), C(1f, 0), C(1f, 0), C(-110, 110), new Color(0.5f, 0.3f, 0.8f, 0.7f));
        var discardLabel = CreateChild(parent, "DiscardPileLabel");
        Rect(discardLabel, C(1f, 0), C(1f, 0), C(1f, 0), C(-110, 80), V2(80, 22));
        var discardLabelText = discardLabel.AddComponent<TextMeshProUGUI>();
        SetupText(discardLabelText, "弃牌堆", 12, C(0.6f, 0.6f, 0.7f, 1f));

        return (endTurnButton, null, null);
    }

    // ====================================================================
    // 顶部栏（只有阶段提示，无回合数）
    // ====================================================================

    private static (TextMeshProUGUI turnText, TextMeshProUGUI phaseHintText) CreateTopBar(Transform parent)
    {
        var phaseObj = CreateChild(parent, "PhaseHintText");
        Rect(phaseObj, C(0.5f, 0.5f), C(0.5f, 0.5f), C(0.5f, 0.5f), C(0, -30), V2(400, 30));
        var phaseHintText = phaseObj.AddComponent<TextMeshProUGUI>();
        SetupText(phaseHintText, "", 18, C(1f, 0.85f, 0.3f, 1f));

        return (null, phaseHintText);
    }

    // ====================================================================
    // 设置按钮（右上角）
    // ====================================================================

    private static Button CreateSettingsButton(Transform parent)
    {
        var btnObj = CreateChild(parent, "SettingsButton");
        Rect(btnObj, C(1f, 1f), C(1f, 1f), C(1f, 1f), C(-60, -40), V2(80, 44));
        btnObj.AddComponent<Image>().color = new Color(0.9f, 0.6f, 0.15f, 0.95f);
        var settingsButton = btnObj.AddComponent<Button>();

        var textObj = CreateChild(btnObj.transform, "Text");
        StretchFill(textObj);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        SetupText(text, "设置", 18, Color.black);

        return settingsButton;
    }

    // ====================================================================
    // 结果面板
    // ====================================================================

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

    private static GameObject LoadPrefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            Debug.LogError($"[BattleSceneBuilder] 无法加载 Prefab: {path}");
        return prefab;
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
