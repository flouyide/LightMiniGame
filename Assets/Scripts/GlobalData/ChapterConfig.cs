using System.Collections.Generic;
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

    [Header("背景（按玩家 Sanity 切换）")]
    [Tooltip("理智高于（含等于）阈值时使用的背景图")]
    public Sprite NormalBG;
    [Tooltip("理智低于阈值时使用的背景图")]
    public Sprite AbnormalBG;
    [Tooltip("理智阈值：玩家 Sanity >= 此值时显示 NormalBG，< 此值时显示 AbnormalBG。可在 Inspector 配置")]
    public int sanityThreshold = 50;        // 理智阈值（可在 Inspector 配置）
}
