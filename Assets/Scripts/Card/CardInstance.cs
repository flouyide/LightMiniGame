using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightMiniGame.Card
{
    /// <summary>
    /// 玩家拥有的「一张卡」—— 模板引用 + 实例覆盖层 + 唯一实例ID。
    /// 它是可序列化普通类（非 MonoBehaviour / 非 ScriptableObject），
    /// 以便在同一角色牌库里共存多张同模板的不同副本。
    /// </summary>
    [Serializable]
    public class CardInstance
    {
        public string instanceId;        // 唯一实例ID（Guid），增删改定位用
        public CardData template;        // 模板引用（不变；为空时按覆盖层降级，见 Effective*）

        // —— 实例覆盖层：仅当玩家手动改过时才启用，否则回落到模板值 ——
        public CardOverride overrideData = new CardOverride();

        // —— 有效值解析：模板值优先被覆盖层接管 ——
        public string EffectiveName        => Pick(overrideData.hasNameOverride,        overrideData.cardName,        template != null ? template.cardName : "");
        public string EffectiveDescription => Pick(overrideData.hasDescriptionOverride, overrideData.description,     template != null ? template.description : "");
        public int    EffectiveCost         => Pick(overrideData.hasCostOverride,        overrideData.actionPointCost, template != null ? template.actionPointCost : 1);
        public int    EffectiveValue        => Pick(overrideData.hasValueOverride,       overrideData.value,          template != null ? template.value : 0);
        public CardGrade EffectiveGrade      => Pick(overrideData.hasGradeOverride,       overrideData.grade,          template != null ? template.grade : CardGrade.Common);
        public ConsumeType EffectiveConsume  => Pick(overrideData.hasConsumeTypeOverride, overrideData.consumeType,    template != null ? template.consumeType : ConsumeType.None);
        public KeywordType EffectiveKeywords => Pick(overrideData.hasKeywordsOverride,    overrideData.keywords,       template != null ? template.keywords : KeywordType.None);
        public int    EffectiveAttackCount  => Pick(overrideData.hasAttackCountOverride, overrideData.attackCount,    template != null ? template.attackCount : 1);
        public int    EffectiveAttackValue  => Pick(overrideData.hasAttackValueOverride, overrideData.attackValue,    template != null ? template.attackValue : 0);
        public ValueType EffectiveAttackValType => Pick(overrideData.hasAttackValueTypeOverride, overrideData.attackValueType, template != null ? template.attackValueType : ValueType.Fixed);
        public PlayerAttributeType EffectiveAttackAttr => Pick(overrideData.hasAttackAttrOverride, overrideData.attackAttribute, template != null ? template.attackAttribute : PlayerAttributeType.Strength);
        public bool   EffectiveIgnoreArmor => Pick(overrideData.hasIgnoreArmorOverride, overrideData.ignoreArmor, template != null ? template.ignoreArmor : false);
        public int    EffectiveArmorValue   => Pick(overrideData.hasArmorValueOverride,  overrideData.armorValue,     template != null ? template.armorValue : 0);
        public ValueType EffectiveArmorValType => Pick(overrideData.hasArmorValTypeOverride, overrideData.armorValueType, template != null ? template.armorValueType : ValueType.Fixed);
        public PlayerAttributeType EffectiveArmorAttr => Pick(overrideData.hasArmorAttrOverride, overrideData.armorAttribute, template != null ? template.armorAttribute : PlayerAttributeType.Dexterity);
        public BuffDurationType EffectiveBuffDuration => Pick(overrideData.hasBuffDurationOverride, overrideData.buffDuration, template != null ? template.buffDuration : BuffDurationType.BattlePermanent);
        public int    EffectiveBuffTurns   => Pick(overrideData.hasBuffTurnsOverride,    overrideData.buffDurationTurns, template != null ? template.buffDurationTurns : 0);
        public int    EffectiveBuffStacks  => Pick(overrideData.hasBuffStacksOverride,   overrideData.buffStacks,     template != null ? template.buffStacks : 1);
        public List<BuffEffect> EffectiveBuffEffects => overrideData.hasBuffEffectsOverride ? overrideData.buffEffects : (template != null ? template.buffEffects : null);

        /// <summary>模板是否有效（缺失模板时为「孤儿卡」，UI 应提示）</summary>
        public bool HasValidTemplate => template != null;

        public CardInstance(CardData t)
        {
            template   = t;
            instanceId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 生成一份以「当前有效值」为初值的覆盖层（所有 has* = true）。
        /// 供「修改这张卡」流程预填表单：用户改哪个字段就保留哪个覆盖，没动的也是覆盖态但值相等。
        /// </summary>
        public CardOverride SnapshotOverride()
        {
            return new CardOverride
            {
                hasNameOverride = true,        cardName = EffectiveName,
                hasDescriptionOverride = true, description = EffectiveDescription,
                hasCostOverride = true,        actionPointCost = EffectiveCost,
                hasValueOverride = true,       value = EffectiveValue,
                hasGradeOverride = true,       grade = EffectiveGrade,
                hasConsumeTypeOverride = true, consumeType = EffectiveConsume,
                hasKeywordsOverride = true,    keywords = EffectiveKeywords,
                hasAttackCountOverride = true, attackCount = EffectiveAttackCount,
                hasAttackValueOverride = true, attackValue = EffectiveAttackValue,
                hasAttackValueTypeOverride = true, attackValueType = EffectiveAttackValType,
                hasAttackAttrOverride = true, attackAttribute = EffectiveAttackAttr,
                hasIgnoreArmorOverride = true, ignoreArmor = EffectiveIgnoreArmor,
                hasArmorValueOverride = true,  armorValue = EffectiveArmorValue,
                hasArmorValTypeOverride = true, armorValueType = EffectiveArmorValType,
                hasArmorAttrOverride = true,   armorAttribute = EffectiveArmorAttr,
                hasBuffDurationOverride = true, buffDuration = EffectiveBuffDuration,
                hasBuffTurnsOverride = true,   buffDurationTurns = EffectiveBuffTurns,
                hasBuffStacksOverride = true,  buffStacks = EffectiveBuffStacks,
                hasBuffEffectsOverride = true, buffEffects = EffectiveBuffEffects != null ? new List<BuffEffect>(EffectiveBuffEffects) : null,
            };
        }

        private static T Pick<T>(bool hasOverride, T overridden, T templateDefault)
            => hasOverride ? overridden : templateDefault;
    }

    /// <summary>
    /// 卡牌实例的覆盖层：只存「玩家手动改过」的字段，其余回落到模板。
    /// 每个字段配一个 has* 开关，决定该字段是否走覆盖值。
    /// </summary>
    [Serializable]
    public class CardOverride
    {
        public bool hasNameOverride;        public string cardName;
        public bool hasDescriptionOverride; public string description;
        public bool hasCostOverride;        public int  actionPointCost;
        public bool hasValueOverride;       public int  value;
        public bool hasGradeOverride;       public CardGrade grade;
        public bool hasConsumeTypeOverride; public ConsumeType consumeType;
        public bool hasKeywordsOverride;    public KeywordType keywords;
        public bool hasAttackCountOverride; public int  attackCount;
        public bool hasAttackValueOverride; public int  attackValue;
        public bool hasAttackValueTypeOverride; public ValueType attackValueType;
        public bool hasAttackAttrOverride;  public PlayerAttributeType attackAttribute;
        public bool hasIgnoreArmorOverride; public bool ignoreArmor;
        public bool hasArmorValueOverride;  public int  armorValue;
        public bool hasArmorValTypeOverride; public ValueType armorValueType;
        public bool hasArmorAttrOverride;   public PlayerAttributeType armorAttribute;
        public bool hasBuffDurationOverride; public BuffDurationType buffDuration;
        public bool hasBuffTurnsOverride;   public int  buffDurationTurns;
        public bool hasBuffStacksOverride;  public int  buffStacks;
        public bool hasBuffEffectsOverride; public List<BuffEffect> buffEffects;
    }
}
