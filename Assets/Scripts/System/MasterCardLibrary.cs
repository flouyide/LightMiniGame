using System;
using System.Collections.Generic;
using System.Linq;
using LightMiniGame.Card;
using LightMiniGame.CardEditor;
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
        public List<CardEntry> cards = new List<CardEntry>();  // 该角色拥有的全部卡牌模板（编辑器 CardEntry 格式）
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

        // 转换缓存：CardEntry -> 运行时 CardData。同一 CardEntry 始终映射到同一实例，
        // 保证商店抽卡去重（HashSet<CardData>）在多次 GetCards 调用间保持一致。
        private Dictionary<CharacterData, List<CardData>> _convertedCache;

        public List<CardData> GetCards(CharacterData c)
        {
            var pool = GetPool(c);
            if (pool == null) return null;
            if (_convertedCache == null) _convertedCache = new Dictionary<CharacterData, List<CardData>>();
            if (!_convertedCache.TryGetValue(c, out var list))
            {
                list = CardEntryAdapter.ConvertToCardData(pool.cards);
                _convertedCache[c] = list;
            }
            return list;
        }

        /// <summary>按索引取角色（0=角色1，1=角色2…）。用于商店按角色拆分卡牌数量。</summary>
        public CharacterData GetCharacter(int index)
            => (index >= 0 && index < pools.Count) ? pools[index].character : null;

        /// <summary>按索引取该角色的卡池（用于抽卡策略）。返回转换后的运行时 CardData。</summary>
        public List<CardData> GetCardsByIndex(int index)
        {
            if (index < 0 || index >= pools.Count) return null;
            var c = pools[index].character;
            if (c == null) return null;
            if (_convertedCache == null) _convertedCache = new Dictionary<CharacterData, List<CardData>>();
            if (!_convertedCache.TryGetValue(c, out var list))
            {
                list = CardEntryAdapter.ConvertToCardData(pools[index].cards);
                _convertedCache[c] = list;
            }
            return list;
        }
    }
}
