using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 融合（Fusion）管理器 —— 卡牌数值融合机制的“融合模式 + 加和 + 随机分配”中枢。
///
/// 职责（对应重构需求）：
///  1) 融合模式控制：进入时开启点击监听；退出时恢复（高亮由各数字节点自身负责：数字加粗变紫）。
///  2) 精准交互接收：任何可融合数值被点击（卡面数字命中层 / FusionNumberTarget 组件）
///     都会把【该数值目标自身】交给本管理器，读取其 Value/Type 精确结算，
///     不存在“点任何数字都是同一固定值”的问题。
///  3) 加和与随机分配：依次记录 A、B 两个目标 → Sum = A.Value + B.Value →
///     随机 R ∈ [0, Sum]，A 变 R、B 变 Sum - R → 分别调回写回调更新卡牌/战场数据。
///  4) 血量对等特殊槽位可经 CustomApply 钩子原子回填。
/// </summary>
public static class FusionManager
{
    /// <summary>融合模式高亮紫（#800080）。</summary>
    public static readonly Color FusionPurple = new Color(0.5f, 0f, 0.5f, 1f);

    /// <summary>被选中（第一个融合对象）时的红色。</summary>
    public static readonly Color SelectedColor = new Color(0.92f, 0.13f, 0.36f, 1f);

    /// <summary>当前第一个（A）融合对象。</summary>
    public static FusionTarget FirstTarget { get; private set; }

    /// <summary>当前第二个（B）融合对象。</summary>
    public static FusionTarget SecondTarget { get; private set; }

    /// <summary>融合模式是否激活。</summary>
    public static bool IsFusionActive { get; private set; }

    /// <summary>每完成一次两两融合后触发（参数：A、B、A的新值、B的新值）。</summary>
    public static event Action<FusionTarget, FusionTarget, int, int> OnFusionResolved;

    /// <summary>
    /// 特殊槽位回填钩子：返回 true 表示已自行处理（跳过默认写回）。
    /// 用于“当前血量 + 血量上限”等需要原子回填的对。
    /// </summary>
    public static Func<FusionTarget, FusionTarget, int, int, bool> CustomApply;

    /// <summary>进入融合模式。</summary>
    public static void BeginFusion() => IsFusionActive = true;

    /// <summary>退出融合模式：清空选中并关闭监听。</summary>
    public static void EndFusionMode()
    {
        IsFusionActive = false;
        ClearPairSelection();
    }

    /// <summary>
    /// 任意可融合数值被点击（卡面数字命中层 / 战场数值组件）。
    /// 依次记录 A、B；凑齐两个后立即结算。
    /// </summary>
    public static void OnTargetClick(FusionTarget t)
    {
        if (!IsFusionActive || t == null || t.locked) return;

        // 再次点击已选对象 → 取消
        if (t == SecondTarget)
        {
            ClearPairSelection();
            return;
        }
        if (t == FirstTarget)
        {
            FirstTarget = null;
            return;
        }

        // 记录第一个
        if (FirstTarget == null)
        {
            FirstTarget = t;
            return;
        }

        // 已有 A：B 确定 → 立即融合
        SecondTarget = t;
        ResolvePair();
    }

    private static void ResolvePair()
    {
        var a = FirstTarget;
        var b = SecondTarget;
        ClearPairSelection();   // 结算完成后清空，可继续选下一对

        if (a == null || b == null) return;

        int aOld = a.value, bOld = b.value;
        int sum = Mathf.Max(0, aOld + bOld);
        int ra = Random.Range(0, sum + 1);
        int rb = sum - ra;

        // 特殊对（血量等）走自定义回填；否则默认：A=ra, B=rb
        bool handled = CustomApply != null && CustomApply(a, b, ra, rb);
        if (!handled)
        {
            a.Apply(ra);
            b.Apply(rb);
        }

        Debug.Log($"[FusionManager] {a.label ?? a.id}({aOld}) + {b.label ?? b.id}({bOld}) = {sum} → 随机拆分 [{ra}, {rb}]");
        OnFusionResolved?.Invoke(a, b, ra, rb);
    }

    /// <summary>取消全部选中（不离开融合模式）。</summary>
    public static void ClearPairSelection()
    {
        FirstTarget = null;
        SecondTarget = null;
    }
}