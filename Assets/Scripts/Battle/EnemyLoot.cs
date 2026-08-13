using System;
using System.Collections.Generic;
using LightMiniGame.Shop;

public enum Difficulty { Weak, Strong, Elite, Boss }

[Serializable]
public class LootEntry
{
    public enum LootKind { Currency, Card, Relic }
    public LootKind kind;
    public int minCount = 1;
    public int maxCount = 1;
    public CardGrade cardGrade = CardGrade.Common;
    public RelicData relic;
    public float weight = 1f;
}

/// <summary>单条掉落结算结果（由 LootTable.RollLoot 产生，供外部系统发放）</summary>
public struct LootResult
{
    public LootEntry.LootKind kind;
    public int count;              // 本次掉落数量（金币=金额，卡牌=张数，遗物=件数）
    public CardGrade cardGrade;   // kind=Card 时有效：指定品级
    public RelicData relic;       // kind=Relic 且配置了具体遗物时有效；null=随机抽取
}

[Serializable]
public class LootTable
{
    public List<LootEntry> entries = new List<LootEntry>();

    /// <summary>
    /// 按权重随机抽取掉落物并结算数量。每条 entry 独立判定是否掉落（按 weight 概率），
    /// 掉落则数量在 [minCount, maxCount] 之间随机。返回待发放的 LootResult 列表。
    /// </summary>
    public List<LootResult> RollLoot()
    {
        var results = new List<LootResult>();
        if (entries == null) return results;

        foreach (var e in entries)
        {
            if (e == null) continue;

            // 按权重判定是否掉落（weight >= 1 视为必掉；0 < weight < 1 按概率掉落）
            if (e.weight <= 0f) continue;
            if (e.weight < 1f && UnityEngine.Random.value > e.weight) continue;

            int min = System.Math.Min(e.minCount, e.maxCount);
            int max = System.Math.Max(e.minCount, e.maxCount);
            int count = min == max ? min : UnityEngine.Random.Range(min, max + 1);
            if (count <= 0) continue;

            results.Add(new LootResult
            {
                kind = e.kind,
                count = count,
                cardGrade = e.cardGrade,
                relic = e.relic,
            });
        }
        return results;
    }

    public static LootTable GetPreset(Difficulty d)
    {
        var t = new LootTable();
        switch (d)
        {
            case Difficulty.Weak:
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Currency, minCount = 2, maxCount = 4 });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, minCount = 1, maxCount = 1, cardGrade = CardGrade.Common });
                break;
            case Difficulty.Strong:
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Currency, minCount = 4, maxCount = 8 });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, minCount = 1, maxCount = 1, cardGrade = CardGrade.Common });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, minCount = 1, maxCount = 1, cardGrade = CardGrade.Fine });
                break;
            case Difficulty.Elite:
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Currency, minCount = 8, maxCount = 15 });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Relic, minCount = 1, maxCount = 1 });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, minCount = 1, maxCount = 1, cardGrade = CardGrade.Rare });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, minCount = 1, maxCount = 1, cardGrade = CardGrade.Epic });
                break;
            case Difficulty.Boss:
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Currency, minCount = 15, maxCount = 25 });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Relic, minCount = 1, maxCount = 1 });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, minCount = 1, maxCount = 1, cardGrade = CardGrade.Epic });
                t.entries.Add(new LootEntry { kind = LootEntry.LootKind.Card, minCount = 1, maxCount = 1, cardGrade = CardGrade.Legendary });
                break;
        }
        return t;
    }
}