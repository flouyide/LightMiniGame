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

    public BattleManager Battle => _battle;

    /// <summary>当前效果发起者敌人槽位；&lt;0 表示玩家出牌。</summary>
    public int InitiatorEnemySlot { get; set; } = -1;

    // === 玩家状态 ===
    public int PlayerHP => _battle.PlayerHP;
    public int PlayerMaxHP => _battle.PlayerMaxHP;
    public int PlayerStrength => _battle.PlayerStrength;
    public int PlayerDexterity => _battle.PlayerDexterity;
    public float PlayerCritRate => _battle.PlayerCritRate;
    public float PlayerCritDamage => _battle.PlayerCritDamage;
    public int PlayerSanity => _battle.PlayerSanity;
    public int PlayerMaxSanity => _battle.PlayerMaxSanity;
    public int PlayerFortune => _battle.PlayerFortune;
    public int PlayerEnergy => _battle.ActionPoints;
    public int PlayerArmor => _battle.PlayerArmor;
    public int PlayerBleed => _battle.PlayerBleed;

    // === 敌人状态 ===
    public int EnemyCount => _battle.EnemyCount;
    public int EnemySlotCount => _battle.EnemySlotCount;
    public bool IsEnemyAlive(int index) => _battle.IsEnemyAlive(index);
    public int GetEnemyHP(int index) => _battle.GetEnemyHP(index);
    public int GetEnemyArmor(int index) => _battle.GetEnemyArmor(index);
    public int GetEnemyBleed(int index) => _battle.GetEnemyBleed(index);
    public int GetEnemyArmorBreak(int index) => _battle.GetEnemyArmorBreak(index);
    public int SelectedEnemyIndex => _battle.SelectedEnemyIndex;

    public int GetEnemyStrength(int index) => _battle.GetEnemyStrength(index);
    public int GetEnemyDexterity(int index) => _battle.GetEnemyDexterity(index);

    // === 牌堆状态 ===
    public int HandCount => _battle.HandCount;
    public int DrawPileCount => _battle.DrawPileCount;
    public int DiscardPileCount => _battle.DiscardPileCount;

    // === 战斗计数 ===
    public int GetTurnCounter(string counterName) => _battle.GetTurnCounter(counterName);
    public int GetBattleCounter(string counterName) => _battle.GetBattleCounter(counterName);

    // === 修改方法 ===
    public void DealDamageToEnemy(int enemyIndex, int amount, bool ignoreArmor, bool isCrit = false, int armorBreak = 0)
        => _battle.DealDamageToEnemy(enemyIndex, amount, ignoreArmor, isCrit, armorBreak);

    public void DealDamageToAllEnemies(int amount, bool ignoreArmor, bool isCrit = false, int armorBreak = 0)
        => _battle.DealDamageToAllEnemies(amount, ignoreArmor, isCrit, armorBreak);

    public void HealPlayer(int amount) => _battle.HealPlayer(amount);
    public void LosePlayerHP(int amount) => _battle.LosePlayerHP(amount);
    public void ModifyEnemyHP(int slot, int delta) => _battle.ModifyEnemyHP(slot, delta);
    public void ModifyAllEnemiesHP(int delta) => _battle.ModifyAllEnemiesHP(delta);
    public int PlayerGold => _battle.PlayerGold;
    public void ModifyPlayerGold(int delta) => _battle.ModifyPlayerGold(delta);
    public void SetPlayerGold(int value) => _battle.SetPlayerGold(value);
    public void AddPlayerArmor(int amount) => _battle.AddPlayerArmor(amount);
    public void AddPlayerEnergy(int amount) => _battle.AddActionPoints(amount);
    public void DrawCards(int amount) => _battle.DrawCards(amount);
    public void ModifyPlayerAttribute(ModifiableAttribute attr, ModifyMethod method, int amount)
        => _battle.ModifyPlayerAttribute(attr, method, amount);
    public void ApplyStatusToEnemy(int enemyIndex, StatusType status, int stacks)
        => _battle.ApplyStatusToEnemy(enemyIndex, status, stacks);
    public void ApplyStatusToPlayer(StatusType status, int stacks)
        => _battle.ApplyStatusToPlayer(status, stacks);
    public void RemoveStatusFromEnemy(int enemyIndex, StatusType status, int stacks)
        => _battle.RemoveStatusFromEnemy(enemyIndex, status, stacks);
    public void RemoveStatusFromPlayer(StatusType status, int stacks)
        => _battle.RemoveStatusFromPlayer(status, stacks);

    public bool IsCurrentFusedAttackGuaranteedCritical(int singleHitDamage)
        => _battle.IsCurrentFusedAttackGuaranteedCritical(singleHitDamage);

    public void AddEnemyArmor(int slotIndex, int amount) => _battle.AddEnemyArmor(slotIndex, amount);
    public bool ApplyEnemyAttributeBuff(int slotIndex, LightMiniGame.CardEditor.PlayerAttributeType attr, int delta) => _battle.ApplyEnemyAttributeBuff(slotIndex, attr, delta);
    public void DealDamageToPlayer(int amount, int sourceEnemySlot) => _battle.DealDamageToPlayer(amount, sourceEnemySlot);

    /// <summary>若指定敌人被融合覆盖了意图伤害则返回 true 并输出覆盖值（供效果执行器实际结算用）。</summary>
    public bool TryGetEnemyIntentOverride(int slot, out int value)
        => _battle.TryGetEnemyIntentOverride(slot, out value);

    // === 自定义数据存取 ===
    public int GetCustomData(string key) => _battle.GetCustomData(key);
    public void SetCustomData(string key, int value) => _battle.SetCustomData(key, value);
    public void ModifyCustomData(string key, int delta) => _battle.ModifyCustomData(key, delta);
    public void ModifySanity(int delta) => _battle.ModifySanity(delta);
    public void ModifyFortune(int delta) => _battle.ModifyFortune(delta);
    public void SetPlayerFortune(int value) => _battle.SetPlayerFortune(value);

    // === 融合覆盖读取（供 EffectExecutor 在打出卡时覆盖数值）===

    /// <summary>若当前手牌融合覆盖了攻击值则返回 true 并输出覆盖值。</summary>
    public bool TryGetFusionAttack(out int value)
    {
        var f = _battle.CurrentFusionCard?.fusion;
        if (f != null && f.overrideAttack) { value = f.attackValue; return true; }
        value = 0; return false;
    }

    /// <summary>若当前手牌融合覆盖了护甲值则返回 true。</summary>
    public bool TryGetFusionArmor(out int value)
    {
        var f = _battle.CurrentFusionCard?.fusion;
        if (f != null && f.overrideArmor) { value = f.armorValue; return true; }
        value = 0; return false;
    }

    /// <summary>若当前手牌融合覆盖了抽牌数则返回 true。</summary>
    public bool TryGetFusionDraw(out int value)
    {
        var f = _battle.CurrentFusionCard?.fusion;
        if (f != null && f.overrideDraw) { value = f.drawCount; return true; }
        value = 0; return false;
    }

    /// <summary>若当前手牌融合覆盖了回费数则返回 true。</summary>
    public bool TryGetFusionRestore(out int value)
    {
        var f = _battle.CurrentFusionCard?.fusion;
        if (f != null && f.overrideRestore) { value = f.restoreAP; return true; }
        value = 0; return false;
    }

    /// <summary>若当前手牌融合覆盖了增益值则返回 true。</summary>
    public bool TryGetFusionBuff(out int value)
    {
        var f = _battle.CurrentFusionCard?.fusion;
        if (f != null && f.overrideBuff) { value = f.buffValue; return true; }
        value = 0; return false;
    }

    /// <summary>若当前手牌对该效果节点的指定数字槽有融合覆盖则返回 true。</summary>
    public bool TryGetFusionSlot(int nodeIndex, FusionSlotKind kind, out int value)
    {
        var f = _battle.CurrentFusionCard?.fusion;
        if (f != null && f.TryGetSlot(nodeIndex, kind, out value)) return true;
        value = 0; return false;
    }

    /// <summary>当前手牌是否已有该类型的逐槽融合（有则不要再用类型级覆盖套到所有同类节点）。</summary>
    public bool HasFusionSlotKind(FusionSlotKind kind)
    {
        var f = _battle.CurrentFusionCard?.fusion;
        return f != null && f.HasSlotKind(kind);
    }
    public void AddBuff(BuffAttributeType type, int stacks, int duration = 0) => _battle.AddPlayerBuff(type, stacks, duration);

    public bool CurrentAttackKilledEnemy => _battle.CurrentAttackKilledEnemy;
    public CardData LastPlayedCard => _battle.LastPlayedCard ?? _battle.CurrentFusionCard;

    public void AddTurnCounter(string name, int delta) => _battle.AddTurnCounter(name, delta);
    public void AddBattleCounter(string name, int delta) => _battle.AddBattleCounter(name, delta);

    public void AddPendingNextAttackDamageBonus(int amount) => _battle.AddPendingNextAttackDamageBonus(amount);
    public void AddPendingNextAttackCritDamageBonus(int percent) => _battle.AddPendingNextAttackCritDamageBonus(percent);
    public void AddPendingNextAttackGuaranteedCrit(int uses = 1) => _battle.AddPendingNextAttackGuaranteedCrit(uses);
    public void EnableFirstAttackEachTurnGuaranteedCrit() => _battle.EnableFirstAttackEachTurnGuaranteedCrit();
    public int ConsumePendingNextAttackDamageBonus() => _battle.ConsumePendingNextAttackDamageBonus();
    public int ConsumePendingNextAttackCritDamageBonus() => _battle.ConsumePendingNextAttackCritDamageBonus();
    public bool ConsumePendingNextAttackGuaranteedCrit() => _battle.ConsumePendingNextAttackGuaranteedCrit();
    public bool IsFirstAttackThisTurnGuaranteedCrit => _battle.IsFirstAttackThisTurnGuaranteedCrit;

    public int ExhaustRandomHandCards(int count) => _battle.ExhaustRandomHandCards(count);
    public int DiscardRandomHandCards(int count) => _battle.DiscardRandomHandCards(count);
    public int AutoPlayTopDrawPile(int count) => _battle.AutoPlayTopDrawPile(count);

    public bool ReplayCardEffects(CardData card, bool skipTriggers)
    {
        bool prev = _battle.SkipRegisterTriggersOnReplay;
        _battle.SkipRegisterTriggersOnReplay = skipTriggers;
        try { return _battle.ReplayCardEffects(card); }
        finally { _battle.SkipRegisterTriggersOnReplay = prev; }
    }

    // === 事件记录 ===
    public bool HasEventOccurred(string eventName) => _battle.HasEventOccurred(eventName);
    public void RecordEvent(string eventName) => _battle.RecordEvent(eventName);

    // === 手牌操作 ===
    public int RequestSelectCardFromHand(string prompt) => _battle.RequestSelectCardFromHand(prompt);
    public void DiscardHandCard(int index) => _battle.DiscardHandCard(index);

    public int MaxHandCount => _battle.HandLimit;
    public string CurrentPlayedCardId => _battle.CurrentFusionCard?.sourceEntry?.cardId ?? "";
    public int GetEnemyStatusStacks(int index, StatusType status) => _battle.GetEnemyStatusStacks(index, status);
    public int GetAllEnemiesStatusStacks(StatusType status) => _battle.GetAllEnemiesStatusStacks(status);
    public int AddGeneratedCards(CardEntry entry, int count, CardZoneType zone)
        => _battle.AddGeneratedCards(entry, count, zone);
    public void SetCardStatusValueOverride(string cardId, StatusType status, int value)
        => _battle.SetCardStatusValueOverride(cardId, status, value);
    public bool TryGetCardStatusValueOverride(string cardId, StatusType status, out int value)
        => _battle.TryGetCardStatusValueOverride(cardId, status, out value);
    public int ApplyTemporaryKeyword(EffectNode node, bool add, int count)
        => _battle.ApplyTemporaryKeyword(node, add, count);

    public int SearchCardInDrawPileToHand(string cardName, int count)
        => _battle.SearchCardInDrawPileToHand(cardName, count);

    public int SearchCardInDiscardPileToHand(string cardName, int count)
        => _battle.SearchCardInDiscardPileToHand(cardName, count);

    public int GenerateRandomCardsByKeyword(CardKeyword2 keyword, int count, CardZoneType zone)
        => _battle.GenerateRandomCardsByKeyword(keyword, count, zone);

    public void AddDamageBonusToHostCard(int bonus)
        => _battle.AddDamageBonusToHostCard(bonus);
}
