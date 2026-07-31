using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using LightMiniGame.CardEditor;

namespace LightMiniGame.CardEditor.Editor
{
    /// <summary>
    /// 卡牌测试窗口 —— 在编辑器内模拟打出卡牌，预览效果执行过程。
    /// 使用 EffectExecutorV2 执行 EffectNode 列表。
    /// </summary>
    public class CardTestWindow : EditorWindow
    {
        private CardEntry _card;
        private bool _useLowSanity;

        // 测试参数
        private int _playerHP = 100, _playerMaxHP = 100, _playerStrength = 0, _playerDexterity = 0;
        private float _playerCritRate = 0.1f, _playerCritDamage = 1.5f;
        private int _playerSanity = 10, _playerEnergy = 3, _playerBleed = 0, _playerHeat = 0;
        private int _enemyHP = 50, _enemyMaxHP = 50, _enemyArmor = 0, _enemyBleed = 0, _enemyArmorBreak = 0;
        private int _randomSeed = 12345;

        // 结果
        private List<string> _logLines = new();
        private Vector2 _scroll;
        private bool _tested;

        [MenuItem("Tools/卡牌编辑器/测试窗口")]
        public static void Open()
        {
            var w = GetWindow<CardTestWindow>("卡牌测试");
            w.minSize = new Vector2(500, 700);
        }

        public static void Open(CardEntry card)
        {
            var w = GetWindow<CardTestWindow>("卡牌测试");
            w._card = card;
            w.Show();
        }

        private void OnGUI()
        {
            if (_card == null)
            {
                EditorGUILayout.LabelField("请先在卡牌编辑器中选择一张卡牌", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField($"测试卡牌: {_card.cardName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"类型: {CardEntry.GetCardTypeName(_card.cardType)}  费用: {_card.GetCost(_useLowSanity)}");

            EditorGUILayout.Space();
            if (_card.hasLowSanityForm)
                _useLowSanity = EditorGUILayout.Toggle("使用低理智形态", _useLowSanity);

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
            _playerHeat = EditorGUILayout.IntField("热度", _playerHeat);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("敌人属性", EditorStyles.boldLabel);
            _enemyHP = EditorGUILayout.IntField("敌人生命", _enemyHP);
            _enemyMaxHP = EditorGUILayout.IntField("敌人最大生命", _enemyMaxHP);
            _enemyArmor = EditorGUILayout.IntField("敌人护甲", _enemyArmor);
            _enemyBleed = EditorGUILayout.IntField("敌人流血", _enemyBleed);
            _enemyArmorBreak = EditorGUILayout.IntField("敌人破甲", _enemyArmorBreak);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("随机设置", EditorStyles.boldLabel);
            _randomSeed = EditorGUILayout.IntField("随机种子", _randomSeed);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("执行测试", GUILayout.Height(30)))
                RunTest();

            EditorGUILayout.Space(10);
            if (_tested)
            {
                EditorGUILayout.LabelField("测试结果", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("最终状态:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"玩家: HP={_playerHP}/{_playerMaxHP} 力量={_playerStrength} 能量={_playerEnergy} 理智={_playerSanity} 热度={_playerHeat}");
                EditorGUILayout.LabelField($"敌人: HP={_enemyHP}/{_enemyMaxHP} 护甲={_enemyArmor} 破甲={_enemyArmorBreak} 流血={_enemyBleed}");
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("执行日志:", EditorStyles.boldLabel);
                foreach (var line in _logLines)
                    EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void RunTest()
        {
            _logLines.Clear();
            _tested = true;
            UnityEngine.Random.InitState(_randomSeed);

            Log($"═══ 测试卡牌: {_card.cardName} ({(_useLowSanity ? "低理智形态" : "普通形态")}) ═══");
            Log($"初始: HP={_playerHP} 力量={_playerStrength} 能量={_playerEnergy} 理智={_playerSanity} 热度={_playerHeat}");
            Log($"初始: 敌人HP={_enemyHP} 护甲={_enemyArmor} 破甲={_enemyArmorBreak}");
            Log("");

            // 检查是否有效果
            var nodes = _card.GetEffectNodes(_useLowSanity);
            if (nodes == null || nodes.Count == 0)
            {
                Log("此形态没有配置效果。");
                return;
            }

            Log($"共 {nodes.Count} 个效果:");
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                Log($"--- 效果[{i + 1}] {node.displayName}: {node.GetDescription()} ---");
                if (!node.enabled)
                {
                    Log("  已禁用，跳过");
                    continue;
                }
                // 简化模拟：只记录描述，实际执行需要完整战斗上下文
                SimulateEffect(node);
                Log("");
            }
            Log("═══ 测试结束 ═══");
        }

        private void SimulateEffect(EffectNode node)
        {
            switch (node.operation)
            {
                case EffectOperation.DealDamage:
                    int dmg = node.value?.intValue ?? 0;
                    int hits = Mathf.Max(1, node.repeatCount?.intValue ?? 1);
                    for (int h = 0; h < hits; h++)
                    {
                        bool crit = node.criticalCheckMode == CriticalCheckMode.Guaranteed || UnityEngine.Random.value < _playerCritRate;
                        int hitDmg = crit ? Mathf.RoundToInt(dmg * _playerCritDamage) : dmg;
                        if (node.scalingMode == ScalingMode.AddStrength) hitDmg += _playerStrength;
                        _enemyHP -= hitDmg;
                        if (_enemyHP < 0) _enemyHP = 0;
                        Log($"  第{h+1}击: {hitDmg}{(crit ? " 暴击" : "")} → 敌人HP={_enemyHP}");
                    }
                    break;
                case EffectOperation.GainBlock:
                    int block = node.value?.intValue ?? 0;
                    Log($"  [格挡] +{block}");
                    break;
                case EffectOperation.ModifyResource:
                    string res = node.resourceType.ToString();
                    int val = node.value?.intValue ?? 0;
                    if (node.resourceType == LightMiniGame.CardEditor.PlayerResourceType.Heat)
                    {
                        int delta = node.resourceOp == ResourceOperation.Subtract ? -val : val;
                        _playerHeat += delta;
                        Log($"  [热度] {delta} → {_playerHeat}");
                    }
                    else if (node.resourceType == LightMiniGame.CardEditor.PlayerResourceType.Sanity)
                    {
                        int delta = node.resourceOp == ResourceOperation.Subtract ? -val : val;
                        _playerSanity += delta;
                        Log($"  [理智] {delta} → {_playerSanity}");
                    }
                    else Log($"  [{res}] {node.resourceOp} {val}");
                    break;
                case EffectOperation.DrawCards:
                    Log($"  [抽牌] {node.value?.intValue ?? 0}张");
                    break;
                case EffectOperation.RestoreActionPoints:
                    int ap = node.value?.intValue ?? 0;
                    _playerEnergy += ap;
                    Log($"  [行动点] +{ap} → {_playerEnergy}");
                    break;
                case EffectOperation.RegisterTrigger:
                    Log($"  [注册触发器] {EffectNode.GetTriggerName(node.triggerEvent)} → {node.childEffects.Count}个子效果");
                    break;
                default:
                    Log($"  [{node.operation}] {node.GetDescription()}");
                    break;
            }
        }

        private void Log(string msg) => _logLines.Add(msg);
    }
}
