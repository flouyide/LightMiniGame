using System;
using System.Collections.Generic;
using UnityEngine;
using LightMiniGame.CardEditor;

/// <summary>
/// 新版效果执行器 —— 处理 List<EffectNode>，支持局部变量、条件评估和所有 EffectOperation。
/// 与旧 EffectExecutor 并行存在，逐步替换。
/// </summary>
public class EffectExecutorV2
{
    private readonly BattleCardContext _ctx;
    private readonly TriggerSystem _triggerSystem;

    // 局部变量上下文（每张卡打出时创建，效果间传递数据）
    private Dictionary<string, int> _localVars;

    // 最近一次效果的结果
    private Dictionary<EffectResultType, int> _lastResult;

    // 当前效果的发起者（出牌者）：<0 表示玩家（默认）；>=0 表示对应槽位的敌人在出牌。
    // 决定 “相对目标”（当前角色/效果发起者）和属性缩放（力量/敏捷/暴击）解析到哪一边。
    private int _initiatorEnemySlot = -1;

    // 执行日志
    public List<string> ExecutionLog { get; private set; } = new List<string>();

    public EffectExecutorV2(BattleCardContext ctx, TriggerSystem triggerSystem = null)
    {
        _ctx = ctx;
        _triggerSystem = triggerSystem;
    }

    /// <summary>
    /// 执行效果列表（按顺序）。
    /// </summary>
    public void ExecuteEffectList(List<EffectNode> effects, Dictionary<string, int> externalVars = null)
    {
        if (effects == null) return;
        _localVars = externalVars ?? new Dictionary<string, int>();
        _lastResult = new Dictionary<EffectResultType, int>();

        for (int i = 0; i < effects.Count; i++)
        {
            var node = effects[i];
            if (node == null || !node.enabled) continue;

            Log($"--- 效果[{i + 1}] {node.displayName}: {node.GetDescription()} ---");

            // 条件检查
            if (node.conditions != null && node.conditions.conditions != null && node.conditions.conditions.Count > 0)
            {
                if (!EvaluateConditions(node.conditions))
                {
                    Log("  条件不满足，跳过");
                    continue;
                }
                Log("  条件满足");
            }

            // 执行
            ExecuteNode(node);

            // 保存输出变量
            if (!string.IsNullOrEmpty(node.outputVariableName) && _lastResult != null)
            {
                int resultVal = _lastResult.GetValueOrDefault(EffectResultType.ActualValue, 0);
                _localVars[node.outputVariableName] = resultVal;
                Log($"  → 保存变量 {node.outputVariableName} = {resultVal}");
            }
        }
    }

    /// <summary>供敌人技能调用：设置发起者为指定槽位敌人后再执行效果列表。执行完发起者恢复为玩家。</summary>
    public void ExecuteEffectListAsEnemy(int enemySlot, List<EffectNode> effects, Dictionary<string, int> externalVars = null)
    {
        int prev = _initiatorEnemySlot;
        _initiatorEnemySlot = enemySlot;
        try { ExecuteEffectList(effects, externalVars); }
        finally { _initiatorEnemySlot = prev; }
    }

    private bool IsEnemyInitiator => _initiatorEnemySlot >= 0;

    /// <summary>发起者（出牌者）的力量：玩家出牌读玩家力量，敌人出牌读该敌人力量。</summary>
    private int OwnerStrength
    {
        get { return IsEnemyInitiator ? _ctx.GetEnemyStrength(_initiatorEnemySlot) : _ctx.PlayerStrength; }
    }

    /// <summary>发起者（出牌者）的敏捷：玩家出牌读玩家敏捷，敌人出牌读该敌人敏捷。</summary>
    private int OwnerDexterity
    {
        get { return IsEnemyInitiator ? _ctx.GetEnemyDexterity(_initiatorEnemySlot) : _ctx.PlayerDexterity; }
    }

    /// <summary>判断 target 是否意在“玩家侧”（结算命中玩家）。
/// 玩家出牌：当前角色/所有角色/效果发起者 → 玩家自身（自buff/自盾）。
/// 敌人出牌（敌人视角反转）：除「效果发起者」指敌人自己外，其余命中目标一律落在玩家身上——
/// 敌人打出的“选定敌人/随机敌人/所有敌人/当前角色”这类攻击/施加目标，在敌人视角下都命中玩家。</summary>
    private bool TargetIsPlayerSide(CombatUnitTarget t)
    {
        if (IsEnemyInitiator)
            return t != CombatUnitTarget.EffectSource;   // 敌人视角：只有“效果发起者”是自己，其余命中玩家

        switch (t)
        {
            case CombatUnitTarget.CurrentCharacter:
            case CombatUnitTarget.AllCharacters:
            case CombatUnitTarget.EffectSource:
                return true;
            default:
                return false;
        }
    }

    // ========================================================================
    // 条件评估
    // ========================================================================
    public bool EvaluateConditions(ConditionGroup group)
    {
        if (group == null || group.conditions == null || group.conditions.Count == 0) return true;

        bool result;
        switch (group.logic)
        {
            case ConditionLogic2.All:
                result = true;
                foreach (var c in group.conditions)
                    if (!EvaluateSingle(c)) { result = false; break; }
                break;
            case ConditionLogic2.Any:
                result = false;
                foreach (var c in group.conditions)
                    if (EvaluateSingle(c)) { result = true; break; }
                break;
            case ConditionLogic2.None:
                result = true;
                foreach (var c in group.conditions)
                    if (EvaluateSingle(c)) { result = false; break; }
                break;
            case ConditionLogic2.Not:
                result = group.conditions.Count > 0 && !EvaluateSingle(group.conditions[0]);
                break;
            default:
                result = true;
                break;
        }
        return result;
    }

