using UnityEngine;

namespace LightMiniGame.CardEditor
{
    /// <summary>
    /// 示例：弃一张手牌再抽两张
    /// 这是一个 CustomCardScript —— 处理整张卡的特殊流程。
    ///
    /// 这个效果无法用通用字段表达，因为：
    ///   1. 需要玩家从手牌中二次选择（通用效果没有「选牌」目标）
    ///   2. 先弃后抽的顺序需要脚本控制
    ///
    /// 编辑器配置：
    ///   在卡牌的「自定义卡牌脚本」字段拖入此资产。
    ///   自定义参数 = {"drawCount": 2}
    ///
    /// 行为：
    ///   1. 弹出提示让玩家选择一张手牌
    ///   2. 弃掉选中的牌
    ///   3. 抽 drawCount 张牌
    ///   4. 返回 false → 跳过卡牌自身的通用效果列表（整张卡由脚本接管）
    /// </summary>
    [CreateAssetMenu(menuName = "CardEditor/自定义卡牌/弃牌抽牌", fileName = "DiscardThenDraw")]
    public class DiscardThenDrawCardScript : CustomCardScript
    {
        public override string GetDisplayName() => "弃一张手牌再抽两张";

        public override bool OnCardPlayed(ICardRuntimeContext ctx, CardEntry card, bool upgraded)
        {
            // 从自定义参数读取抽牌数
            int drawCount = 2;
            if (!string.IsNullOrEmpty(card.customCardScript != null ? card.designerNotes : ""))
            {
                // 也可以从 designerNotes 读，这里简单用默认值
            }

            // 步骤1：请求玩家选择一张手牌
            int selectedIndex = ctx.RequestSelectCardFromHand("选择一张手牌弃掉");
            if (selectedIndex < 0)
            {
                Debug.Log("[DiscardThenDrawCardScript] 玩家取消选择，卡牌效果不执行");
                return false; // 玩家取消，不执行效果，也不执行通用效果
            }

            // 步骤2：弃掉选中的牌
            ctx.DiscardHandCard(selectedIndex);
            Debug.Log($"[DiscardThenDrawCardScript] 弃掉了手牌第 {selectedIndex + 1} 张");

            // 步骤3：抽牌
            ctx.DrawCards(drawCount);
            Debug.Log($"[DiscardThenDrawCardScript] 抽了 {drawCount} 张牌");

            // 步骤4：返回 false 表示整张卡由脚本接管，不执行通用效果列表
            return false;
        }
    }
}
