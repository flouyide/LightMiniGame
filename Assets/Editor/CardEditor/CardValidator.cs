using System.Collections.Generic;
using System.Linq;
using LightMiniGame.CardEditor;

namespace LightMiniGame.CardEditor.Editor
{
    public class ValidationResult
    {
        public enum Severity { Error, Warning, Info }
        public Severity severity;
        public string message;
        public string context;

        public ValidationResult(Severity sev, string msg, string ctx = "")
        {
            severity = sev; message = msg; context = ctx;
        }
    }

    public static class CardValidator
    {
        public static List<ValidationResult> Validate(CardEntry card, CardDatabase database)
        {
            var results = new List<ValidationResult>();

            if (string.IsNullOrEmpty(card.cardId))
                results.Add(new ValidationResult(ValidationResult.Severity.Error, "卡牌 ID 为空"));
            else if (database != null && database.IsIdDuplicate(card.cardId, card))
                results.Add(new ValidationResult(ValidationResult.Severity.Error, $"卡牌 ID 重复: {card.cardId}"));

            if (string.IsNullOrEmpty(card.cardName))
                results.Add(new ValidationResult(ValidationResult.Severity.Error, "卡牌名称为空"));

            if (card.normalCost < 0)
                results.Add(new ValidationResult(ValidationResult.Severity.Error, "普通费用不能小于 0"));
            if (card.hasLowSanityForm && card.lowSanityCost < 0)
                results.Add(new ValidationResult(ValidationResult.Severity.Error, "低理智费用不能小于 0"));

            if (card.cardArt == null)
                results.Add(new ValidationResult(ValidationResult.Severity.Warning, "缺少卡面原画"));

            if (card.hasLowSanityForm)
            {
                if (card.lowSanityEffectNodes == null || card.lowSanityEffectNodes.Count == 0)
                    results.Add(new ValidationResult(ValidationResult.Severity.Warning, "配置了低理智形态但缺少低理智效果"));
            }

            ValidateEffectNodes(card.normalEffectNodes, "普通形态", results);
            if (card.hasLowSanityForm)
                ValidateEffectNodes(card.lowSanityEffectNodes, "低理智形态", results);

            return results;
        }

        private static void ValidateEffectNodes(List<EffectNode> nodes, string label, List<ValidationResult> results)
        {
            if (nodes == null) return;
            var outputVars = new HashSet<string>();

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!node.enabled) continue;
                string ctx = $"{label}效果[{i + 1}] {node.displayName}";

                if (node.operation == EffectOperation.DealDamage || node.operation == EffectOperation.GainBlock)
                {
                    int repeat = node.repeatCount?.intValue ?? 1;
                    if (repeat < 1)
                        results.Add(new ValidationResult(ValidationResult.Severity.Error, "重复次数不能小于 1", ctx));
                }

                if (node.operation == EffectOperation.CustomOperation && node.customOperation == null)
                    results.Add(new ValidationResult(ValidationResult.Severity.Error, "自定义操作未绑定脚本", ctx));

                if (node.operation == EffectOperation.RegisterTrigger && (node.childEffects == null || node.childEffects.Count == 0))
                    results.Add(new ValidationResult(ValidationResult.Severity.Error, "注册触发器但没有子效果", ctx));

                if (!string.IsNullOrEmpty(node.outputVariableName))
                {
                    if (outputVars.Contains(node.outputVariableName))
                        results.Add(new ValidationResult(ValidationResult.Severity.Warning, $"输出变量名重复: {node.outputVariableName}", ctx));
                    else
                        outputVars.Add(node.outputVariableName);
                }
            }
        }

        public static List<(CardEntry card, List<ValidationResult> results)> ValidateAll(CardDatabase database)
        {
            var allResults = new List<(CardEntry, List<ValidationResult>)>();
            if (database == null) return allResults;
            foreach (var card in database.cards)
            {
                var results = Validate(card, database);
                if (results.Any(r => r.severity == ValidationResult.Severity.Error))
                    allResults.Add((card, results));
            }
            return allResults;
        }
    }
}