    private bool EvaluateSingle(ConditionEntry c)
    {
        switch (c.conditionType)
        {
            case ConditionType2.CompareValue:
                int left = EvaluateValue(c.leftValue);
                int right = EvaluateValue(c.rightValue);
                return c.comparison switch
                {
                    ComparisonOperator.Less => left < right,
                    ComparisonOperator.LessOrEqual => left <= right,
                    ComparisonOperator.Equal => left == right,
                    ComparisonOperator.NotEqual => left != right,
                    ComparisonOperator.GreaterOrEqual => left >= right,
                    ComparisonOperator.Greater => left > right,
                    _ => true
                };

            case ConditionType2.HasStatus:
                // 暂时通过资源值近似判断（正式实现需要状态系统）
                return false;

            case ConditionType2.RuntimeFlagCheck:
                return c.flagRef switch
                {
                    CombatFlagType.IsLowSanity => _ctx.PlayerSanity <= 4,
                    CombatFlagType.TookDamageThisTurn => _ctx.GetTurnCounter("DamageTaken") > 0,
                    CombatFlagType.AttackedThisTurn => _ctx.GetTurnCounter("AttacksPerformed") > 0,
                    CombatFlagType.PlayedCardThisTurn => _ctx.GetTurnCounter("CardsPlayed") > 0,
                    CombatFlagType.SwitchedCharacterThisTurn => _ctx.GetTurnCounter("CharactersSwitched") > 0,
                    CombatFlagType.IsOverheated => _ctx.GetCustomData("Heat") >= 25,
                    _ => false
                };

            case ConditionType2.EventContextCheck:
                return _ctx.HasEventOccurred(c.eventName);

            case ConditionType2.TargetExists:
                return _ctx.EnemyCount > 0;

            case ConditionType2.ChanceCheck:
                return UnityEngine.Random.value < (c.chancePercent / 100f);

            case ConditionType2.CustomCondition:
                return c.customConditionScript != null && c.customConditionScript.Evaluate(_ctx, "");

            default:
                return true;
        }
    }

