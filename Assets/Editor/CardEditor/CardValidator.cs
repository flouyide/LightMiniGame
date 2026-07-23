using System.Collections.Generic;
using System.Linq;
using System.Text;
using LightMiniGame.CardEditor;

namespace LightMiniGame.CardEditor.Editor
{
    /// <summary>
    /// 校验结果
    /// </summary>
    public class ValidationResult
    {
        public enum Severity { Error, Warning, Info }

        public Severity severity;
        public string message;
        public string context;

        public ValidationResult(Severity sev, string msg, string ctx = "")
        {
            severity = sev;
            message = msg;
            context = ctx;
        }

        public string GetColoredTag()
        {
            switch (severity)
            {
                case Severity.Error: return "<color=red>[错误]</color>";
                case Severity.Warning: return "<color=yellow>[警告]</color>";
                default: return "<color=white>[信息]</color>";
            }
        }
    }

    /// <summary>
    /// 卡牌校验器 —— 保存或测试前校验卡牌数据的完整性
    /// </summary>
    public static class CardValidator
    {
        /// <summary>
        /// 校验单张卡牌
        /// </summary>
        public static List<ValidationResult> Validate(CardEntry card, CardDatabase database)
        {
            var results = new List<ValidationResult>();

            // === ID 检查 ===
            if (string.IsNullOrEmpty(card.cardId))
                results.Add(new ValidationResult(ValidationResult.Severity.Error, "卡牌 ID 为空"));
            else if (database != null && database.IsIdDuplicate(card.cardId, card))
                results.Add(new ValidationResult(ValidationResult.Severity.Error, $"卡牌 ID 重复: {card.cardId}"));

            // === 名称检查 ===
            if (string.IsNullOrEmpty(card.cardName))
                results.Add(new ValidationResult(ValidationResult.Severity.Error, "卡牌名称为空"));

            // === 费用检查 ===
            if (card.baseCost < 0)
                results.Add(new ValidationResult(ValidationResult.Severity.Error, "基础费用不能小于 0"));
            if (card.upgradable && card.upgradeCost < 0)
                results.Add(new ValidationResult(ValidationResult.Severity.Error, "升级后费用不能小于 0"));

            // === 卡面检查 ===
            if (card.cardArt == null)
                results.Add(new ValidationResult(ValidationResult.Severity.Warning, "缺少卡面原画"));

            // === 升级检查 ===
            if (card.upgradable)
            {
                if (card.cardType == CardType.Ability)
                {
                    if (card.upgradeAbility == null || card.upgradeAbility.triggeredEffects == null || card.upgradeAbility.triggeredEffects.Count == 0)
                        results.Add(new ValidationResult(ValidationResult.Severity.Warning, "可升级的能力卡缺少升级效果"));
                }
                else
                {
                    if (card.upgradeEffects == null || card.upgradeEffects.Count == 0)
                        results.Add(new ValidationResult(ValidationResult.Severity.Warning, "可升级卡缺少升级效果列表"));
                }
            }

            // === 能力卡检查 ===
            if (card.cardType == CardType.Ability)
            {
                if (card.baseAbility == null || string.IsNullOrEmpty(card.baseAbility.abilityName))
                    results.Add(new ValidationResult(ValidationResult.Severity.Warning, "能力卡缺少能力名称"));
                if (card.baseAbility != null && card.baseAbility.triggeredEffects.Count == 0)
                    results.Add(new ValidationResult(ValidationResult.Severity.Error, "能力卡缺少触发后执行的效果"));
            }

            // === 效果检查 ===
            ValidateEffects(card.baseEffects, "基础", results);
            if (card.upgradable)
            {
                if (card.cardType == CardType.Ability)
                    ValidateEffects(card.upgradeAbility?.triggeredEffects, "升级", results);
                else
                    ValidateEffects(card.upgradeEffects, "升级", results);
            }

            return results;
        }

        private static void ValidateEffects(List<CardEffect> effects, string label, List<ValidationResult> results)
        {
            if (effects == null) return;

            for (int i = 0; i < effects.Count; i++)
            {
                var eff = effects[i];
                if (!eff.enabled) continue;

                string ctx = $"{label}效果[{i + 1}] {eff.effectName}";

                // 攻击次数
                if (eff.effectType == EffectType.Damage && eff.times < 1)
                    results.Add(new ValidationResult(ValidationResult.Severity.Error, "攻击次数不能小于 1", ctx));

                // 目标选择
                if (eff.target == EffectTarget.SelectedEnemy || eff.source == EffectSource.SelectedEnemy)
                {
                    // 需要目标选择的卡牌，需确认卡牌类型支持
                    // 这里只做提示，不做硬性错误
                }

                // 自定义效果未绑定脚本
                if (eff.effectType == EffectType.Custom && eff.customEffectScript == null)
                    results.Add(new ValidationResult(ValidationResult.Severity.Error, "自定义效果未绑定脚本", ctx));

                // 条件检查
                if (eff.conditions != null)
                {
                    foreach (var cond in eff.conditions)
                    {
                        if (cond.conditionType == ConditionType.Custom && cond.customConditionScript == null)
                            results.Add(new ValidationResult(ValidationResult.Severity.Error, "自定义条件未绑定脚本", ctx));

                        if (cond.conditionType != ConditionType.EventOccurred &&
                            cond.conditionType != ConditionType.SourceHasStatus &&
                            cond.conditionType != ConditionType.TargetHasStatus &&
                            cond.conditionType != ConditionType.Custom)
                        {
                            // 需要比较值
                        }

                        if (cond.conditionType == ConditionType.EventOccurred && string.IsNullOrEmpty(cond.eventName))
                            results.Add(new ValidationResult(ValidationResult.Severity.Warning, "事件条件未填写事件名称", ctx));
                    }
                }

                // 持续时间检查
                if (eff.duration == EffectDuration.SpecifiedTurns && eff.durationValue <= 0)
                    results.Add(new ValidationResult(ValidationResult.Severity.Error, "持续指定回合但未填写回合数", ctx));

                // 永久效果存档提示
                if (eff.duration == EffectDuration.GamePermanent)
                    results.Add(new ValidationResult(ValidationResult.Severity.Info, "本局永久生效的效果需确保有对应的存档接口", ctx));

                // 施加破甲但未填数值
                if (eff.applyArmorBreak && eff.armorBreakAmount <= 0)
                    results.Add(new ValidationResult(ValidationResult.Severity.Warning, "勾选了施加破甲但破甲数值为 0", ctx));
            }
        }

        /// <summary>
        /// 批量校验数据库中所有卡牌
        /// </summary>
        public static List<(CardEntry card, List<ValidationResult> results)> ValidateAll(CardDatabase database)
        {
            var allResults = new List<(CardEntry, List<ValidationResult>)>();
            if (database == null) return allResults;

            foreach (var card in database.cards)
            {
                var results = Validate(card, database);
                bool hasError = results.Any(r => r.severity == ValidationResult.Severity.Error);
                if (hasError)
                    allResults.Add((card, results));
            }
            return allResults;
        }
    }
}
