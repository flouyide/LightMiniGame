using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using LightMiniGame.CardEditor;

namespace LightMiniGame.CardEditor.Editor
{
    /// <summary>
    /// 卡牌测试窗口 —— 在编辑器内模拟打出卡牌，预览效果执行过程
    /// </summary>
    public class CardTestWindow : EditorWindow
    {
        private CardEntry _card;
        private bool _useUpgrade;

        // 测试参数
        private int _playerHP = 100;
        private int _playerMaxHP = 100;
        private int _playerStrength = 0;
        private int _playerDexterity = 0;
        private float _playerCritRate = 0.1f;
        private float _playerCritDamage = 1.5f;
        private int _playerSanity = 100;
        private int _playerEnergy = 3;
        private int _playerBleed = 0;

        private int _enemyCount = 1;
        private int _enemyHP = 50;
        private int _enemyMaxHP = 50;
        private int _enemyArmor = 0;
        private int _enemyBleed = 0;
        private int _enemyArmorBreak = 0;

        private int _randomSeed = 12345;

        // 结果
        private List<string> _logLines = new List<string>();
        private Vector2 _scroll;
        private bool _tested;

        [MenuItem("Tools/卡牌编辑器/测试窗口")]
        public static void Open()
        {
            var window = GetWindow<CardTestWindow>("卡牌测试");
            window.minSize = new Vector2(500, 700);
        }

        public static void Open(CardEntry card)
        {
            var window = GetWindow<CardTestWindow>("卡牌测试");
            window._card = card;
            window.Show();
        }

