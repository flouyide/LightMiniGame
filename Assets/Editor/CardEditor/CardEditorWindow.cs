using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LightMiniGame.CardEditor;

namespace LightMiniGame.CardEditor.Editor
{
    /// <summary>
    /// 卡牌编辑器主窗口 —— 三栏布局：左侧卡牌列表 / 中间基础信息 / 右侧效果编辑
    /// </summary>
    public class CardEditorWindow : EditorWindow
    {
        // === 状态 ===
        private CardDatabase _database;
        private List<CardEntry> _filteredCards = new List<CardEntry>();
        private CardEntry _selectedCard;
        private bool _viewingLowSanity;  // 右栏当前查看的是基础(false)还是升级(true)
        private Vector2 _leftScroll, _middleScroll, _rightScroll;

        // === 筛选 ===
        private string _searchQuery = "";
        private int _filterGradeIdx;
        private int _filterTypeIdx;
        private int _filterCost = -1;
        private int _filterKeywordIdx;
        private bool _filterEnabled;

        // === 校验 ===
        private List<ValidationResult> _validationResults = new List<ValidationResult>();
        private bool _showValidation;

        // === 折叠状态 ===
        private bool _showConditions;
        private bool _showAbility;
        private bool _showPreview = true;
        private bool _showValidationFoldout = true;

        // === 效果折叠 ===
        private Dictionary<string, bool> _effectFoldouts = new Dictionary<string, bool>();

        // === 菜单 ===
        [MenuItem("Tools/卡牌编辑器/Card Editor")]
        public static void Open()
        {
            var window = GetWindow<CardEditorWindow>("卡牌编辑器");
            window.minSize = new Vector2(900, 600);
        }

        private void OnEnable()
        {
            LoadDatabase();
            RefreshFilter();
        }

        private void LoadDatabase()
        {
            _database = CardDatabase.Load();
            if (_database == null)
            {
                var guids = AssetDatabase.FindAssets("t:CardDatabase");
                if (guids.Length > 0)
                    _database = AssetDatabase.LoadAssetAtPath<CardDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        // ========================================================================
        // 主布局
        // ========================================================================
        private void OnGUI()
        {
            if (_database == null)
            {
                DrawNoDatabase();
                return;
            }

            DrawToolbar();

            EditorGUILayout.BeginHorizontal();

            // 左侧：卡牌列表
            DrawLeftPanel();

            // 中间：基础信息
            DrawMiddlePanel();

            // 右侧：效果编辑
            DrawRightPanel();

            EditorGUILayout.EndHorizontal();

            if (GUI.changed && _selectedCard != null)
            {
                EditorUtility.SetDirty(_selectedCard);
            }
        }

        // ========================================================================
        // 顶部工具栏
        // ========================================================================
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("新建卡牌", EditorStyles.toolbarButton, GUILayout.Width(80)))
                CreateCard();

            if (GUILayout.Button("复制卡牌", EditorStyles.toolbarButton, GUILayout.Width(80)))
                DuplicateCard(_selectedCard);

            if (GUILayout.Button("删除卡牌", EditorStyles.toolbarButton, GUILayout.Width(80)))
                DeleteCard(_selectedCard);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("批量验证", EditorStyles.toolbarButton, GUILayout.Width(80)))
                ValidateAllCards();

            if (GUILayout.Button("测试卡牌", EditorStyles.toolbarButton, GUILayout.Width(80)))
                CardTestWindow.Open(_selectedCard);

            if (GUILayout.Button("品级配置", EditorStyles.toolbarButton, GUILayout.Width(80)))
                GradeConfigWindow.Open();

            EditorGUILayout.EndHorizontal();
        }