    // ========================================================================
    // 数值表达式求值
    // ========================================================================
    private int EvaluateValue(ValueNode node)
    {
        if (node == null) return 0;

        switch (node.nodeType)
        {
            // 常量
            case ValueNodeType.IntegerConstant: return node.intValue;
            case ValueNodeType.FloatConstant: return Mathf.RoundToInt(node.floatValue);

            // 读取属性
            case ValueNodeType.ReadAttribute:
                return node.attributeRef switch
                {
                    LightMiniGame.CardEditor.PlayerAttributeType.Strength => OwnerStrength,
                    LightMiniGame.CardEditor.PlayerAttributeType.Dexterity => OwnerDexterity,
                    LightMiniGame.CardEditor.PlayerAttributeType.MaxHealth => _ctx.PlayerMaxHP,
                    LightMiniGame.CardEditor.PlayerAttributeType.CriticalChance => Mathf.RoundToInt(_ctx.PlayerCritRate * 100),
                    LightMiniGame.CardEditor.PlayerAttributeType.CriticalDamageMultiplier => Mathf.RoundToInt(_ctx.PlayerCritDamage * 100),
                    LightMiniGame.CardEditor.PlayerAttributeType.TotalDamageMultiplier => _ctx.GetCustomData("PlayerDamageMultiplier"),
                    LightMiniGame.CardEditor.PlayerAttributeType.IncomingDamageMultiplier => _ctx.GetCustomData("PlayerDamageTakenMultiplier"),
                    _ => 0
                };

            // 读取资源
            case ValueNodeType.ReadResource:
                return node.resourceRef switch
                {
                    LightMiniGame.CardEditor.PlayerResourceType.CurrentHealth => _ctx.PlayerHP,
                    LightMiniGame.CardEditor.PlayerResourceType.Sanity => _ctx.PlayerSanity,
                    LightMiniGame.CardEditor.PlayerResourceType.ActionPoints => _ctx.PlayerEnergy,
                    LightMiniGame.CardEditor.PlayerResourceType.Heat => _ctx.GetCustomData("Heat"),
                    LightMiniGame.CardEditor.PlayerResourceType.Block => _ctx.PlayerArmor,
                    LightMiniGame.CardEditor.PlayerResourceType.Currency => _ctx.GetCustomData("Currency"),
                    LightMiniGame.CardEditor.PlayerResourceType.Fortune => _ctx.PlayerFortune,
                    _ => 0
                };

            // 读取已损失资源
            case ValueNodeType.ReadResourceLostAmount:
                return node.resourceRef switch
                {
                    LightMiniGame.CardEditor.PlayerResourceType.CurrentHealth => _ctx.PlayerMaxHP - _ctx.PlayerHP,
                    LightMiniGame.CardEditor.PlayerResourceType.Sanity => _ctx.GetBattleCounter("TotalSanityLost"),
                    _ => 0
                };

            // 读取状态层数
            case ValueNodeType.ReadStatusStacks:
                // 需要 BattleManager 状态系统支持，暂返回 0
                return 0;

            // 读取计数器
            case ValueNodeType.ReadCounter:
                return node.counterRef switch
                {
                    CombatCounterType.CardsPlayedThisTurn => _ctx.GetTurnCounter("CardsPlayed"),
                    CombatCounterType.AttackCardsPlayedThisTurn => _ctx.GetTurnCounter("AttackCardsPlayed"),
                    CombatCounterType.AttacksPerformedThisTurn => _ctx.GetTurnCounter("AttacksPerformed"),
                    CombatCounterType.HitsPerformedThisTurn => _ctx.GetTurnCounter("HitsPerformed"),
                    CombatCounterType.CriticalHitsThisTurn => _ctx.GetTurnCounter("CriticalHits"),
                    CombatCounterType.DamageTakenThisTurn => _ctx.GetTurnCounter("DamageTaken"),
                    CombatCounterType.DamageDealtThisTurn => _ctx.GetTurnCounter("DamageDealt"),
                    CombatCounterType.SanityLostThisTurn => _ctx.GetTurnCounter("SanityLost"),
                    CombatCounterType.SanityLostThisCombat => _ctx.GetBattleCounter("TotalSanityLost"),
                    CombatCounterType.HeatGainedThisTurn => _ctx.GetTurnCounter("HeatGained"),
                    CombatCounterType.HeatLostThisTurn => _ctx.GetTurnCounter("HeatLost"),
                    CombatCounterType.CharactersSwitchedThisTurn => _ctx.GetTurnCounter("CharactersSwitched"),
                    CombatCounterType.EnemiesKilledThisTurn => _ctx.GetTurnCounter("EnemiesKilled"),
                    CombatCounterType.BlockGainedThisTurn => _ctx.GetTurnCounter("BlockGained"),
                    CombatCounterType.CardsDrawnThisTurn => _ctx.GetTurnCounter("CardsDrawn"),
                    CombatCounterType.CardsDiscardedThisTurn => _ctx.GetTurnCounter("CardsDiscarded"),
                    CombatCounterType.CardsExhaustedThisTurn => _ctx.GetTurnCounter("CardsExhausted"),
                    _ => 0
                };

            // 读取运行时标志
            case ValueNodeType.ReadRuntimeFlag:
                return node.flagRef switch
                {
                    CombatFlagType.IsLowSanity => _ctx.PlayerSanity <= 4 ? 1 : 0,
                    CombatFlagType.TookDamageThisTurn => _ctx.GetTurnCounter("DamageTaken") > 0 ? 1 : 0,
                    CombatFlagType.AttackedThisTurn => _ctx.GetTurnCounter("AttacksPerformed") > 0 ? 1 : 0,
                    CombatFlagType.PlayedCardThisTurn => _ctx.GetTurnCounter("CardsPlayed") > 0 ? 1 : 0,
                    CombatFlagType.IsOverheated => _ctx.GetCustomData("Heat") >= 25 ? 1 : 0,
                    _ => 0
                };

            // 读取卡牌费用
            case ValueNodeType.ReadCardCost:
                return _ctx.GetCustomData("CurrentCardCost");
            case ValueNodeType.ReadActualPaidCost:
                return _ctx.GetCustomData("ActualPaidCost");

            // 读取牌堆信息
            case ValueNodeType.ReadHandCount: return _ctx.HandCount;
            case ValueNodeType.ReadDrawPileCount: return _ctx.DrawPileCount;
            case ValueNodeType.ReadDiscardPileCount: return _ctx.DiscardPileCount;
            case ValueNodeType.ReadEnemyCount: return _ctx.EnemyCount;

            // 读取局部变量
            case ValueNodeType.ReadLocalVariable:
                return _localVars != null && _localVars.TryGetValue(node.variableName, out var lv) ? lv : 0;

            // 读取上次效果结果
            case ValueNodeType.ReadLastEffectResult:
                return _lastResult != null && _lastResult.TryGetValue(node.resultRef, out var lr) ? lr : 0;

            // === 运算 ===
            case ValueNodeType.Add:
                return EvaluateChild(node, 0) + EvaluateChild(node, 1);
            case ValueNodeType.Subtract:
                return EvaluateChild(node, 0) - EvaluateChild(node, 1);
            case ValueNodeType.Multiply:
                return EvaluateChild(node, 0) * EvaluateChild(node, 1);
            case ValueNodeType.Divide:
                int divisor = EvaluateChild(node, 1);
                return divisor != 0 ? EvaluateChild(node, 0) / divisor : 0;
            case ValueNodeType.Floor:
                return Mathf.FloorToInt(EvaluateChild(node, 0));
            case ValueNodeType.Ceil:
                return Mathf.CeilToInt(EvaluateChild(node, 0));
            case ValueNodeType.Round:
                return Mathf.RoundToInt(EvaluateChild(node, 0));
            case ValueNodeType.Min:
                return Mathf.Min(EvaluateChild(node, 0), EvaluateChild(node, 1));
            case ValueNodeType.Max:
                return Mathf.Max(EvaluateChild(node, 0), EvaluateChild(node, 1));
            case ValueNodeType.Clamp:
                return Mathf.Clamp(EvaluateChild(node, 0), EvaluateChild(node, 1), EvaluateChild(node, 2));
            case ValueNodeType.Absolute:
                return Mathf.Abs(EvaluateChild(node, 0));
            case ValueNodeType.Negate:
                return -EvaluateChild(node, 0);
            case ValueNodeType.Percentage:
                return Mathf.RoundToInt(EvaluateChild(node, 0) * EvaluateChild(node, 1) / 100f);
            case ValueNodeType.EveryNConvertToM:
                int source = EvaluateChild(node, 0);
                return (source / node.everyN) * node.convertToM;

            default:
                return 0;
        }
    }

