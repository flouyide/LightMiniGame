using UnityEngine;

namespace LightMiniGame.CardEditor
{
    /// <summary>
    /// 示例：热度高于阈值
    /// 用于「狙击」等需要检查「热度是否大于指定值」的条件。
    ///
    /// 编辑器配置：
    ///   条件类型 = 自定义
    ///   自定义参数 = {"threshold": 15}
    ///
    /// 策划操作：
    ///   1. 程序员创建此脚本的 .asset 资产
    ///   2. 策划在条件编辑中拖入「条件脚本」字段
    ///   3. 在「自定义参数」中填写阈值
    /// </summary>
    [CreateAssetMenu(menuName = "CardEditor/自定义条件/热度高于阈值", fileName = "HeatAboveThreshold")]
    public class HeatAboveThresholdCondition : CustomConditionScript
    {
        public override string GetDisplayName() => "热度高于阈值";

        public override bool Evaluate(ICardRuntimeContext ctx, string customParams)
        {
            int threshold = 0;
            if (!string.IsNullOrEmpty(customParams))
            {
                var match = System.Text.RegularExpressions.Regex.Match(customParams, @"threshold[""\s]*:\s*(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed))
                    threshold = parsed;
            }

            int currentHeat = ctx.GetCustomData("Heat");
            bool result = currentHeat > threshold;
            Debug.Log($"[HeatAboveThresholdCondition] 热度={currentHeat} 阈值={threshold} → {result}");
            return result;
        }
    }
}
