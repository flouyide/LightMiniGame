using System.Collections.Generic;

namespace LightMiniGame.CardEditor
{
    /// <summary>
    /// 卡牌效果的运行时上下文接口。
    /// 自定义脚本通过此接口访问和修改战斗状态，不直接依赖战斗系统实现。
    /// 战斗系统负责实现此接口，并在执行自定义脚本时传入。
    /// </summary>
    public interface ICardRuntimeContext
    {
        // === 玩家状态 ===
        int PlayerHP { get; }
        int PlayerMaxHP { get; }
        int PlayerStrength { get; }
        int PlayerDexterity { get; }
        float PlayerCritRate { get; }
        float PlayerCritDamage { get; }
        int PlayerSanity { get; }
        int PlayerEnergy { get; }
        int PlayerArmor { get; }
        int PlayerBleed { get; }

        // === 敌人状态 ===
        int EnemyCount { get; }
        int GetEnemyHP(int index);
        int GetEnemyArmor(int index);
        int GetEnemyBleed(int index);
        int GetEnemyArmorBreak(int index);
        int SelectedEnemyIndex { get; }

        // === 牌堆状态 ===
        int HandCount { get; }
        int DrawPileCount { get; }
        int DiscardPileCount { get; }

        // === 战斗计数 ===
        int GetTurnCounter(string counterName);
        int GetBattleCounter(string counterName);

        // === 修改方法 ===
        void DealDamageToEnemy(int enemyIndex, int amount, bool ignoreArmor);
        void DealDamageToAllEnemies(int amount, bool ignoreArmor);
        void HealPlayer(int amount);
        void AddPlayerArmor(int amount);
        void AddPlayerEnergy(int amount);
        void DrawCards(int amount);
        void ModifyPlayerAttribute(ModifiableAttribute attr, ModifyMethod method, int amount);
        void ApplyStatusToEnemy(int enemyIndex, StatusType status, int stacks);
        void ApplyStatusToPlayer(StatusType status, int stacks);

        // === 自定义数据存取（用于热度等非通用属性）===
        int GetCustomData(string key);
        void SetCustomData(string key, int value);
        void ModifyCustomData(string key, int delta);

        // === 事件记录 ===
        bool HasEventOccurred(string eventName);
        void RecordEvent(string eventName);

        // === 手牌操作（CustomCardScript 可能需要）===
        /// <summary>请求玩家从手牌中选择一张牌，返回选中的牌索引（-1 = 取消）</summary>
        int RequestSelectCardFromHand(string prompt);
        /// <summary>从手牌中弃掉指定索引的牌</summary>
        void DiscardHandCard(int index);
    }
}
