using System.Collections.Generic;
using LightMiniGame.CardEditor;
using UnityEditor;
using UnityEngine;

namespace LightMiniGame.CardEditor.Editor
{
    /// <summary>
    /// 为未完成卡牌创建自定义效果资产，并写入效果列表的「脚本」字段。
    /// 菜单：Tools/卡牌编辑器/绑定未完成卡牌自定义脚本
    /// </summary>
    public static class UnfinishedCardScriptBinder
    {
        private const string AssetDir = "Assets/ScriptableObjects/Cards/CustomScripts";

        [MenuItem("Tools/卡牌编辑器/绑定未完成卡牌自定义脚本")]
        public static void Bind()
        {
            if (!AssetDatabase.IsValidFolder(AssetDir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects/Cards", "CustomScripts");

            var halfCost = LoadOrCreate<SetHalfHandCostThisTurnEffect>("SetHalfHandCostThisTurn");
            var watch = LoadOrCreate<MarkWatchTargetThisTurnEffect>("MarkWatchTargetThisTurn");
            var summon = LoadOrCreate<SummonEnemyCompanionEffect>("SummonEnemyCompanion");
            var entangle = LoadOrCreate<ApplyEntangleEffect>("ApplyEntangle");
            var fromImpostor = LoadOrCreate<GainStrengthFromImpostorEffect>("GainStrengthFromImpostor");
            var addImpostor = LoadOrCreate<AddImpostorStacksEffect>("AddImpostorStacks");
            var rename = LoadOrCreate<ApplyRenameThisTurnEffect>("ApplyRenameThisTurn");
            var dirtyWork = LoadOrCreate<ApplyDirtyWorkEffect>("ApplyDirtyWork");
            var dirtyDmg = LoadOrCreate<DirtyWorkBonusDamageEffect>("DirtyWorkBonusDamage");

            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:CardEntry"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardEntry>(path);
                if (card == null || string.IsNullOrEmpty(card.cardName) || !card.cardName.Contains("未完成"))
                    continue;

                bool dirty = false;
                if (card.cardName.Contains("最终警告"))
                    dirty |= EnsureAppend(card, halfCost, "{\"cost\":9}", "半数手牌费用变为9");
                else if (card.cardName.Contains("APP定位"))
                    dirty |= EnsureReplaceOrAppend(card, watch, "{\"count\":3}", "监控目标", preferMoveCards: true);
                else if (card.cardName.Contains("拉小团体"))
                    dirty |= EnsureAppend(card, summon, "", "召唤双头鼠同伴");
                else if (card.cardName.Contains("中伤"))
                    dirty |= EnsureReplaceOrAppend(card, entangle, "{\"stacks\":1}", "缠结", preferMoveCards: true);
                else if (card.cardName.Contains("力量不属于你"))
                {
                    dirty |= EnsureAppend(card, fromImpostor, "{\"bonus\":0}", "按冒名获得力量", lowSanityParams: "{\"bonus\":1}");
                }
                else if (card.cardName.Contains("冒名顶替"))
                    dirty |= EnsureAppend(card, addImpostor, "{\"stacks\":1}", "获得1层冒名");
                else if (card.cardName.Contains("借用你的名字"))
                    dirty |= EnsureAppend(card, rename, "", "手牌改名为本回合攻击");
                else if (card.cardName.Contains("都是为你好"))
                    dirty |= EnsureAppend(card, dirtyDmg, "{\"perStack\":3}", "脏活额外伤害");
                else if (card.cardName.Contains("这个你来做"))
                    dirty |= EnsureAppend(card, dirtyWork, "{\"stacks\":2}", "施加脏活", lowSanityParams: "{\"stacks\":3}");
                else if (card.cardName.Contains("压榨"))
                    dirty |= EnsureAppend(card, dirtyWork, "{\"stacks\":1}", "施加1层脏活");

                if (!dirty) continue;
                EditorUtility.SetDirty(card);
                changed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[未完成卡牌脚本] 已绑定 {changed} 张卡。自定义效果资产在 {AssetDir}");
            EditorUtility.DisplayDialog("未完成卡牌脚本", $"已为 {changed} 张未完成卡牌写入脚本区。", "确定");
        }

        [InitializeOnLoadMethod]
        private static void AutoBindOnce()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool("UnfinishedCardScriptsBound_v1", false)) return;
                try
                {
                    Bind();
                    EditorPrefs.SetBool("UnfinishedCardScriptsBound_v1", true);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[未完成卡牌脚本] 自动绑定跳过：{ex.Message}");
                }
            };
        }

