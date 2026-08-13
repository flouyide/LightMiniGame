using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EnemyConfig 的自定义 Inspector：把字段标签翻译成中文。
/// 项目入口：Unity 默认 Project Inspector + 弹窗 `EnemyConfigEditorWindow` 都会用上（Editor.CreateEditor 自动走 CustomEditor）。
/// </summary>
[CustomEditor(typeof(EnemyConfig))]
public class EnemyConfigEditor : Editor
{
    // 字段名 → 中文标签（与 EnemyConfig 字段一一对应）。[Tooltip] 保留原 Tooltip 文本。
    private static readonly Dictionary<string, string> Labels = new()
    {
        // —— 基础信息 ——
        { "enemyName",       "敌人名称" },
        { "maxHP",           "高理智最大生命值" },
        { "phase2MaxHP",     "低理智最大生命值" },
        { "armor",           "初始护甲" },
        { "phase1Portrait",  "高理智立绘" },
        { "phase2Portrait",  "低理智立绘" },

        // —— 阶段1/2 卡组 ——
        { "phase1Skills",      "高理智卡组列表" },
        { "phase2Skills",      "低理智卡组列表" },

        // —— 5.3 文档扩展字段 ——
        { "difficulty",             "难度类型" },
        { "actionPriority",         "出招优先级" },
        { "strength",               "力量" },
        { "dexterity",                "敏捷" },
        { "damageTakenMultiplier",  "受到伤害倍率" },
        { "damageDealtMultiplier",  "造成伤害倍率" },
        { "highSanityCardCount",    "高理智出招数" },
        { "lowSanityCardCount",     "低理智出招数" },
        { "abilities",              "能力列表" },
        { "lootTable",               "掉落物表" },
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 走序列化迭代，自动处理 Header / Foldout / 嵌套 List 等，
        // 仅用字典把字段名替换成中文标签。
        var it = serializedObject.GetIterator();
        // 进入第一个子属性（跳过 m_Script）
        if (!it.NextVisible(true)) { serializedObject.ApplyModifiedProperties(); return; }

        do
        {
            if (it.name == "m_Script") continue;
            var label = Labels.TryGetValue(it.name, out var zh) ? new GUIContent(zh) : new GUIContent(it.name);
            EditorGUILayout.PropertyField(it, label, true);
        } while (it.NextVisible(false));

        serializedObject.ApplyModifiedProperties();
    }
}