    private int EvaluateChild(ValueNode node, int index)
    {
        if (node?.operands == null || index >= node.operands.Count || node.operands[index] == null)
            return 0;
        return EvaluateValue(node.operands[index]);
    }

    // ========================================================================
    // 单个效果节点执行
    // ========================================================================
    private void ExecuteNode(EffectNode node)
    {
        _lastResult.Clear();

        switch (node.operation)
        {
            case EffectOperation.DealDamage:
                ExecuteDamage(node);
                break;
            case EffectOperation.GainBlock:
                ExecuteGainBlock(node);
                break;
            case EffectOperation.ModifyAttribute:
                ExecuteModifyAttribute(node);
                break;
            case EffectOperation.ModifyResource:
                ExecuteModifyResource(node);
                break;
            case EffectOperation.ApplyStatus:
                ExecuteApplyStatus(node);
                break;
            case EffectOperation.RemoveStatus:
                ExecuteRemoveStatus(node);
                break;
            case EffectOperation.DrawCards:
                ExecuteDrawCards(node);
                break;
            case EffectOperation.RestoreActionPoints:
                ExecuteRestoreAP(node);
                break;
            case EffectOperation.MoveCards:
                ExecuteMoveCards(node);
                break;
            case EffectOperation.SwitchCharacter:
                Log("  [切换角色] 需要外部系统处理");
                break;
            case EffectOperation.RegisterTrigger:
                ExecuteRegisterTrigger(node);
                break;
            case EffectOperation.SetVariable:
                int val = EvaluateValue(node.value);
                _localVars[node.outputVariableName] = val;
                _lastResult[EffectResultType.ActualValue] = val;
                Log($"  [设置变量] {node.outputVariableName} = {val}");
                break;
            case EffectOperation.ModifyVariable:
                int current = _localVars.GetValueOrDefault(node.outputVariableName, 0);
                int mod = EvaluateValue(node.value);
                int newVal = node.resourceOp switch
                {
                    ResourceOperation.Add => current + mod,
                    ResourceOperation.Subtract => current - mod,
                    ResourceOperation.Multiply => current * mod,
                    ResourceOperation.Set => mod,
                    _ => current
                };
                _localVars[node.outputVariableName] = newVal;
                _lastResult[EffectResultType.ActualValue] = newVal;
                Log($"  [修改变量] {node.outputVariableName}: {current} → {newVal}");
                break;
            case EffectOperation.CustomOperation:
                if (node.customOperation != null)
                {
                    node.customOperation.Execute(_ctx, node.customParams);
                    Log($"  [自定义操作] {node.customOperation.GetDisplayName()}");
                }
                else
                    Log("  [自定义操作] 未绑定脚本!");
                break;
            default:
                Log($"  [未实现] {node.operation}");
                break;
        }
    }

