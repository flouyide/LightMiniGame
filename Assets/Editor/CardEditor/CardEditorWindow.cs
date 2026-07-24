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
        private bool _viewingUpgrade;  // 右栏当前查看的是基础(false)还是升级(true)
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
                        _viewingUpgrade = false;
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
            card.cardName = EditorGUILayout.TextField("卡牌名称", card.cardName);
            card.cardArt = (Sprite)EditorGUILayout.ObjectField("卡面原画", card.cardArt, typeof(Sprite), false);
            card.darkCardArt = (Sprite)EditorGUILayout.ObjectField("黑暗卡面", card.darkCardArt, typeof(Sprite), false);
            card.grade = (CardGrade)EditorGUILayout.Popup("品级", (int)card.grade, new[] { "铜", "银", "金" });
            card.cardType = (CardType)EditorGUILayout.Popup("卡牌类型", (int)card.cardType, new[] { "攻击", "技能", "能力" });
            card.existence = (CardExistence)EditorGUILayout.Popup("存在形式", (int)card.existence, new[] { "普通", "战斗内移除", "永久移除" });
            card.keyword = (CardKeyword)EditorGUILayout.Popup("词条", (int)card.keyword, new[] { "无", "回响", "灾厄", "命运" });

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("费用", EditorStyles.boldLabel);
            card.baseCost = EditorGUILayout.IntField("基础费用", card.baseCost);
    card.upgradable = EditorGUILayout.BeginToggleGroup("升级", card.upgradable);
            EditorGUILayout.EndToggleGroup();
            if (card.upgradable)
            {
                card.upgradeCost = EditorGUILayout.IntField("升级后费用", card.upgradeCost);
            }
            EditorGUILayout.EndToggleGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("描述", EditorStyles.boldLabel);
            card.baseDescription = EditorGUILayout.TextArea(card.baseDescription, GUILayout.MinHeight(50));
            if (card.upgradable)
            {
                EditorGUILayout.LabelField("升级后描述:", EditorStyles.miniLabel);
                card.upgradeDescription = EditorGUILayout.TextArea(card.upgradeDescription, GUILayout.MinHeight(50));
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
                // 基础/升级切换
                if (_selectedCard.upgradable)
                {
                    EditorGUILayout.BeginHorizontal();
                    var oldLabel = GUI.skin.label.fontSize;
                    GUI.skin.label.fontSize = 13;
                    var baseColor = !_viewingUpgrade ? new Color(0.4f, 0.8f, 0.4f) : Color.white;
                    var upgradeColor = _viewingUpgrade ? new Color(0.4f, 0.8f, 0.4f) : Color.white;
                    var oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = baseColor;
                    if (GUILayout.Button("基础效果", GUILayout.Height(24)))
                    {
                        _viewingUpgrade = false;
                        _validationResults.Clear();
                    }
                    GUI.backgroundColor = upgradeColor;
                    if (GUILayout.Button("升级效果", GUILayout.Height(24)))
                    {
                        _viewingUpgrade = true;
                        _validationResults.Clear();
                    }
                    GUI.backgroundColor = oldBg;
                    GUI.skin.label.fontSize = oldLabel;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("从基础复制到升级", EditorStyles.miniButton))
                        CopyBaseToUpgrade();
                    if (GUILayout.Button("清空升级效果", EditorStyles.miniButton))
                        ClearUpgradeEffects();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space();

                if (_selectedCard.cardType == CardType.Ability)
                    DrawAbilityEditor();
                else
                    DrawEffectList();

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
        // 效果列表
        // ========================================================================
        private void DrawEffectList()
        {
            var effects = _viewingUpgrade ? _selectedCard.upgradeEffects : _selectedCard.baseEffects;
            if (effects == null) effects = new List<CardEffect>();

            EditorGUILayout.LabelField(_viewingUpgrade ? "升级效果列表" : "基础效果列表", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("效果按列表顺序依次结算。可拖动排序（↑↓）、复制、启用/禁用。", MessageType.Info);

            for (int i = 0; i < effects.Count; i++)
            {
                DrawEffectItem(effects, i);
            }

            if (GUILayout.Button("+ 添加效果", GUILayout.Height(24)))
            {
                effects.Add(new CardEffect { effectName = $"效果 {effects.Count + 1}" });
            }
        }

        private void DrawEffectItem(List<CardEffect> effects, int index)
        {
            var eff = effects[index];
            string foldoutKey = $"{(_viewingUpgrade ? "U" : "B")}_{index}";
            if (!_effectFoldouts.ContainsKey(foldoutKey)) _effectFoldouts[foldoutKey] = true;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            eff.enabled = EditorGUILayout.Toggle(eff.enabled, GUILayout.Width(20));
            var oldBg = GUI.color;
            if (!eff.enabled) GUI.color = new Color(0.6f, 0.6f, 0.6f);

            _effectFoldouts[foldoutKey] = EditorGUILayout.Foldout(_effectFoldouts[foldoutKey], $"{index + 1}. {eff.effectName} [{CardEffect.GetEffectTypeName(eff.effectType)}]", true);

            // 排序按钮
            EditorGUI.BeginDisabledGroup(index == 0);
            if (GUILayout.Button("↑", EditorStyles.miniButtonLeft, GUILayout.Width(22)))
            {
                (effects[index], effects[index - 1]) = (effects[index - 1], effects[index]);
                return;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(index == effects.Count - 1);
            if (GUILayout.Button("↓", EditorStyles.miniButtonMid, GUILayout.Width(22)))
            {
                (effects[index], effects[index + 1]) = (effects[index + 1], effects[index]);
                return;
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("复制", EditorStyles.miniButtonMid, GUILayout.Width(40)))
            {
                effects.Insert(index + 1, eff.Clone());
                return;
            }
            if (GUILayout.Button("删除", EditorStyles.miniButtonRight, GUILayout.Width(40)))
            {
                effects.RemoveAt(index);
                return;
            }

            GUI.color = oldBg;
            EditorGUILayout.EndHorizontal();

            if (_effectFoldouts[foldoutKey])
            {
                EditorGUI.BeginDisabledGroup(!eff.enabled);

                // 效果名称
                eff.effectName = EditorGUILayout.TextField("效果名称", eff.effectName);

                // 效果类型
                eff.effectType = (EffectType)EditorGUILayout.Popup("效果类型", (int)eff.effectType,
                    new[] { "伤害", "护甲", "抽牌", "回费", "修改属性", "修改手牌费用", "施加状态", "自定义" });

                // 发起者 & 目标
                eff.source = (EffectSource)EditorGUILayout.Popup("效果发起者", (int)eff.source,
                    new[] { "玩家", "选定敌人", "随机敌人", "所有敌人", "自定义" });
                eff.target = (EffectTarget)EditorGUILayout.Popup("效果目标", (int)eff.target,
                    new[] { "玩家", "选定敌人", "随机敌人", "所有敌人", "切换后角色", "发起者自身", "自定义" });

                EditorGUILayout.Space();

                // 按效果类型显示字段
                switch (eff.effectType)
                {
                    case EffectType.Damage: DrawDamageFields(eff); break;
                    case EffectType.Armor: DrawArmorFields(eff); break;
                    case EffectType.DrawCard: DrawDrawCardFields(eff); break;
                    case EffectType.RestoreEnergy: DrawEnergyFields(eff); break;
                    case EffectType.ModifyAttribute: DrawModifyAttributeFields(eff); break;
                    case EffectType.ModifyHandCost: DrawHandCostFields(eff); break;
                    case EffectType.ApplyStatus: DrawStatusFields(eff); break;
                    case EffectType.Custom: DrawCustomEffectFields(eff); break;
                }

                EditorGUILayout.Space();

                // 时机与持续
                DrawTimingAndDuration(eff);

                EditorGUILayout.Space();

                // 条件
                DrawConditions(eff);

                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
        }

        // ========================================================================
        // 攻击效果字段
        // ========================================================================
        private void DrawDamageFields(CardEffect eff)
        {
            EditorGUILayout.LabelField("伤害配置", EditorStyles.boldLabel);
            DrawValueExpression(eff.value, "伤害数值", AttributeRef.Strength);
            eff.times = EditorGUILayout.IntField("攻击次数", eff.times);
            if (eff.times < 1) eff.times = 1;
            eff.alwaysCrit = EditorGUILayout.Toggle("必定暴击", eff.alwaysCrit);
            eff.ignoreArmor = EditorGUILayout.Toggle("无视护甲", eff.ignoreArmor);

            EditorGUILayout.BeginHorizontal();
            eff.applyArmorBreak = EditorGUILayout.Toggle("施加破甲", eff.applyArmorBreak);
            if (eff.applyArmorBreak)
            {
                eff.armorBreakAmount = EditorGUILayout.IntField("破甲数值", eff.armorBreakAmount);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ========================================================================
        // 护甲效果字段
        // ========================================================================
        private void DrawArmorFields(CardEffect eff)
        {
            EditorGUILayout.LabelField("护甲配置", EditorStyles.boldLabel);
            DrawValueExpression(eff.value, "护甲数值", AttributeRef.Dexterity);
            EditorGUILayout.HelpBox("护甲是否在回合结束时保留由玩家的全局战斗属性决定，不由每张卡单独配置。", MessageType.Info);
        }

        // ========================================================================
        // 抽牌效果字段
        // ========================================================================
        private void DrawDrawCardFields(CardEffect eff)
        {
            EditorGUILayout.LabelField("抽牌配置", EditorStyles.boldLabel);
            DrawValueExpression(eff.value, "抽牌数量", AttributeRef.Strength);
        }

        // ========================================================================
        // 回费效果字段
        // ========================================================================
        private void DrawEnergyFields(CardEffect eff)
        {
            EditorGUILayout.LabelField("回费配置", EditorStyles.boldLabel);
            DrawValueExpression(eff.value, "回复能量", AttributeRef.Strength);
        }

        // ========================================================================
        // 修改属性字段
        // ========================================================================
        private void DrawModifyAttributeFields(CardEffect eff)
        {
            EditorGUILayout.LabelField("增减益配置", EditorStyles.boldLabel);
            eff.buffTarget = (BuffTarget)EditorGUILayout.Popup("增减益目标", (int)eff.buffTarget,
                new[] { "玩家", "敌人", "当前手牌", "下一张牌", "指定类型牌", "自定义" });
            eff.modAttribute = (ModifiableAttribute)EditorGUILayout.Popup("修改属性", (int)eff.modAttribute,
                new[] { "力量", "敏捷", "玩家暴击率", "敌人被暴击率", "玩家暴击伤害", "敌人暴击伤害",
                        "破甲值", "最大生命值", "当前生命值", "每回合抽牌数", "每回合能量", "流血值", "货币", "手牌费用" });
            eff.modifyMethod = (ModifyMethod)EditorGUILayout.Popup("修改方式", (int)eff.modifyMethod,
                new[] { "增加", "减少", "乘算", "覆盖" });
            DrawValueExpression(eff.value, "修改数值", AttributeRef.Strength);
        }

        // ========================================================================
        // 手牌费用字段
        // ========================================================================
        private void DrawHandCostFields(CardEffect eff)
        {
            EditorGUILayout.LabelField("手牌费用配置", EditorStyles.boldLabel);
            eff.handCostTarget = (HandCostTarget)EditorGUILayout.Popup("费用目标", (int)eff.handCostTarget,
                new[] { "下一张牌", "当前手牌" });
            eff.costChange = EditorGUILayout.IntField("费用变化值", eff.costChange);
        }

        // ========================================================================
        // 状态效果字段
        // ========================================================================
        private void DrawStatusFields(CardEffect eff)
        {
            EditorGUILayout.LabelField("状态配置", EditorStyles.boldLabel);
            eff.statusType = (StatusType)EditorGUILayout.Popup("状态类型", (int)eff.statusType,
                new[] { "流血", "破甲", "力量", "敏捷", "疯狂", "下次攻击增伤", "下一张牌减费", "暴击率提升", "暴击伤害提升" });
            eff.statusStacks = EditorGUILayout.IntField("层数", eff.statusStacks);
            if (eff.statusStacks < 1) eff.statusStacks = 1;
            eff.stackable = EditorGUILayout.Toggle("可叠加", eff.stackable);
        }

        // ========================================================================
        // 自定义效果字段
        // ========================================================================
        private void DrawCustomEffectFields(CardEffect eff)
        {
            EditorGUILayout.LabelField("自定义效果配置", EditorStyles.boldLabel);
            eff.customEffectScript = (CustomEffectScript)EditorGUILayout.ObjectField("效果脚本", eff.customEffectScript, typeof(CustomEffectScript), false);
            if (eff.customEffectScript != null)
                EditorGUILayout.LabelField($"已绑定: {eff.customEffectScript.GetDisplayName()}", EditorStyles.miniLabel);
            else
                EditorGUILayout.HelpBox("请绑定自定义效果脚本（继承 CustomEffectScript 的 ScriptableObject）。", MessageType.Warning);
            EditorGUILayout.LabelField("自定义参数:");
            eff.customParams = EditorGUILayout.TextArea(eff.customParams, GUILayout.MinHeight(40));
        }

        // ========================================================================
        // 数值表达式
        // ========================================================================
        private void DrawValueExpression(ValueExpression val, string label, AttributeRef defaultAttr)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            val.formulaType = (ValueFormulaType)EditorGUILayout.Popup("公式类型", (int)val.formulaType,
                new[] { "固定值", "基础值 + 属性", "基础值 + 属性 × 系数", "基础值 + 计数 × 系数" });
            val.baseValue = EditorGUILayout.IntField("基础数值", val.baseValue);

            switch (val.formulaType)
            {
                case ValueFormulaType.BasePlusAttribute:
                case ValueFormulaType.BasePlusAttributeTimesCoeff:
                    val.attributeRef = (AttributeRef)EditorGUILayout.Popup("引用属性", (int)val.attributeRef, GetAttributeRefNames());
                    if (val.formulaType == ValueFormulaType.BasePlusAttributeTimesCoeff)
                        val.coefficient = EditorGUILayout.FloatField("系数", val.coefficient);
                    break;
                case ValueFormulaType.BasePlusCounterTimesCoeff:
                    val.counterRef = (AttributeRef)EditorGUILayout.Popup("引用计数", (int)val.counterRef, GetAttributeRefNames());
                    val.coefficient = EditorGUILayout.FloatField("系数", val.coefficient);
                    break;
            }
        }

        private string[] GetAttributeRefNames() => new[]
        {
            "力量", "敏捷", "当前生命", "最大生命", "已损失生命",
            "当前理智", "本回合已损失理智", "本场战斗累计失去理智",
            "流血值", "破甲值", "暴击率", "暴击伤害"
        };

        // ========================================================================
        // 时机与持续时间
        // ========================================================================
        private void DrawTimingAndDuration(CardEffect eff)
        {
            EditorGUILayout.LabelField("时机与持续", EditorStyles.boldLabel);
            eff.timing = (EffectTiming)EditorGUILayout.Popup("生效时机", (int)eff.timing,
                new[] { "立即", "下回合开始", "下次攻击", "下一张牌", "下次切换", "每次切换" });
            eff.duration = (EffectDuration)EditorGUILayout.Popup("持续时间", (int)eff.duration,
                new[] { "一次行动", "指定回合", "战斗持续", "本局永久" });
            if (eff.duration == EffectDuration.SpecifiedTurns)
                eff.durationValue = EditorGUILayout.IntField("持续回合数", eff.durationValue);
            else if (eff.duration == EffectDuration.OneAction)
                eff.durationValue = EditorGUILayout.IntField("持续次数", eff.durationValue);
        }

        // ========================================================================
        // 条件编辑
        // ========================================================================
        private void DrawConditions(CardEffect eff)
        {
            _showConditions = EditorGUILayout.Foldout(_showConditions, $"生效条件 ({eff.conditions?.Count ?? 0})");
            if (!_showConditions) return;

            if (eff.conditions == null) eff.conditions = new List<EffectCondition>();

            if (eff.conditions.Count > 0)
            {
                eff.conditionLogic = (ConditionLogic)EditorGUILayout.Popup("条件逻辑", (int)eff.conditionLogic,
                    new[] { "全部满足 (AND)", "任意满足 (OR)" });
            }

            for (int i = 0; i < eff.conditions.Count; i++)
            {
                var cond = eff.conditions[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"条件 {i + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    eff.conditions.RemoveAt(i);
                    return;
                }
                EditorGUILayout.EndHorizontal();

                cond.conditionType = (ConditionType)EditorGUILayout.Popup("条件类型", (int)cond.conditionType,
                    new[] { "发起者属性检查", "目标属性检查", "事件发生", "发起者状态检查", "目标状态检查",
                            "本回合计数", "本场战斗计数", "自定义" });

                switch (cond.conditionType)
                {
                    case ConditionType.SourceAttributeCheck:
                    case ConditionType.TargetAttributeCheck:
                    case ConditionType.TurnCounterCheck:
                    case ConditionType.BattleCounterCheck:
                        cond.attributeRef = (AttributeRef)EditorGUILayout.Popup("属性", (int)cond.attributeRef, GetAttributeRefNames());
                        cond.comparison = (ComparisonOp)EditorGUILayout.Popup("比较方式", (int)cond.comparison,
                            new[] { "小于", "小于等于", "等于", "大于等于", "大于", "不等于" });
                        cond.compareValue = EditorGUILayout.FloatField("比较值", cond.compareValue);
                        break;
                    case ConditionType.EventOccurred:
                        cond.eventName = EditorGUILayout.TextField("事件名称", cond.eventName);
                        break;
                    case ConditionType.SourceHasStatus:
                    case ConditionType.TargetHasStatus:
                        cond.statusType = (StatusType)EditorGUILayout.Popup("状态类型", (int)cond.statusType,
                            new[] { "流血", "破甲", "力量", "敏捷", "疯狂", "下次攻击增伤", "下一张牌减费", "暴击率提升", "暴击伤害提升" });
                        break;
                    case ConditionType.Custom:
                        cond.customConditionScript = (CustomConditionScript)EditorGUILayout.ObjectField("条件脚本", cond.customConditionScript, typeof(CustomConditionScript), false);
                        if (cond.customConditionScript != null)
                            EditorGUILayout.LabelField($"已绑定: {cond.customConditionScript.GetDisplayName()}", EditorStyles.miniLabel);
                        break;
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ 添加条件", EditorStyles.miniButton))
                eff.conditions.Add(new EffectCondition());
        }

        // ========================================================================
        // 能力编辑
        // ========================================================================
        private void DrawAbilityEditor()
        {
            var ability = _viewingUpgrade ? _selectedCard.upgradeAbility : _selectedCard.baseAbility;
            if (ability == null) ability = new AbilityData();

            EditorGUILayout.LabelField(_viewingUpgrade ? "升级能力配置" : "基础能力配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("能力卡：持续监听触发器，事件发生时执行一组普通效果。", MessageType.Info);

            ability.abilityName = EditorGUILayout.TextField("能力名称", ability.abilityName);
            ability.abilityDescription = EditorGUILayout.TextArea(ability.abilityDescription, GUILayout.MinHeight(40));

            ability.trigger = (AbilityTrigger)EditorGUILayout.Popup("触发时机", (int)ability.trigger,
                new[] { "回合开始", "回合结束", "暴击时", "失去理智时", "理智低于阈值",
                        "获得减益时", "每回合首次攻击", "施加破甲时", "自定义事件" });

            // 触发条件
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("触发条件", EditorStyles.boldLabel);
            if (ability.triggerConditions == null) ability.triggerConditions = new List<EffectCondition>();
            if (ability.triggerConditions.Count > 0)
            {
                ability.triggerConditionLogic = (ConditionLogic)EditorGUILayout.Popup("条件逻辑", (int)ability.triggerConditionLogic,
                    new[] { "全部满足 (AND)", "任意满足 (OR)" });
            }
            for (int i = 0; i < ability.triggerConditions.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"触发条件 {i + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    ability.triggerConditions.RemoveAt(i);
                    return;
                }
                EditorGUILayout.EndHorizontal();
                DrawConditionSimple(ability.triggerConditions[i]);
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("+ 添加触发条件", EditorStyles.miniButton))
                ability.triggerConditions.Add(new EffectCondition());

            // 触发限制
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("触发限制", EditorStyles.boldLabel);
            ability.maxTriggers = EditorGUILayout.IntField("总触发次数 (0=无限)", ability.maxTriggers);
            ability.maxTriggersPerTurn = EditorGUILayout.IntField("每回合触发次数 (0=无限)", ability.maxTriggersPerTurn);

            // 自定义能力脚本
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("自定义能力脚本", EditorStyles.boldLabel);
            ability.customAbilityScript = (CustomAbilityScript)EditorGUILayout.ObjectField("脚本", ability.customAbilityScript, typeof(CustomAbilityScript), false);

            // 触发后效果
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("触发后执行的效果", EditorStyles.boldLabel);
            if (ability.triggeredEffects == null) ability.triggeredEffects = new List<CardEffect>();

            for (int i = 0; i < ability.triggeredEffects.Count; i++)
            {
                DrawEffectItem(ability.triggeredEffects, i);
            }
            if (GUILayout.Button("+ 添加触发效果", GUILayout.Height(24)))
                ability.triggeredEffects.Add(new CardEffect { effectName = $"效果 {ability.triggeredEffects.Count + 1}" });
        }

        private void DrawConditionSimple(EffectCondition cond)
        {
            cond.conditionType = (ConditionType)EditorGUILayout.Popup("条件类型", (int)cond.conditionType,
                new[] { "发起者属性检查", "目标属性检查", "事件发生", "发起者状态检查", "目标状态检查",
                        "本回合计数", "本场战斗计数", "自定义" });
            switch (cond.conditionType)
            {
                case ConditionType.SourceAttributeCheck:
                case ConditionType.TargetAttributeCheck:
                case ConditionType.TurnCounterCheck:
                case ConditionType.BattleCounterCheck:
                    cond.attributeRef = (AttributeRef)EditorGUILayout.Popup("属性", (int)cond.attributeRef, GetAttributeRefNames());
                    cond.comparison = (ComparisonOp)EditorGUILayout.Popup("比较", (int)cond.comparison,
                        new[] { "<", "≤", "=", "≥", ">", "≠" });
                    cond.compareValue = EditorGUILayout.FloatField("值", cond.compareValue);
                    break;
                case ConditionType.EventOccurred:
                    cond.eventName = EditorGUILayout.TextField("事件名称", cond.eventName);
                    break;
                case ConditionType.SourceHasStatus:
                case ConditionType.TargetHasStatus:
                    cond.statusType = (StatusType)EditorGUILayout.Popup("状态", (int)cond.statusType,
                        new[] { "流血", "破甲", "力量", "敏捷", "疯狂", "下次攻击增伤", "下一张牌减费", "暴击率提升", "暴击伤害提升" });
                    break;
                case ConditionType.Custom:
                    cond.customConditionScript = (CustomConditionScript)EditorGUILayout.ObjectField("脚本", cond.customConditionScript, typeof(CustomConditionScript), false);
                    break;
            }
        }

        // ========================================================================
        // 预览
        // ========================================================================
        private void DrawPreview()
        {
            EditorGUILayout.BeginVertical("box");
            var card = _selectedCard;

            // 卡牌标题
            EditorGUILayout.LabelField($"【{CardEntry.GetGradeName(card.grade)}】{card.cardName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"类型: {CardEntry.GetCardTypeName(card.cardType)}  费用: {(_viewingUpgrade && card.upgradable ? card.upgradeCost : card.baseCost)}  存在形式: {CardEntry.GetExistenceName(card.existence)}");
            if (card.keyword != CardKeyword.None)
                EditorGUILayout.LabelField($"词条: {CardEntry.GetKeywordName(card.keyword)}");

            EditorGUILayout.Space();

            // 描述
            EditorGUILayout.LabelField("描述:", EditorStyles.boldLabel);
            var desc = card.GetDescription(_viewingUpgrade && card.upgradable);
            EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedLabel);

            // 效果列表
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("效果结算顺序:", EditorStyles.boldLabel);
            var effects = card.GetEffects(_viewingUpgrade && card.upgradable);
            for (int i = 0; i < effects.Count; i++)
            {
                if (!effects[i].enabled) continue;
                EditorGUILayout.LabelField($"{i + 1}. {effects[i].GetDescription()}", EditorStyles.wordWrappedLabel);
            }

            // 能力信息
            if (card.cardType == CardType.Ability)
            {
                var ability = card.GetAbility(_viewingUpgrade && card.upgradable);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("能力信息:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(ability.GetDescription(), EditorStyles.wordWrappedLabel);
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
        private void CreateCard()
        {
            var dir = "Assets/ScriptableObjects/CardEditor";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var card = CreateInstance<CardEntry>();
            card.cardId = $"card_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
            card.cardName = "新卡牌";
            card.baseEffects = new List<CardEffect>();
            card.upgradeEffects = new List<CardEffect>();
            card.baseAbility = new AbilityData();
            card.upgradeAbility = new AbilityData();

            var path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{card.cardName}.asset");
            AssetDatabase.CreateAsset(card, path);
            AssetDatabase.SaveAssets();

            _database.cards.Add(card);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();

            _selectedCard = card;
            _viewingUpgrade = false;
            RefreshFilter();
            EditorGUIUtility.PingObject(card);
        }

        private void DuplicateCard(CardEntry source)
        {
            if (source == null) return;

            var dir = "Assets/ScriptableObjects/CardEditor";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var card = CreateInstance<CardEntry>();
            card.cardId = $"{source.cardId}_copy_{System.Guid.NewGuid().ToString("N").Substring(0, 4)}";
            card.cardName = source.cardName + " (副本)";
            card.baseDescription = source.baseDescription;
            card.upgradeDescription = source.upgradeDescription;
            card.cardArt = source.cardArt;
            card.darkCardArt = source.darkCardArt;
            card.grade = source.grade;
            card.cardType = source.cardType;
            card.existence = source.existence;
            card.baseCost = source.baseCost;
            card.upgradeCost = source.upgradeCost;
            card.keyword = source.keyword;
            card.upgradable = source.upgradable;
            card.designerNotes = source.designerNotes;
            card.customCardScript = source.customCardScript;
            card.baseEffects = source.baseEffects?.ConvertAll(e => e.Clone()) ?? new List<CardEffect>();
            card.upgradeEffects = source.upgradeEffects?.ConvertAll(e => e.Clone()) ?? new List<CardEffect>();
            card.baseAbility = source.baseAbility?.Clone() ?? new AbilityData();
            card.upgradeAbility = source.upgradeAbility?.Clone() ?? new AbilityData();

            var path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{card.cardName}.asset");
            AssetDatabase.CreateAsset(card, path);
            AssetDatabase.SaveAssets();

            _database.cards.Add(card);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();

            _selectedCard = card;
            _viewingUpgrade = false;
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
                _selectedCard.upgradeAbility = _selectedCard.baseAbility?.Clone() ?? new AbilityData();
            }
            else
            {
                _selectedCard.upgradeEffects = _selectedCard.baseEffects?.ConvertAll(e => e.Clone()) ?? new List<CardEffect>();
            }
            EditorUtility.SetDirty(_selectedCard);
        }

        private void ClearUpgradeEffects()
        {
            if (_selectedCard == null) return;
            if (_selectedCard.cardType == CardType.Ability)
            {
                _selectedCard.upgradeAbility = new AbilityData();
            }
            else
            {
                _selectedCard.upgradeEffects = new List<CardEffect>();
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
                if (_filterCost >= 0) query = query.Where(c => c.baseCost == _filterCost);
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
