using System;
using System.Collections.Generic;
using System.Linq;
using LightMiniGame.Card;
using UnityEngine;

namespace LightMiniGame.Shop
{
    /// <summary>
    /// 单个角色的「全部卡牌」卡池（总牌库的组成单元）。
    /// </summary>
    [Serializable]
    public class CharacterCardPool
    {
        public CharacterData character;             // 归属角色（= 该角色在游戏中的身份令牌）
        public List<CardData> cards = new List<CardData>();  // 该角色拥有的全部卡牌模板
    }

    /// <summary>
    /// 总牌库：内部包含每个角色的所有卡牌（按角色隔离的卡池列表）。
    /// 商店每次开张时从这里按角色比例随机抽取出售的卡牌。
    /// 在编辑器通过菜单 CardGame/Master Card Library 创建资产，然后拖到场景中 ShopManager 组件的 Inspector 字段。
    /// </summary>
    [CreateAssetMenu(menuName = "CardGame/Master Card Library", fileName = "MasterCardLibrary")]
    public class MasterCardLibrary : ScriptableObject
    {
        [Tooltip("每个角色拥有的全部卡牌。列表顺序即 角色1、角色2…；商店按角色比例从中抽取。")]
        public List<CharacterCardPool> pools = new List<CharacterCardPool>();

        public CharacterCardPool GetPool(CharacterData c)
            => pools.FirstOrDefault(p => p.character == c);

        public List<CardData> GetCards(CharacterData c)
            => GetPool(c)?.cards;

        /// <summary>按索引取角色（0=角色1，1=角色2…）。用于商店按角色拆分卡牌数量。</summary>
        public CharacterData GetCharacter(int index)
            => (index >= 0 && index < pools.Count) ? pools[index].character : null;

        /// <summary>按索引取该角色的卡池（用于抽卡策略）。</summary>
        public List<CardData> GetCardsByIndex(int index)
            => (index >= 0 && index < pools.Count) ? pools[index].cards : null;
    }
}