    // ========================================================================
    // 伤害
    // ========================================================================
    private void ExecuteDamage(EffectNode node)
    {
        int baseDamage = EvaluateValue(node.value);
        // 融合覆盖：手牌攻击值被融合修改时，完全替换基础伤害（力量加成仍按后续叠加）
        if (_ctx.TryGetFusionAttack(out int fusionAtk))
            baseDamage = fusionAtk;
        // 融合覆盖：敌人意图伤害已含该敌人力量，直接替换、不再叠加
        if (IsEnemyInitiator && _ctx.TryGetEnemyIntentOverride(_initiatorEnemySlot, out int fusionIntent))
            baseDamage = fusionIntent;
        else if ((node.scalingMode == ScalingMode.AddStrength || IsEnemyInitiator)
            && !ValueNode.ReadsAttribute(node.value, LightMiniGame.CardEditor.PlayerAttributeType.Strength))
            baseDamage += OwnerStrength;

        int hitCount = Mathf.Max(1, EvaluateValue(node.repeatCount));
        int totalDamage = 0;
        int critCount = 0;
        bool anyCrit = false;

        // 敌人出牌：命中玩家侧目标时对玩家结算，且敌人不参与暴击
        bool toPlayer = IsEnemyInitiator && TargetIsPlayerSide(node.target.unitTarget);

        for (int hit = 0; hit < hitCount; hit++)
        {
            bool isCrit;
            int hitDamage;
            if (toPlayer)
            {
                isCrit = false;
                hitDamage = baseDamage;
                // 敌人对玩家的破甲语义：沿用敌人伤害，此处不再单独追加
                _ctx.DealDamageToPlayer(hitDamage, _initiatorEnemySlot);
            }
            else
            {
                // 烧水壶等遗物可为“融合攻击值已覆盖且单次伤害达到阈值”的攻击牌强制暴击。
                // 规则优先级高于卡牌的普通随机判定；未命中规则时保留节点自身 Guaranteed / Disabled 语义。
                bool forcedCritical = !IsEnemyInitiator &&
                    _ctx.IsCurrentFusedAttackGuaranteedCritical(baseDamage);
                isCrit = forcedCritical || (node.criticalCheckMode switch
                {
                    CriticalCheckMode.PerHit => UnityEngine.Random.value < _ctx.PlayerCritRate,
                    CriticalCheckMode.PerAttack => hit == 0 && UnityEngine.Random.value < _ctx.PlayerCritRate,
                    CriticalCheckMode.Guaranteed => true,
                    CriticalCheckMode.Disabled => false,
                    _ => false
                });
                hitDamage = isCrit ? Mathf.RoundToInt(baseDamage * _ctx.PlayerCritDamage) : baseDamage;

                // 破甲
                int armorBreak = 0;
                if (node.useArmorBreak)
                    armorBreak = EvaluateValue(node.armorBreakValue);

                // 目标选择
                if (node.target.unitTarget == CombatUnitTarget.AllEnemies)
                {
                    _ctx.DealDamageToAllEnemies(hitDamage, node.ignoreAllBlock, isCrit);
                }
                else
                {
                    int targetIdx = node.target.unitTarget switch
                    {
                        CombatUnitTarget.SelectedEnemy => _ctx.SelectedEnemyIndex,
                        CombatUnitTarget.RandomEnemy => GetRandomAliveEnemyIndex(),
                        CombatUnitTarget.LowestHPEnemy => GetLowestHPEnemyIndex(),
                        _ => _ctx.SelectedEnemyIndex
                    };
                    if (targetIdx >= 0)
                    {
                        _ctx.DealDamageToEnemy(targetIdx, hitDamage, node.ignoreAllBlock, isCrit);
                        if (armorBreak > 0)
                            _ctx.ApplyStatusToEnemy(targetIdx, StatusType.ArmorBreak, armorBreak);
                    }
                }
            }

            totalDamage += hitDamage;
            if (isCrit) { critCount++; anyCrit = true; }

            // 触发攻击事件
            if (isCrit)
            {
                _ctx.RecordEvent("OnCrit");
                _triggerSystem?.FireEvent(TriggerEvent.OnCriticalHit);
            }
            _triggerSystem?.FireEvent(TriggerEvent.OnHit);

            Log($"  第{hit + 1}击: {hitDamage}{(isCrit ? " 暴击" : "")}");
        }

        _triggerSystem?.FireEvent(TriggerEvent.AfterAttack);

        _lastResult[EffectResultType.ActualDamage] = totalDamage;
        _lastResult[EffectResultType.CriticalHitCount] = critCount;
        _lastResult[EffectResultType.AnyCriticalHit] = anyCrit ? 1 : 0;
        _lastResult[EffectResultType.ActualValue] = totalDamage;
    }

    // ========================================================================
    // 格挡
    // ========================================================================
    private void ExecuteGainBlock(EffectNode node)
    {
        int block = EvaluateValue(node.value);
        // 融合覆盖：护甲值被融合修改时替换基础格挡
        if (_ctx.TryGetFusionArmor(out int fusionArmor))
            block = fusionArmor;
        else if (node.scalingMode == ScalingMode.AddStrength
            && !ValueNode.ReadsAttribute(node.value, LightMiniGame.CardEditor.PlayerAttributeType.Dexterity))
            block += OwnerDexterity;

        // 敌人出牌：格挡一律加给敌人自己（敌人防御技能=自护盾，无“给玩家加盾”的敌人卡）
        if (IsEnemyInitiator)
        {
            _ctx.AddEnemyArmor(_initiatorEnemySlot, block);
            Log($"  [格挡] {_initiatorEnemySlot}号敌人 +{block}");
        }
        else
        {
            _ctx.AddPlayerArmor(block);
            _lastResult[EffectResultType.ActualBlockGained] = block;
            Log($"  [格挡] 玩家 +{block}");
        }
        _lastResult[EffectResultType.ActualValue] = block;
        _triggerSystem?.FireEvent(TriggerEvent.OnBlockGained);
    }

