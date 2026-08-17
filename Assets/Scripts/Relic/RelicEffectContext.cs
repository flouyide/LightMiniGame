using LightMiniGame.Card;   // CharacterData
using LightMiniGame.Shop;    // RelicData

namespace LightMiniGame.Relic
{
    /// <summary>
    /// 遗物效果执行上下文：为效果提供可操作的系统引用。
    /// 由 RelicEffectManager 在各生命周期触发时填充并传入，效果内据此修改游戏状态。
    /// </summary>
    public class RelicEffectContext
    {
        /// <summary>局外/全局系统：金币、玩家持久属性、遗物/卡牌发放等（ChapterManager）。</summary>
        public ChapterManager chapter;

        /// <summary>当前战斗实例（OnBattleStart/OnBattleEnd 时有效；非战斗场景为 null）。</summary>
        public BattleManager battle;

        /// <summary>归属角色（遗物库按角色隔离）。</summary>
        public CharacterData owner;

        /// <summary>对应的遗物数据（RelicData），可读取 value / description / relicName 等。</summary>
        public RelicData relic;
    }
}
