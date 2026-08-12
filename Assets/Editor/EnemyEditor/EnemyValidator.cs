using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 单条验证结果（针对一个 EnemyConfig）。
/// </summary>
public class ValidationResult
{
    public EnemyConfig config;
    public string assetPath;
    public List<string> issues = new List<string>();
    public bool IsValid => issues.Count == 0;

    public string Summary
    {
        get
        {
            var name = config != null && !string.IsNullOrEmpty(config.enemyName) ? config.enemyName : "(未命名)";
            return IsValid ? $"{name}: OK" : $"{name}: {issues.Count} 个问题";
        }
    }
}

/// <summary>
/// 批量校验：敌人名/HP/技能/立绘/掉落物 等基本完整性检查。
/// </summary>
public static class EnemyValidator
{
    public static List<ValidationResult> ValidateAll(List<EnemyConfig> configs)
    {
        var results = new List<ValidationResult>(configs?.Count ?? 0);
        if (configs == null) return results;
        foreach (var c in configs) results.Add(Validate(c));
        return results;
    }

    public static ValidationResult Validate(EnemyConfig c)
    {
        var r = new ValidationResult
        {
            config = c,
            assetPath = c != null ? AssetDatabase.GetAssetPath(c) : "(null)"
        };
        if (c == null) { r.issues.Add("资产为 null"); return r; }
        if (string.IsNullOrWhiteSpace(c.enemyName))   r.issues.Add("敌人名（enemyName）为空");
        if (c.maxHP <= 0)                            r.issues.Add("最大生命值 maxHP ≤ 0");
        if (c.phase1Portrait == null)                r.issues.Add("阶段1立绘未设置");
        if (c.phase2MaxHP > 0 && c.phase2Portrait == null) r.issues.Add("配置了阶段2但阶段2立绘未设置");

        if (c.phase1Skills == null || c.phase1Skills.Count == 0)
            r.issues.Add("阶段1技能列表为空（至少配一个技能）");
        else
        {
            for (int i = 0; i < c.phase1Skills.Count; i++)
            {
                var s = c.phase1Skills[i];
                if (s == null) { r.issues.Add($"阶段1技能 [{i}] 为 null"); continue; }
                if (string.IsNullOrEmpty(s.skillName)) r.issues.Add($"阶段1技能 [{i}] 名称为空");
            }
        }

        if (c.phase2Skills != null)
        {
            for (int i = 0; i < c.phase2Skills.Count; i++)
            {
                var s = c.phase2Skills[i];
                if (s == null) { r.issues.Add($"阶段2技能 [{i}] 为 null"); continue; }
                if (string.IsNullOrEmpty(s.skillName)) r.issues.Add($"阶段2技能 [{i}] 名称为空");
            }
        }

        if (c.abilities != null)
        {
            for (int i = 0; i < c.abilities.Count; i++)
            {
                var a = c.abilities[i];
                if (a == null) { r.issues.Add($"能力 [{i}] 为 null"); continue; }
                if (a.relic == null) r.issues.Add($"能力 [{i}]（{a.displayName}）未指定遗物");
            }
        }

        if (c.lootTable != null && c.lootTable.entries != null)
        {
            int rel = 0, card = 0, cur = 0;
            foreach (var e in c.lootTable.entries)
            {
                if (e == null) continue;
                switch (e.kind)
                {
                    case LootEntry.LootKind.Currency: cur++; break;
                    case LootEntry.LootKind.Card:     card++; break;
                    case LootEntry.LootKind.Relic:    rel++; break;
                }
            }
            // 精英/boss 必须有遗物
            if ((c.difficulty == Difficulty.Elite || c.difficulty == Difficulty.Boss) && rel == 0)
                r.issues.Add($"精英/Boss 难度应至少配置 1 条遗物掉落");
        }

        return r;
    }

    /// <summary>把所有问题汇总成多行文本用于弹窗显示。</summary>
    public static string BuildReport(List<ValidationResult> results)
    {
        var sb = new StringBuilder();
        int errorCount = 0;
        foreach (var r in results)
        {
            if (!r.IsValid) errorCount++;
        }
        sb.AppendLine($"校验完成：共 {results.Count} 个敌人，{errorCount} 个有错误");
        sb.AppendLine();
        foreach (var r in results)
        {
            sb.AppendLine(r.Summary);
            if (!r.IsValid)
            {
                foreach (var issue in r.issues) sb.AppendLine($"  · {issue}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}