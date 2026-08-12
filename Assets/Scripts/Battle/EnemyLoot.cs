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

[Serializable]
public class LootTable
{
    public List<LootEntry> entries = new List<LootEntry>();

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