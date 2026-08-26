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

    /// <summary>当前被破甲削减的护甲总量（用于 RemoveStatus 还原）</summary>
    public int ArmorBreakStacks;

    /// <summary>运行时力量，开战从 EnemyConfig.strength 拷入，战斗内增减不写回资产。</summary>
    public int Strength;
    /// <summary>运行时敏捷，开战从 EnemyConfig.dexterity 拷入，战斗内增减不写回资产。</summary>
    public int Dexterity;

    public int EffectiveStrength => Strength;
    public int EffectiveDexterity => Dexterity;

    /// <summary>当前阶段（1=高理智形态，2=低理智形态）</summary>
    public int Phase = 1;
    /// <summary>低理智牌库是否启用（由 BattleManager 依据玩家理智实时置位；true 时直接用 phase2Skills）</summary>
    public bool UseLowSanityPool;
    /// <summary>技能轮转计数（保留字段：阶段切换时依据；选牌已改为随机抽，不再直接用轮转下标）</summary>
    public int TurnInCycle;
    /// <summary>锁定的角色索引（-1 = 未锁定；光束扫描类技能用）</summary>
    public int LockedCharIdx = -1;
    /// <summary>是否已死亡（HP≤0；阶段切换不重置生命值，血条唯一）</summary>
    public bool IsDead;

    /// <summary>融合回填：意图伤害覆盖值（-1 表示未覆盖，按当前卡牌效果节点计算）</summary>
    public int IntentDamageOverride = -1;

    /// <summary>对应视图，由 BattleManager 生成后注入</summary>
    public EnemyView View;

    public string Name => Config != null ? Config.enemyName : "敌人";
    /// <summary>是否有阶段2（低理智形态）：配置了低理智牌库即视为有阶段2</summary>
    public bool HasPhase2 => Config != null && Config.phase2Skills != null && Config.phase2Skills.Count > 0;

    /// <summary>当前应使用的技能牌库：低理智时用 phase2Skills（一进低理智即切换），否则按阶段。</summary>
    public List<CardEntry> CurrentSkillPool
    {
        get
        {
            if (Config == null) return null;
            if (UseLowSanityPool) return Config.phase2Skills;
            return Phase == 1 ? Config.phase1Skills : Config.phase2Skills;
        }
    }

    /// <summary>
    /// 本回合应打出的卡牌（列表，按当前形态出牌数抽取缓存）。
    /// 每回合开始时调用 ResetDrawnSkill() 清空，使意图预览与实抽一致。
    /// </summary>
    public List<CardEntry> GetCurrentSkills()
    {
        if (_drawnSkills != null) return _drawnSkills;
        _drawnSkills = RollRandomSkills(CardsThisTurn);
        return _drawnSkills;
    }

    /// <summary>本回合主卡（返回已抽取列表的首张；用于单卡意图/伤害预览）。</summary>
    public CardEntry GetCurrentSkill()
    {
        var list = GetCurrentSkills();
        return list != null && list.Count > 0 ? list[0] : null;
    }

    /// <summary>本回合出牌数：低理智用低理智出招数，否则高理智出招数；配置≤0 时默认 1 张。</summary>
    public int CardsThisTurn
    {
        get
        {
            if (Config == null) return 1;
            int c = UseLowSanityPool ? Config.lowSanityCardCount : (Phase == 1 ? Config.highSanityCardCount : Config.lowSanityCardCount);
            if (c <= 0) return CurrentSkillPool != null ? CurrentSkillPool.Count : 1;   // 0=全部轮转：出牌库全部牌
            return c;
        }
    }

    /// <summary>从当前阶段牌库随机抽取 count 张（不重复）并缓存为本回合技能。</summary>
    public List<CardEntry> RollRandomSkills(int count)
    {
        var pool = CurrentSkillPool;
        var result = new List<CardEntry>();
        if (pool == null || pool.Count == 0) return result;
        if (count <= 0) count = 1;

        // 从牌库随机抽（不重复，直到抽够或牌库抽空）
        var remaining = new List<CardEntry>(pool);
        while (result.Count < count && remaining.Count > 0)
        {
            int idx = Random.Range(0, remaining.Count);
            result.Add(remaining[idx]);
            remaining.RemoveAt(idx);
        }
        return result;
    }

    private List<CardEntry> _drawnSkills;

    /// <summary>清空本回合已抽卡牌（玩家回合开始时调用，使下个敌人回合重新随机）。</summary>
    public void ResetDrawnSkill()
    {
        _drawnSkills = null;
    }

    /// <summary>
    /// 检查并执行阶段切换（仅由玩家理智驱动）。返回是否发生了切换（供 BattleManager 刷视图/记日志）。
    /// 阶段1 → 阶段2：玩家理智 < 全局 sanityThreshold；切换时清空护甲。
    /// 阶段2 → 阶段1：玩家理智恢复到阈值以上。
    /// 生命值不变：敌人只有一条血（maxHP），HP/MaxHP 不随阶段切换重置。
    /// HP≤0 不再触发转阶段（由 BattleManager 直接判定死亡）。
    /// 两个方向都会重置技能轮转计数。
    /// </summary>
    public bool CheckPhaseSwitch(int playerSanity, int sanityThreshold)
    {
        if (Config == null) return false;

        bool sanityLow = playerSanity < sanityThreshold;

        // 阶段1→2：理智低于阈值
        if (Phase == 1 && sanityLow)
        {
            Phase = 2;
            TurnInCycle = 0;
            Armor = 0;          // 形态切换清空护甲；HP/MaxHP 保持不变
            ResetDrawnSkill();  // 切换阶段 → 牌库变化，清空以重新随机
            return true;
        }
        // 阶段2→1：理智恢复
        if (Phase == 2 && !sanityLow)
        {
            Phase = 1;
            TurnInCycle = 0;
            ResetDrawnSkill();  // 切换阶段 → 牌库变化，清空以重新随机；HP/MaxHP 保持不变
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
        {
            int actual = Mathf.Min(Armor, stacks);
            Armor -= actual;
            ArmorBreakStacks += actual;
        }
    }

    /// <summary>移除状态（ArmorBreak：还原被削减的护甲，上限为 ArmorBreakStacks）</summary>
    public void RemoveStatus(StatusType status, int stacks)
    {
        if (status == StatusType.ArmorBreak)
        {
            int restore = Mathf.Min(ArmorBreakStacks, stacks);
            Armor += restore;
            ArmorBreakStacks -= restore;
        }
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
