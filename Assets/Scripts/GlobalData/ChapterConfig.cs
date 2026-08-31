using System;
using System.Collections.Generic;
using LightMiniGame.Card;
using LightMiniGame.CardEditor;
using UnityEngine;

/// <summary>
/// 单章配置
/// </summary>
[CreateAssetMenu(fileName = "Chapter", menuName = "LightGame/Chapter Config")]
public class ChapterConfig : ScriptableObject
{
    [Header("章节信息")]
    public string chapterName = "第一章";   // 章节名称
    public int maxSelections = 10;           // Z: 最多可选择页数
    public List<PageEventData> events = new(); // Y: 本章所有事件池

    [Header("章节起始卡组覆盖")]
    [Tooltip("进入本章节时替换指定角色的运行时卡组。条目中的卡组为空时，不改变该角色现有卡组。")]
    public List<CharacterDeckOverride> characterDeckOverrides = new();

    [Header("背景（按玩家 Sanity 切换）")]
    [Tooltip("理智高于（含等于）阈值时使用的背景图")]
    public Sprite NormalBG;
    [Tooltip("理智低于阈值时使用的背景图")]
    public Sprite AbnormalBG;
    [Tooltip("理智阈值：玩家 Sanity >= 此值时显示 NormalBG，< 此值时显示 AbnormalBG。可在 Inspector 配置")]
    public int sanityThreshold = 50;        // 理智阈值（可在 Inspector 配置）
}

/// <summary>
/// 单章节的角色卡组覆盖配置。进入章节时，非空 cards 会完全替换该角色现有运行时卡组；
/// cards 为空时不做任何变更，保留前一章节/起始卡组。
/// </summary>
[Serializable]
public class CharacterDeckOverride
{
    [Tooltip("要覆盖卡组的角色")]
    public CharacterData character;

    [Tooltip("章节开始时写入的卡组。为空时不改变该角色原有卡组；允许重复卡牌。")]
    public List<CardEntry> cards = new();
}
