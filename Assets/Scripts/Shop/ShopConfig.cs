using UnityEngine;

namespace LightMiniGame.Shop
{
    /// <summary>
    /// 商店全局配置（ScriptableObject）。仿《杀戮尖塔》商店的可配置参数。
    /// 在编辑器通过菜单 CardGame/Shop Config 创建资产，然后拖到场景中 ShopManager 组件的 Inspector 字段。
    /// </summary>
    [CreateAssetMenu(menuName = "CardGame/Shop Config", fileName = "ShopConfig")]
    public class ShopConfig : ScriptableObject
    {
        [Header("商品数量")]
        [Tooltip("商店内同时出售的卡牌总数")]
        public int shopCardCount = 5;
        [Tooltip("商店内同时出售的遗物数量")]
        public int shopRelicCount = 3;

        [Header("卡牌角色比例")]
        [Range(0f, 1f)]
        [Tooltip("商店卡牌中来自 Character1 卡池的比例；Character2 数量 = 卡牌总数 - Character1 数量（保证两者之和=卡牌总数）")]
        public float character1CardRatio = 0.5f;
        [Range(0f, 1f)]
        [Tooltip("Character2 卡牌比例（仅作参考/校验，实际数量由 character1CardRatio 推导）")]
        public float character2CardRatio = 0.5f;

        [Header("遗物角色比例")]
        [Range(0f, 1f)]
        [Tooltip("商店遗物中来自 Character1 遗物池的比例；Character2 数量 = 遗物总数 - Character1 数量（保证两者之和=遗物总数）。每个角色从各自独立的遗物池中按此比例抽取。")]
        public float character1RelicRatio = 0.5f;
        [Range(0f, 1f)]
        [Tooltip("Character2 遗物比例（仅作参考/校验，实际数量由 character1RelicRatio 推导）")]
        public float character2RelicRatio = 0.5f;

        [Header("删牌服务")]
        [Tooltip("商店提供的可删牌次数（每次消耗一次服务并支付价格）")]
        public int removalCount = 3;
        [Tooltip("单次删牌服务价格")]
        public int removalBasePrice = 75;
        [Tooltip("每多删一次，价格递增量（仿 StS：75 / 100 / 125 …）")]
        public int removalPriceStep = 25;

        // ===== 按品级概率抽取（后续升级，目前只留接口）=====
        // 抽卡策略通过 ShopManager 的 ICardDrawStrategy 注入：默认 UniformCardDraw（均匀随机）。
        // 后续实现 GradeWeightedCardDraw（按 CardGrade 权重抽取），并在此处暴露权重表即可，无需改动调用方。
    }
}
