using System.Collections.Generic;
using UnityEngine;

namespace LightMiniGame.Shop
{
    /// <summary>
    /// 总遗物库：内部包含游戏中所有的遗物。
    /// 商店每次开张时从这里随机抽取出售的遗物。
    /// 在编辑器通过菜单 CardGame/Master Relic Library 创建，放到 Resources/MasterRelicLibrary.asset
    /// 或拖到 ShopManager 的对应字段。
    /// </summary>
    [CreateAssetMenu(menuName = "CardGame/Master Relic Library", fileName = "MasterRelicLibrary")]
    public class MasterRelicLibrary : ScriptableObject
    {
        public const string ResourcePath = "MasterRelicLibrary";

        [Tooltip("游戏内所有遗物。商店从中随机抽取出售。")]
        public List<RelicData> allRelics = new List<RelicData>();

        public static MasterRelicLibrary Load()
            => Resources.Load<MasterRelicLibrary>(ResourcePath);
    }
}
