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
        /// <summary>存活敌人数量（死亡敌人不计）</summary>
        int EnemyCount { get; }
        /// <summary>敌人槽位总数（含已死亡；槽位索引 0..EnemySlotCount-1 稳定，死亡不压缩）</summary>
        int EnemySlotCount { get; }
        /// <summary>指定槽位的敌人是否存活（越界返回 false）</summary>
        bool IsEnemyAlive(int index);
        int GetEnemyHP(int index);
        int GetEnemyArmor(int index);
        int GetEnemyBleed(int index);
        int GetEnemyArmorBreak(int index);
        int SelectedEnemyIndex { get; }

        // === 敌人侧属性（敌人作为效果发起者时读取作属性缩放；对玩家不适用）===
        int GetEnemyStrength(int slotIndex);
        int GetEnemyDexterity(int slotIndex);

        // === 牌堆状态 ===
        int HandCount { get; }
        int DrawPileCount { get; }
        int DiscardPileCount { get; }

        // === 战斗计数 ===
        int GetTurnCounter(string counterName);
        int GetBattleCounter(string counterName);

        // === 修改方法 ===
        void DealDamageToEnemy(int enemyIndex, int amount, bool ignoreArmor, bool isCrit = false, int armorBreak = 0);
        void DealDamageToAllEnemies(int amount, bool ignoreArmor, bool isCrit = false, int armorBreak = 0);
        void HealPlayer(int amount);
        void AddPlayerArmor(int amount);
        void AddPlayerEnergy(int amount);
        void DrawCards(int amount);
        void ModifyPlayerAttribute(ModifiableAttribute attr, ModifyMethod method, int amount);
        void ApplyStatusToEnemy(int enemyIndex, StatusType status, int stacks);
        void ApplyStatusToPlayer(StatusType status, int stacks);
        void RemoveStatusFromEnemy(int enemyIndex, StatusType status, int stacks);
        void RemoveStatusFromPlayer(StatusType status, int stacks);

        // === 融合攻击规则 ===
        /// <summary>
        /// 当前执行中的手牌若为融合攻击牌，且已结算的单次伤害满足活跃遗物规则则返回 true。
        /// 由 EffectExecutorV2 在暴击判定前调用；实现方不得在此重复修改伤害。
        /// </summary>
        bool IsCurrentFusedAttackGuaranteedCritical(int singleHitDamage);

        // === 敌人侧修改（敌人作为效果发起者 / 对敌人自身结算时使用）===
        /// <summary>给指定槽位敌人叠加护甲（敌人自护盾/给友军护盾）。</summary>
        void AddEnemyArmor(int slotIndex, int amount);
        /// <summary>给指定槽位敌人施加属性增益（敌人自buff）。仅敌人支持属性生效，否则返回 false。</summary>
        bool ApplyEnemyAttributeBuff(int slotIndex, PlayerAttributeType attr, int delta);

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

        int MaxHandCount { get; }
        string CurrentPlayedCardId { get; }
        int GetEnemyStatusStacks(int index, StatusType status);
        int GetAllEnemiesStatusStacks(StatusType status);
        int AddGeneratedCards(CardEntry entry, int count, CardZoneType zone);
        void SetCardStatusValueOverride(string cardId, StatusType status, int value);
        bool TryGetCardStatusValueOverride(string cardId, StatusType status, out int value);
    }
}