    // ========================================================================
    // 修改属性
    // ========================================================================
    private void ExecuteModifyAttribute(EffectNode node)
    {
        int amount = EvaluateValue(node.value);
        var attr = node.attributeType;
        var op = node.resourceOp;

        // 融合覆盖：增益值被融合修改时替换基础值（与伤害/格挡/抽牌/回费一致）
        if (_ctx.TryGetFusionBuff(out int fusionBuff))
            amount = fusionBuff;

        // 敌人作为发起者且目标为「自己（效果发起者）」时 → 敌人自buff，走带能力检测的敌人增益
        if (IsEnemyInitiator && node.target.unitTarget == CombatUnitTarget.EffectSource)
        {
            if (op == ResourceOperation.Add || op == ResourceOperation.Subtract)
            {
                int delta = op == ResourceOperation.Subtract ? -amount : amount;
                bool ok = _ctx.ApplyEnemyAttributeBuff(_initiatorEnemySlot, attr, delta);
                _lastResult[EffectResultType.ActualValue] = delta;
                Log(ok
                    ? $"  [敌人自buff] {ValueNode.GetAttrName(attr)} {delta:+0;-0;0} → {_initiatorEnemySlot}号敌人"
                    : $"  [敌人自buff] {ValueNode.GetAttrName(attr)} 敌人不支持，忽略");
            }
            else
            {
                Log($"  [敌人自buff] 敌人仅支持 Add/Subtract，操作 {op} 忽略");
                _lastResult[EffectResultType.ActualValue] = 0;
            }
            return;
        }

        // Buff 属性路由到 BuffSystem
        if (op == ResourceOperation.Add || op == ResourceOperation.Subtract)
        {
            int delta = op == ResourceOperation.Subtract ? -amount : amount;
            switch (attr)
            {
                case LightMiniGame.CardEditor.PlayerAttributeType.Strength:
                    _ctx.AddBuff(BuffAttributeType.Strength, delta);
                    _lastResult[EffectResultType.ActualValue] = delta;
                    Log($"  [Buff] 力量 {delta}");
                    return;
                case LightMiniGame.CardEditor.PlayerAttributeType.Dexterity:
                    _ctx.AddBuff(BuffAttributeType.Dexterity, delta);
                    _lastResult[EffectResultType.ActualValue] = delta;
                    Log($"  [Buff] 敏捷 {delta}");
                    return;
                case LightMiniGame.CardEditor.PlayerAttributeType.CriticalChance:
                    _ctx.AddBuff(BuffAttributeType.CriticalChance, delta);
                    _lastResult[EffectResultType.ActualValue] = delta;
                    Log($"  [Buff] 暴击率 {delta}");
                    return;
                case LightMiniGame.CardEditor.PlayerAttributeType.CriticalDamageMultiplier:
                    _ctx.AddBuff(BuffAttributeType.CriticalDamage, delta);
                    _lastResult[EffectResultType.ActualValue] = delta;
                    Log($"  [Buff] 暴伤 {delta}");
                    return;
            }
        }

        // 非属性 buff 走旧路径
        _ctx.ModifyPlayerAttribute(MapAttr(attr), MapMethod(op), amount);
        _lastResult[EffectResultType.ActualValue] = amount;
        Log($"  [修改属性] {ValueNode.GetAttrName(attr)} {op} {amount}");
    }

    private ModifiableAttribute MapAttr(LightMiniGame.CardEditor.PlayerAttributeType a) => a switch
    {
        LightMiniGame.CardEditor.PlayerAttributeType.Strength => ModifiableAttribute.Strength,
        LightMiniGame.CardEditor.PlayerAttributeType.Dexterity => ModifiableAttribute.Dexterity,
        LightMiniGame.CardEditor.PlayerAttributeType.CriticalChance => ModifiableAttribute.PlayerCritRate,
        LightMiniGame.CardEditor.PlayerAttributeType.CriticalDamageMultiplier => ModifiableAttribute.PlayerCritDamage,
        LightMiniGame.CardEditor.PlayerAttributeType.MaxHealth => ModifiableAttribute.MaxHP,
        LightMiniGame.CardEditor.PlayerAttributeType.TotalDamageMultiplier => ModifiableAttribute.PlayerDamageMultiplier,
        LightMiniGame.CardEditor.PlayerAttributeType.IncomingDamageMultiplier => ModifiableAttribute.PlayerDamageTakenMultiplier,
        LightMiniGame.CardEditor.PlayerAttributeType.ActionPointsPerTurn => ModifiableAttribute.EnergyPerTurn,
        LightMiniGame.CardEditor.PlayerAttributeType.CardsDrawnPerTurn => ModifiableAttribute.DrawPerTurn,
        _ => ModifiableAttribute.Strength
    };

    private ModifyMethod MapMethod(ResourceOperation op) => op switch
    {
        ResourceOperation.Add => ModifyMethod.Add,
        ResourceOperation.Subtract => ModifyMethod.Subtract,
        ResourceOperation.Multiply => ModifyMethod.Multiply,
        ResourceOperation.Set => ModifyMethod.Override,
        _ => ModifyMethod.Add
    };

