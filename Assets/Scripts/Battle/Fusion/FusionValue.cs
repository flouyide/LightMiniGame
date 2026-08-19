using System;
using UnityEngine;

/// <summary>
/// 融合（Fusion）机制 —— 一个可被玩家选中并参与数值融合的“数字槽位”。
/// 只读描述：当前值 + 标签 + 是否被低理智锁定 + 回填回调 + 原位锚点。
/// FusionController 通过 Provider 枚举出当前场上所有可选 FusableValue；
/// 玩家确认融合后，把拆分结果逐个调用 apply 回填到对应槽位。
/// anchor 用于在场上“原位”生成高亮徽章（玩家能量/护甲/血量、敌人护甲/意图等）。
/// </summary>
public class FusableValue
{
    /// <summary>唯一 key（如 "player:energy"、"hand:cost:0"、"enemy:0:armor"、"player:hp"）</summary>
    public string id;

    /// <summary>中文显示名（如 "能量"、"手牌0·费用"、"敌人1·护甲"）</summary>
    public string label;

    /// <summary>当前值（进入融合状态时快照/实时读取）</summary>
    public int current;

    /// <summary>是否已被理智锁定（理智≤4 时血量类为 true，不可被选中参与融合）</summary>
    public bool lockedBySanity;

    /// <summary>原位锚点：徽章生成于此 RectTransform 上（null 则不生成原位徽章）。</summary>
    public RectTransform anchor;

    /// <summary>徽章相对锚点的局部偏移。</summary>
    public Vector2 anchorOffset = Vector2.zero;

    /// <summary>可选的源 CardDisplay（用于卡面描述内数字的精确定位）。</summary>
    public CardDisplay cardView;

    /// <summary>预计算的精确数字位置（世界坐标，中心+尺寸）。优先级高于 cardView.TryGetNumberRects，供意图牌库 token 用。</summary>
    public bool hasExactRect;
    public Vector2 exactCenter;
    public Vector2 exactSize;

    /// <summary>回填回调：把拆分得到的某个数值写回该槽位。</summary>
    public Action<int> apply;

    public FusableValue() { }

    public FusableValue(string id, string label, int current, bool locked, Action<int> apply)
    {
        this.id = id;
        this.label = label;
        this.current = current;
        this.lockedBySanity = locked;
        this.apply = apply;
    }

    public override string ToString() => $"{label}: {current}";
}