        private static T LoadOrCreate<T>(string fileName) where T : CustomEffectScript
        {
            string path = $"{AssetDir}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static bool EnsureAppend(
            CardEntry card,
            CustomEffectScript script,
            string customParams,
            string displayName,
            string lowSanityParams = null)
        {
            if (card.normalEffectNodes == null) card.normalEffectNodes = new List<EffectNode>();
            if (card.lowSanityEffectNodes == null) card.lowSanityEffectNodes = new List<EffectNode>();
            bool dirty = AppendIfMissing(card.normalEffectNodes, script, customParams, displayName);
            dirty |= AppendIfMissing(
                card.lowSanityEffectNodes,
                script,
                string.IsNullOrEmpty(lowSanityParams) ? customParams : lowSanityParams,
                displayName);
            return dirty;
        }

        private static bool EnsureReplaceOrAppend(
            CardEntry card,
            CustomEffectScript script,
            string customParams,
            string displayName,
            bool preferMoveCards)
        {
            if (card.normalEffectNodes == null) card.normalEffectNodes = new List<EffectNode>();
            if (card.lowSanityEffectNodes == null) card.lowSanityEffectNodes = new List<EffectNode>();
            bool dirty = ReplaceOrAppend(card.normalEffectNodes, script, customParams, displayName, preferMoveCards);
            dirty |= ReplaceOrAppend(card.lowSanityEffectNodes, script, customParams, displayName, preferMoveCards);
            return dirty;
        }

        private static bool AppendIfMissing(List<EffectNode> nodes, CustomEffectScript script, string customParams, string displayName)
        {
            if (HasScript(nodes, script)) return false;
            nodes.Add(MakeCustomNode(script, customParams, displayName));
            return true;
        }

        private static bool ReplaceOrAppend(
            List<EffectNode> nodes,
            CustomEffectScript script,
            string customParams,
            string displayName,
            bool preferMoveCards)
        {
            if (HasScript(nodes, script)) return false;
            if (preferMoveCards)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node == null || node.operation != EffectOperation.MoveCards) continue;
                    node.operation = EffectOperation.CustomOperation;
                    node.customOperation = script;
                    node.customParams = customParams;
                    node.displayName = displayName;
                    return true;
                }
            }
            nodes.Add(MakeCustomNode(script, customParams, displayName));
            return true;
        }

        private static bool HasScript(List<EffectNode> nodes, CustomEffectScript script)
        {
            if (nodes == null) return false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].customOperation == script)
                    return true;
            }
            return false;
        }

        private static EffectNode MakeCustomNode(CustomEffectScript script, string customParams, string displayName)
        {
            return new EffectNode
            {
                enabled = true,
                displayName = displayName,
                operation = EffectOperation.CustomOperation,
                customOperation = script,
                customParams = customParams ?? "",
                duration = new EffectDuration { type = DurationType.Instant },
                value = ValueNode.Constant(0),
                repeatCount = ValueNode.Constant(1),
                statusValue = ValueNode.Constant(1),
                armorBreakValue = ValueNode.Constant(0),
                zoneCount = ValueNode.Constant(1),
                conditions = new ConditionGroup(),
                childEffects = new List<EffectNode>()
            };
        }
    }
}