    // ========================================================================
    // 修改资源
    // ========================================================================
    private void ExecuteModifyResource(EffectNode node)
    {
        int amount = EvaluateValue(node.value);

        switch (node.resourceType)
        {
            case LightMiniGame.CardEditor.PlayerResourceType.Heat:
                if (node.resourceOp == ResourceOperation.Add || node.resourceOp == ResourceOperation.Subtract)
                {
                    int delta = node.resourceOp == ResourceOperation.Subtract ? -amount : amount;
                    int before = _ctx.GetCustomData("Heat");
                    _ctx.ModifyCustomData("Heat", delta);
                    int after = _ctx.GetCustomData("Heat");
                    int actual = after - before;
                    _lastResult[EffectResultType.ActualHeatReduced] = actual < 0 ? -actual : 0;
                    _lastResult[EffectResultType.ActualValue] = actual;
                    _triggerSystem?.FireEvent(TriggerEvent.OnHeatChanged);
                    Log($"  [热度] {before} → {after} (变化{actual})");
                }
                else if (node.resourceOp == ResourceOperation.Set)
                {
                    _ctx.SetCustomData("Heat", amount);
                    _lastResult[EffectResultType.ActualValue] = amount;
                    Log($"  [热度] 设置为 {amount}");
                }
                break;

            case LightMiniGame.CardEditor.PlayerResourceType.Sanity:
                if (node.resourceOp == ResourceOperation.Add || node.resourceOp == ResourceOperation.Subtract)
                {
                    int delta = node.resourceOp == ResourceOperation.Subtract ? -amount : amount;
                    _ctx.ModifySanity(delta);
                    _lastResult[EffectResultType.ActualSanityLost] = delta < 0 ? -delta : 0;
                    _lastResult[EffectResultType.ActualValue] = delta;
                    _triggerSystem?.FireEvent(TriggerEvent.OnSanityChanged);
                    Log($"  [理智] 变化 {delta}");
                }
                break;

            case LightMiniGame.CardEditor.PlayerResourceType.ActionPoints:
                if (node.resourceOp == ResourceOperation.Add)
                    _ctx.AddPlayerEnergy(amount);
                else if (node.resourceOp == ResourceOperation.Subtract)
                    _ctx.AddPlayerEnergy(-amount);
                _lastResult[EffectResultType.ActualValue] = amount;
                Log($"  [行动点] {node.resourceOp} {amount}");
                break;

            case LightMiniGame.CardEditor.PlayerResourceType.CurrentHealth:
                if (node.resourceOp == ResourceOperation.Add)
                    _ctx.HealPlayer(amount);
                _lastResult[EffectResultType.ActualValue] = amount;
                Log($"  [生命] {node.resourceOp} {amount}");
                break;

            case LightMiniGame.CardEditor.PlayerResourceType.Block:
                if (node.resourceOp == ResourceOperation.Add)
                    _ctx.AddPlayerArmor(amount);
                _lastResult[EffectResultType.ActualValue] = amount;
                Log($"  [格挡] +{amount}");
                break;

            case LightMiniGame.CardEditor.PlayerResourceType.Fortune:
                if (node.resourceOp == ResourceOperation.Add || node.resourceOp == ResourceOperation.Subtract)
                {
                    int delta = node.resourceOp == ResourceOperation.Subtract ? -amount : amount;
                    _ctx.ModifyFortune(delta);
                    _lastResult[EffectResultType.ActualValue] = delta;
                    Log($"  [福报] 变化 {delta}");
                }
                else if (node.resourceOp == ResourceOperation.Set)
                {
                    _ctx.SetPlayerFortune(amount);
                    _lastResult[EffectResultType.ActualValue] = amount;
                    Log($"  [福报] 设置为 {amount}");
                }
                break;

            default:
                Log($"  [修改资源] {node.resourceType} {node.resourceOp} {amount} (需扩展)");
                break;
        }
    }

    // ========================================================================
    // 施加状态
    // ========================================================================
    private void ExecuteApplyStatus(EffectNode node)
    {
        int stacks = EvaluateValue(node.statusValue);

        // 玩家侧目标：玩家出牌时当前角色/效果发起者指玩家；敌人出牌时当前角色指玩家。
        // 敌人出牌时 EffectSource（对敌人自己施加）走敌人分支。
        bool toPlayer = TargetIsPlayerSide(node.target.unitTarget);
        if (toPlayer)
        {
            _ctx.ApplyStatusToPlayer(MapStatus(node.statusType), stacks);
            Log($"  [施加状态] {ValueNode.GetStatusName(node.statusType)} {stacks}层 → 玩家");
        }
        else if (IsEnemyInitiator && node.target.unitTarget == CombatUnitTarget.EffectSource)
        {
            _ctx.ApplyStatusToEnemy(_initiatorEnemySlot, MapStatus(node.statusType), stacks);
            Log($"  [施加状态] {ValueNode.GetStatusName(node.statusType)} {stacks}层 → {_initiatorEnemySlot}号敌人");
        }
        else
        {
            // AllEnemies 传 -1（全体存活敌人），由 BattleManager 展开；其余按槽位索引
            if (node.target.unitTarget == CombatUnitTarget.AllEnemies)
                _ctx.ApplyStatusToEnemy(-1, MapStatus(node.statusType), stacks);
            else
                _ctx.ApplyStatusToEnemy(_ctx.SelectedEnemyIndex, MapStatus(node.statusType), stacks);
            Log($"  [施加状态] {ValueNode.GetStatusName(node.statusType)} {stacks}层 → 敌人");
        }
        _lastResult[EffectResultType.StatusStacksAdded] = stacks;
        _lastResult[EffectResultType.ActualValue] = stacks;
        _triggerSystem?.FireEvent(TriggerEvent.OnStatusApplied);
    }

    // ========================================================================
    // 敌人目标解析（多敌人：槽位索引稳定，死亡不压缩；在存活槽位中选择）
    // ========================================================================

    /// <summary>在存活敌人槽位中随机取一个；无存活敌人返回 -1</summary>
    private int GetRandomAliveEnemyIndex()
    {
        int alive = _ctx.EnemyCount;
        if (alive <= 0) return -1;
        int pick = UnityEngine.Random.Range(0, alive);
        int slots = _ctx.EnemySlotCount;
        for (int i = 0; i < slots; i++)
        {
            if (!_ctx.IsEnemyAlive(i)) continue;
            if (pick-- == 0) return i;
        }
        return -1;
    }

    /// <summary>取存活敌人中 HP 最低者的槽位；无存活敌人返回 -1</summary>
    private int GetLowestHPEnemyIndex()
    {
        int best = -1, bestHP = int.MaxValue;
        int slots = _ctx.EnemySlotCount;
        for (int i = 0; i < slots; i++)
        {
            if (!_ctx.IsEnemyAlive(i)) continue;
            int hp = _ctx.GetEnemyHP(i);
            if (hp < bestHP) { bestHP = hp; best = i; }
        }
        return best;
    }

