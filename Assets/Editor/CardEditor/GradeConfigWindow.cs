using System.IO;
using UnityEditor;
using UnityEngine;
using LightMiniGame.CardEditor;

namespace LightMiniGame.CardEditor.Editor
{
    /// <summary>
    /// 品级全局配置窗口 —— 独立于卡牌编辑器，调整每个品级的价格和刷新权重
    /// </summary>
    public class GradeConfigWindow : EditorWindow
    {
        private GradeConfig _config;
        private Vector2 _scroll;

        [MenuItem("Tools/卡牌编辑器/品级配置")]
        public static void Open()
        {
            var window = GetWindow<GradeConfigWindow>("品级配置");
            window.minSize = new Vector2(500, 350);
        }

        private void OnEnable()
        {
            _config = GradeConfig.Load();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("品级全局配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("此处配置每个品级的商店价格和刷新权重。\n价格和权重不写死在卡牌中，由战斗/商店系统读取此配置。", MessageType.Info);

            if (_config == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("未找到 GradeConfig 资产。\n点击下方按钮在 Resources/CardEditor/ 下创建。", MessageType.Warning);
                if (GUILayout.Button("创建 GradeConfig 资产", GUILayout.Height(30)))
                {
                    CreateGradeConfigAsset();
                }
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // 表头
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField("品级", GUILayout.Width(60));
            EditorGUILayout.LabelField("商店基础价格", GUILayout.Width(100));
            EditorGUILayout.LabelField("商店刷新权重", GUILayout.Width(100));
            EditorGUILayout.LabelField("奖励刷新权重", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // 确保三个品级都存在
            EnsureGradeEntries();

            foreach (var entry in _config.gradeConfigs)
            {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField(CardEntry.GetGradeName(entry.grade), GUILayout.Width(60));
                entry.shopBasePrice = EditorGUILayout.IntField(entry.shopBasePrice, GUILayout.Width(100));
                entry.shopRefreshWeight = EditorGUILayout.FloatField(entry.shopRefreshWeight, GUILayout.Width(100));
                entry.rewardRefreshWeight = EditorGUILayout.FloatField(entry.rewardRefreshWeight, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            if (GUILayout.Button("保存配置", GUILayout.Height(30)))
            {
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();
                Debug.Log("[GradeConfig] 配置已保存");
            }
        }

        private void EnsureGradeEntries()
        {
            var grades = new[] { CardGrade.Bronze, CardGrade.Silver, CardGrade.Gold };
            foreach (var g in grades)
            {
                if (!_config.gradeConfigs.Exists(e => e.grade == g))
                    _config.gradeConfigs.Add(new GradeConfigEntry { grade = g });
            }
        }

        private void CreateGradeConfigAsset()
        {
            var dir = "Assets/Resources/CardEditor";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var path = $"{dir}/GradeConfig.asset";
            _config = CreateInstance<GradeConfig>();
            AssetDatabase.CreateAsset(_config, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(_config);
        }
    }
}
