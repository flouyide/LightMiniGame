using System;
using System.Collections.Generic;
using UnityEngine;

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
    GainGold,    // 获得金币
    LoseGold,    // 失去金币
    GainHP,      // 获得生命值
    LoseHP,      // 失去生命值
    GainItem,    // 获得物品
    LoseItem,    // 失去物品
    EnterBattle  // 进入战斗
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
