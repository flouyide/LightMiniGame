using System.Collections.Generic;
using UnityEngine;
using LightMiniGame.CardEditor;

/// <summary>
/// 战斗运行时上下文 —— 实现 ICardRuntimeContext，桥接 CardEntry 效果系统与 BattleManager。
/// 由 BattleManager 创建并传入 EffectExecutor，让效果/自定义脚本能访问和修改真实战斗状态。
/// </summary>
public class BattleCardContext : ICardRuntimeContext
{
    private readonly BattleManager _battle;

    public BattleCardContext(BattleManager battle) { _battle = battle; }

    // === 玩家状态 ===
    public int PlayerHP => _battle.PlayerHP;
    public int PlayerMaxHP => _battle.PlayerMaxHP;
    public int PlayerStrength => _battle.PlayerStrength;
    public int PlayerDexterity => _battle.PlayerDexterity;
    public float PlayerCritRate => _battle.PlayerCritRate;
    public float PlayerCritDamage => _battle.PlayerCritDamage;
    public int PlayerSanity => _battle.PlayerSanity;
    public int PlayerEnergy => _battle.ActionPoints;
    public int PlayerArmor => _battle.PlayerArmor;
    public int PlayerBleed => _battle.PlayerBleed;

    // === 敌人状态 ===
    public int EnemyCount => _battle.EnemyCount;
    public int GetEnemyHP(int index) => _battle.GetEnemyHP(index);
    public int GetEnemyArmor(int index) => _battle.GetEnemyArmor(index);
    public int GetEnemyBleed(int index) => _battle.GetEnemyBleed(index);
    public int GetEnemyArmorBreak(int index) => _battle.GetEnemyArmorBreak(index);
    public int SelectedEnemyIndex => _battle.SelectedEnemyIndex;

    // === 牌堆状态 ===
    public int HandCount => _battle.HandCount;
    public int DrawPileCount => _battle.DrawPileCount;
    public int DiscardPileCount => _battle.DiscardPileCount;

    // === 战斗计数 ===
    public int GetTurnCounter(string counterName) => _battle.GetTurnCounter(counterName);
    public int GetBattleCounter(string counterName) => _battle.GetBattleCounter(counterName);

    // === 修改方法 ===
    public void DealDamageToEnemy(int enemyIndex, int amount, bool ignoreArmor)
        => _battle.DealDamageToEnemy(enemyIndex, amount, ignoreArmor);

    public void DealDamageToAllEnemies(int amount, bool ignoreArmor)
        => _battle.DealDamageToAllEnemies(amount, ignoreArmor);

    public void HealPlayer(int amount) => _battle.HealPlayer(amount);
    public void AddPlayerArmor(int amount) => _battle.AddPlayerArmor(amount);
    public void AddPlayerEnergy(int amount) => _battle.AddActionPoints(amount);
    public void DrawCards(int amount) => _battle.DrawCards(amount);
    public void ModifyPlayerAttribute(ModifiableAttribute attr, ModifyMethod method, int amount)
        => _battle.ModifyPlayerAttribute(attr, method, amount);
    public void ApplyStatusToEnemy(int enemyIndex, StatusType status, int stacks)
        => _battle.ApplyStatusToEnemy(enemyIndex, status, stacks);
    public void ApplyStatusToPlayer(StatusType status, int stacks)
        => _battle.ApplyStatusToPlayer(status, stacks);

    // === 自定义数据存取 ===
    public int GetCustomData(string key) => _battle.GetCustomData(key);
    public void SetCustomData(string key, int value) => _battle.SetCustomData(key, value);
    public void ModifyCustomData(string key, int delta) => _battle.ModifyCustomData(key, delta);

    // === 事件记录 ===
    public bool HasEventOccurred(string eventName) => _battle.HasEventOccurred(eventName);
    public void RecordEvent(string eventName) => _battle.RecordEvent(eventName);

    // === 手牌操作 ===
    public int RequestSelectCardFromHand(string prompt) => _battle.RequestSelectCardFromHand(prompt);
    public void DiscardHandCard(int index) => _battle.DiscardHandCard(index);
}
