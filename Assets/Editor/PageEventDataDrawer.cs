using UnityEditor;
using UnityEngine;

/// <summary>
/// PageEventData 的自定义属性绘制器：
/// 1. 每个 event 在 Inspector 里显示为可折叠标题（用 eventId 作为标题）。
/// 2. Event 类型显示 options 字段；Rest 类型显示休整回复百分比；Battle/Shop 类型显示 defaultEffects 字段。
/// 3. description / displayName 等核心字段对任意 EventType 都强制显示（不会被类型切换逻辑隐藏）。
/// </summary>
[CustomPropertyDrawer(typeof(PageEventData))]
public class PageEventDataDrawer : PropertyDrawer
{
    private static readonly float Spacing = EditorGUIUtility.standardVerticalSpacing;
    private static readonly float HeaderHeight = EditorGUIUtility.singleLineHeight;

    // 任意类型都始终显示的字段（含 description，确保 Event 类型也显示描述）
    private static readonly string[] AlwaysFields =
    {
        "eventId",
        "displayName",
        "description",
        "icon",
        "eventType",
        "isRepeatable",
        "isFinalNode",
        "mutuallyExclusiveIds",
        "followUpIds",
        "prerequisiteIds",
    };

    // Battle 系列配置字段（所有类型都绘制，无害；如需 Event 隐藏可在此剔除）
    private static readonly string[] BattleFields =
    {
        "enemies",
        "normalBattleBackground",
        "lowSanityBattleBackground",
        "backgroundSanityThreshold",
    };

    // 仅 Battle 类型才显示的字段（掉落物表：只有战斗事件可配置）
    private static readonly string[] BattleOnlyFields =
    {
        "lootTable",
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var eventIdProp = property.FindPropertyRelative("eventId");
        var eventTypeProp = property.FindPropertyRelative("eventType");

        string title = !string.IsNullOrEmpty(eventIdProp.stringValue)
            ? eventIdProp.stringValue
            : "未命名事件";

        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, position.y, position.width, HeaderHeight),
            property.isExpanded, title, true);

        if (property.isExpanded)
        {
            bool isEvent = eventTypeProp != null
                && eventTypeProp.enumValueIndex == (int)PageEventType.Event;
            bool isRest = eventTypeProp != null
                && eventTypeProp.enumValueIndex == (int)PageEventType.Rest;
            bool isBattle = eventTypeProp != null
                && eventTypeProp.enumValueIndex == (int)PageEventType.Battle;

            EditorGUI.indentLevel++;

            float y = position.y + HeaderHeight + Spacing;

            // —— 始终显示的字段（含 description）——
            foreach (var name in AlwaysFields)
            {
                var prop = property.FindPropertyRelative(name);
                if (prop == null) continue;
                y = DrawProperty(position, y, prop);
            }

            // —— 类型相关：Event 显示 options；Rest 显示回复百分比；Battle/Shop 显示 defaultEffects ——
            SerializedProperty typeSpecific = null;
            if (isEvent)
                typeSpecific = property.FindPropertyRelative("options");
            else if (isRest)
                typeSpecific = property.FindPropertyRelative("restHealingPercent");
            else
                typeSpecific = property.FindPropertyRelative("defaultEffects");
            if (typeSpecific != null)
                y = DrawProperty(position, y, typeSpecific);

            // —— Battle 配置字段 ——
            foreach (var name in BattleFields)
            {
                var prop = property.FindPropertyRelative(name);
                if (prop == null) continue;
                y = DrawProperty(position, y, prop);
            }

            // —— 仅 Battle 类型显示的字段（掉落物表等）——
            if (isBattle)
            {
                foreach (var name in BattleOnlyFields)
                {
                    var prop = property.FindPropertyRelative(name);
                    if (prop == null) continue;
                    y = DrawProperty(position, y, prop);
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    /// <summary>绘制单个属性并推进 y 坐标（正确计算多行 [TextArea] 高度）。</summary>
    private static float DrawProperty(Rect position, float y, SerializedProperty prop)
    {
        float h = EditorGUI.GetPropertyHeight(prop, true);
        var rect = new Rect(position.x, y, position.width, h);
        EditorGUI.PropertyField(rect, prop, true);
        return y + h + Spacing;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return HeaderHeight;

        var eventTypeProp = property.FindPropertyRelative("eventType");
        bool isEvent = (eventTypeProp != null
            && eventTypeProp.enumValueIndex == (int)PageEventType.Event);
        bool isRest = (eventTypeProp != null
            && eventTypeProp.enumValueIndex == (int)PageEventType.Rest);
        bool isBattle = (eventTypeProp != null
            && eventTypeProp.enumValueIndex == (int)PageEventType.Battle);

        float height = HeaderHeight + Spacing;

        foreach (var name in AlwaysFields)
        {
            var prop = property.FindPropertyRelative(name);
            if (prop == null) continue;
            height += EditorGUI.GetPropertyHeight(prop, true) + Spacing;
        }

        SerializedProperty typeSpecific = null;
        if (isEvent)
            typeSpecific = property.FindPropertyRelative("options");
        else if (isRest)
            typeSpecific = property.FindPropertyRelative("restHealingPercent");
        else
            typeSpecific = property.FindPropertyRelative("defaultEffects");
        if (typeSpecific != null)
            height += EditorGUI.GetPropertyHeight(typeSpecific, true) + Spacing;

        foreach (var name in BattleFields)
        {
            var prop = property.FindPropertyRelative(name);
            if (prop == null) continue;
            height += EditorGUI.GetPropertyHeight(prop, true) + Spacing;
        }

        if (isBattle)
        {
            foreach (var name in BattleOnlyFields)
            {
                var prop = property.FindPropertyRelative(name);
                if (prop == null) continue;
                height += EditorGUI.GetPropertyHeight(prop, true) + Spacing;
            }
        }

        return height;
    }
}
