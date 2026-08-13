using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 敌人编辑器主窗口 —— 视觉对齐菜谱管理系统 v4.0（深色，左分类 + 顶部搜索 + 右侧网格）。
/// Step 2（骨架）：菜单 + 工具栏 + 左分类树 + 右侧列表 + 选中联动 + 基础 CRUD。Step 3 会把右侧升级为 3 列网格卡片 + 详情面板。
/// </summary>
public class EnemyEditorWindow : EditorWindow
{
    // === 状态 ===
    private List<EnemyConfig> _allEnemies = new List<EnemyConfig>();
    private EnemyConfig _selected;
    private string _searchQuery = "";
    private Vector2 _leftScroll, _rightScroll;

    // === 分类筛选 ===
    private enum Category { All, Weak, Strong, Elite, Boss, Incomplete, ByDirectory }
    private Category _activeCategory = Category.All;
    private string _activeDirectory = null;  // 当 _activeCategory = ByDirectory 时使用的子目录路径

    private List<string> _subDirectories = new List<string>();

    // === 详情面板（SerializedObject + 折叠状态） ===
    private SerializedObject _selectedSO;
    private bool _foldBasic = true, _foldStats = true, _foldSkillDecks = true, _foldAbilities = true, _foldLoot = true;

    [MenuItem("Tools/敌人编辑器/Enemy Editor")]
    public static void Open()
    {
        var win = GetWindow<EnemyEditorWindow>("敌人编辑器");
        win.minSize = new Vector2(1100, 680);
        win.RefreshAssets();
    }

    private void OnEnable()
    {
        EnemyEditorStyles.Init();
        RefreshAssets();
    }

    private void RefreshAssets()
    {
        _allEnemies = EnemyAssetStore.LoadAll();
        _subDirectories = EnemyAssetStore.GetSubDirectories();
        // 当前选中丢失
        if (_selected != null && !_allEnemies.Contains(_selected)) _selected = null;
        RebuildSerializedObject();
        Repaint();
    }

    private void RebuildSerializedObject()
    {
        if (_selected != null)
        {
            _selectedSO = new SerializedObject(_selected);
        }
        else
        {
            _selectedSO = null;
        }
    }

