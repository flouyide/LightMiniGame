using UnityEngine;

namespace LightMiniGame.Shop
{
    /// <summary>
    /// 遗物数据（ScriptableObject）。仿《杀戮尖塔》遗物：全局持有，拥有价格 value。
    /// 在编辑器通过菜单 CardGame/Relic Data 创建。
    /// </summary>
    [CreateAssetMenu(menuName = "CardGame/Relic Data", fileName = "NewRelic")]
    public class RelicData : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("稳定唯一ID（如 iron_ring），持久化/查询用")]
        public string relicId;

        [Tooltip("显示名（如 铁戒指）")]
        public string relicName;

        [TextArea(2, 4)]
        public string description;

        public Sprite icon;

        [Header("商店")]
        [Tooltip("购买价格（仿 CardData.value 的商店价值字段）")]
        public int value = 100;

        [Header("品级（预留：后续按品级抽取遗物）")]
        public CardGrade grade = CardGrade.Common;

        [Header("排序")]
        [Tooltip("在总遗物库中的排序权重，越大越靠前（可选）")]
        public int sortOrder;
    }
}
