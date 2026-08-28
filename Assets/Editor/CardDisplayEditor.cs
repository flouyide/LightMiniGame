using UnityEditor;
using UnityEngine;

/// <summary>
/// CardDisplay 自定义Inspector —— 根据卡牌类型只显示相关字段，隐藏无关配置
/// </summary>
[CustomEditor(typeof(CardDisplay))]
public class CardDisplayEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var card = (CardDisplay)target;

        // === 基础信息 ===
        EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);

        // 卡牌类型：只读显示，不可更改（由Prefab模板决定）
        GUI.enabled = false;
        EditorGUILayout.EnumPopup("Card Type (只读)", card.cardType);
        GUI.enabled = true;

        card.cardName = EditorGUILayout.TextField("Card Name", card.cardName);
        card.cardArt = (Sprite)EditorGUILayout.ObjectField("Card Art", card.cardArt, typeof(Sprite), false);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));

        EditorGUILayout.Space();

        // === 通用属性 ===
        EditorGUILayout.LabelField("通用属性", EditorStyles.boldLabel);
        card.value = EditorGUILayout.IntField("Value (商店价值)", card.value);
        card.grade = (CardGrade)EditorGUILayout.EnumPopup("Grade (品级)", card.grade);
        card.actionPointCost = EditorGUILayout.IntField("Action Point Cost", card.actionPointCost);
        card.consumeType = (ConsumeType)EditorGUILayout.EnumPopup("Consume Type", card.consumeType);
        int kwMask = EditorGUILayout.MaskField("词条（可多选）", (int)card.keywords, CardKeywords.FlagMaskNames);
        card.keywords = (KeywordType)kwMask;
        var kwTip = CardKeywords.GetTooltip(card.keywords);
        if (!string.IsNullOrEmpty(kwTip))
            EditorGUILayout.HelpBox(kwTip, MessageType.None);

        EditorGUILayout.Space();

        serializedObject.Update();

        // === 按类型显示专属属性 ===
        switch (card.cardType)
        {
            case CardType.Attack:
                DrawAttackFields(card);
                break;
            case CardType.Skill:
                DrawArmorFields(card);
                break;
            case CardType.Ability:
                DrawBuffFields(card);
                break;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("UI 引用（Prefab 内部绑定）", EditorStyles.boldLabel);

        // 绘制所有 UI 引用字段（nameText/descText/costText 等）
        var nameTextProp = serializedObject.FindProperty("nameText");
        var descTextProp = serializedObject.FindProperty("descText");
        var costTextProp = serializedObject.FindProperty("costText");
        var typeTextProp = serializedObject.FindProperty("typeText");
        var keywordTextProp = serializedObject.FindProperty("keywordText");
        var gradeTextProp = serializedObject.FindProperty("gradeText");
        var frameImageProp = serializedObject.FindProperty("frameImage");
        var bgImageProp = serializedObject.FindProperty("backgroundImage");
        var artImageProp = serializedObject.FindProperty("artImage");
        var descBoxImageProp = serializedObject.FindProperty("descBoxImage");
        var typeBadgeProp = serializedObject.FindProperty("typeBadgeImage");
        var costBadgeProp = serializedObject.FindProperty("costBadgeImage");

        EditorGUILayout.PropertyField(nameTextProp);
        EditorGUILayout.PropertyField(descTextProp);
        EditorGUILayout.PropertyField(costTextProp);
        EditorGUILayout.PropertyField(typeTextProp);
        EditorGUILayout.PropertyField(keywordTextProp);
        EditorGUILayout.PropertyField(gradeTextProp);
        EditorGUILayout.PropertyField(frameImageProp);
        EditorGUILayout.PropertyField(bgImageProp);
        EditorGUILayout.PropertyField(artImageProp);
        EditorGUILayout.PropertyField(descBoxImageProp);
        EditorGUILayout.PropertyField(typeBadgeProp);
        EditorGUILayout.PropertyField(costBadgeProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("词条图标", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("keywordIconContainer"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("keywordIconLibrary"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("keywordIconSprites"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("keywordIconSize"), new GUIContent("图标大小"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("keywordIconSpacing"), new GUIContent("图标间距"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("词条悬浮提示", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("keywordTooltip"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tooltipText"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tooltipBgColor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tooltipTextColor"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("类型颜色", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackColor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("armorColor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("buffColor"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("品级颜色", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bronzeColor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("silverColor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("goldColor"));

        serializedObject.ApplyModifiedProperties();

        // 手动触发刷新
        if (GUI.changed)
        {
            card.UpdateDisplay();
            EditorUtility.SetDirty(target);
        }
    }

    private void DrawAttackFields(CardDisplay card)
    {
        EditorGUILayout.LabelField("攻击属性", EditorStyles.boldLabel);
        card.attackCount = EditorGUILayout.IntField("Attack Count (攻击次数)", card.attackCount);
        card.attackValueType = (ValueType)EditorGUILayout.EnumPopup("Attack Value Type", card.attackValueType);
        card.attackValue = EditorGUILayout.IntField("Attack Value (基础攻击数值)", card.attackValue);

        if (card.attackValueType == ValueType.AttributeBased)
        {
            card.attackAttribute = (PlayerAttributeType)EditorGUILayout.EnumPopup(
                "Attack Attribute (附加属性)", card.attackAttribute);
        }

        card.ignoreArmor = EditorGUILayout.Toggle("Ignore Armor (无视护甲)", card.ignoreArmor);
    }

    private void DrawArmorFields(CardDisplay card)
    {
        EditorGUILayout.LabelField("护甲属性", EditorStyles.boldLabel);
        card.armorValueType = (ValueType)EditorGUILayout.EnumPopup("Armor Value Type", card.armorValueType);
        card.armorValue = EditorGUILayout.IntField("Armor Value (基础护甲值)", card.armorValue);

        if (card.armorValueType == ValueType.AttributeBased)
        {
            card.armorAttribute = (PlayerAttributeType)EditorGUILayout.EnumPopup(
                "Armor Attribute (附加属性)", card.armorAttribute);
        }
    }

    private void DrawBuffFields(CardDisplay card)
    {
        EditorGUILayout.LabelField("增益属性", EditorStyles.boldLabel);
        card.buffDuration = (BuffDurationType)EditorGUILayout.EnumPopup("Buff Duration (增益时效)", card.buffDuration);

        if (card.buffDuration == BuffDurationType.BattleXTurns)
        {
            card.buffDurationTurns = EditorGUILayout.IntField("Buff Duration Turns (回合数)", card.buffDurationTurns);
        }

        card.buffStacks = EditorGUILayout.IntField("Buff Stacks (增益层数)", card.buffStacks);

        EditorGUILayout.Space();

        // Buff Effects 列表
        EditorGUILayout.LabelField("Buff Effects (增益效果列表)", EditorStyles.boldLabel);

        if (card.buffEffects == null)
            card.buffEffects = new System.Collections.Generic.List<BuffEffect>();

        for (int i = 0; i < card.buffEffects.Count; i++)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"效果 {i + 1}", EditorStyles.miniBoldLabel);

            var effect = card.buffEffects[i];
            effect.effectType = (BuffEffectType)EditorGUILayout.EnumPopup("Effect Type", effect.effectType);
            effect.value = EditorGUILayout.IntField("Value", effect.value);

            if (effect.effectType == BuffEffectType.IncreaseAttribute)
            {
                effect.targetAttribute = (PlayerAttributeType)EditorGUILayout.EnumPopup(
                    "Target Attribute", effect.targetAttribute);
            }

            if (GUILayout.Button("删除此效果", EditorStyles.miniButton))
            {
                card.buffEffects.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        if (GUILayout.Button("+ 添加增益效果"))
        {
            card.buffEffects.Add(new BuffEffect());
        }
    }
}
