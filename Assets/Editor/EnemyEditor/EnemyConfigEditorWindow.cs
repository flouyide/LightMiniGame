using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 敌人配置编辑弹窗：用 Unity 默认 ScriptableObject Inspector 显示 EnemyConfig 全部字段。
/// 同一个 EnemyConfig 只能弹出一个窗口（再点"编辑"会聚焦已存在的窗口）。
/// </summary>
public class EnemyConfigEditorWindow : EditorWindow
{
    private EnemyConfig _target;
    private Editor _editor;
    private Vector2 _scroll;

    // 按 EnemyConfig 实例去重：同一个资产全局只开一个窗口
    private static readonly Dictionary<EnemyConfig, EnemyConfigEditorWindow> _openWindows = new();

    /// <summary>打开或聚焦已存在的窗口。同一 EnemyConfig 只会有一个窗口。</summary>
    public static void OpenOrFocus(EnemyConfig cfg)
    {
        if (cfg == null) return;

        // 已存在 → 聚焦
        if (_openWindows.TryGetValue(cfg, out var existing) && existing != null)
        {
            existing.Focus();
            return;
        }

        // 创建新窗口
        var win = CreateInstance<EnemyConfigEditorWindow>();
        win._target = cfg;
        win.titleContent = new GUIContent(string.IsNullOrEmpty(cfg.enemyName) ? "敌人编辑" : cfg.enemyName);
        win._editor = Editor.CreateEditor(cfg);
        _openWindows[cfg] = win;
        win.Show();
    }

    private void OnGUI()
    {
        if (_target == null)
        {
            Close();
            return;
        }

        // 标题栏
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField(_target.enemyName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("定位资产", EditorStyles.toolbarButton))
                EditorGUIUtility.PingObject(_target);
        }

        // 默认 Inspector（含全部字段：基础/阶段/技能/扩展字段/能力/掉落物）
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        if (_editor == null) _editor = Editor.CreateEditor(_target);
        _editor.OnInspectorGUI();
        EditorGUILayout.EndScrollView();

        if (GUI.changed)
            EditorUtility.SetDirty(_target);
    }

    private void OnDestroy()
    {
        // 从字典移除（仅当字典里记录的是自己时才移除，防止竞态）
        if (_target != null
            && _openWindows.TryGetValue(_target, out var w)
            && w == this)
        {
            _openWindows.Remove(_target);
        }
        if (_editor != null)
            DestroyImmediate(_editor);
    }
}