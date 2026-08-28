using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LightMiniGame.CardEditor;

namespace LightMiniGame.CardEditor.Editor
{
    /// <summary>
    /// EffectNode 编辑器绘制工具 —— 负责在编辑器窗口中绘制效果列表和单个效果的动态字段。
    /// 被 CardEditorWindow 调用。
    /// </summary>
    public static class EffectNodeEditor
    {
        // 折叠状态
        private static readonly Dictionary<string, bool> _foldouts = new();

        // ========================================================================
        // 效果列表绘制
        // ========================================================================
        public static void DrawEffectList(List<EffectNode> effects, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("效果按列表顺序依次结算。可上下移动、复制、启用/禁用。", MessageType.Info);

            if (effects == null) effects = new List<EffectNode>();

            for (int i = 0; i < effects.Count; i++)
            {
                DrawEffectItem(effects, i);
            }

            // 添加按钮 + 模板
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 添加效果", GUILayout.Height(24)))
            {
                effects.Add(new EffectNode { displayName = $"效果 {effects.Count + 1}" });
            }
            if (GUILayout.Button("模板...", GUILayout.Width(60), GUILayout.Height(24)))
            {
                var menu = new GenericMenu();
                foreach (var name in EffectTemplates.TemplateNames)
                {
                    menu.AddItem(new GUIContent(name), false, () =>
                    {
                        var node = EffectTemplates.CreateFromTemplate(name);
                        if (node != null) effects.Add(node);
                    });
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
        }

        // ========================================================================
        // 单个效果绘制
        // ========================================================================
        private static void DrawEffectItem(List<EffectNode> effects, int index)
        {
            var node = effects[index];
            string key = $"eff_{index}_{node.GetHashCode()}";
            if (!_foldouts.ContainsKey(key)) _foldouts[key] = false;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            node.enabled = EditorGUILayout.Toggle(node.enabled, GUILayout.Width(20));
            var oldColor = GUI.color;
            if (!node.enabled) GUI.color = new Color(0.6f, 0.6f, 0.6f);

            string summary = node.GetDescription();
            if (summary.Length > 80) summary = summary.Substring(0, 77) + "...";
            _foldouts[key] = EditorGUILayout.Foldout(_foldouts[key],
                $"{index + 1}. {node.displayName} [{EffectNode.GetOperationName(node.operation)}] {summary}", true);

            // 排序按钮
            EditorGUI.BeginDisabledGroup(index == 0);
            if (GUILayout.Button("↑", EditorStyles.miniButtonLeft, GUILayout.Width(22)))
            {
                (effects[index], effects[index - 1]) = (effects[index - 1], effects[index]);
                GUI.color = oldColor;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(index == effects.Count - 1);
            if (GUILayout.Button("↓", EditorStyles.miniButtonMid, GUILayout.Width(22)))
            {
                (effects[index], effects[index + 1]) = (effects[index + 1], effects[index]);
                GUI.color = oldColor;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("复制", EditorStyles.miniButtonMid, GUILayout.Width(40)))
            {
                effects.Insert(index + 1, node.Clone());
                GUI.color = oldColor;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            if (GUILayout.Button("删除", EditorStyles.miniButtonRight, GUILayout.Width(40)))
            {
                effects.RemoveAt(index);
                GUI.color = oldColor;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            GUI.color = oldColor;
            EditorGUILayout.EndHorizontal();

            if (_foldouts[key])
            {
                EditorGUI.BeginDisabledGroup(!node.enabled);
                DrawEffectFields(node);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
        }

        // ========================================================================
        // 效果字段（根据 Operation 动态显示）
        // ========================================================================
        private static void DrawEffectFields(EffectNode node)
        {
            EditorGUILayout.Space(4);

            // 基础
            node.displayName = EditorGUILayout.TextField("名称", node.displayName);
            node.operation = (EffectOperation)EditorGUILayout.Popup("操作类型", (int)node.operation, GetOperationNames());

            EditorGUILayout.Space(4);

            // 发起者 & 目标
            EditorGUILayout.LabelField("发起者", EditorStyles.miniBoldLabel);
            DrawTargetSelector(node.source);

            EditorGUILayout.LabelField("目标", EditorStyles.miniBoldLabel);
            DrawTargetSelector(node.target);

            EditorGUILayout.Space(4);

            // 按 Operation 显示不同字段
            switch (node.operation)
            {
                case EffectOperation.DealDamage:
                    DrawDamageFields(node);
                    break;
                case EffectOperation.GainBlock:
                    EditorGUILayout.LabelField("格挡配置", EditorStyles.boldLabel);
                    DrawValueNodeField("格挡值", node.value);
                    node.scalingMode = (ScalingMode)EditorGUILayout.Popup("缩放模式", (int)node.scalingMode,
                        new[] { "固定", "力量加成", "自定义" });
                    break;
                case EffectOperation.ModifyAttribute:
                    DrawModifyAttributeFields(node);
                    break;
                case EffectOperation.ModifyResource:
                    DrawModifyResourceFields(node);
                    break;
                case EffectOperation.ApplyStatus:
                    DrawApplyStatusFields(node);
                    break;
                case EffectOperation.RemoveStatus:
                    DrawRemoveStatusFields(node);
                    break;
                case EffectOperation.DrawCards:
                    EditorGUILayout.LabelField("抽牌配置", EditorStyles.boldLabel);
                    DrawValueNodeField("抽牌数", node.value);
                    break;
                case EffectOperation.RestoreActionPoints:
                    EditorGUILayout.LabelField("行动点配置", EditorStyles.boldLabel);
                    DrawValueNodeField("恢复量", node.value);
                    break;
                case EffectOperation.MoveCards:
                    DrawMoveCardsFields(node);
                    break;
                case EffectOperation.CreateCard:
                    DrawCreateCardFields(node);
                    break;
                case EffectOperation.ModifyCardProperty:
                    DrawModifyCardPropertyFields(node);
                    break;
                case EffectOperation.RegisterTrigger:
                    DrawRegisterTriggerFields(node);
                    break;
                case EffectOperation.SetVariable:
                case EffectOperation.ModifyVariable:
                    EditorGUILayout.LabelField("变量操作", EditorStyles.boldLabel);
                    node.outputVariableName = EditorGUILayout.TextField("变量名", node.outputVariableName);
                    DrawValueNodeField("数值", node.value);
                    if (node.operation == EffectOperation.ModifyVariable)
                        node.resourceOp = (ResourceOperation)EditorGUILayout.Popup("操作方式", (int)node.resourceOp,
                            new[] { "增加", "减少", "乘算", "设置" });
                    break;
                case EffectOperation.CustomOperation:
                    EditorGUILayout.LabelField("自定义操作", EditorStyles.boldLabel);
                    node.customOperation = (CustomEffectScript)EditorGUILayout.ObjectField("脚本", node.customOperation, typeof(CustomEffectScript), false);
                    node.customParams = EditorGUILayout.TextField("参数", node.customParams);
                    break;
            }

            EditorGUILayout.Space(4);

            // 输出变量
            node.outputVariableName = EditorGUILayout.TextField("输出变量名", node.outputVariableName);

            EditorGUILayout.Space(4);

            // 条件
            DrawConditionGroup(node.conditions);

            EditorGUILayout.Space(4);

            // 描述预览
            EditorGUILayout.LabelField("摘要:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(node.GetDescription(), EditorStyles.wordWrappedMiniLabel);
        }

        // ========================================================================
        // 伤害字段
        // ========================================================================
        private static void DrawDamageFields(EffectNode node)
        {
            EditorGUILayout.LabelField("伤害配置", EditorStyles.boldLabel);
            DrawValueNodeField("伤害值", node.value);
            DrawValueNodeField("攻击次数", node.repeatCount);
            node.scalingMode = (ScalingMode)EditorGUILayout.Popup("缩放模式", (int)node.scalingMode,
                new[] { "固定", "力量加成", "自定义" });
            node.criticalCheckMode = (CriticalCheckMode)EditorGUILayout.Popup("暴击判定", (int)node.criticalCheckMode,
                new[] { "每段独立", "整牌一次", "必定暴击", "无法暴击" });
            node.ignoreAllBlock = EditorGUILayout.Toggle("无视格挡", node.ignoreAllBlock);
            node.useArmorBreak = EditorGUILayout.Toggle("使用破甲", node.useArmorBreak);
            if (node.useArmorBreak)
                DrawValueNodeField("破甲值", node.armorBreakValue);
        }

        // ========================================================================
        // 修改属性字段
        // ========================================================================
        private static void DrawModifyAttributeFields(EffectNode node)
        {
            EditorGUILayout.LabelField("属性配置", EditorStyles.boldLabel);
            node.attributeType = (LightMiniGame.CardEditor.PlayerAttributeType)EditorGUILayout.Popup("属性",
                (int)node.attributeType, GetAttributeNames());
            node.resourceOp = (ResourceOperation)EditorGUILayout.Popup("操作方式", (int)node.resourceOp,
                new[] { "增加", "减少", "乘算", "设置" });
            DrawValueNodeField("数值", node.value);
            DrawDurationField(node);
        }

        // ========================================================================
        // 修改资源字段
        // ========================================================================
        private static void DrawModifyResourceFields(EffectNode node)
        {
            EditorGUILayout.LabelField("资源配置", EditorStyles.boldLabel);
            node.resourceType = (LightMiniGame.CardEditor.PlayerResourceType)EditorGUILayout.Popup("资源",
                (int)node.resourceType, GetResourceNames());
            node.resourceOp = (ResourceOperation)EditorGUILayout.Popup("操作方式", (int)node.resourceOp,
                new[] { "增加", "减少", "乘算", "设置", "消耗", "消耗全部", "恢复至上限", "限制" });
            DrawValueNodeField("数值", node.value);
        }

        // ========================================================================
        // 施加状态字段
        // ========================================================================
        private static void DrawApplyStatusFields(EffectNode node)
        {
            EditorGUILayout.LabelField("状态配置", EditorStyles.boldLabel);
            node.statusType = (StatusType2)EditorGUILayout.Popup("状态类型", (int)node.statusType, GetStatusNames());
            DrawValueNodeField("层数/数值", node.statusValue);
            node.stackMode = (StatusStackMode)EditorGUILayout.Popup("叠加方式", (int)node.stackMode,
                new[] { "叠加层数", "叠加数值", "替换", "保留较高", "保留较低", "刷新时间", "独立实例" });
            DrawDurationField(node);
        }

        // ========================================================================
        // 移除状态字段
        // ========================================================================
        private static void DrawRemoveStatusFields(EffectNode node)
        {
            EditorGUILayout.LabelField("状态配置", EditorStyles.boldLabel);
            node.statusType = (StatusType2)EditorGUILayout.Popup("状态类型", (int)node.statusType, GetStatusNames());
            DrawValueNodeField("移除层数", node.statusValue);
        }

        // ========================================================================
        // 卡牌区域字段
        // ========================================================================
        private static void DrawMoveCardsFields(EffectNode node)
        {
            EditorGUILayout.LabelField("卡牌区域操作", EditorStyles.boldLabel);
            var newOp = (CardZoneOperation)EditorGUILayout.Popup("操作", (int)node.zoneOperation, GetZoneOpNames());
            if (newOp != node.zoneOperation)
            {
                node.zoneOperation = newOp;
                if (newOp == CardZoneOperation.AddTemporaryKeyword
                    || newOp == CardZoneOperation.RemoveTemporaryKeyword)
                {
                    if (node.target == null) node.target = new TargetSelector();
                    node.target.category = TargetCategory.Card;
                    node.target.cardTarget = CardTarget.AllCardsInHand;
                    if (node.duration == null) node.duration = new EffectDuration();
                    if (node.duration.type == DurationType.Instant)
                        node.duration.type = DurationType.UntilCombatEnd;
                }
            }

            if (node.zoneOperation == CardZoneOperation.AddTemporaryKeyword
                || node.zoneOperation == CardZoneOperation.RemoveTemporaryKeyword)
            {
                EditorGUILayout.HelpBox(
                    "给目标卡牌添加/移除词条。上方「目标」选卡牌（全部手牌 / 手牌随机 / 当前卡等）。也可指定一张卡，则本场该卡的所有副本都会改。",
                    MessageType.Info);
                int kwMask = EditorGUILayout.MaskField("词条（可多选）", (int)node.keywordToApply, CardKeywords.FlagMaskNames);
                node.keywordToApply = (CardKeyword)kwMask;
                node.createdCard = (CardEntry)EditorGUILayout.ObjectField("指定卡牌（可选）", node.createdCard, typeof(CardEntry), false);
                DrawValueNodeField("随机数量", node.zoneCount);
                DrawDurationField(node);
                return;
            }

            node.sourceZone = (CardZoneType)EditorGUILayout.Popup("源区域", (int)node.sourceZone, GetZoneNames());
            node.destinationZone = (CardZoneType)EditorGUILayout.Popup("目标区域", (int)node.destinationZone, GetZoneNames());
            DrawValueNodeField("数量", node.zoneCount);
        }

        private static void DrawCreateCardFields(EffectNode node)
        {
            EditorGUILayout.LabelField("创建卡牌", EditorStyles.boldLabel);
            node.createdCard = (CardEntry)EditorGUILayout.ObjectField("卡牌", node.createdCard, typeof(CardEntry), false);
            DrawValueNodeField("张数", node.value);
            node.destinationZone = (CardZoneType)EditorGUILayout.Popup("放入", (int)node.destinationZone, GetZoneNames());
        }

        private static void DrawModifyCardPropertyFields(EffectNode node)
        {
            EditorGUILayout.LabelField("本场覆盖卡牌数值", EditorStyles.boldLabel);
            node.createdCard = (CardEntry)EditorGUILayout.ObjectField("卡牌", node.createdCard, typeof(CardEntry), false);
            node.statusType = (StatusType2)EditorGUILayout.Popup("状态字段", (int)node.statusType, GetStatusNames());
            DrawValueNodeField("覆盖值", node.statusValue);
        }

        // ========================================================================
        // 注册触发器字段
        // ========================================================================
        private static void DrawRegisterTriggerFields(EffectNode node)
        {
            EditorGUILayout.LabelField("触发器配置", EditorStyles.boldLabel);
            node.triggerEvent = (TriggerEvent)EditorGUILayout.Popup("触发事件", (int)node.triggerEvent, GetTriggerNames());
            node.maxTriggers = EditorGUILayout.IntField("总触发限制(0=无限)", node.maxTriggers);
            node.maxTriggersPerTurn = EditorGUILayout.IntField("每回合限制(0=无限)", node.maxTriggersPerTurn);
            node.activeOnlyWhenOwnerIsActive = EditorGUILayout.Toggle("仅持有角色激活时", node.activeOnlyWhenOwnerIsActive);
            DrawDurationField(node);
            EditorGUILayout.HelpBox("下方「条件」在触发时检查，打出本牌时不会拦截注册。能力类请把持续时间设为「直到战斗结束」。", MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"子效果 ({node.childEffects.Count})", EditorStyles.boldLabel);
            DrawEffectList(node.childEffects, "触发后执行");
        }

        // ========================================================================
        // 目标选择器
        // ========================================================================
        private static void DrawTargetSelector(TargetSelector sel)
        {
            sel.category = (TargetCategory)EditorGUILayout.Popup("类别", (int)sel.category,
                new[] { "战斗单位", "角色", "敌人", "卡牌", "卡牌区域", "触发器", "变量", "全局" });

            switch (sel.category)
            {
                case TargetCategory.Enemy:
                case TargetCategory.CombatUnit:
                case TargetCategory.Character:
                    sel.unitTarget = (CombatUnitTarget)EditorGUILayout.Popup("目标", (int)sel.unitTarget, GetUnitTargetNames());
                    break;
                case TargetCategory.Card:
                    sel.cardTarget = (CardTarget)EditorGUILayout.Popup("卡牌目标", (int)sel.cardTarget, GetCardTargetNames());
                    sel.selectionMode = (CardSelectionMode)EditorGUILayout.Popup("选择模式", (int)sel.selectionMode,
                        new[] { "全部", "随机N张", "选择N张", "顶N张", "底N张", "首个匹配", "末个匹配", "当前卡", "最后打出" });
                    if (sel.selectionMode != CardSelectionMode.All && sel.selectionMode != CardSelectionMode.FirstMatching && sel.selectionMode != CardSelectionMode.LastMatching)
                        sel.selectionCount = EditorGUILayout.IntField("数量", sel.selectionCount);
                    EditorGUILayout.LabelField("筛选", EditorStyles.miniBoldLabel);
                    sel.cardFilterType = (CardType2)EditorGUILayout.Popup("类型", (int)sel.cardFilterType, new[] { "攻击", "技能", "能力" });
                    break;
            }
        }

        // ========================================================================
        // 条件组
        // ========================================================================
        private static void DrawConditionGroup(ConditionGroup group)
        {
            string key = $"cond_{group.GetHashCode()}";
            if (!_foldouts.ContainsKey(key)) _foldouts[key] = false;

            int count = group?.conditions?.Count ?? 0;
            _foldouts[key] = EditorGUILayout.Foldout(_foldouts[key], $"条件 ({count})", true);
            if (!_foldouts[key]) return;

            if (count > 0)
                group.logic = (ConditionLogic2)EditorGUILayout.Popup("逻辑", (int)group.logic,
                    new[] { "全部满足(AND)", "任意满足(OR)", "全部不满足(NONE)", "取反(NOT)" });

            for (int i = 0; i < group.conditions.Count; i++)
            {
                var cond = group.conditions[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"条件 {i + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    group.conditions.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
                EditorGUILayout.EndHorizontal();

                cond.conditionType = (ConditionType2)EditorGUILayout.Popup("类型", (int)cond.conditionType,
                    new[] { "比较值", "有状态", "无状态", "事件检查", "标志检查", "卡牌属性", "目标存在", "概率", "自定义", "打出的卡是" });

                switch (cond.conditionType)
                {
                    case ConditionType2.CompareValue:
                        DrawValueNodeField("左值", cond.leftValue);
                        cond.comparison = (ComparisonOperator)EditorGUILayout.Popup("比较", (int)cond.comparison,
                            new[] { "<", "≤", "=", "≠", "≥", ">" });
                        DrawValueNodeField("右值", cond.rightValue);
                        break;
                    case ConditionType2.HasStatus:
                    case ConditionType2.DoesNotHaveStatus:
                        cond.statusType = (StatusType2)EditorGUILayout.Popup("状态", (int)cond.statusType, GetStatusNames());
                        if (cond.statusTarget == null)
                            cond.statusTarget = new TargetSelector { category = TargetCategory.Enemy, unitTarget = CombatUnitTarget.SelectedEnemy };
                        EditorGUILayout.LabelField("检查目标", EditorStyles.miniBoldLabel);
                        DrawTargetSelector(cond.statusTarget);
                        break;
                    case ConditionType2.PlayedCardMatches:
                        cond.cardRef = (CardEntry)EditorGUILayout.ObjectField("卡牌", cond.cardRef, typeof(CardEntry), false);
                        break;
                    case ConditionType2.EventContextCheck:
                        cond.eventName = EditorGUILayout.TextField("事件名", cond.eventName);
                        break;
                    case ConditionType2.RuntimeFlagCheck:
                        cond.flagRef = (CombatFlagType)EditorGUILayout.Popup("标志", (int)cond.flagRef, GetFlagNames());
                        break;
                    case ConditionType2.ChanceCheck:
                        cond.chancePercent = EditorGUILayout.Slider("概率(%)", cond.chancePercent, 0f, 100f);
                        break;
                    case ConditionType2.CustomCondition:
                        cond.customConditionScript = (CustomConditionScript)EditorGUILayout.ObjectField("脚本", cond.customConditionScript, typeof(CustomConditionScript), false);
                        break;
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ 添加条件", EditorStyles.miniButton))
            {
                if (group.conditions == null) group.conditions = new List<ConditionEntry>();
                group.conditions.Add(new ConditionEntry { leftValue = ValueNode.Constant(0), rightValue = ValueNode.Constant(0) });
            }
        }

        // ========================================================================
        // 数值表达式节点
        // ========================================================================
        private static void DrawValueNodeField(string label, ValueNode node)
        {
            if (node == null) { EditorGUILayout.LabelField(label, "(null)"); return; }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(80));

            node.nodeType = (ValueNodeType)EditorGUILayout.Popup((int)node.nodeType, GetValueTypeNames(), GUILayout.Width(120));

            switch (node.nodeType)
            {
                case ValueNodeType.IntegerConstant:
                    node.intValue = EditorGUILayout.IntField(node.intValue);
                    break;
                case ValueNodeType.FloatConstant:
                    node.floatValue = EditorGUILayout.FloatField(node.floatValue);
                    break;
                case ValueNodeType.ReadAttribute:
                    node.attributeRef = (LightMiniGame.CardEditor.PlayerAttributeType)EditorGUILayout.Popup((int)node.attributeRef, GetAttributeNames());
                    break;
                case ValueNodeType.ReadResource:
                    node.resourceRef = (LightMiniGame.CardEditor.PlayerResourceType)EditorGUILayout.Popup((int)node.resourceRef, GetResourceNames());
                    break;
                case ValueNodeType.ReadResourceLostAmount:
                    node.resourceRef = (LightMiniGame.CardEditor.PlayerResourceType)EditorGUILayout.Popup((int)node.resourceRef, GetResourceNames());
                    break;
                case ValueNodeType.ReadStatusStacks:
                    node.statusRef = (StatusType2)EditorGUILayout.Popup((int)node.statusRef, GetStatusNames());
                    break;
                case ValueNodeType.ReadAllEnemiesStatusStacks:
                    node.statusRef = (StatusType2)EditorGUILayout.Popup((int)node.statusRef, GetStatusNames());
                    break;
                case ValueNodeType.ReadCounter:
                    node.counterRef = (CombatCounterType)EditorGUILayout.Popup((int)node.counterRef, GetCounterNames());
                    break;
                case ValueNodeType.ReadRuntimeFlag:
                    node.flagRef = (CombatFlagType)EditorGUILayout.Popup((int)node.flagRef, GetFlagNames());
                    break;
                case ValueNodeType.ReadLocalVariable:
                    node.variableName = EditorGUILayout.TextField(node.variableName);
                    break;
                case ValueNodeType.ReadLastEffectResult:
                    node.resultRef = (EffectResultType)EditorGUILayout.Popup((int)node.resultRef, GetResultNames());
                    break;
                case ValueNodeType.Add:
                case ValueNodeType.Subtract:
                case ValueNodeType.Multiply:
                case ValueNodeType.Divide:
                case ValueNodeType.Min:
                case ValueNodeType.Max:
                case ValueNodeType.Clamp:
                    EditorGUILayout.LabelField("子节点:", GUILayout.Width(50));
                    break;
                case ValueNodeType.EveryNConvertToM:
                    node.everyN = EditorGUILayout.IntField("N", node.everyN);
                    node.convertToM = EditorGUILayout.IntField("M", node.convertToM);
                    break;
                case ValueNodeType.Modulo:
                    EditorGUILayout.LabelField("子节点:", GUILayout.Width(50));
                    break;
            }
            EditorGUILayout.EndHorizontal();

            // 运算节点显示子节点
            bool isOpNode = node.nodeType is ValueNodeType.Add or ValueNodeType.Subtract or ValueNodeType.Multiply
                or ValueNodeType.Divide or ValueNodeType.Min or ValueNodeType.Max or ValueNodeType.Clamp
                or ValueNodeType.Floor or ValueNodeType.Ceil or ValueNodeType.Round or ValueNodeType.Absolute
                or ValueNodeType.Negate or ValueNodeType.Percentage or ValueNodeType.EveryNConvertToM
                or ValueNodeType.Modulo;

            if (isOpNode)
            {
                // 运算节点 operands 为空时自动补齐默认子节点
                if (node.operands == null) node.operands = new List<ValueNode>();
                int requiredCount = node.nodeType switch
                {
                    ValueNodeType.Add or ValueNodeType.Subtract or ValueNodeType.Multiply or ValueNodeType.Divide
                        or ValueNodeType.Min or ValueNodeType.Max or ValueNodeType.Percentage or ValueNodeType.Modulo => 2,
                    ValueNodeType.Clamp => 3,
                    ValueNodeType.Floor or ValueNodeType.Ceil or ValueNodeType.Round
                        or ValueNodeType.Absolute or ValueNodeType.Negate or ValueNodeType.EveryNConvertToM => 1,
                    _ => 0
                };
                while (node.operands.Count < requiredCount)
                    node.operands.Add(ValueNode.Constant(0));

                EditorGUI.indentLevel++;
                string opKey = $"vn_{node.GetHashCode()}";
                if (!_foldouts.ContainsKey(opKey)) _foldouts[opKey] = true;
                _foldouts[opKey] = EditorGUILayout.Foldout(_foldouts[opKey], $"子节点 ({node.operands.Count})", true);
                if (_foldouts[opKey])
                {
                    for (int i = 0; i < node.operands.Count; i++)
                    {
                        if (node.operands[i] == null) node.operands[i] = ValueNode.Constant(0);
                        DrawValueNodeField($"[{i}]", node.operands[i]);
                    }
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("+ 子节点", EditorStyles.miniButton))
                        node.operands.Add(ValueNode.Constant(0));
                    if (node.operands.Count > requiredCount && GUILayout.Button("- 移除末尾", EditorStyles.miniButton))
                        node.operands.RemoveAt(node.operands.Count - 1);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            // 显示表达式描述
            EditorGUILayout.LabelField("= " + node.GetDescription(), EditorStyles.miniLabel);
        }

        // ========================================================================
        // 持续时间
        // ========================================================================
        private static void DrawDurationField(EffectNode node)
        {
            EditorGUILayout.LabelField("持续时间", EditorStyles.miniBoldLabel);
            node.duration.type = (DurationType)EditorGUILayout.Popup("类型", (int)node.duration.type,
                new[] { "立即", "下次触发", "触发N次", "本回合", "N回合", "角色切换前", "战斗结束前", "本局永久" });
            if (node.duration.type == DurationType.Turns)
                node.duration.turns = EditorGUILayout.IntField("回合数", node.duration.turns);
            if (node.duration.type == DurationType.TriggerCount)
                node.duration.triggerCount = EditorGUILayout.IntField("触发次数", node.duration.triggerCount);
        }

        // ========================================================================
        // 枚举名称数组
        // ========================================================================
        private static string[] GetOperationNames() => new[]
        {
            "伤害", "格挡", "修改属性", "修改资源", "施加状态", "移除状态", "抽牌", "恢复行动点",
            "卡牌区域操作", "创建卡牌", "复制卡牌", "自动打牌", "重新释放本牌", "修改费用",
            "修改卡牌属性", "切换角色", "注册触发器", "移除触发器", "设置变量", "修改变量", "自定义操作"
        };

        private static string[] GetAttributeNames() => new[]
        {
            "最大生命", "力量", "敏捷", "回复", "吸血", "暴击率", "暴击伤害倍率",
            "每回合行动点", "每回合抽牌数", "总伤害倍率", "受击倍率"
        };

        private static string[] GetResourceNames() => new[]
        {
            "当前生命", "理智", "行动点", "货币", "热度", "格挡"
        };

        private static string[] GetStatusNames() => new[]
        {
            "破甲", "流血", "卡壳", "疯狂", "易伤", "临时力量", "临时敏捷",
            "下次攻击增伤", "下次暴伤提升", "下次必暴", "下张牌减费", "下张攻击牌减费",
            "手牌减费", "暴击率变化", "暴伤变化", "格挡保留", "自定义状态", "疲惫"
        };

        private static string[] GetCounterNames() => new[]
        {
            "本回合出牌数", "本回合攻击牌数", "本回合技能牌数", "本回合能力牌数",
            "本回合攻击次数", "本回合命中次数", "本回合暴击次数", "本回合受到伤害",
            "本回合受伤害次数", "本回合造成伤害", "本回合失去理智", "本场失去理智",
            "本回合获得热度", "本回合降低热度", "本回合切换角色", "本场切换角色",
            "本回合击杀敌人", "本场击杀敌人", "本回合获得格挡", "本回合失去格挡",
            "本回合抽牌数", "本回合弃牌数", "本回合消耗牌数", "当前攻击牌索引",
            "当前攻击段索引", "当前攻击总段数"
        };

        private static string[] GetFlagNames() => new[]
        {
            "本回合受伤", "本回合攻击", "本回合出牌", "本回合切换角色",
            "当前段暴击", "本次攻击有暴击", "本次攻击击杀", "低理智", "过热",
            "本回合首次攻击", "本回合首张攻击牌"
        };

        private static string[] GetResultNames() => new[]
        {
            "请求值", "实际值", "实际伤害", "实际生命伤害", "格挡吸收", "实际获得格挡",
            "实际消耗格挡", "实际增加资源", "实际减少资源", "实际降低热度", "实际失去理智",
            "抽牌数", "弃牌数", "消耗牌数", "击杀敌人数", "受影响目标数",
            "暴击次数", "是否有暴击", "施加状态层数", "移除状态层数"
        };

        private static string[] GetUnitTargetNames() => new[]
        {
            "当前角色", "登场角色", "退场角色", "选定敌人", "随机敌人", "所有敌人",
            "效果发起者", "当前攻击目标", "被击杀敌人", "所有角色", "指定角色",
            "生命最低敌人", "生命最高敌人", "破甲最高敌人", "随机N个敌人"
        };

        private static string[] GetCardTargetNames() => new[]
        {
            "当前卡牌", "下一张打出", "下一张攻击牌", "下一张技能牌", "下一张能力牌",
            "手牌选中", "手牌随机", "全部手牌", "抽牌堆顶", "弃牌堆", "消耗堆",
            "本回合打出", "最后打出"
        };

        private static string[] GetZoneNames() => new[]
        {
            "手牌", "抽牌堆", "弃牌堆", "消耗堆", "永久牌库", "本回合打出", "临时生成"
        };

        private static string[] GetZoneOpNames() => new[]
        {
            "抽牌", "弃牌", "消耗", "永久移除", "移到手牌", "移到抽牌堆顶", "移到抽牌堆底",
            "移到弃牌堆", "洗入抽牌堆", "创建", "复制", "自动打出", "重新释放", "修改费用",
            "添加临时词条", "移除临时词条"
        };

        private static string[] GetTriggerNames() => new[]
        {
            "出牌尝试", "出牌后", "出攻击牌后", "出技能牌后", "出能力牌后", "抽牌后", "弃牌后",
            "消耗后", "费用支付后", "攻击前", "命中前", "命中后", "暴击后", "造成伤害后",
            "命中后", "攻击结束后", "击杀敌人后", "获得格挡后", "失去格挡后", "受到伤害后",
            "失去生命后", "理智变化后", "失去理智后", "热度变化后", "获得热度后", "降低热度后",
            "过热时", "回合开始", "回合结束", "本回合首次攻击", "本回合第N次攻击",
            "施加状态后", "获得减益后", "施加破甲后", "施加流血后", "角色切换前", "角色切换后",
            "角色激活", "角色停用", "战斗开始", "战斗结束"
        };

        private static string[] GetValueTypeNames() => new[]
        {
            "整数常量", "浮点常量", "读取属性", "读取资源", "读取已损失资源", "读取状态层数",
            "读取计数器", "读取标志", "读取卡牌费用", "读取实际支付费用", "手牌数", "抽牌堆数",
            "弃牌堆数", "敌人数", "目标数", "读取局部变量", "读取效果结果",
            "加", "减", "乘", "除", "向下取整", "向上取整", "四舍五入", "最小", "最大",
            "限制", "绝对值", "取反", "百分比", "每N转M",
            "全体状态层数", "手牌上限", "手牌空位", "取模"
        };
    }
}
