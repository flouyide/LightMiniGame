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

    /// <summary>
    /// 疲惫层数。开战从 EnemyConfig.fatigue 拷入。
    /// 每轮（敌人回合开始）造成等于当前层数的直接扣血，然后层数 -1。
    /// </summary>
    public int Tiredness;

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

    /// <summary>融合回填：意图伤害覆盖值（-1 表示未覆盖，按当前卡牌效果节点计算）。兼容旧读取；新路径写入 _skillFusions。</summary>
    public int IntentDamageOverride = -1;

    /// <summary>本回合已抽意图卡上的融合覆盖，与 _drawnSkills 下标对齐。</summary>
    private List<FusionCardDelta> _skillFusions;

    /// <summary>对应视图，由 BattleManager 生成后注入</summary>
    public EnemyView View;

    public string Name => Config != null ? Config.enemyName : "敌人";
    /// <summary>是否有阶段2（低理智形态）：配置了低理智牌库即视为有阶段2</summary>
    public bool HasPhase2 => Config != null && Config.phase2Skills != null && Config.phase2Skills.Count > 0;

    /// <summary>
    /// 战斗内由敌人能力临时覆盖的复制技能池。
    /// 不写回 EnemyConfig.phase1Skills/phase2Skills，避免污染共享 ScriptableObject 配置资产。
    /// 非 null 时会完全替换对应状态下的常规出牌池；null 时回退到 EnemyConfig 配置。
    /// </summary>
    private List<CardEntry> _runtimePhase1CopiedSkills;
    private List<CardEntry> _runtimePhase2CopiedSkills;

    /// <summary>
    /// 仅在当前敌人回合生效的强制出牌列表。
    /// 站队天平等能力用它覆盖本回合实际执行与意图展示，但不会改动抄袭专家的复制牌池，
    /// 也不会写入 EnemyConfig 的共享配置资产。
    /// </summary>
    private List<CardEntry> _forcedSkillsThisTurn;

    /// <summary>当前应使用的技能牌库：低理智时用 phase2Skills（一进低理智即切换），否则按阶段。</summary>
    public List<CardEntry> CurrentSkillPool
    {
        get
        {
            if (Config == null) return null;
            if (UseLowSanityPool)
                return _runtimePhase2CopiedSkills ?? Config.phase2Skills;

            return Phase == 1
                ? _runtimePhase1CopiedSkills ?? Config.phase1Skills
                : _runtimePhase2CopiedSkills ?? Config.phase2Skills;
        }
    }

    /// <summary>当前生效牌库是否为战斗内复制牌池。</summary>
    public bool HasActiveRuntimeCopiedSkills
    {
        get
        {
            if (UseLowSanityPool || Phase != 1)
                return _runtimePhase2CopiedSkills != null;
            return _runtimePhase1CopiedSkills != null;
        }
    }

    /// <summary>
    /// 写入指定理智状态的战斗内复制技能池，并失效当前已抽取的意图缓存。
    /// 传 null 表示清除该状态的覆盖，恢复 EnemyConfig 中的常规技能池。
    /// </summary>
    public void SetRuntimeCopiedSkills(bool lowSanity, List<CardEntry> skills)
    {
        if (lowSanity)
            _runtimePhase2CopiedSkills = skills;
        else
            _runtimePhase1CopiedSkills = skills;

        ResetDrawnSkill();
    }

    /// <summary>清除高、低理智两套战斗内复制技能覆盖，并恢复常规技能池。</summary>
    public void ClearRuntimeCopiedSkills()
    {
        _runtimePhase1CopiedSkills = null;
        _runtimePhase2CopiedSkills = null;
        ResetDrawnSkill();
    }

    /// <summary>
    /// 设置当前敌人回合唯一允许执行的技能。传入列表会复制为独立列表，
    /// 其优先级高于抄袭专家复制池与 EnemyConfig 常规牌库。
    /// </summary>
    public void SetForcedSkillsThisTurn(IList<CardEntry> skills)
    {
        _forcedSkillsThisTurn = skills != null ? new List<CardEntry>(skills) : null;
        ResetDrawnSkill();
    }

    /// <summary>清除当前敌人回合的强制技能，恢复抄袭专家覆盖池或 EnemyConfig 常规牌库。</summary>
    public void ClearForcedSkillsThisTurn()
    {
        if (_forcedSkillsThisTurn == null) return;
        _forcedSkillsThisTurn = null;
        ResetDrawnSkill();
    }

    /// <summary>
    /// 本回合应打出的卡牌。若存在本回合强制技能，意图与实际执行均直接使用它；
    /// 否则按当前形态从技能池随机抽取并缓存。
    /// 每回合开始时调用 ResetDrawnSkill() 清空随机缓存，使意图预览与实抽一致。
    /// </summary>
    public List<CardEntry> GetCurrentSkills()
    {
        if (_forcedSkillsThisTurn != null) return _forcedSkillsThisTurn;
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
            if (_forcedSkillsThisTurn != null)
                return _forcedSkillsThisTurn.Count;

            if (HasActiveRuntimeCopiedSkills)
                return CurrentSkillPool != null ? CurrentSkillPool.Count : 0;

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

    /// <summary>取指定意图卡的融合覆盖（无则 null）。</summary>
    public FusionCardDelta GetSkillFusion(int skillIndex)
    {
        if (_skillFusions == null || skillIndex < 0 || skillIndex >= _skillFusions.Count)
            return null;
        return _skillFusions[skillIndex];
    }

    /// <summary>保证指定意图卡有融合覆盖层并返回。</summary>
    public FusionCardDelta EnsureSkillFusion(int skillIndex)
    {
        if (skillIndex < 0) skillIndex = 0;
        if (_skillFusions == null) _skillFusions = new List<FusionCardDelta>();
        while (_skillFusions.Count <= skillIndex)
            _skillFusions.Add(null);
        if (_skillFusions[skillIndex] == null)
            _skillFusions[skillIndex] = new FusionCardDelta();
        return _skillFusions[skillIndex];
    }

    /// <summary>清空本回合已抽卡牌（玩家回合开始时调用，使下个敌人回合重新随机）。</summary>
    public void ResetDrawnSkill()
    {
        _drawnSkills = null;
        _skillFusions = null;
        IntentDamageOverride = -1;
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

    /// <summary>施加状态：破甲减甲；力量/敏捷改运行时属性；疲惫叠加层数。</summary>
    public void ApplyStatus(StatusType status, int stacks)
    {
        if (stacks == 0) return;
        switch (status)
        {
            case StatusType.ArmorBreak:
            {
                int actual = Mathf.Min(Armor, Mathf.Max(0, stacks));
                Armor -= actual;
                ArmorBreakStacks += actual;
                break;
            }
            case StatusType.Strength:
                Strength += stacks;
                break;
            case StatusType.Dexterity:
                Dexterity += stacks;
                break;
            case StatusType.Fatigue:
                Tiredness = Mathf.Max(0, Tiredness + stacks);
                break;
        }
    }

    /// <summary>移除状态（ArmorBreak：还原被削减的护甲；力量/敏捷/疲惫按层数回退）。</summary>
    public void RemoveStatus(StatusType status, int stacks)
    {
        if (stacks <= 0) return;
        switch (status)
        {
            case StatusType.ArmorBreak:
            {
                int restore = Mathf.Min(ArmorBreakStacks, stacks);
                Armor += restore;
                ArmorBreakStacks -= restore;
                break;
            }
            case StatusType.Strength:
                Strength -= stacks;
                break;
            case StatusType.Dexterity:
                Dexterity -= stacks;
                break;
            case StatusType.Fatigue:
                Tiredness = Mathf.Max(0, Tiredness - stacks);
                break;
        }
    }

    /// <summary>
    /// 疲惫结算：直接扣等于当前层数的血（无视护甲），然后层数 -1。
    /// 返回本次扣血量；层数为 0 时不生效。
    /// </summary>
    public int TickFatigue()
    {
        if (IsDead || Tiredness <= 0) return 0;
        int dmg = Tiredness;
        HP = Mathf.Max(0, HP - dmg);
        Tiredness = Mathf.Max(0, Tiredness - 1);
        return dmg;
    }

    /// <summary>敌人 buff 栏：疲惫、破甲、力量，以及 Config.abilities 里的能力（用 RelicData.icon）。0 层不显示。</summary>
    public List<DisplayedBuff> GetDisplayedBuffs()
    {
        var list = new List<DisplayedBuff>();
        if (Tiredness != 0)
            list.Add(new DisplayedBuff { attributeType = BuffAttributeType.Fatigue, totalStacks = Tiredness });
        if (ArmorBreakStacks != 0)
            list.Add(new DisplayedBuff { attributeType = BuffAttributeType.ArmorBreak, totalStacks = ArmorBreakStacks });
        if (Strength != 0)
            list.Add(new DisplayedBuff { attributeType = BuffAttributeType.Strength, totalStacks = Strength });

        var abilities = Config != null ? Config.abilities : null;
        if (abilities != null)
        {
            var seen = new HashSet<LightMiniGame.Shop.RelicData>();
            for (int i = 0; i < abilities.Count; i++)
            {
                var relic = abilities[i] != null ? abilities[i].relic : null;
                if (relic == null || !seen.Add(relic)) continue;
                list.Add(new DisplayedBuff
                {
                    customIcon = relic.icon,
                    hideStacks = true,
                    totalStacks = 0
                });
            }
        }
        return list;
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