        private void OnGUI()
        {
            if (_card == null)
            {
                EditorGUILayout.LabelField("请先在卡牌编辑器中选择一张卡牌", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // 卡牌信息
            EditorGUILayout.LabelField($"测试卡牌: {_card.cardName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"类型: {CardEntry.GetCardTypeName(_card.cardType)}  费用: {_card.GetCost(_useUpgrade)}");

            EditorGUILayout.Space();

            // 升级选择
            if (_card.upgradable)
            {
                _useUpgrade = EditorGUILayout.Toggle("使用升级版", _useUpgrade);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("玩家属性", EditorStyles.boldLabel);
            _playerHP = EditorGUILayout.IntField("当前生命", _playerHP);
            _playerMaxHP = EditorGUILayout.IntField("最大生命", _playerMaxHP);
            _playerStrength = EditorGUILayout.IntField("力量", _playerStrength);
            _playerDexterity = EditorGUILayout.IntField("敏捷", _playerDexterity);
            _playerCritRate = EditorGUILayout.Slider("暴击率", _playerCritRate, 0f, 1f);
            _playerCritDamage = EditorGUILayout.Slider("暴击伤害倍率", _playerCritDamage, 1f, 5f);
            _playerSanity = EditorGUILayout.IntField("当前理智", _playerSanity);
            _playerEnergy = EditorGUILayout.IntField("当前能量", _playerEnergy);
            _playerBleed = EditorGUILayout.IntField("玩家流血值", _playerBleed);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("敌人属性", EditorStyles.boldLabel);
            _enemyCount = EditorGUILayout.IntField("敌人数量", _enemyCount);
            if (_enemyCount < 1) _enemyCount = 1;
            _enemyHP = EditorGUILayout.IntField("敌人生命", _enemyHP);
            _enemyMaxHP = EditorGUILayout.IntField("敌人最大生命", _enemyMaxHP);
            _enemyArmor = EditorGUILayout.IntField("敌人护甲", _enemyArmor);
            _enemyBleed = EditorGUILayout.IntField("敌人流血值", _enemyBleed);
            _enemyArmorBreak = EditorGUILayout.IntField("敌人破甲值", _enemyArmorBreak);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("随机设置", EditorStyles.boldLabel);
            _randomSeed = EditorGUILayout.IntField("随机种子", _randomSeed);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("执行测试", GUILayout.Height(30)))
            {
                RunTest();
            }

            EditorGUILayout.Space(10);

            // 结果显示
            if (_tested)
            {
                EditorGUILayout.LabelField("测试结果", EditorStyles.boldLabel);

                // 最终状态
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("最终状态:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"玩家: 生命={_playerHP}/{_playerMaxHP}  能量={_playerEnergy}  力量={_playerStrength}  敏捷={_playerDexterity}");
                EditorGUILayout.LabelField($"      暴击率={_playerCritRate:P0}  流血={_playerBleed}  理智={_playerSanity}");
                EditorGUILayout.LabelField($"敌人: 生命={_enemyHP}/{_enemyMaxHP}  护甲={_enemyArmor}  破甲={_enemyArmorBreak}  流血={_enemyBleed}");
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();

                // 执行日志
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("执行日志:", EditorStyles.boldLabel);
                foreach (var line in _logLines)
                {
                    EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        // ========================================================================
        // 测试执行
        // ========================================================================
        private void RunTest()
        {
            _logLines.Clear();
            _tested = true;

            UnityEngine.Random.InitState(_randomSeed);

            Log($"═══ 开始测试卡牌: {_card.cardName} ({(_useUpgrade && _card.upgradable ? "升级版" : "基础版")}) ═══");
            Log($"初始状态: 玩家HP={_playerHP} 力量={_playerStrength} 敏捷={_playerDexterity} 能量={_playerEnergy} 暴击率={_playerCritRate:P0}");
            Log($"初始状态: 敌人HP={_enemyHP} 护甲={_enemyArmor} 破甲={_enemyArmorBreak} 流血={_enemyBleed}");
            Log("");

            // 扣除费用
            int cost = _card.GetCost(_useUpgrade && _card.upgradable);
            _playerEnergy -= cost;
            Log($"[费用] 消耗 {cost} 点能量，剩余 {_playerEnergy}");

            // 卡牌存在形式
            switch (_card.existence)
            {
                case CardExistence.Normal:
                    Log("[存在形式] 普通 — 使用后进入弃牌堆");
                    break;
                case CardExistence.BattleRemove:
                    Log("[存在形式] 战斗内移除 — 从本次战斗牌库移除");
                    break;
                case CardExistence.PermanentRemove:
                    Log("[存在形式] 永久移除 — 从玩家牌库永久移除");
                    break;
            }

            Log("");

            if (_card.cardType == CardType.Ability)
            {
                RunAbilityTest();
            }
            else
            {
                RunEffectListTest();
            }

            Log("");
            Log("═══ 测试结束 ═══");
        }

        private void RunEffectListTest()
        {
            var effects = _card.GetEffects(_useUpgrade && _card.upgradable);
            Log($"共 {effects.Count} 个效果，按顺序结算:");
            Log("");

            for (int i = 0; i < effects.Count; i++)
            {
                var eff = effects[i];
                Log($"--- 效果 {i + 1}: {eff.effectName} [{CardEffect.GetEffectTypeName(eff.effectType)}] ---");

                if (!eff.enabled)
                {
                    Log("  ⏸ 已禁用，跳过");
                    continue;
                }

                // 条件检查
                if (eff.conditions != null && eff.conditions.Count > 0)
                {
                    bool conditionsMet = EvaluateConditions(eff);
                    if (!conditionsMet)
                    {
                        Log("  ✗ 条件不满足，跳过此效果");
                        continue;
                    }
                    Log("  ✓ 条件满足");
                }

                // 执行效果
                ExecuteEffect(eff);
                Log("");
            }
        }

        private void RunAbilityTest()
        {
            var ability = _card.GetAbility(_useUpgrade && _card.upgradable);
            Log($"能力: {ability.abilityName}");
            Log($"触发时机: {AbilityData.GetTriggerName(ability.trigger)}");
            Log($"触发后效果: {ability.triggeredEffects.Count} 个");
            Log("");

            // 模拟触发一次
            Log("▼ 模拟触发一次能力 ▼");
            for (int i = 0; i < ability.triggeredEffects.Count; i++)
            {
                var eff = ability.triggeredEffects[i];
                Log($"--- 触发效果 {i + 1}: {eff.effectName} [{CardEffect.GetEffectTypeName(eff.effectType)}] ---");

                if (!eff.enabled)
                {
                    Log("  ⏸ 已禁用，跳过");
                    continue;
                }

                if (eff.conditions != null && eff.conditions.Count > 0)
                {
                    bool conditionsMet = EvaluateConditions(eff);
                    if (!conditionsMet)
                    {
                        Log("  ✗ 条件不满足，跳过");
                        continue;
                    }
                    Log("  ✓ 条件满足");
                }

                ExecuteEffect(eff);
                Log("");
            }
        }

        // ========================================================================
        // 效果执行
        // ========================================================================
        private void ExecuteEffect(CardEffect eff)
        {
            switch (eff.effectType)
            {
                case EffectType.Damage:
                    ExecuteDamage(eff);
                    break;
                case EffectType.Armor:
                    ExecuteArmor(eff);
                    break;
                case EffectType.DrawCard:
                    ExecuteDrawCard(eff);
                    break;
                case EffectType.RestoreEnergy:
                    ExecuteRestoreEnergy(eff);
                    break;
                case EffectType.ModifyAttribute:
                    ExecuteModifyAttribute(eff);
                    break;
                case EffectType.ModifyHandCost:
                    Log($"  [修改手牌费用] {CardEffect.GetHandCostTargetName(eff.handCostTarget)} {(eff.costChange >= 0 ? "+" : "")}{eff.costChange}");
                    break;
                case EffectType.ApplyStatus:
                    ExecuteApplyStatus(eff);
                    break;
                case EffectType.Custom:
                    if (eff.customEffectScript != null)
                        Log($"  [自定义效果] 执行脚本: {eff.customEffectScript.GetDisplayName()}");
                    else
                        Log($"  [自定义效果] ⚠ 未绑定脚本，跳过");
                    break;
            }
        }

        private void ExecuteDamage(CardEffect eff)
        {
            int baseDamage = EvaluateValue(eff.value);
            Log($"  [伤害] 基础伤害: {eff.value.GetDescription()} = {baseDamage}");

            for (int hit = 0; hit < eff.times; hit++)
            {
                bool isCrit = eff.alwaysCrit || UnityEngine.Random.value < _playerCritRate;
                int hitDamage = baseDamage;
                if (isCrit) hitDamage = Mathf.RoundToInt(hitDamage * _playerCritDamage);

                bool ignoreArmor = eff.ignoreArmor;
                int armorReduction = 0;

                if (!ignoreArmor && _enemyArmor > 0)
                {
                    armorReduction = Mathf.Min(_enemyArmor, hitDamage);
                    _enemyArmor -= armorReduction;
                    hitDamage -= armorReduction;
                }

                _enemyHP -= hitDamage;
                if (_enemyHP < 0) _enemyHP = 0;

                string critText = isCrit ? $" ✓暴击(x{_playerCritDamage:F1})" : " ✗未暴击";
                string armorText = armorReduction > 0 ? $" 护甲抵消{armorReduction}" : "";
                string ignoreText = ignoreArmor ? " 无视护甲" : "";

                Log($"  第{hit + 1}击: 伤害={hitDamage}{critText}{armorText}{ignoreText} → 敌人HP={_enemyHP}/{_enemyMaxHP}");

                if (eff.applyArmorBreak && eff.armorBreakAmount > 0)
                {
                    _enemyArmorBreak += eff.armorBreakAmount;
                    Log($"    施加破甲 {eff.armorBreakAmount}，敌人破甲值={_enemyArmorBreak}");
                }
            }
        }

        private void ExecuteArmor(CardEffect eff)
        {
            int armor = EvaluateValue(eff.value);
            Log($"  [护甲] 获得 {eff.value.GetDescription()} = {armor} 点护甲");
            Log($"  (护甲是否跨回合保留由玩家全局属性决定)");
        }

        private void ExecuteDrawCard(CardEffect eff)
        {
            int count = EvaluateValue(eff.value);
            Log($"  [抽牌] 抽 {count} 张牌 ({CardEffect.GetTimingName(eff.timing)})");
        }

        private void ExecuteRestoreEnergy(CardEffect eff)
        {
            int amount = EvaluateValue(eff.value);
            _playerEnergy += amount;
            Log($"  [回费] 恢复 {amount} 点能量 ({CardEffect.GetTimingName(eff.timing)})，当前能量={_playerEnergy}");
        }

        private void ExecuteModifyAttribute(CardEffect eff)
        {
            int amount = EvaluateValue(eff.value);
            string target = CardEffect.GetBuffTargetName(eff.buffTarget);
            string attr = CardEffect.GetModAttrName(eff.modAttribute);
            string method = CardEffect.GetModifyMethodName(eff.modifyMethod);

            Log($"  [修改属性] {target} {attr} {method} {amount} ({CardEffect.GetDurationName(eff.duration)})");

            // 简化模拟：直接修改对应属性
            switch (eff.modAttribute)
            {
                case ModifiableAttribute.Strength:
                    _playerStrength = ApplyModify(_playerStrength, amount, eff.modifyMethod);
                    Log($"    力量 → {_playerStrength}");
                    break;
                case ModifiableAttribute.Dexterity:
                    _playerDexterity = ApplyModify(_playerDexterity, amount, eff.modifyMethod);
                    Log($"    敏捷 → {_playerDexterity}");
                    break;
                case ModifiableAttribute.PlayerCritRate:
                    _playerCritRate = ApplyModifyFloat(_playerCritRate, amount / 100f, eff.modifyMethod);
                    Log($"    暴击率 → {_playerCritRate:P0}");
                    break;
                case ModifiableAttribute.PlayerCritDamage:
                    _playerCritDamage = ApplyModifyFloat(_playerCritDamage, amount / 100f, eff.modifyMethod);
                    Log($"    暴击伤害 → {_playerCritDamage:F2}");
                    break;
                case ModifiableAttribute.MaxHP:
                    _playerMaxHP = ApplyModify(_playerMaxHP, amount, eff.modifyMethod);
                    Log($"    最大生命 → {_playerMaxHP}");
                    break;
                case ModifiableAttribute.CurrentHP:
                    _playerHP = ApplyModify(_playerHP, amount, eff.modifyMethod);
                    Log($"    当前生命 → {_playerHP}");
                    break;
                case ModifiableAttribute.ArmorBreakValue:
                    _enemyArmorBreak = ApplyModify(_enemyArmorBreak, amount, eff.modifyMethod);
                    Log($"    敌人破甲值 → {_enemyArmorBreak}");
                    break;
                case ModifiableAttribute.BleedValue:
                    _enemyBleed = ApplyModify(_enemyBleed, amount, eff.modifyMethod);
                    Log($"    敌人流血值 → {_enemyBleed}");
                    break;
                case ModifiableAttribute.EnemyCritRate:
                    Log($"    敌人被暴击率 {method} {amount}% (需运行时处理)");
                    break;
                case ModifiableAttribute.EnemyCritDamage:
                    Log($"    敌人暴击伤害 {method} {amount}% (需运行时处理)");
                    break;
                case ModifiableAttribute.DrawPerTurn:
                    Log($"    每回合抽牌数 {method} {amount} (需运行时处理)");
                    break;
                case ModifiableAttribute.EnergyPerTurn:
                    Log($"    每回合能量 {method} {amount} (需运行时处理)");
                    break;
                case ModifiableAttribute.Currency:
                    Log($"    货币 {method} {amount} (需运行时处理)");
                    break;
                case ModifiableAttribute.HandCost:
                    Log($"    手牌费用 {method} {amount} (需运行时处理)");
                    break;
            }
        }

        private void ExecuteApplyStatus(CardEffect eff)
        {
            string statusName = EffectCondition.GetStatusName(eff.statusType);
            Log($"  [施加状态] {statusName} {eff.statusStacks}层 → {CardEffect.GetTargetName(eff.target)} ({CardEffect.GetDurationName(eff.duration)})");

            switch (eff.statusType)
            {
                case StatusType.Bleed:
                    _enemyBleed += eff.statusStacks;
                    Log($"    敌人流血值 → {_enemyBleed}");
                    break;
                case StatusType.ArmorBreak:
                    _enemyArmorBreak += eff.statusStacks;
                    Log($"    敌人破甲值 → {_enemyArmorBreak}");
                    break;
                case StatusType.Strength:
                    _playerStrength += eff.statusStacks;
                    Log($"    玩家力量 → {_playerStrength}");
                    break;
                case StatusType.Dexterity:
                    _playerDexterity += eff.statusStacks;
                    Log($"    玩家敏捷 → {_playerDexterity}");
                    break;
                case StatusType.CritRateBoost:
                    _playerCritRate += eff.statusStacks / 100f;
                    Log($"    玩家暴击率 → {_playerCritRate:P0}");
                    break;
                case StatusType.CritDamageBoost:
                    _playerCritDamage += eff.statusStacks / 100f;
                    Log($"    玩家暴击伤害 → {_playerCritDamage:F2}");
                    break;
                default:
                    Log($"    (状态 {statusName} 需战斗系统运行时处理)");
                    break;
            }
        }

        // ========================================================================
        // 辅助方法
        // ========================================================================
        private int EvaluateValue(ValueExpression val)
        {
            int attrVal = GetAttrValue(val.attributeRef);
            int counterVal = GetAttrValue(val.counterRef);

            return val.formulaType switch
            {
                ValueFormulaType.Fixed => val.baseValue,
                ValueFormulaType.BasePlusAttribute => val.baseValue + attrVal,
                ValueFormulaType.BasePlusAttributeTimesCoeff => val.baseValue + Mathf.RoundToInt(attrVal * val.coefficient),
                ValueFormulaType.BasePlusCounterTimesCoeff => val.baseValue + Mathf.RoundToInt(counterVal * val.coefficient),
                _ => val.baseValue
            };
        }

        private int GetAttrValue(AttributeRef attr) => attr switch
        {
            AttributeRef.Strength => _playerStrength,
            AttributeRef.Dexterity => _playerDexterity,
            AttributeRef.CurrentHP => _playerHP,
            AttributeRef.MaxHP => _playerMaxHP,
            AttributeRef.LostHP => _playerMaxHP - _playerHP,
            AttributeRef.CurrentSanity => _playerSanity,
            AttributeRef.SanityLostThisTurn => 0,
            AttributeRef.TotalSanityLostThisBattle => 0,
            AttributeRef.BleedValue => _enemyBleed,
            AttributeRef.ArmorBreakValue => _enemyArmorBreak,
            AttributeRef.CritRate => Mathf.RoundToInt(_playerCritRate * 100),
            AttributeRef.CritDamage => Mathf.RoundToInt(_playerCritDamage * 100),
            _ => 0
        };

        private bool EvaluateConditions(CardEffect eff)
        {
            if (eff.conditions == null || eff.conditions.Count == 0) return true;

            bool result = eff.conditionLogic == ConditionLogic.All;
            foreach (var cond in eff.conditions)
            {
                bool met = EvaluateSingleCondition(cond);
                Log($"  条件: {cond.GetDescription()} → {(met ? "✓" : "✗")}");

                if (eff.conditionLogic == ConditionLogic.All)
                {
                    result = result && met;
                    if (!met) break;
                }
                else
                {
                    result = result || met;
                }
            }
            return result;
        }

        private bool EvaluateSingleCondition(EffectCondition cond)
        {
            switch (cond.conditionType)
            {
                case ConditionType.SourceAttributeCheck:
                case ConditionType.TargetAttributeCheck:
                    int val = GetAttrValue(cond.attributeRef);
                    return cond.comparison switch
                    {
                        ComparisonOp.Less => val < cond.compareValue,
                        ComparisonOp.LessEqual => val <= cond.compareValue,
                        ComparisonOp.Equal => Mathf.Approximately(val, cond.compareValue),
                        ComparisonOp.GreaterEqual => val >= cond.compareValue,
                        ComparisonOp.Greater => val > cond.compareValue,
                        ComparisonOp.NotEqual => !Mathf.Approximately(val, cond.compareValue),
                        _ => true
                    };
                case ConditionType.EventOccurred:
                    Log($"    (事件条件需运行时评估)");
                    return true;
                case ConditionType.SourceHasStatus:
                case ConditionType.TargetHasStatus:
                    Log($"    (状态条件需运行时评估)");
                    return true;
                case ConditionType.TurnCounterCheck:
                case ConditionType.BattleCounterCheck:
                    Log($"    (计数条件需运行时评估)");
                    return true;
                case ConditionType.Custom:
                    if (cond.customConditionScript != null)
                        Log($"    (自定义条件: {cond.customConditionScript.GetDisplayName()} 需运行时评估)");
                    return true;
                default:
                    return true;
            }
        }

        private int ApplyModify(int current, int amount, ModifyMethod method) => method switch
        {
            ModifyMethod.Add => current + amount,
            ModifyMethod.Subtract => current - amount,
            ModifyMethod.Multiply => current * amount,
            ModifyMethod.Override => amount,
            _ => current
        };

        private float ApplyModifyFloat(float current, float amount, ModifyMethod method) => method switch
        {
            ModifyMethod.Add => current + amount,
            ModifyMethod.Subtract => current - amount,
            ModifyMethod.Multiply => current * amount,
            ModifyMethod.Override => amount,
            _ => current
        };

        private void Log(string msg)
        {
            _logLines.Add(msg);
        }
    }
}