    private StatusType MapStatus(StatusType2 s) => s switch
    {
        StatusType2.ArmorBreak => StatusType.ArmorBreak,
        StatusType2.Bleed => StatusType.Bleed,
        StatusType2.TemporaryStrength => StatusType.Strength,
        StatusType2.TemporaryDexterity => StatusType.Dexterity,
        StatusType2.CriticalChanceModifier => StatusType.CritRateBoost,
        StatusType2.CriticalDamageModifier => StatusType.CritDamageBoost,
        StatusType2.Jammed => StatusType.Insane,
        StatusType2.Vulnerable => StatusType.Insane,
        StatusType2.Madness => StatusType.Insane,
        _ => StatusType.Bleed
    };

    // ========================================================================
    // 移除状态
    // ========================================================================
    private void ExecuteRemoveStatus(EffectNode node)
    {
        int stacks = EvaluateValue(node.statusValue);

        bool toPlayer = TargetIsPlayerSide(node.target.unitTarget);
        if (toPlayer)
        {
            _ctx.RemoveStatusFromPlayer(MapStatus(node.statusType), stacks);
            Log($"  [移除状态] {ValueNode.GetStatusName(node.statusType)} {stacks}层 → 玩家");
        }
        else if (IsEnemyInitiator && node.target.unitTarget == CombatUnitTarget.EffectSource)
        {
            _ctx.RemoveStatusFromEnemy(_initiatorEnemySlot, MapStatus(node.statusType), stacks);
            Log($"  [移除状态] {ValueNode.GetStatusName(node.statusType)} {stacks}层 → {_initiatorEnemySlot}号敌人");
        }
        else
        {
            if (node.target.unitTarget == CombatUnitTarget.AllEnemies)
                _ctx.RemoveStatusFromEnemy(-1, MapStatus(node.statusType), stacks);
            else
                _ctx.RemoveStatusFromEnemy(_ctx.SelectedEnemyIndex, MapStatus(node.statusType), stacks);
            Log($"  [移除状态] {ValueNode.GetStatusName(node.statusType)} {stacks}层 → 敌人");
        }
        _lastResult[EffectResultType.StatusStacksRemoved] = stacks;
        _lastResult[EffectResultType.ActualValue] = stacks;
    }

    // ========================================================================
    // 抽牌
    // ========================================================================
    private void ExecuteDrawCards(EffectNode node)
    {
        int count = EvaluateValue(node.value);
        // 融合覆盖：抽牌数被融合修改时替换
        if (_ctx.TryGetFusionDraw(out int fusionDraw))
            count = fusionDraw;
        _ctx.DrawCards(count);
        _lastResult[EffectResultType.CardsDrawn] = count;
        _lastResult[EffectResultType.ActualValue] = count;
        Log($"  [抽牌] {count}张");
    }

    // ========================================================================
    // 恢复行动点
    // ========================================================================
    private void ExecuteRestoreAP(EffectNode node)
    {
        int amount = EvaluateValue(node.value);
        // 融合覆盖：回费数被融合修改时替换
        if (_ctx.TryGetFusionRestore(out int fusionRestore))
            amount = fusionRestore;
        _ctx.AddPlayerEnergy(amount);
        _lastResult[EffectResultType.ActualValue] = amount;
        Log($"  [行动点] +{amount}");
    }

    // ========================================================================
    // 卡牌区域操作
    // ========================================================================
    private void ExecuteMoveCards(EffectNode node)
    {
        int count = EvaluateValue(node.zoneCount);
        string opName = node.zoneOperation switch
        {
            CardZoneOperation.Draw => "抽",
            CardZoneOperation.Discard => "弃",
            CardZoneOperation.ExhaustThisCombat => "消耗",
            CardZoneOperation.ShuffleIntoDrawPile => "洗入抽牌堆",
            CardZoneOperation.MoveToHand => "移到手牌",
            CardZoneOperation.AutoPlay => "自动打出",
            _ => node.zoneOperation.ToString()
        };

        switch (node.zoneOperation)
        {
            case CardZoneOperation.Draw:
                _ctx.DrawCards(count);
                _lastResult[EffectResultType.CardsDrawn] = count;
                break;
            default:
                Log($"  [卡牌区域] {opName} {count}张 (需要牌堆系统支持)");
                break;
        }
        _lastResult[EffectResultType.ActualValue] = count;
        Log($"  [卡牌区域] {opName} {count}张: {EffectNode.GetZoneName(node.sourceZone)}→{EffectNode.GetZoneName(node.destinationZone)}");
    }

    // ========================================================================
    // 注册触发器
    // ========================================================================
    private void ExecuteRegisterTrigger(EffectNode node)
    {
        if (_triggerSystem == null)
        {
            Log("  [注册触发器] 触发器系统未初始化");
            return;
        }

        var varSnapshot = new Dictionary<string, int>(_localVars);
        _triggerSystem.RegisterTemporary(
            node.triggerEvent,
            node.conditions,
            node.childEffects,
            node.duration,
            localVarSnapshot: varSnapshot
        );

        Log($"  [注册触发器] {EffectNode.GetTriggerName(node.triggerEvent)} → {node.childEffects.Count}个子效果 ({node.duration.GetDescription()})");
        _lastResult[EffectResultType.ActualValue] = 1;
    }

    // ========================================================================
    // 日志
    // ========================================================================
    private void Log(string msg)
    {
        ExecutionLog.Add(msg);
        Debug.Log($"[EffectExecutorV2] {msg}");
    }

    /// <summary>清除执行日志</summary>
    public void ClearLog() { ExecutionLog.Clear(); }
}
