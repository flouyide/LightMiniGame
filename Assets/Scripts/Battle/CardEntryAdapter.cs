using System.Collections.Generic;
using UnityEngine;
using LightMiniGame.CardEditor;

/// <summary>
/// CardEntry → CardData 运行时适配器。
/// 战斗系统内部以 CardData 为单位运作（牌堆、手牌、弃牌堆），
/// 此工具把卡牌编辑器的 CardEntry 包装为运行时 CardData，使战斗系统能直接使用新卡。
///
/// 工作原理：
///   每张 CardEntry 在战斗开始时生成一个运行时 CardData 实例，
///   设置 sourceEntry 指向原始 CardEntry，费用从 CardEntry 读取，
///   卡牌类型/品级映射到旧枚举。
///   打牌时 BattleManager.ApplyCardEffects 检测到 sourceEntry != null，
///   自动走 EffectExecutor 路径。
/// </summary>
public static class CardEntryAdapter
{
    /// <summary>
    /// 把一组 CardEntry 转为运行时 CardData 实例列表。
    /// 每次调用都生成新实例（互不影响），适合战斗初始化。
    /// </summary>
    public static List<CardData> ConvertToCardData(List<CardEntry> entries)
    {
        var result = new List<CardData>();
        if (entries == null) return result;

        foreach (var entry in entries)
        {
            if (entry == null) continue;
            var cd = ConvertSingle(entry);
            if (cd != null) result.Add(cd);
        }
        return result;
    }

    /// <summary>
    /// 把单张 CardEntry 转为运行时 CardData 实例。
    /// 使用 ScriptableObject.CreateInstance 创建临时实例（不写入磁盘），
    /// 战斗结束后随场景销毁。
    /// </summary>
    public static CardData ConvertSingle(CardEntry entry)
    {
        if (entry == null) return null;

        var cd = ScriptableObject.CreateInstance<CardData>();

        // 关联 CardEntry（核心：让 ApplyCardEffects 走 EffectExecutor）
        cd.sourceEntry = entry;

        // 基本信息
        cd.cardName = entry.cardName;
        cd.description = entry.GetDescription(false);
        cd.cardArt = entry.cardArt;
        cd.value = entry.price;   // 商店售价（金币）

        // 费用
        cd.actionPointCost = entry.baseCost;

        // 品级映射
        cd.grade = entry.grade switch
        {
            LightMiniGame.CardEditor.CardGrade.Bronze => CardGrade.Common,
            LightMiniGame.CardEditor.CardGrade.Silver => CardGrade.Fine,
            LightMiniGame.CardEditor.CardGrade.Gold => CardGrade.Rare,
            _ => CardGrade.Common
        };

        // 卡牌类型映射（与编辑器统一）
        cd.cardType = entry.cardType switch
        {
            LightMiniGame.CardEditor.CardType.Attack => CardType.Attack,
            LightMiniGame.CardEditor.CardType.Skill => CardType.Skill,
            LightMiniGame.CardEditor.CardType.Ability => CardType.Ability,
            _ => CardType.Attack
        };

        // 词条映射（3词条：回响/灾厄/命运）
        cd.keywords = KeywordType.None;
        if (entry.keyword == CardKeyword.Echo)
            cd.keywords |= KeywordType.Echo;
        if (entry.keyword == CardKeyword.Calamity)
            cd.keywords |= KeywordType.Calamity;
        if (entry.keyword == CardKeyword.Fate)
            cd.keywords |= KeywordType.Fate;

        // 黑暗卡面
        cd.darkCardArt = entry.darkCardArt;

        // 消耗类型映射
        cd.consumeType = entry.existence switch
        {
            CardExistence.Normal => ConsumeType.None,
            CardExistence.BattleRemove => ConsumeType.ThisBattle,
            CardExistence.PermanentRemove => ConsumeType.ThisRun,
            _ => ConsumeType.None
        };

        // 攻击/护甲的旧字段设为默认值（实际由 EffectExecutor 从 CardEffect 读取）
        cd.attackValue = 0;
        cd.armorValue = 0;
        cd.buffEffects = new List<BuffEffect>();

        return cd;
    }
}
