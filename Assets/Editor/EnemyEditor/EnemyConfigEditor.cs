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
        { "maxHP",           "最大生命值" },
        { "phase2MaxHP",     "阶段2最大生命值（0=不切阶段）" },
        { "armor",           "初始护甲" },
        { "phase1Portrait",  "阶段1立绘（注视形态）" },
        { "phase2Portrait",  "阶段2立绘（睁眼形态）" },

        // —— 阶段切换 ——
        { "phase2HPThresholdPercent", "HP低于此百分比时进入阶段2" },
        { "phase2SanityThreshold",    "玩家理智低于等于此值时进入阶段2" },
        { "gazeMaxValue",             "凝视值上限（达到触发特殊技能）" },

        // —— 阶段1/2 技能 ——
        { "phase1Skills",      "阶段1技能列表（按回合顺序循环执行）" },
        { "phase2Skills",      "阶段2常规技能（每回合执行）" },
        { "phase2GazeSkill",   "阶段2凝视值满时触发的技能" },

        // —— 5.3 文档扩展字段 ——
        { "difficulty",             "难度类型" },
        { "actionPriority",         "出招优先级（1最高，同值随机；运行时由 SpawnInfo.actionOrder 决定）" },
        { "strength",               "力量" },
        { "agility",                "敏捷" },
        { "inCritRate",             "被暴击率（百分比，0=5%默认）" },
        { "damageTakenMultiplier",  "受到伤害倍率（1=正常）" },
        { "damageDealtMultiplier",  "造成伤害倍率（1=正常）" },
        { "highSanityCardCount",    "高理智出招数（0=全部轮转）" },
        { "lowSanityCardCount",     "低理智出招数（0=全部轮转）" },
        { "abilities",              "能力列表（精英/boss 敌人遗物）" },
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