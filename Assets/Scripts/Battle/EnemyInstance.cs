using System.Collections.Generic;
using LightMiniGame.CardEditor;
using UnityEngine;

/// <summary>
/// 战斗内单个敌人的运行时实例（纯数据 + 自身逻辑，非 MonoBehaviour）。
/// 多敌人框架下每个敌人独立维护 阶段/凝视值/技能轮转/锁定角色 状态，互不影响。
/// 职责边界：
/// - 本类：只读写"单个敌人状态"的逻辑（技能轮转、阶段切换、受击结算、自身状态）。
/// - BattleManager：技能对玩家的结算、协程编排、意图文本、战斗结束判定、全局伤害倍率。
/// </summary>
public class EnemyInstance
{
    /// <summary>槽位索引（生成顺序，稳定不变；死亡后槽位保留不压缩）</summary>
    public int SlotIndex;
    public EnemyConfig Config;
    /// <summary>行动顺序值（来自 EnemyConfig.actionPriority）：数值小的先行动；相同值随机先后</summary>
    public int ActionOrder;

    public int HP;
    public int MaxHP;
    public int Armor;

    /// <summary>运行时力量增益（叠加在 Config.strength 之上，战斗内临时）。</summary>
    public int StrengthBuff;
    /// <summary>运行时敏捷增益（叠加在 Config.dexterity 之上，战斗内临时）。</summary>
    public int DexterityBuff;

    /// <summary>有效力量 = 基础力量 + 运行时增益</summary>
    public int EffectiveStrength => (Config != null ? Config.strength : 0) + StrengthBuff;
    /// <summary>有效敏捷 = 基础敏捷 + 运行时增益</summary>
    public int EffectiveDexterity => (Config != null ? Config.dexterity : 0) + DexterityBuff;

    /// <summary>当前阶段（1=高理智形态，2=低理智形态）</summary>
    public int Phase = 1;
    /// <summary>技能轮转计数（保留字段：阶段切换时依据；选牌已改为随机抽，不再直接用轮转下标）</summary>
    public int TurnInCycle;
    /// <summary>本回合已随机抽取的技能卡（意图预览与实抽一致）；每个敌人回合开始时重新抽取。</summary>
    public CardEntry DrawnSkill;
    /// <summary>锁定的角色索引（-1 = 未锁定；光束扫描类技能用）</summary>
    public int LockedCharIdx = -1;
    /// <summary>是否已死亡（HP≤0 且无阶段2，或阶段2被打死）</summary>
    public bool IsDead;

    /// <summary>融合回填：意图伤害覆盖值（-1 表示未覆盖，按当前卡牌效果节点计算）</summary>
    public int IntentDamageOverride = -1;

    /// <summary>对应视图，由 BattleManager 生成后注入</summary>
    public EnemyView View;

    public string Name => Config != null ? Config.enemyName : "敌人";
    /// <summary>是否有阶段2</summary>
    public bool HasPhase2 => Config != null && Config.phase2MaxHP > 0;

    /// <summary>当前阶段对应的技能牌库（阶段1=phase1Skills，阶段2=phase2Skills）。</summary>
    public List<CardEntry> CurrentSkillPool
    {
        get
        {
            if (Config == null) return null;
            return Phase == 1 ? Config.phase1Skills : Config.phase2Skills;
        }
    }

    /// <summary>
    /// 本回合应打出的卡牌：返回已抽取缓存的 DrawnSkill（若尚未抽取则立即随机抽一张并缓存）。
    /// 每回合开始时调用 ResetDrawnSkill() 清空，使意图预览(GetCurrentSkill)与实抽一致。
    /// </summary>
    public CardEntry GetCurrentSkill()
    {
        if (DrawnSkill != null) return DrawnSkill;
        DrawnSkill = RollRandomSkill();
        return DrawnSkill;
    }

    /// <summary>从当前阶段牌库随机抽一张卡牌并缓存为该敌人的本回合技能（不重置轮转计数）。</summary>
    public CardEntry RollRandomSkill()
    {
        var pool = CurrentSkillPool;
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    /// <summary>清空本回合已抽卡牌（玩家回合开始时调用，使下个敌人回合重新随机）。</summary>
    public void ResetDrawnSkill() => DrawnSkill = null;

    /// <summary>
    /// 检查并执行阶段切换。返回是否发生了切换（供 BattleManager 刷视图/记日志）。
    /// 阶段1 → 阶段2：HP≤0（打穿）或玩家理智 < 全局 sanityThreshold；切换时清空护甲。
    /// 阶段2 → 阶段1：玩家理智恢复到阈值以上（需配置 phase2MaxHP）。
    /// 两个方向都会重置技能轮转计数。HP 打穿但无阶段2时不做任何事（死亡由 BattleManager 判定）。
    /// </summary>
    public bool CheckPhaseSwitch(int playerSanity, int sanityThreshold)
    {
        if (Config == null) return false;

        bool sanityLow = playerSanity < sanityThreshold;

        // 阶段1→2：阶段1血量打完 或 理智低于阈值
        if (Phase == 1 && (HP <= 0 || sanityLow))
        {
            Phase = 2;
            TurnInCycle = 0;
            MaxHP = Config.phase2MaxHP > 0 ? Config.phase2MaxHP : MaxHP;
            HP = MaxHP;
            Armor = 0;
            ResetDrawnSkill();   // 切换阶段 → 牌库变化，清空以重新随机
            return true;
        }
        // 阶段2→1：理智恢复且配置了阶段2
        if (Phase == 2 && !sanityLow && Config.phase2MaxHP > 0)
        {
            Phase = 1;
            TurnInCycle = 0;
            MaxHP = Config.maxHP;
            HP = MaxHP;
            ResetDrawnSkill();   // 切换阶段 → 牌库变化，清空以重新随机
            return true;
        }
        return false;
    }

    /// <summary>
    /// 受击结算（伤害倍率已在 BattleManager 算好）。返回实际造成的总伤害。
    /// armorBreak&gt;0 时为"破甲伤害"：额外 X 点直接扣血、无视护甲（沿用原单敌人 DealDamageToEnemy 语义）。
    /// 注意与 ApplyStatus(ArmorBreak) 区分：后者才是削减护甲值。
    /// </summary>
    public int TakeDamage(int damage, bool ignoreArmor, int armorBreak)
    {
        int actualDamage = 0;

        // 破甲：额外X点伤害直接扣血，无视护甲
        if (armorBreak > 0)
        {
            HP = Mathf.Max(0, HP - armorBreak);
            actualDamage += armorBreak;
        }

        // 基础伤害走护甲
        if (!ignoreArmor && Armor > 0 && damage > 0)
        {
            int absorbed = Mathf.Min(Armor, damage);
            Armor -= absorbed;
            damage -= absorbed;
        }

        if (damage > 0)
        {
            HP = Mathf.Max(0, HP - damage);
            actualDamage += damage;
        }

        return actualDamage;
    }

    /// <summary>施加状态（当前仅 ArmorBreak 减甲；敌人暂无流血/其他状态系统）</summary>
    public void ApplyStatus(StatusType status, int stacks)
    {
        if (status == StatusType.ArmorBreak)
            Armor = Mathf.Max(0, Armor - stacks);
    }

    /// <summary>获得护甲（加法累加）。由 BattleManager 在执行 gainBlock 技能时调用。</summary>
    public void AddArmor(int amount)
    {
        if (amount > 0) Armor += amount;
    }

    /// <summary>敌人回合开始时重置护甲为 0（同玩家每回合清护甲）。</summary>
    public void ResetArmorOnTurnStart()
    {
        Armor = 0;
    }
}
