using System;
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
        EditorGUILayout.Space(2);

        // 自定义掉落物列表 UI（按 kind 显示不同字段）
        var entries = _selected.lootTable.entries;
        if (entries == null) entries = _selected.lootTable.entries = new List<LootEntry>();

        // 列表操作栏
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"掉落条目（{entries.Count}）", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 货币", GUILayout.Width(70)))
            {
                entries.Add(new LootEntry { kind = LootEntry.LootKind.Currency, currencyAmount = 10 });
                MarkDirty();
            }
            if (GUILayout.Button("+ 卡牌", GUILayout.Width(70)))
            {
                entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, cardRarities = new List<CardGrade> { CardGrade.Bronze }, cardDrawCount = 3, cardPickCount = 1 });
                MarkDirty();
            }
            if (GUILayout.Button("+ 遗物", GUILayout.Width(70)))
            {
                entries.Add(new LootEntry { kind = LootEntry.LootKind.Relic, relicRarities = new List<CardGrade> { CardGrade.Bronze } });
                MarkDirty();
            }
        }

        EditorGUILayout.Space(2);

        // 绘制每条 entry
        int toRemove = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            DrawLootEntryItem(entries[i], i, ref toRemove);
        }
        if (toRemove >= 0)
        {
            entries.RemoveAt(toRemove);
            MarkDirty();
        }

        EditorGUI.indentLevel--;
    }

    private void DrawLootEntryItem(LootEntry entry, int index, ref int toRemove)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // 类型标签 + 序号
                var kindLabel = entry.kind switch
                {
                    LootEntry.LootKind.Currency => "货币",
                    LootEntry.LootKind.Card   => "卡牌",
                    LootEntry.LootKind.Relic   => "遗物",
                    _ => "?"
                };
                var labelColor = entry.kind switch
                {
                    LootEntry.LootKind.Currency => new Color(0.6f, 0.4f, 1f),  // 紫色
                    LootEntry.LootKind.Card   => new Color(0.4f, 0.9f, 0.4f),  // 绿色
                    LootEntry.LootKind.Relic   => new Color(1f, 0.5f, 0.5f),    // 红色
                    _ => Color.gray
                };
                var prev = GUI.color;
                GUI.color = labelColor;
                EditorGUILayout.LabelField($"[{kindLabel}] #{index + 1}", EditorStyles.boldLabel, GUILayout.Width(100));
                GUI.color = prev;

                GUILayout.FlexibleSpace();

                // 上移 / 下移 / 删除
                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUILayout.Button("↑", GUILayout.Width(24))) { entriesSwap(entry, index, index - 1); }
                }
                using (new EditorGUI.DisabledScope(index >= _selected.lootTable.entries.Count - 1))
                {
                    if (GUILayout.Button("↓", GUILayout.Width(24))) { entriesSwap(entry, index, index + 1); }
                }
                if (GUILayout.Button("×", GUILayout.Width(24), GUILayout.Height(18))) { toRemove = index; }
            }

            EditorGUI.indentLevel++;

            switch (entry.kind)
            {
                case LootEntry.LootKind.Currency:
                    entry.currencyAmount = EditorGUILayout.IntField(new GUIContent("货币数量", "战斗结束后获得的固定货币数"), entry.currencyAmount);
                    break;

                case LootEntry.LootKind.Card:
                    EditorGUILayout.LabelField(new GUIContent("卡牌品级", "从这些品级的角色可获取牌库里抽取"), EditorStyles.boldLabel);
                    DrawCardGradeList(entry.cardRarities);
                    entry.cardDrawCount = EditorGUILayout.IntField(new GUIContent("抽取数量(n)", "展示给玩家的卡牌数，如「3选1」填 3"), entry.cardDrawCount);
                    entry.cardPickCount = EditorGUILayout.IntField(new GUIContent("可选数量", "玩家最终可选几张（通常为 1）"), entry.cardPickCount);
                    break;

                case LootEntry.LootKind.Relic:
                    EditorGUILayout.LabelField(new GUIContent("遗物品级", "从这些品级的角色可获取遗物库里抽 1 个"), EditorStyles.boldLabel);
                    DrawCardGradeList(entry.relicRarities);
                    break;
            }

            EditorGUI.indentLevel--;
        }
    }

    /// <summary>绘制 CardGrade 列表（带 Add/Remove 按钮）</summary>
    private void DrawCardGradeList(List<CardGrade> list)
    {
        if (list == null) return;
        // 品级快捷全选按钮行
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (CardGrade g in Enum.GetValues(typeof(CardGrade)))
            {
                bool has = list.Contains(g);
                bool toggled = GUILayout.Toggle(has, g.ToString(), EditorStyles.miniButton, GUILayout.Width(70));
                if (toggled && !has) list.Add(g);
                else if (!toggled && has) list.Remove(g);
            }
        }
        // 当前已选列表
        if (list.Count > 0)
        {
            EditorGUILayout.LabelField($"已选：{string.Join(", ", list)}", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.HelpBox("未选择任何品级", MessageType.Warning);
        }
    }

    private void entriesSwap(LootEntry entry, int i, int j)
    {
        var entries = _selected.lootTable.entries;
        var tmp = entries[i];
        entries[i] = entries[j];
        entries[j] = tmp;
        MarkDirty();
    }

    private void MarkDirty()
    {
        EditorUtility.SetDirty(_selected);
        _selectedSO?.Update();
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