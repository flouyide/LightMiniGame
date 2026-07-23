using System.Collections.Generic;
using System.Linq;
using LightMiniGame.Card;
using UnityEngine;

namespace LightMiniGame.Shop
{
    /// <summary>
    /// 单个角色的「全部遗物」池（总遗物库的组成单元）。仿 CharacterCardPool。
    /// </summary>
    [System.Serializable]
    public class CharacterRelicPool
    {
        public CharacterData character;             // 归属角色（= 该角色在游戏中的身份令牌）
        public List<RelicData> relics = new List<RelicData>();  // 该角色拥有的全部遗物
    }

    /// <summary>
    /// 总遗物库：内部包含每个角色的所有遗物（按角色隔离的遗物池列表，仿 MasterCardLibrary）。
    /// 商店每次开张时从这里按角色比例随机抽取出售的遗物。
    /// 在编辑器通过菜单 CardGame/Master Relic Library 创建资产，然后拖到场景中 ShopManager 组件的 Inspector 字段。
    ///
    /// 遗物按角色隔离：为每个角色配置一个 CharacterRelicPool，商店按角色比例从中抽取；
    /// 未配置分池（pools 为空）时 GetRelics 返回 null。
    /// </summary>
    [CreateAssetMenu(menuName = "CardGame/Master Relic Library", fileName = "MasterRelicLibrary")]
    public class MasterRelicLibrary : ScriptableObject
    {
        [Tooltip("每个角色拥有的全部遗物。列表顺序即 角色1、角色2…；商店按角色比例从中抽取。")]
        public List<CharacterRelicPool> pools = new List<CharacterRelicPool>();

        public CharacterRelicPool GetPool(CharacterData c)
            => pools.FirstOrDefault(p => p.character == c);

        public List<RelicData> GetRelics(CharacterData c)
        {
            var pool = GetPool(c);
            if (pool != null) return pool.relics;
            return null;
        }

        /// <summary>按索引取角色（0=角色1，1=角色2…）。用于商店按角色拆分遗物数量。</summary>
        public CharacterData GetCharacter(int index)
            => (index >= 0 && index < pools.Count) ? pools[index].character : null;

        /// <summary>按索引取该角色的遗物池（用于抽遗物策略）。</summary>
        public List<RelicData> GetRelicsByIndex(int index)
            => (index >= 0 && index < pools.Count) ? pools[index].relics : null;
    }
}
