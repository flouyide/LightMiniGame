using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 卡牌上“可融合数值”的类型（对应卡牌数据槽位 / 战场数值）。
/// </summary>
public enum FusionNumberType
{
    Cost,        // 费用
    Attack,      // 攻击
    Armor,       // 护甲
    Buff,        // 增益
    Draw,        // 抽牌
    RestoreAP,   // 回费
    Intent,      // 敌人意图卡数值
    PlayerStat,  // 玩家面板数值（能量/血量/护甲/货币）
    EnemyStat,   // 敌人通用数值（护甲/血量）
    Other        // 兜底
}

/// <summary>
/// 一个可融合数值目标（运行期数据，非组件）。
/// FusionController 在进入融合时枚举场上所有可融合数字构建本对象；
/// FusionManager 据此完成 A/B 选择与 加和→随机拆分→回填。
/// </summary>
[Serializable]
public class FusionTarget
{
    /// <summary>唯一 key（如 "hand:0:cost" / "player:energy"）。</summary>
    public string id;

    /// <summary>中文显示名（状态栏提示用）。</summary>
    public string label;

    /// <summary>数值类型。</summary>
    public FusionNumberType type;

    /// <summary>当前数值（进入融合时快照，融合后写回）。</summary>
    public int value;

    /// <summary>是否被理智锁定（低理智下血量类为 true，不可选、不染色）。</summary>
    public bool locked;

    /// <summary>回填回调：把融合得到的某个数值写回该槽位。</summary>
    [NonSerialized] public Action<int> apply;

    public FusionTarget() { }

    public FusionTarget(string id, string label, FusionNumberType type, int value, bool locked, Action<int> apply)
    {
        this.id = id;
        this.label = label;
        this.type = type;
        this.value = value;
        this.locked = locked;
        this.apply = apply;
    }

    public void Apply(int v) => apply?.Invoke(v);

    public override string ToString() => $"{label}: {value}";
}

/// <summary>
/// 可融合数值组件 —— 挂在“战场数值的数字节点”上（能量/血量/护甲/货币/敌人护甲等）。
///
/// 视觉：融合模式中【数字本身加粗变紫】（直接改所绑定 TMP 的 color + Bold），
/// 退出时恢复原色/原字重；不生成任何高亮方块。
/// 交互：实现 IPointerClickHandler，点击把本节点交给 FusionManager 参与两两融合。
/// 卡面描述/费用数字使用 CardDisplay 的原位富文本着色 + 透明命中层，不挂本组件。
/// </summary>
public class FusionNumberTarget : MonoBehaviour, IPointerClickHandler
{
    [Header("配置")]
    [Tooltip("数值目标（由 FusionController 构建注入）")]
    public FusionTarget target;

    private TMPro.TextMeshProUGUI _text;      // 绑定的数字文本（可空：透明命中层无文字）
    private Color _origColor;
    private TMPro.FontStyles _origStyle;
    private bool _hasVisual;
    private bool _fusionActive;
    private bool _selected;

    /// <summary>当前数值。</summary>
    public int Value => target != null ? target.value : 0;

    /// <summary>是否被选中（第一个融合对象）。</summary>
    public bool IsSelected => _selected;

    /// <summary>绑定目标与可选的文字节点（文字节点负责数字视觉：加粗变紫）。</summary>
    public void Setup(FusionTarget target, TextMeshProUGUI text)
    {
        this.target = target;
        _text = text;
        if (text != null && !_hasCaptured)
        {
            _origColor = text.color;
            _origStyle = text.fontStyle;
            _hasCaptured = true;
        }
        RefreshVisual();
    }

    private bool _hasCaptured;

    /// <summary>进入/退出融合模式。</summary>
    public void SetFusionActive(bool on)
    {
        _fusionActive = on;
        if (!on) _selected = false;
        RefreshVisual();
    }

    /// <summary>设置/取消选中（融合中第一个被点选对象）。</summary>
    public void SetSelected(bool sel)
    {
        _selected = sel;
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (_text == null) return;
        if (!_fusionActive || (target != null && target.locked))
        {
            _text.color = _origColor;
            _text.fontStyle = _origStyle;
            return;
        }
        _text.color = _selected ? FusionManager.SelectedColor : FusionManager.FusionPurple;
        _text.fontStyle = TMPro.FontStyles.Bold;
    }

    /// <summary>UGUI 精准点击：把本节点数据交给 FusionManager。</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        FusionManager.OnTargetClick(target);
    }
}