using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单章配置
/// </summary>
[CreateAssetMenu(fileName = "Chapter", menuName = "BookGame/Chapter Config")]
public class ChapterConfig : ScriptableObject
{
    [Header("章节信息")]
    public string chapterName = "第一章";   // 章节名称
    public int maxSelections = 10;           // Z: 最多可选择页数
    public List<PageEventData> events = new(); // Y: 本章所有事件池
}
