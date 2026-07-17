using UnityEditor;
using UnityEngine;

/// <summary>
/// PageEventData 的自定义属性绘制器：
/// 1. 每个 event 在 Inspector 里显示为可折叠标题（用 eventId 作为标题）。
/// 2. Event 类型显示 options 字段；Battle/Shop/Rest 类型显示 defaultEffects 字段。
/// </summary>
[CustomPropertyDrawer(typeof(PageEventData))]
public class PageEventDataDrawer : PropertyDrawer
{
    private static readonly float Spacing = EditorGUIUtility.standardVerticalSpacing;
    private static readonly float HeaderHeight = EditorGUIUtility.singleLineHeight;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var eventIdProp = property.FindPropertyRelative("eventId");
        var eventTypeProp = property.FindPropertyRelative("eventType");

        // —— 折叠标题行（普通 Foldout，不嵌套 List 字段内部的 header）——
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

            EditorGUI.indentLevel++;

            float y = position.y + HeaderHeight + Spacing;

            var prop = property.Copy();
            var end = property.GetEndProperty();
            bool enterChildren = true;

            while (prop.NextVisible(enterChildren) && !SerializedProperty.EqualContents(prop, end))
            {
                enterChildren = false;

                // Event 类型显示 options；非 Event 类型显示 defaultEffects
                if (prop.name == "options" && !isEvent)
                    continue;
                if (prop.name == "defaultEffects" && isEvent)
                    continue;

                float h = EditorGUI.GetPropertyHeight(prop, true);
                var rect = new Rect(position.x, y, position.width, h);
                EditorGUI.PropertyField(rect, prop, true);
                y += h + Spacing;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return HeaderHeight;

        var eventTypeProp = property.FindPropertyRelative("eventType");
        bool isEvent = eventTypeProp != null
            && eventTypeProp.enumValueIndex == (int)PageEventType.Event;

        float height = HeaderHeight + Spacing;

        var prop = property.Copy();
        var end = property.GetEndProperty();
        bool enterChildren = true;

        while (prop.NextVisible(enterChildren) && !SerializedProperty.EqualContents(prop, end))
        {
            enterChildren = false;

            if (prop.name == "options" && !isEvent)
                continue;
            if (prop.name == "defaultEffects" && isEvent)
                continue;

            height += EditorGUI.GetPropertyHeight(prop, true) + Spacing;
        }

        return height;
    }
}
