using System;
using System.Collections.Generic;
using LightMiniGame.Shop;
using UnityEngine;

public enum Difficulty { Weak, Strong, Elite, Boss }

/// <summary>
/// 单条掉落配置。每条 entry 代表一种掉落物（货币 / 卡牌 / 遗物）。
/// kind 决定哪些字段生效：
///   - Currency：仅 currencyAmount 生效
///   - Card：  cardRarities + cardDrawCount + cardPickCount 生效
///   - Relic： relicRarities 生效（固定抽 1 个）
/// </summary>
[Serializable]
public class LootEntry
{
    public enum LootKind { Currency, Card, Relic }

    [Tooltip("掉落物类型")]
    public LootKind kind;

    // === 货币 ===
    [Tooltip("货币掉落数量（固定值）")]
    public int currencyAmount = 10;

    // === 卡牌 ===
    [Tooltip("卡牌可选品级（从这些品级的角色可获取牌库里按品级概率抽取）")]
    public List<CardGrade> cardRarities = new List<CardGrade> { CardGrade.Common };
    [Tooltip("抽取数量（展示给玩家的卡牌数，如「3选1」则填 3）")]
    public int cardDrawCount = 3;
    [Tooltip("玩家可选数量（通常为 1）")]
    public int cardPickCount = 1;

    // === 遗物 ===
    [Tooltip("遗物可选品级（从这些品级的角色可获取遗物库里按品级概率抽 1 个）")]
    public List<CardGrade> relicRarities = new List<CardGrade> { CardGrade.Common };
}

/// <summary>
/// 单条掉落结算结果（由战斗结束后的掉落逻辑产生，供外部系统发放）。
/// </summary>
public struct LootResult
{
    public LootEntry.LootKind kind;

    // --- Currency ---
    public int currencyAmount;       // kind=Currency 时：货币数量

    // --- Card ---
    public List<CardGrade> cardRarities;  // kind=Card 时：允许的品级列表
    public int cardDrawCount;             // kind=Card 时：抽取数量（n选1 的 n）
    public int cardPickCount;             // kind=Card 时：玩家可选数量

    // --- Relic ---
    public List<CardGrade> relicRarities; // kind=Relic 时：允许的品级列表
}

[Serializable]
public class LootTable
{
    public List<LootEntry> entries = new List<LootEntry>();

    /// <summary>
    /// 按难度枚举返回预设掉落表（供编辑器一键填充）。
    /// 实际掉落结算逻辑（RollLoot）由战斗结束后的掉落系统实现，此处仅提供配置数据。
    /// </summary>
    public static LootTable GetPreset(Difficulty d)
    {
        var t = new LootTable();
        switch (d)
        {
            case Difficulty.Weak:
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Currency, currencyAmount = 5 });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, cardRarities = new List<CardGrade> { CardGrade.Common }, cardDrawCount = 3, cardPickCount = 1 });
                break;
            case Difficulty.Strong:
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Currency, currencyAmount = 10 });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, cardRarities = new List<CardGrade> { CardGrade.Common, CardGrade.Fine }, cardDrawCount = 3, cardPickCount = 1 });
                break;
            case Difficulty.Elite:
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Currency, currencyAmount = 15 });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Relic, relicRarities = new List<CardGrade> { CardGrade.Common, CardGrade.Fine } });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, cardRarities = new List<CardGrade> { CardGrade.Rare, CardGrade.Epic }, cardDrawCount = 3, cardPickCount = 1 });
                break;
            case Difficulty.Boss:
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Currency, currencyAmount = 25 });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Relic, relicRarities = new List<CardGrade> { CardGrade.Fine, CardGrade.Rare, CardGrade.Epic } });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, cardRarities = new List<CardGrade> { CardGrade.Epic, CardGrade.Legendary }, cardDrawCount = 3, cardPickCount = 1 });
                break;
        }
        return t;
    }
}
