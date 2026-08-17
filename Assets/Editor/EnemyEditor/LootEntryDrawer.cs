using UnityEditor;
using UnityEngine;

/// <summary>
/// LootEntry 的自定义属性绘制器：根据 kind 只显示对应类型的配置字段。
///   - Currency：仅 currencyAmount
///   - Card：仅 cardRarities / cardDrawCount / cardPickCount
///   - Relic：仅 relicRarities
/// 通过 [CustomPropertyDrawer] 自动应用到所有展示 LootEntry 的地方（EnemyConfig 掉落表等）。
/// </summary>
[CustomPropertyDrawer(typeof(LootEntry))]
public class LootEntryDrawer : PropertyDrawer
{
    private const float LineHeight = 18f;
    private const float Spacing = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var kindProp = property.FindPropertyRelative("kind");
        string kindLabel = kindProp.enumValueIndex switch
        {
            0 => "金币",
            1 => "卡牌",
            2 => "遗物",
            _ => "未知"
        };

        // 折叠头：显示 kind 作为标题
        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, position.y, position.width, LineHeight),
            property.isExpanded,
            new GUIContent($"掉落项 ({kindLabel})"),
            true);

        if (!property.isExpanded) return;

        float y = position.y + LineHeight + Spacing;
        float x = position.x;

        // kind 枚举（始终显示）
        DrawField(ref y, x, position.width, kindProp, "掉落类型");

        // 按 kind 只显示对应字段
        var kind = (LootEntry.LootKind)kindProp.enumValueIndex;
        switch (kind)
        {
            case LootEntry.LootKind.Currency:
                DrawField(ref y, x, position.width, property.FindPropertyRelative("currencyAmount"), "金币数量");
                break;

            case LootEntry.LootKind.Card:
                DrawField(ref y, x, position.width, property.FindPropertyRelative("cardRarities"), "卡牌品级");
                DrawField(ref y, x, position.width, property.FindPropertyRelative("cardDrawCount"), "抽取数量");
                DrawField(ref y, x, position.width, property.FindPropertyRelative("cardPickCount"), "可选数量");
                break;

            case LootEntry.LootKind.Relic:
                DrawField(ref y, x, position.width, property.FindPropertyRelative("relicRarities"), "遗物品级");
                break;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 折叠头始终占一行
        float height = LineHeight;

        if (!property.isExpanded) return height;

        height += Spacing;

        // kind 始终显示
        height += LineHeight + Spacing;

        var kindProp = property.FindPropertyRelative("kind");
        var kind = (LootEntry.LootKind)kindProp.enumValueIndex;

        switch (kind)
        {
            case LootEntry.LootKind.Currency:
                height += GetFieldHeight(property.FindPropertyRelative("currencyAmount"));
                break;
            case LootEntry.LootKind.Card:
                height += GetFieldHeight(property.FindPropertyRelative("cardRarities"));
                height += GetFieldHeight(property.FindPropertyRelative("cardDrawCount"));
                height += GetFieldHeight(property.FindPropertyRelative("cardPickCount"));
                break;
            case LootEntry.LootKind.Relic:
                height += GetFieldHeight(property.FindPropertyRelative("relicRarities"));
                break;
        }

        return height;
    }

    private void DrawField(ref float y, float x, float width, SerializedProperty prop, string label)
    {
        if (prop == null) return;
        var h = EditorGUI.GetPropertyHeight(prop, new GUIContent(label), true);
        EditorGUI.PropertyField(new Rect(x, y, width, h), prop, new GUIContent(label), true);
        y += h + Spacing;
    }

    private float GetFieldHeight(SerializedProperty prop)
    {
        if (prop == null) return 0f;
        return EditorGUI.GetPropertyHeight(prop, true) + Spacing;
    }
}