    // ========================================================================
    // 主布局
    // ========================================================================

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EnemyEditorStyles.ToolbarStyle))
        {
            if (GUILayout.Button("新建", EnemyEditorStyles.ToolbarButtonStyle, GUILayout.Width(60)))
            {
                var created = EnemyAssetStore.Create("NewEnemy");
                if (created != null) { RefreshAssets(); _selected = created; }
            }
            using (new EditorGUI.DisabledScope(_selected == null))
            {
                if (GUILayout.Button("复制", EnemyEditorStyles.ToolbarButtonStyle, GUILayout.Width(60)))
                {
                    var copy = EnemyAssetStore.Duplicate(_selected);
                    if (copy != null) { RefreshAssets(); _selected = copy; }
                }
                if (GUILayout.Button("删除", EnemyEditorStyles.ToolbarButtonStyle, GUILayout.Width(60)))
                {
                    if (EnemyAssetStore.Delete(_selected)) { _selected = null; RefreshAssets(); }
                }
            }
            if (GUILayout.Button("批量验证", EnemyEditorStyles.ToolbarButtonStyle, GUILayout.Width(80))) RunBatchValidation();
            using (new EditorGUI.DisabledScope(_selected == null))
            {
                if (GUILayout.Button("打开资产位置", EnemyEditorStyles.ToolbarButtonStyle, GUILayout.Width(100)))
                    EnemyAssetStore.Ping(_selected);
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label($"全部 {_allEnemies.Count} 项", EditorStyles.toolbarButton, GUILayout.Width(80));
            _searchQuery = GUILayout.TextField(_searchQuery ?? "", EnemyEditorStyles.SearchFieldStyle, GUILayout.Width(220));
            if (GUILayout.Button("刷新", EnemyEditorStyles.ToolbarButtonStyle, GUILayout.Width(50))) RefreshAssets();
        }
    }

    private void DrawLeftPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(180)))
        {
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            DrawCategoryItem("全部", Category.All, _allEnemies.Count.ToString() + " 项");
            DrawCategoryItem("弱怪", Category.Weak, CountByDifficulty(Difficulty.Weak).ToString() + " 项");
            DrawCategoryItem("强怪", Category.Strong, CountByDifficulty(Difficulty.Strong).ToString() + " 项");
            DrawCategoryItem("精英", Category.Elite, CountByDifficulty(Difficulty.Elite).ToString() + " 项");
            DrawCategoryItem("Boss", Category.Boss, CountByDifficulty(Difficulty.Boss).ToString() + " 项");
            DrawCategoryItem("未完成配置", Category.Incomplete, CountIncomplete().ToString() + " 项");
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("按目录", EditorStyles.miniLabel);
            foreach (var dir in _subDirectories)
            {
                var shortName = System.IO.Path.GetFileName(dir);
                DrawDirectoryItem(shortName, dir);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawCategoryItem(string label, Category cat, string count)
    {
        var style = (_activeCategory == cat && cat != Category.ByDirectory)
            ? EnemyEditorStyles.CategoryItemSelectedStyle
            : EnemyEditorStyles.CategoryItemStyle;
        using (new EditorGUILayout.HorizontalScope(style))
        {
            if (GUILayout.Button(label, EditorStyles.label)) { _activeCategory = cat; _activeDirectory = null; }
            GUILayout.FlexibleSpace();
            GUILayout.Label(count, EditorStyles.miniLabel, GUILayout.Width(50));
        }
    }

    private void DrawDirectoryItem(string label, string dirPath)
    {
        var style = (_activeCategory == Category.ByDirectory && _activeDirectory == dirPath)
            ? EnemyEditorStyles.CategoryItemSelectedStyle
            : EnemyEditorStyles.CategoryItemStyle;
        using (new EditorGUILayout.HorizontalScope(style))
        {
            if (GUILayout.Button(label, EditorStyles.label)) { _activeCategory = Category.ByDirectory; _activeDirectory = dirPath; }
        }
    }

    private void DrawRightPanel()
    {
        // 上半：网格卡片（无外层卡片包装，避免与卡片自身的 CardStyle 双层 padding）
        var filtered = GetFilteredEnemies();
        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll, GUILayout.ExpandHeight(true));
        if (filtered.Count == 0)
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("(空)", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            int i = 0;
            while (i < filtered.Count)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int c = 0; c < 3 && i < filtered.Count; c++, i++)
                    {
                        var cfg = filtered[i];
                            EnemyCardDrawers.DrawGridCardCell(
                                cfg,
                                cfg == _selected,
                                () => SelectEnemy(cfg),
                                () => EnemyConfigEditorWindow.OpenOrFocus(cfg),
                                () => EnemyAssetStore.Ping(cfg));
                        GUILayout.Space(8);
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        // 下半：详情面板（选中时显示，带外层卡片风格区分）
        if (_selected != null)
        {
            DrawDetailPanel();
        }
    }

    private void SelectEnemy(EnemyConfig cfg)
    {
        _selected = cfg;
        RebuildSerializedObject();
    }

    // ========================================================================
    // 批量验证
    // ========================================================================

    private void RunBatchValidation()
    {
        var results = EnemyValidator.ValidateAll(_allEnemies);
        var report = EnemyValidator.BuildReport(results);
        EditorUtility.DisplayDialog("敌人校验报告", report, "确定");
    }

    // ========================================================================
    // 详情面板（5 个 Foldout 分组）
    // ========================================================================

    private Vector2 _detailScroll;

    private void DrawDetailPanel()
    {
        if (_selected == null) return;
        if (_selectedSO == null) RebuildSerializedObject();
        if (_selectedSO == null) return;

        _selectedSO.Update();

        using (new EditorGUILayout.VerticalScope(EnemyEditorStyles.CardStyle))
        {
            // 顶部名称 + 路径
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(_selected.enemyName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                var path = AssetDatabase.GetAssetPath(_selected);
                EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(4);

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.MinHeight(260));

            // === 1. 基础信息 ===
            _foldBasic = EditorGUILayout.Foldout(_foldBasic, "基础信息", true, EditorStyles.foldoutHeader);
            if (_foldBasic) DrawBasicFoldout();

            // === 2. 属性 ===
            _foldStats = EditorGUILayout.Foldout(_foldStats, "属性", true, EditorStyles.foldoutHeader);
            if (_foldStats) DrawStatsFoldout();

            // === 3. 出招牌库 ===
            _foldSkillDecks = EditorGUILayout.Foldout(_foldSkillDecks, "出招牌库（高理智=phase1 / 低理智=phase2）", true, EditorStyles.foldoutHeader);
            if (_foldSkillDecks) DrawSkillDecksFoldout();

            // === 4. 能力 ===
            _foldAbilities = EditorGUILayout.Foldout(_foldAbilities, "能力（精英/boss 敌人遗物）", true, EditorStyles.foldoutHeader);
            if (_foldAbilities) DrawAbilitiesFoldout();

            // === 5. 掉落物 ===
            _foldLoot = EditorGUILayout.Foldout(_foldLoot, "掉落物", true, EditorStyles.foldoutHeader);
            if (_foldLoot) DrawLootFoldout();

            EditorGUILayout.EndScrollView();

            if (_selectedSO.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selected);
            }
        }
    }

    private void DrawBasicFoldout()
    {
        EditorGUI.indentLevel++;
        SerialProp("enemyName");
        SerialProp("difficulty");
        SerialProp("actionPriority",
            new GUIContent("出招优先级",
                "运行时由 SpawnInfo.actionOrder 决定；此字段仅作编辑器提示（同值随机，1=最高）"));
        SerialProp("phase1Portrait");
        SerialProp("phase2Portrait");
        EditorGUI.indentLevel--;
    }

    private void DrawStatsFoldout()
    {
        EditorGUI.indentLevel++;
        SerialProp("maxHP");
        SerialProp("phase2MaxHP");
        SerialProp("armor");
        SerialProp("strength");
        SerialProp("agility");
        SerialProp("inCritRate");
        SerialProp("damageTakenMultiplier");
        SerialProp("damageDealtMultiplier");
        EditorGUI.indentLevel--;
    }

    private void DrawSkillDecksFoldout()
    {
        EditorGUI.indentLevel++;
        SerialProp("highSanityCardCount", new GUIContent("高理智出招数"));
        SerialProp("lowSanityCardCount", new GUIContent("低理智出招数"));
        EditorGUILayout.Space(2);
        SerialProp("phase1Skills", new GUIContent("高理智牌库（phase1）"));
        SerialProp("phase2Skills", new GUIContent("低理智牌库（phase2）"));
        EditorGUI.indentLevel--;
    }

    private void DrawAbilitiesFoldout()
    {
        EditorGUI.indentLevel++;
        SerialProp("abilities");
        EditorGUI.indentLevel--;
    }

    private void DrawLootFoldout()
    {
        EditorGUI.indentLevel++;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"当前难度：{_selected.difficulty}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("应用难度预设", GUILayout.Width(110)))
            {
                _selected.lootTable = LootTable.GetPreset(_selected.difficulty);
                _selectedSO.Update();
                EditorUtility.SetDirty(_selected);
                GUIUtility.ExitGUI();
            }
        }
        SerialProp("lootTable");
        EditorGUI.indentLevel--;
    }

    private void SerialProp(string propName, GUIContent label = null)
    {
        var p = _selectedSO.FindProperty(propName);
        if (p == null) { EditorGUILayout.HelpBox($"字段 {propName} 未找到", MessageType.Warning); return; }
        EditorGUILayout.PropertyField(p, label ?? new GUIContent(propName), true);
    }

    // ========================================================================
    // 筛选逻辑
    // ========================================================================

    private List<EnemyConfig> GetFilteredEnemies()
    {
        IEnumerable<EnemyConfig> query = _allEnemies;
        switch (_activeCategory)
        {
            case Category.Weak:       query = query.Where(c => c.difficulty == Difficulty.Weak); break;
            case Category.Strong:     query = query.Where(c => c.difficulty == Difficulty.Strong); break;
            case Category.Elite:      query = query.Where(c => c.difficulty == Difficulty.Elite); break;
            case Category.Boss:       query = query.Where(c => c.difficulty == Difficulty.Boss); break;
            case Category.Incomplete: query = query.Where(IsIncomplete); break;
            case Category.ByDirectory:
                if (!string.IsNullOrEmpty(_activeDirectory))
                {
                    var dir = _activeDirectory;
                    query = query.Where(c =>
                    {
                        var p = AssetDatabase.GetAssetPath(c);
                        return !string.IsNullOrEmpty(p) && p.StartsWith(dir + "/");
                    });
                }
                break;
        }
        if (!string.IsNullOrEmpty(_searchQuery))
        {
            var q = _searchQuery.ToLowerInvariant();
            query = query.Where(c =>
                (c.enemyName ?? "").ToLowerInvariant().Contains(q));
        }
        return query.ToList();
    }

    private int CountByDifficulty(Difficulty d) => _allEnemies.Count(c => c != null && c.difficulty == d);

    private int CountIncomplete() => _allEnemies.Count(IsIncomplete);

    private static bool IsIncomplete(EnemyConfig c)
    {
        if (c == null) return true;
        if (string.IsNullOrEmpty(c.enemyName)) return true;
        if (c.maxHP <= 0) return true;
        if (c.phase1Skills == null || c.phase1Skills.Count == 0) return true;
        return false;
    }
}