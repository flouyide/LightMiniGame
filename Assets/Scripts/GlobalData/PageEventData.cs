using System;
using System.Collections.Generic;
using UnityEngine;
using LightMiniGame.Shop;   // RelicData（GrantRelic 用）

/// <summary>
/// 书页事件类型
/// </summary>
public enum PageEventType
{
    Battle, // 战斗书页
    Shop,   // 商店书页
    Rest,   // 休整书页
    Event   // 事件书页
}

/// <summary>
/// 效果类型
/// </summary>
public enum EffectType
{
    // —— 基础类型（仅保留物品 / 战斗）——
    // 注：原 GainGold/LoseGold/GainHP/LoseHP/GainSanity/LoseSanity 已移除，
    // 统一改为 ModifyAttribute + attributeOp(Gain/Lose) + targetAttribute(Gold/HP/Sanity)。
    GainItem = 4,    // 获得物品
    LoseItem = 5,    // 失去物品
    EnterBattle = 6, // 进入战斗

    // —— 以下为特殊事件（Event）扩展效果；只追加，不重排/删除（保持 .asset 序列化兼容）——
    ModifyAttribute = 9,    // 修改玩家属性（基础属性 / 金币 / 生命 / 理智），用 attributeOp 选增/减
    GrantRelic = 10,        // 某角色获得遗物（可指定具体遗物，或从其池随机）
    GrantCard = 11,         // 某角色获得卡牌
    EnterDiscountShop = 12, // 进入特价商店（带折扣）
    RemoveCard = 13,        // 删牌（交互式：弹出牌库界面）
    AddKeywordToCard = 14   // 给点选的一张卡附加词条（交互式）
}

/// <summary>
/// 玩家持久基础属性（由特殊事件 ModifyAttribute 修改，永久保留；战斗开始时由 BattleManager 读入）。
/// 与战斗内 PlayerAttributeType 区分，避免语义耦合。
/// </summary>
public enum PlayerBaseAttribute
{
    Strength,    // 力量
    Agility,     // 敏捷（灵巧）
    Lifesteal,   // 吸血
    CritRate,    // 暴击率
    CritDamage,  // 暴击伤害

    // —— 运行时资源（追加，不重排）：由 ModifyAttribute + attributeOp 处理，等价于原 Gain*/Lose*HP/Gold/Sanity ——
    Gold,        // 金币
    HP,          // 生命值
    Sanity       // 理智（背景据此切换）
}

/// <summary>
/// 属性增/减方向（ModifyAttribute 用）。amount 恒为正数，方向决定最终是加还是减。
/// Inspector 里：先选此项（Gain / Lose），再选 targetAttribute（力量/敏捷/...），最后填 amount。
/// </summary>
public enum PlayerAttributeOp
{
    Gain, // 增加
    Lose  // 减少
}

/// <summary>
/// 效果数据
/// </summary>
[Serializable]
public class EffectData
{
    public EffectType type;
    public int amount;
    public string itemDesc = ""; // 物品描述（无物品系统，仅文本）

    // —— 以下为特殊事件扩展效果的可选字段（仅对应 type 使用）——
    public PlayerAttributeOp attributeOp = PlayerAttributeOp.Gain;  // ModifyAttribute：先选 增/减（Gain/Lose）
    public PlayerBaseAttribute targetAttribute;                     // ModifyAttribute：再选 属性（Strength/Agility/...）
    public RelicData relic;                                          // GrantRelic：具体遗物（留空=从角色池随机）
    public CardData card;                                            // GrantCard：具体卡牌
    public KeywordType keywordToAdd;                                 // AddKeywordToCard：要附加的词条（位标枚举，Inspector 可多选）
    [Range(0.1f, 1f)]
    public float discountRatio = 0.6f;                               // EnterDiscountShop：折扣比例（0.6=6折）；原 ChapterConfig.discountShopRatio 已迁移至此，按事件单独配置
}

/// <summary>
/// 事件选项
/// </summary>
[Serializable]
public class PageEventOption
{
    [TextArea] public string optionText = "";        // 选项文本，如"获得100金币"
    public List<EffectData> effects = new();        // 该选项的效果列表
}

/// <summary>
/// 书页事件数据
/// </summary>
[Serializable]
public class PageEventData
{
    [Header("基本信息")]
    public string eventId = "";                      // 唯一ID，用于互斥/后续/前置引用
    public string displayName = "";                  // 卡片标题
    [TextArea] public string description = "";       // 卡片描述文字
    public Sprite icon;                              // 卡片图标（可为空）
    public PageEventType eventType = PageEventType.Battle;

    [Header("刷新规则")]
    public bool isRepeatable;                        // 是否可重复出现
    public bool isFinalNode;                         // 是否为最终节点（boss）

    [Header("事件关系")]
    public List<string> mutuallyExclusiveIds = new();  // 互斥事件ID列表
    public List<string> followUpIds = new();           // 后续事件ID列表（选择后解锁）
    public List<string> prerequisiteIds = new();       // 前置事件ID列表（需完成后才可刷新）

    [Header("选项（Event 类型用）")]
    public List<PageEventOption> options = new()      // 选项列表（默认1个"确定"选项）
    {
        new() { optionText = "确定" }
    };

    [Header("默认效果（Battle/Shop/Rest 类型用，点击\"进入\"后直接应用）")]
    public List<EffectData> defaultEffects = new();
}
