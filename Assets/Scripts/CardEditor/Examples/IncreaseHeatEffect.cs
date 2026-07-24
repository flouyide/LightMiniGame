using UnityEngine;

namespace LightMiniGame.CardEditor
{
    /// <summary>
    /// 示例：增加热度
    /// 用于「弹链」等需要操作自定义「热度」属性的卡牌。
    /// 热度不在通用属性列表中，因此通过 ICardRuntimeContext.GetCustomData / SetCustomData 存取。
    ///
    /// 编辑器配置：
    ///   效果类型 = 自定义
    ///   自定义参数 = {"heatIncrease": 1}
    ///
    /// 策划操作：
    ///   1. 程序员创建此脚本的 .asset 资产
    ///   2. 策划在效果编辑器中拖入「效果脚本」字段
    ///   3. 在「自定义参数」中填写 heatIncrease 的值
    /// </summary>
    [CreateAssetMenu(menuName = "CardEditor/自定义效果/增加热度", fileName = "IncreaseHeatEffect")]
    public class IncreaseHeatEffect : CustomEffectScript
    {
        public override string GetDisplayName() => "增加热度";

        public override void Execute(ICardRuntimeContext ctx, string customParams)
        {
            // 默认增加 1 点热度，也可从 customParams 读取
            int amount = 1;
            if (!string.IsNullOrEmpty(customParams))
            {
                // 简单解析 {"heatIncrease": N} 格式
                var match = System.Text.RegularExpressions.Regex.Match(customParams, @"heatIncrease[""\s]*:\s*(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed))
                    amount = parsed;
            }

            ctx.ModifyCustomData("Heat", amount);
            Debug.Log($"[IncreaseHeatEffect] 热度 +{amount}，当前热度 = {ctx.GetCustomData("Heat")}");
        }
    }
}
