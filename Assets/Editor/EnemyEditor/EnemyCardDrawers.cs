using UnityEditor;
using UnityEngine;

/// <summary>
/// 敌人卡片网格单元绘制（左/中/右 3 列，单卡固定宽 ~300 高 ~180）。
/// 选中/未选中样式通过 EnemyEditorStyles.CardStyle/CardSelectedStyle 区分。
/// </summary>
public static class EnemyCardDrawers
{
    /// <summary>绘制单张敌人卡片。点击名字调用 onSelect（选中）；编辑按钮调用 onEdit（弹窗）；定位按钮调用 onPing。</summary>
    public static void DrawGridCardCell(
        EnemyConfig cfg, bool selected, System.Action onSelect, System.Action onEdit, System.Action onPing)
    {
        var style = selected ? EnemyEditorStyles.CardSelectedStyle : EnemyEditorStyles.CardStyle;
        using (new EditorGUILayout.VerticalScope(style, GUILayout.Width(280), GUILayout.Height(180)))
        {
            // 顶部：难度色条 + 立绘缩略图 + 名字
            using (new EditorGUILayout.HorizontalScope())
            {
                // 难度色条（左侧 8px）
                var barRect = GUILayoutUtility.GetRect(8, 60, GUILayout.Width(8), GUILayout.Height(60));
                EditorGUI.DrawRect(barRect, EnemyEditorStyles.DifficultyColor(cfg.difficulty));

                var portrait = cfg.phase1Portrait != null ? cfg.phase1Portrait.texture : null;
                var iconRect = GUILayoutUtility.GetRect(60, 60, GUILayout.Width(60), GUILayout.Height(60));
                GUI.DrawTexture(iconRect, portrait ?? Texture2D.grayTexture, ScaleMode.ScaleToFit);

                using (new EditorGUILayout.VerticalScope())
                {
                    var name = string.IsNullOrEmpty(cfg.enemyName) ? "(未命名)" : cfg.enemyName;
                    var prev = GUI.color;
                    GUI.color = selected ? Color.white : EnemyEditorStyles.TextPrimary;
                    if (GUILayout.Button(name, EditorStyles.boldLabel)) { onSelect?.Invoke(); }
                    GUI.color = prev;
                    GUILayout.Label($"[{cfg.difficulty}]", EditorStyles.miniLabel);
                }
            }

            // 中部：HP / 技能数
            GUILayout.Label($"HP {cfg.maxHP}/{cfg.maxHP} (阶段2: {cfg.phase2MaxHP})", EditorStyles.miniLabel);
            GUILayout.Label($"技能 阶段1:{cfg.phase1Skills?.Count ?? 0}  阶段2:{cfg.phase2Skills?.Count ?? 0}", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            // 底部：按钮
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("编辑", GUILayout.Width(60))) onEdit?.Invoke();
                if (GUILayout.Button("定位", GUILayout.Width(50))) onPing?.Invoke();
            }
        }
    }
}