        // ========================================================================
        // 无数据库提示
        // ========================================================================
        private void DrawNoDatabase()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox("未找到 CardDatabase 资产。\n点击下方按钮在 Resources/CardEditor/ 下创建。", MessageType.Warning);
            if (GUILayout.Button("创建 CardDatabase 资产", GUILayout.Width(200), GUILayout.Height(30)))
            {
                var dir = "Assets/Resources/CardEditor";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var path = $"{dir}/CardDatabase.asset";
                _database = CreateInstance<CardDatabase>();
                AssetDatabase.CreateAsset(_database, path);
                AssetDatabase.SaveAssets();
                RefreshFilter();
            }
        }

        // ========================================================================
        // 左侧：卡牌列表
        // ========================================================================
        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(260));

            // 搜索栏
            _searchQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField);

            // 筛选器
            EditorGUILayout.BeginHorizontal();
            _filterEnabled = GUILayout.Toggle(_filterEnabled, "筛选", GUILayout.Width(40));
            if (_filterEnabled)
            {
                _filterGradeIdx = EditorGUILayout.Popup(_filterGradeIdx, new[] { "全部品级", "铜", "银", "金" }, GUILayout.Width(60));
                _filterTypeIdx = EditorGUILayout.Popup(_filterTypeIdx, new[] { "全部类型", "攻击", "技能", "能力" }, GUILayout.Width(60));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (_filterEnabled)
            {
                _filterKeywordIdx = EditorGUILayout.Popup(_filterKeywordIdx, new[] { "全部词条", "无", "回响" }, GUILayout.Width(60));
                _filterCost = EditorGUILayout.IntField("费用", _filterCost, GUILayout.Width(80));
            }
            EditorGUILayout.EndHorizontal();

            // 卡牌列表
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            if (_filteredCards != null)
            {
                foreach (var card in _filteredCards)
                {
                    if (card == null) continue;
                    var style = card == _selectedCard ? "SelectionRect" : "box";
                    EditorGUILayout.BeginHorizontal(style);
                    var gradeColor = card.grade switch
                    {
                        CardGrade.Bronze => new Color(0.8f, 0.5f, 0.3f),
                        CardGrade.Silver => new Color(0.8f, 0.8f, 0.8f),
                        CardGrade.Gold => new Color(1f, 0.85f, 0.3f),
                        _ => Color.white
                    };
                    var oldColor = GUI.color;
                    GUI.color = gradeColor;
                    GUILayout.Label("◆", GUILayout.Width(14));
                    GUI.color = oldColor;
                    if (GUILayout.Button(card.cardName ?? "(未命名)", EditorStyles.label))
                    {
                        _selectedCard = card;
                        _viewingLowSanity = false;
                        _validationResults.Clear();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ========================================================================
        // 中间：基础信息
        // ========================================================================
        private void DrawMiddlePanel()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
            _middleScroll = EditorGUILayout.BeginScrollView(_middleScroll);

            if (_selectedCard == null)
            {
                EditorGUILayout.LabelField("请选择或创建一张卡牌", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                DrawBasicInfo();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBasicInfo()
        {
            var card = _selectedCard;

            EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);

            card.cardId = EditorGUILayout.TextField("卡牌 ID", card.cardId);

            // 记录改名前的名称，用于重命名 .asset 文件
            string oldName = card.cardName;
            card.cardName = EditorGUILayout.TextField("卡牌名称", card.cardName);
            if (oldName != card.cardName && !string.IsNullOrEmpty(card.cardName))
            {
                RenameCardAsset(card, card.cardName);
            }
            card.cardArt = (Sprite)EditorGUILayout.ObjectField("卡面原画", card.cardArt, typeof(Sprite), false);
            card.darkCardArt = (Sprite)EditorGUILayout.ObjectField("黑暗卡面", card.darkCardArt, typeof(Sprite), false);
            card.grade = (CardGrade)EditorGUILayout.Popup("品级", (int)card.grade, new[] { "铜", "银", "金" });
            card.cardType = (CardType)EditorGUILayout.Popup("卡牌类型", (int)card.cardType, new[] { "攻击", "技能", "能力" });
            card.existence = (CardExistence)EditorGUILayout.Popup("存在形式(普通)", (int)card.existence, new[] { "普通", "战斗内移除", "永久移除" });
            card.keyword = (CardKeyword)EditorGUILayout.Popup("词条", (int)card.keyword, new[] { "无", "回响", "灾厄", "命运" });

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("费用", EditorStyles.boldLabel);
            card.normalCost = EditorGUILayout.IntField("普通费用", card.normalCost);
    card.hasLowSanityForm = EditorGUILayout.BeginToggleGroup("是否配置低理智形态", card.hasLowSanityForm);
            if (card.hasLowSanityForm)
            {
                card.lowSanityCost = EditorGUILayout.IntField("低理智费用", card.lowSanityCost);
                card.lowSanityExistence = (CardExistence)EditorGUILayout.Popup("存在形式(低理智)", (int)card.lowSanityExistence, new[] { "普通", "战斗内移除", "永久移除" });
            }
            EditorGUILayout.EndToggleGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("描述", EditorStyles.boldLabel);
            card.normalDescription = EditorGUILayout.TextArea(card.normalDescription, GUILayout.MinHeight(50));
            if (card.hasLowSanityForm)
            {
                EditorGUILayout.LabelField("低理智描述:", EditorStyles.miniLabel);
                card.lowSanityDescription = EditorGUILayout.TextArea(card.lowSanityDescription, GUILayout.MinHeight(50));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("备注", EditorStyles.boldLabel);
            card.designerNotes = EditorGUILayout.TextArea(card.designerNotes, GUILayout.MinHeight(40));

            // 自定义卡牌脚本
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("自定义卡牌脚本", EditorStyles.boldLabel);
            card.customCardScript = (CustomCardScript)EditorGUILayout.ObjectField("脚本", card.customCardScript, typeof(CustomCardScript), false);
            if (card.customCardScript != null)
                EditorGUILayout.LabelField($"已绑定: {card.customCardScript.GetDisplayName()}", EditorStyles.miniLabel);
        }

        // ========================================================================
        // 右侧：效果编辑 + 预览 + 校验
        // ========================================================================
        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_selectedCard == null)
            {
                EditorGUILayout.LabelField("请选择或创建一张卡牌", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // 普通形态/低理智形态切换
                if (_selectedCard.hasLowSanityForm)
                {
                    EditorGUILayout.BeginHorizontal();
                    var oldLabel = GUI.skin.label.fontSize;
                    GUI.skin.label.fontSize = 13;
                    var baseColor = !_viewingLowSanity ? new Color(0.4f, 0.8f, 0.4f) : Color.white;
                    var upgradeColor = _viewingLowSanity ? new Color(0.4f, 0.8f, 0.4f) : Color.white;
                    var oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = baseColor;
                    if (GUILayout.Button("普通形态效果", GUILayout.Height(24)))
                    {
                        _viewingLowSanity = false;
                        _validationResults.Clear();
                    }
                    GUI.backgroundColor = upgradeColor;
                    if (GUILayout.Button("低理智形态效果", GUILayout.Height(24)))
                    {
                        _viewingLowSanity = true;
                        _validationResults.Clear();
                    }
                    GUI.backgroundColor = oldBg;
                    GUI.skin.label.fontSize = oldLabel;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("从普通形态复制到低理智", EditorStyles.miniButton))
                        CopyBaseToUpgrade();
                    if (GUILayout.Button("清空低理智形态效果", EditorStyles.miniButton))
                        ClearUpgradeEffects();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space();

                if (_selectedCard.cardType == CardType.Ability)
                {
                    // 能力卡也用 EffectNode 编辑器（RegisterTrigger 就是能力效果）
                    DrawEffectNodeList();
                }
                else
                {
                    DrawEffectNodeList();
                }

                EditorGUILayout.Space();

                // 预览
                _showPreview = EditorGUILayout.Foldout(_showPreview, "卡牌预览");
                if (_showPreview)
                    DrawPreview();

                // 校验
                _showValidationFoldout = EditorGUILayout.Foldout(_showValidationFoldout, "校验结果");
                if (_showValidationFoldout)
                    DrawValidation();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ========================================================================
        // 新 EffectNode 效果列表
        // ========================================================================
        private void DrawEffectNodeList()
        {
            EditorGUILayout.Space(10);
            var nodes = _viewingLowSanity ? _selectedCard.lowSanityEffectNodes : _selectedCard.normalEffectNodes;
            if (nodes == null)
            {
                nodes = new List<EffectNode>();
                if (_viewingLowSanity) _selectedCard.lowSanityEffectNodes = nodes;
                else _selectedCard.normalEffectNodes = nodes;
            }

            EditorGUILayout.LabelField(_viewingLowSanity ? "低理智形态效果（新格式）" : "普通形态效果（新格式）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("新格式效果优先于旧格式。如果这里配置了效果，战斗时将使用新格式执行。", MessageType.Info);

            EffectNodeEditor.DrawEffectList(nodes, _viewingLowSanity ? "低理智形态效果" : "普通形态效果");

            if (GUI.changed && _selectedCard != null)
                EditorUtility.SetDirty(_selectedCard);
        }

        // ========================================================================
        private void DrawPreview()
        {
            EditorGUILayout.BeginVertical("box");
            var card = _selectedCard;

            // 卡牌标题
            EditorGUILayout.LabelField($"【{CardEntry.GetGradeName(card.grade)}】{card.cardName}", EditorStyles.boldLabel);
            var ex = _viewingLowSanity && card.hasLowSanityForm ? card.GetExistence(true) : card.existence;
            EditorGUILayout.LabelField($"类型: {CardEntry.GetCardTypeName(card.cardType)}  费用: {(_viewingLowSanity && card.hasLowSanityForm ? card.lowSanityCost : card.normalCost)}  存在形式: {CardEntry.GetExistenceName(ex)}");
            if (card.keyword != CardKeyword.None)
                EditorGUILayout.LabelField($"词条: {CardEntry.GetKeywordName(card.keyword)}");

            EditorGUILayout.Space();

            // 描述
            EditorGUILayout.LabelField("描述:", EditorStyles.boldLabel);
            var desc = card.GetDescription(_viewingLowSanity && card.hasLowSanityForm);
            EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedLabel);

            // 效果列表
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("效果结算顺序:", EditorStyles.boldLabel);
            var effects = card.GetEffectNodes(_viewingLowSanity && card.hasLowSanityForm);
            for (int i = 0; i < effects.Count; i++)
            {
                if (!effects[i].enabled) continue;
                EditorGUILayout.LabelField($"{i + 1}. {effects[i].GetDescription()}", EditorStyles.wordWrappedLabel);
            }

            // 能力信息
            if (card.cardType == CardType.Ability)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("能力信息:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("(通过 RegisterTrigger 效果节点配置)", EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.EndVertical();
        }

        // ========================================================================
        // 校验
        // ========================================================================
        private void DrawValidation()
        {
            EditorGUILayout.BeginVertical("box");

            if (GUILayout.Button("校验当前卡牌", GUILayout.Height(24)))
            {
                _validationResults = CardValidator.Validate(_selectedCard, _database);
            }

            if (_validationResults.Count == 0)
            {
                EditorGUILayout.LabelField("点击上方按钮进行校验", EditorStyles.miniLabel);
            }
            else
            {
                foreach (var result in _validationResults)
                {
                    var icon = result.severity == ValidationResult.Severity.Error ? "❌" :
                               result.severity == ValidationResult.Severity.Warning ? "⚠️" : "ℹ️";
                    var label = $"{icon} {result.message}";
                    if (!string.IsNullOrEmpty(result.context))
                        label += $" ({result.context})";
                    EditorGUILayout.LabelField(label, EditorStyles.wordWrappedLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        // ========================================================================
        // 卡牌操作
        // ========================================================================
        private void RenameCardAsset(CardEntry card, string newName)
        {
            if (card == null || string.IsNullOrEmpty(newName)) return;
            var path = AssetDatabase.GetAssetPath(card);
            if (string.IsNullOrEmpty(path)) return;

            var dir = System.IO.Path.GetDirectoryName(path);
            var newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{newName}.asset");
            if (newPath != path)
            {
                AssetDatabase.RenameAsset(path, newName);
                AssetDatabase.SaveAssets();
            }
        }

        private void CreateCard()
        {
            var dir = "Assets/ScriptableObjects/Cards";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var card = CreateInstance<CardEntry>();
            card.cardId = $"card_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
            card.cardName = "新卡牌";
            card.normalEffectNodes = new List<EffectNode>();
            card.lowSanityEffectNodes = new List<EffectNode>();
            card.normalEffectNodes = new List<EffectNode>();
            card.lowSanityEffectNodes = new List<EffectNode>();

            var path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{card.cardName}.asset");
            AssetDatabase.CreateAsset(card, path);
            AssetDatabase.SaveAssets();

            _database.cards.Add(card);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();

            _selectedCard = card;
            _viewingLowSanity = false;
            RefreshFilter();
            EditorGUIUtility.PingObject(card);
        }

        private void DuplicateCard(CardEntry source)
        {
            if (source == null) return;

            var dir = "Assets/ScriptableObjects/Cards";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var card = CreateInstance<CardEntry>();
            card.cardId = $"{source.cardId}_copy_{System.Guid.NewGuid().ToString("N").Substring(0, 4)}";
            card.cardName = source.cardName + " (副本)";
            card.normalDescription = source.normalDescription;
            card.lowSanityDescription = source.lowSanityDescription;
            card.cardArt = source.cardArt;
            card.darkCardArt = source.darkCardArt;
            card.grade = source.grade;
            card.cardType = source.cardType;
            card.existence = source.existence;
            card.normalCost = source.normalCost;
            card.lowSanityCost = source.lowSanityCost;
            card.keyword = source.keyword;
            card.hasLowSanityForm = source.hasLowSanityForm;
            card.designerNotes = source.designerNotes;
            card.customCardScript = source.customCardScript;
            card.normalEffectNodes = source.normalEffectNodes?.ConvertAll(e => e.Clone()) ?? new List<EffectNode>();
            card.lowSanityEffectNodes = source.lowSanityEffectNodes?.ConvertAll(e => e.Clone()) ?? new List<EffectNode>();
            card.normalEffectNodes = source.normalEffectNodes?.ConvertAll(e => e.Clone()) ?? new List<EffectNode>();
            card.lowSanityEffectNodes = source.lowSanityEffectNodes?.ConvertAll(e => e.Clone()) ?? new List<EffectNode>();

            var path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{card.cardName}.asset");
            AssetDatabase.CreateAsset(card, path);
            AssetDatabase.SaveAssets();

            _database.cards.Add(card);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();

            _selectedCard = card;
            _viewingLowSanity = false;
            RefreshFilter();
            EditorGUIUtility.PingObject(card);
        }

        private void DeleteCard(CardEntry card)
        {
            if (card == null) return;
            if (!EditorUtility.DisplayDialog("删除卡牌", $"确认删除「{card.cardName}」？此操作不可撤销。", "删除", "取消"))
                return;

            _database.cards.Remove(card);
            EditorUtility.SetDirty(_database);

            var path = AssetDatabase.GetAssetPath(card);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();

            if (_selectedCard == card) _selectedCard = null;
            RefreshFilter();
        }

        // ========================================================================
        // 效果操作
        // ========================================================================
        private void CopyBaseToUpgrade()
        {
            if (_selectedCard == null) return;
            if (_selectedCard.cardType == CardType.Ability)
            {
            }
            else
            {
                _selectedCard.lowSanityEffectNodes = _selectedCard.normalEffectNodes?.ConvertAll(e => e.Clone()) ?? new List<EffectNode>();
                _selectedCard.lowSanityEffectNodes = _selectedCard.normalEffectNodes?.ConvertAll(e => e.Clone()) ?? new List<EffectNode>();
            }
            EditorUtility.SetDirty(_selectedCard);
        }

        private void ClearUpgradeEffects()
        {
            if (_selectedCard == null) return;
            if (_selectedCard.cardType == CardType.Ability)
            {
            }
            else
            {
                _selectedCard.lowSanityEffectNodes = new List<EffectNode>();
                _selectedCard.lowSanityEffectNodes = new List<EffectNode>();
            }
            EditorUtility.SetDirty(_selectedCard);
        }

        // ========================================================================
        // 批量校验
        // ========================================================================
        private void ValidateAllCards()
        {
            if (_database == null) return;
            var allResults = CardValidator.ValidateAll(_database);
            if (allResults.Count == 0)
            {
                EditorUtility.DisplayDialog("批量验证", "所有卡牌校验通过，无错误。", "确定");
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"共 {allResults.Count} 张卡牌存在错误:\n");
                foreach (var (card, results) in allResults)
                {
                    sb.AppendLine($"【{card.cardName}】");
                    foreach (var r in results.Where(r => r.severity == ValidationResult.Severity.Error))
                        sb.AppendLine($"  ❌ {r.message}" + (string.IsNullOrEmpty(r.context) ? "" : $" ({r.context})"));
                    sb.AppendLine();
                }
                EditorUtility.DisplayDialog("批量验证结果", sb.ToString(), "确定");
            }
        }

        // ========================================================================
        // 筛选刷新
        // ========================================================================
        private void RefreshFilter()
        {
            if (_database == null) { _filteredCards = new List<CardEntry>(); return; }

            var query = _database.cards.AsEnumerable();

            if (!string.IsNullOrEmpty(_searchQuery))
                query = query.Where(c => c.cardName != null && c.cardName.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));

            if (_filterEnabled)
            {
                if (_filterGradeIdx > 0) query = query.Where(c => (int)c.grade == _filterGradeIdx - 1);
                if (_filterTypeIdx > 0) query = query.Where(c => (int)c.cardType == _filterTypeIdx - 1);
                if (_filterKeywordIdx > 0) query = query.Where(c => (int)c.keyword == _filterKeywordIdx - 1);
                if (_filterCost >= 0) query = query.Where(c => c.normalCost == _filterCost);
            }

            _filteredCards = query.ToList();
        }

        private void OnInspectorUpdate()
        {
            // 实时刷新筛选（搜索框输入后即时更新）
            Repaint();
        }
    }
}
