using System.Collections.Generic;
using LightMiniGame.CardEditor;
using UnityEditor;
using UnityEngine;

namespace LightMiniGame.CardEditor.Editor
{
    /// <summary>
    /// 按策划表紫色格，为未完成卡牌写入自定义效果脚本。
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

            var watch = LoadOrCreate<MarkWatchTargetThisTurnEffect>("MarkWatchTargetThisTurn");
            var lockHalf = LoadOrCreate<LockHalfHandNextTurnEffect>("LockHalfHandNextTurn");
            var summon = LoadOrCreate<SummonEnemyCompanionEffect>("SummonEnemyCompanion");
            var entangle = LoadOrCreate<ApplyEntangleEffect>("ApplyEntangle");
            var fromImpostor = LoadOrCreate<GainStrengthFromImpostorEffect>("GainStrengthFromImpostor");
            var addImpostor = LoadOrCreate<AddImpostorStacksEffect>("AddImpostorStacks");
            var rename = LoadOrCreate<ApplyRenameThisTurnEffect>("ApplyRenameThisTurn");
            var dirtyWork = LoadOrCreate<ApplyDirtyWorkEffect>("ApplyDirtyWork");

            TryAssignRenameReplacement(rename);

            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:CardEntry"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardEntry>(path);
                if (card == null || string.IsNullOrEmpty(card.cardName) || !card.cardName.Contains("未完成"))
                    continue;

                bool dirty = false;
                if (card.cardName.Contains("APP定位"))
                    dirty |= EnsureBoth(card, watch, "{\"count\":3}", "添加监控目标词条");
                else if (card.cardName.Contains("最终警告") || card.cardName.Contains("考勤警告"))
                    dirty |= EnsureLowSanityOnly(card, lockHalf, "", "半数手牌下回合无法打出");
                else if (card.cardName.Contains("拉小团体"))
                    dirty |= EnsureBoth(card, summon, "", "召唤同伙");
                else if (card.cardName.Contains("中伤"))
                    dirty |= EnsureAppend(card, entangle, "{\"stacks\":1}", "施加缠结", "{\"stacks\":2}");
                else if (card.cardName.Contains("力量不属于你"))
                    dirty |= EnsureAppend(card, fromImpostor, "{\"bonus\":0}", "按冒名获得力量", "{\"bonus\":1}");
                else if (card.cardName.Contains("冒名顶替"))
                    dirty |= EnsureAppend(card, addImpostor, "{\"stacks\":1}", "获得冒名", "{\"stacks\":2}");
                else if (card.cardName.Contains("借用你的名字") || card.cardName.Contains("改名"))
                    dirty |= EnsureBoth(card, rename, "", "牌库随机牌替换为攻击");
                else if (card.cardName.Contains("这个你来做"))
                    dirty |= EnsureBoth(card, dirtyWork, "{\"stacks\":2}", "施加脏活");
                else if (card.cardName.Contains("压榨"))
                    dirty |= EnsureBoth(card, dirtyWork, "{\"stacks\":1}", "施加1层脏活");

                if (!dirty) continue;
                EditorUtility.SetDirty(card);
                changed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[未完成卡牌脚本] 已绑定 {changed} 张卡。资产目录 {AssetDir}");
        }

        [InitializeOnLoadMethod]
        private static void AutoBindOnce()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool("UnfinishedCardScriptsBound_v2", false)) return;
                try
                {
                    Bind();
                    EditorPrefs.SetBool("UnfinishedCardScriptsBound_v2", true);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[未完成卡牌脚本] 自动绑定跳过：{ex.Message}");
                }
            };
        }

        private static void TryAssignRenameReplacement(ApplyRenameThisTurnEffect rename)
        {
            if (rename == null || rename.replacementCard != null) return;
            foreach (var guid in AssetDatabase.FindAssets("t:CardEntry 点名"))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardEntry>(AssetDatabase.GUIDToAssetPath(guid));
                if (card == null || !card.cardName.Contains("点名")) continue;
                rename.replacementCard = card;
                EditorUtility.SetDirty(rename);
                return;
            }
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

        private static bool EnsureBoth(CardEntry card, CustomEffectScript script, string customParams, string displayName)
            => EnsureAppend(card, script, customParams, displayName, customParams);

        private static bool EnsureLowSanityOnly(CardEntry card, CustomEffectScript script, string customParams, string displayName)
        {
            if (card.lowSanityEffectNodes == null) card.lowSanityEffectNodes = new List<EffectNode>();
            return ReplaceMoveCardsOrAppend(card.lowSanityEffectNodes, script, customParams, displayName);
        }

        private static bool EnsureAppend(
            CardEntry card,
            CustomEffectScript script,
            string customParams,
            string displayName,
            string lowSanityParams)
        {
            if (card.normalEffectNodes == null) card.normalEffectNodes = new List<EffectNode>();
            if (card.lowSanityEffectNodes == null) card.lowSanityEffectNodes = new List<EffectNode>();
            bool dirty = ReplaceMoveCardsOrAppend(card.normalEffectNodes, script, customParams, displayName);
            dirty |= ReplaceMoveCardsOrAppend(card.lowSanityEffectNodes, script, lowSanityParams, displayName);
            return dirty;
        }

        private static bool ReplaceMoveCardsOrAppend(
            List<EffectNode> nodes,
            CustomEffectScript script,
            string customParams,
            string displayName)
        {
            if (HasScript(nodes, script)) return false;
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
