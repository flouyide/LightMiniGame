using UnityEngine;

namespace LightMiniGame.Card
{
    /// <summary>
    /// 游戏角色身份（ScriptableObject）。
    /// 既作为角色的标识令牌，也充当牌库的索引 key（与 CardData 风格一致）。
    /// </summary>
    [CreateAssetMenu(menuName = "CardGame/Character Data", fileName = "NewCharacter")]
    public class CharacterData : ScriptableObject
    {
        [Header("角色标识")]
        [Tooltip("稳定唯一ID（如 warrior），持久化/查询用，不要用 displayName 当 key")]
        public string characterId;

        [Tooltip("显示名（如 战士），可变，不作为路由 key")]
        public string displayName;

        [Header("角色外观")]
        [Tooltip("角色头像（牌库面板/角色栏显示用，拖入 Sprite）")]
        public Sprite avatar;

        [Header("初始牌组")]
        [Tooltip("该角色的初始牌库配置；游戏开始时据此构建起始卡组。为空则预置空牌库")]
        public CharacterStartingLibrary startingLibrary;

        [Header("可选约束")]
        [Tooltip("该角色牌库容量上限；<=0 表示不限制")]
        public int maxLibrarySize = 100;

        /// <summary>用于字典 key / 日志的友好名</summary>
        public string Label => string.IsNullOrEmpty(displayName) ? characterId : displayName;
    }
}
