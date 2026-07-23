using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using LightMiniGame.CardEditor;

namespace LightMiniGame.CardEditor.Editor
{
    /// <summary>
    /// 示例卡牌生成器 —— 一次性运行，生成 10 张示例卡牌到 ScriptableObjects/CardEditor/ 目录
    /// </summary>
    public static class GenerateExampleCards
    {
        private const string CARD_DIR = "Assets/ScriptableObjects/CardEditor";
        private const string DB_DIR = "Assets/Resources/CardEditor";

        [MenuItem("Tools/卡牌编辑器/生成示例卡牌")]
        public static void Generate()
        {
            if (!Directory.Exists(CARD_DIR)) Directory.CreateDirectory(CARD_DIR);
            if (!Directory.Exists(DB_DIR)) Directory.CreateDirectory(DB_DIR);

            // 查找或创建数据库
            var db = CardDatabase.Load();
            if (db == null)
            {
                var guids = AssetDatabase.FindAssets("t:CardDatabase");
                if (guids.Length > 0)
                    db = AssetDatabase.LoadAssetAtPath<CardDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<CardDatabase>();
                AssetDatabase.CreateAsset(db, $"{DB_DIR}/CardDatabase.asset");
            }

            db.cards.Clear();

            GenerateCard1(db);
            GenerateCard2(db);
            GenerateCard3(db);
            GenerateCard4(db);
            GenerateCard5(db);
            GenerateCard6(db);
            GenerateCard7(db);
            GenerateCard8(db);
            GenerateCard9(db);
            GenerateCard10(db);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GenerateExampleCards] 已生成 {db.cards.Count} 张示例卡牌到 {CARD_DIR}/");
            EditorGUIUtility.PingObject(db);
        }

        // ========================================================================
        // 1. 瞄准射击
        // ========================================================================
        private static void GenerateCard1(CardDatabase db)
        {
            var card = CreateCard("AimShot", "瞄准射击", CardGrade.Bronze, CardType.Attack, CardExistence.Normal, 1, 1, CardKeyword.None, true);
            card.baseDescription = "对指定敌人造成 8 点伤害；若暴击，抽 1 张牌。";
            card.upgradeDescription = "造成 13 点伤害；若暴击，抽 1 张牌。";
            card.designerNotes = "暴击条件通过事件条件 OnCrit 实现。";

            // 基础效果
            card.baseEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 8 },
                    times = 1
                },
                new CardEffect
                {
                    effectName = "暴击抽牌",
                    effectType = EffectType.DrawCard,
                    source = EffectSource.Player,
                    target = EffectTarget.Player,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 1 },
                    conditions = new List<EffectCondition>
                    {
                        new EffectCondition { conditionType = ConditionType.EventOccurred, eventName = "OnCrit" }
                    },
                    conditionLogic = ConditionLogic.All
                }
            };

            // 升级效果
            card.upgradeEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 13 },
                    times = 1
                },
                new CardEffect
                {
                    effectName = "暴击抽牌",
                    effectType = EffectType.DrawCard,
                    source = EffectSource.Player,
                    target = EffectTarget.Player,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 1 },
                    conditions = new List<EffectCondition>
                    {
                        new EffectCondition { conditionType = ConditionType.EventOccurred, eventName = "OnCrit" }
                    },
                    conditionLogic = ConditionLogic.All
                }
            };

            SaveAndAdd(card, db);
        }

        // ========================================================================
        // 2. 校准
        // ========================================================================
        private static void GenerateCard2(CardDatabase db)
        {
            var card = CreateCard("Calibrate", "校准", CardGrade.Bronze, CardType.Skill, CardExistence.Normal, 1, 1, CardKeyword.None, true);
            card.baseDescription = "本场战斗暴击率增加 15%，抽 1 张牌。";
            card.upgradeDescription = "本场战斗暴击率增加 25%，抽 1 张牌。";

            card.baseEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "暴击率提升",
                    effectType = EffectType.ModifyAttribute,
                    source = EffectSource.Player,
                    target = EffectTarget.Player,
                    buffTarget = BuffTarget.Player,
                    modAttribute = ModifiableAttribute.PlayerCritRate,
                    modifyMethod = ModifyMethod.Add,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 15 },
                    duration = EffectDuration.BattlePermanent
                },
                new CardEffect
                {
                    effectName = "抽牌",
                    effectType = EffectType.DrawCard,
                    source = EffectSource.Player,
                    target = EffectTarget.Player,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 1 }
                }
            };

            card.upgradeEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "暴击率提升",
                    effectType = EffectType.ModifyAttribute,
                    source = EffectSource.Player,
                    target = EffectTarget.Player,
                    buffTarget = BuffTarget.Player,
                    modAttribute = ModifiableAttribute.PlayerCritRate,
                    modifyMethod = ModifyMethod.Add,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 25 },
                    duration = EffectDuration.BattlePermanent
                },
                new CardEffect
                {
                    effectName = "抽牌",
                    effectType = EffectType.DrawCard,
                    source = EffectSource.Player,
                    target = EffectTarget.Player,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 1 }
                }
            };

            SaveAndAdd(card, db);
        }

        // ========================================================================
        // 3. 连锁爆发
        // ========================================================================
        private static void GenerateCard3(CardDatabase db)
        {
            var card = CreateCard("ChainBurst", "连锁爆发", CardGrade.Bronze, CardType.Attack, CardExistence.Normal, 1, 1, CardKeyword.None, true);
            card.baseDescription = "对指定敌人造成 6 点伤害；若此次攻击暴击，再造成 6 点伤害。";
            card.upgradeDescription = "造成 9 点伤害；若暴击，再造成 9 点伤害。";

            card.baseEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "主伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 6 },
                    times = 1
                },
                new CardEffect
                {
                    effectName = "暴击追加伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 6 },
                    times = 1,
                    conditions = new List<EffectCondition>
                    {
                        new EffectCondition { conditionType = ConditionType.EventOccurred, eventName = "OnCrit" }
                    },
                    conditionLogic = ConditionLogic.All
                }
            };

            card.upgradeEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "主伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 9 },
                    times = 1
                },
                new CardEffect
                {
                    effectName = "暴击追加伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 9 },
                    times = 1,
                    conditions = new List<EffectCondition>
                    {
                        new EffectCondition { conditionType = ConditionType.EventOccurred, eventName = "OnCrit" }
                    },
                    conditionLogic = ConditionLogic.All
                }
            };

            SaveAndAdd(card, db);
        }

        // ========================================================================
        // 4. 狙击
        // ========================================================================
        private static void GenerateCard4(CardDatabase db)
        {
            var card = CreateCard("SniperShot", "狙击", CardGrade.Silver, CardType.Attack, CardExistence.Normal, 2, 2, CardKeyword.None, true);
            card.baseDescription = "造成 16 点伤害；若热度大于 15，则必定暴击。";
            card.upgradeDescription = "造成 22 点伤害；条件不变。";
            card.designerNotes = "热度(Heat)为自定义属性，需战斗系统实现。条件可通过自定义条件脚本实现。";

            card.baseEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "狙击伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 16 },
                    times = 1,
                    alwaysCrit = false,
                    conditions = new List<EffectCondition>
                    {
                        new EffectCondition
                        {
                            conditionType = ConditionType.Custom,
                            customConditionScript = null // 需绑定热度检查的自定义条件脚本
                        }
                    },
                    conditionLogic = ConditionLogic.All
                }
            };

            card.upgradeEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "狙击伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 22 },
                    times = 1,
                    alwaysCrit = false,
                    conditions = new List<EffectCondition>
                    {
                        new EffectCondition
                        {
                            conditionType = ConditionType.Custom,
                            customConditionScript = null
                        }
                    },
                    conditionLogic = ConditionLogic.All
                }
            };

            SaveAndAdd(card, db);
        }

        // ========================================================================
        // 5. 深呼吸
        // ========================================================================
        private static void GenerateCard5(CardDatabase db)
        {
            var card = CreateCard("DeepBreath", "深呼吸", CardGrade.Bronze, CardType.Skill, CardExistence.Normal, 0, 0, CardKeyword.None, true);
            card.baseDescription = "下一张攻击牌的暴击伤害增加 40%。";
            card.upgradeDescription = "接下来两张攻击牌的暴击伤害增加 40%。";

            card.baseEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "暴击伤害提升",
                    effectType = EffectType.ModifyAttribute,
                    source = EffectSource.Player,
                    target = EffectTarget.Player,
                    buffTarget = BuffTarget.NextCard,
                    modAttribute = ModifiableAttribute.PlayerCritDamage,
                    modifyMethod = ModifyMethod.Add,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 40 },
                    timing = EffectTiming.NextAttack,
                    duration = EffectDuration.OneAction,
                    durationValue = 1
                }
            };

            card.upgradeEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "暴击伤害提升(两张)",
                    effectType = EffectType.ModifyAttribute,
                    source = EffectSource.Player,
                    target = EffectTarget.Player,
                    buffTarget = BuffTarget.NextCard,
                    modAttribute = ModifiableAttribute.PlayerCritDamage,
                    modifyMethod = ModifyMethod.Add,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 40 },
                    timing = EffectTiming.NextAttack,
                    duration = EffectDuration.OneAction,
                    durationValue = 2
                }
            };

            SaveAndAdd(card, db);
        }

        // ========================================================================
        // 6. 弱点洞穿
        // ========================================================================
        private static void GenerateCard6(CardDatabase db)
        {
            var card = CreateCard("WeakPoint", "弱点洞穿", CardGrade.Silver, CardType.Attack, CardExistence.BattleRemove, 2, 2, CardKeyword.None, true);
            card.baseDescription = "造成 10 点伤害，并使该敌人受到的暴击率永久增加 40%，随后移除此牌。";
            card.upgradeDescription = "伤害提高为 17。";
            card.designerNotes = "存在形式为「战斗内移除」，使用后从本次战斗牌库移除。";

            card.baseEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 10 },
                    times = 1
                },
                new CardEffect
                {
                    effectName = "暴击率永久增加",
                    effectType = EffectType.ModifyAttribute,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    buffTarget = BuffTarget.Enemy,
                    modAttribute = ModifiableAttribute.EnemyCritRate,
                    modifyMethod = ModifyMethod.Add,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 40 },
                    duration = EffectDuration.GamePermanent
                }
            };

            card.upgradeEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 17 },
                    times = 1
                },
                new CardEffect
                {
                    effectName = "暴击率永久增加",
                    effectType = EffectType.ModifyAttribute,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    buffTarget = BuffTarget.Enemy,
                    modAttribute = ModifiableAttribute.EnemyCritRate,
                    modifyMethod = ModifyMethod.Add,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 40 },
                    duration = EffectDuration.GamePermanent
                }
            };

            SaveAndAdd(card, db);
        }

        // ========================================================================
        // 7. 节拍器 (能力卡)
        // ========================================================================
        private static void GenerateCard7(CardDatabase db)
        {
            var card = CreateCard("Metronome", "节拍器", CardGrade.Bronze, CardType.Ability, CardExistence.Normal, 1, 1, CardKeyword.None, true);
            card.baseDescription = "每回合第一次攻击必定暴击。";
            card.upgradeDescription = "每回合第二次攻击必定暴击。";
            card.designerNotes = "能力卡：触发时机为每回合首次攻击，触发后施加暴击率提升100%(=必定暴击)。升级版触发次数提升为2。";

            // 基础能力
            card.baseAbility = new AbilityData
            {
                abilityName = "节拍器",
                abilityDescription = "每回合第一次攻击必定暴击",
                trigger = AbilityTrigger.FirstAttackEachTurn,
                maxTriggers = 0,
                maxTriggersPerTurn = 1,
                triggeredEffects = new List<CardEffect>
                {
                    new CardEffect
                    {
                        effectName = "必定暴击",
                        effectType = EffectType.ApplyStatus,
                        source = EffectSource.Player,
                        target = EffectTarget.Player,
                        statusType = StatusType.CritRateBoost,
                        statusStacks = 100,
                        stackable = true,
                        timing = EffectTiming.NextAttack,
                        duration = EffectDuration.OneAction,
                        durationValue = 1
                    }
                }
            };

            // 升级能力
            card.upgradeAbility = new AbilityData
            {
                abilityName = "节拍器+",
                abilityDescription = "每回合前两次攻击必定暴击",
                trigger = AbilityTrigger.FirstAttackEachTurn,
                maxTriggers = 0,
                maxTriggersPerTurn = 2,
                triggeredEffects = new List<CardEffect>
                {
                    new CardEffect
                    {
                        effectName = "必定暴击",
                        effectType = EffectType.ApplyStatus,
                        source = EffectSource.Player,
                        target = EffectTarget.Player,
                        statusType = StatusType.CritRateBoost,
                        statusStacks = 100,
                        stackable = true,
                        timing = EffectTiming.NextAttack,
                        duration = EffectDuration.OneAction,
                        durationValue = 1
                    }
                }
            };

            SaveAndAdd(card, db);
        }

        // ========================================================================
        // 8. 连环枪
        // ========================================================================
        private static void GenerateCard8(CardDatabase db)
        {
            var card = CreateCard("ChainGun", "连环枪", CardGrade.Silver, CardType.Attack, CardExistence.Normal, 2, 2, CardKeyword.None, true);
            card.baseDescription = "攻击 3 次，每次造成 6 点伤害，每次独立判定暴击；每次暴击额外追加 3 点伤害。";
            card.upgradeDescription = "每次伤害提高为 8 点。";
            card.designerNotes = "每段独立判定暴击。暴击追加伤害需战斗系统逐段判定后触发。";

            card.baseEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "三连射",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 6 },
                    times = 3
                },
                new CardEffect
                {
                    effectName = "暴击追加伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 3 },
                    times = 1,
                    conditions = new List<EffectCondition>
                    {
                        new EffectCondition { conditionType = ConditionType.EventOccurred, eventName = "OnCrit" }
                    },
                    conditionLogic = ConditionLogic.All
                }
            };

            card.upgradeEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "三连射",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 8 },
                    times = 3
                },
                new CardEffect
                {
                    effectName = "暴击追加伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 3 },
                    times = 1,
                    conditions = new List<EffectCondition>
                    {
                        new EffectCondition { conditionType = ConditionType.EventOccurred, eventName = "OnCrit" }
                    },
                    conditionLogic = ConditionLogic.All
                }
            };

            SaveAndAdd(card, db);
        }

        // ========================================================================
        // 9. 弹链 (能力卡)
        // ========================================================================
        private static void GenerateCard9(CardDatabase db)
        {
            var card = CreateCard("BulletChain", "弹链", CardGrade.Silver, CardType.Ability, CardExistence.Normal, 1, 1, CardKeyword.None, true);
            card.baseDescription = "此后每次暴击，返还 1 点能量并增加 1 点热度。";
            card.upgradeDescription = "效果不变。";
            card.designerNotes = "能力卡：触发时机为暴击时。热度(Heat)增加需通过自定义效果脚本实现。";
            card.upgradable = false;

            card.baseAbility = new AbilityData
            {
                abilityName = "弹链",
                abilityDescription = "每次暴击返还1点能量并增加1点热度",
                trigger = AbilityTrigger.OnCrit,
                maxTriggers = 0,
                maxTriggersPerTurn = 0,
                triggeredEffects = new List<CardEffect>
                {
                    new CardEffect
                    {
                        effectName = "返还能量",
                        effectType = EffectType.RestoreEnergy,
                        source = EffectSource.Player,
                        target = EffectTarget.Player,
                        value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 1 }
                    },
                    new CardEffect
                    {
                        effectName = "增加热度",
                        effectType = EffectType.Custom,
                        source = EffectSource.Player,
                        target = EffectTarget.Player,
                        customEffectScript = null, // 需绑定热度增加的自定义效果脚本
                        customParams = "{\"heatIncrease\": 1}"
                    }
                }
            };

            card.upgradeAbility = new AbilityData();

            SaveAndAdd(card, db);
        }

        // ========================================================================
        // 10. 穿甲弹
        // ========================================================================
        private static void GenerateCard10(CardDatabase db)
        {
            var card = CreateCard("ArmorPiercing", "穿甲弹", CardGrade.Bronze, CardType.Attack, CardExistence.Normal, 1, 1, CardKeyword.None, true);
            card.baseDescription = "造成 6 点伤害并施加 2 点破甲。";
            card.upgradeDescription = "造成 9 点伤害并施加 2 点破甲。";

            card.baseEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "穿甲伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 6 },
                    times = 1,
                    applyArmorBreak = true,
                    armorBreakAmount = 2
                }
            };

            card.upgradeEffects = new List<CardEffect>
            {
                new CardEffect
                {
                    effectName = "穿甲伤害",
                    effectType = EffectType.Damage,
                    source = EffectSource.Player,
                    target = EffectTarget.SelectedEnemy,
                    value = new ValueExpression { formulaType = ValueFormulaType.Fixed, baseValue = 9 },
                    times = 1,
                    applyArmorBreak = true,
                    armorBreakAmount = 2
                }
            };

            SaveAndAdd(card, db);
        }

        // ========================================================================
        // 辅助方法
        // ========================================================================
        private static CardEntry CreateCard(string id, string name, CardGrade grade, CardType type, CardExistence existence, int baseCost, int upgradeCost, CardKeyword keyword, bool upgradable)
        {
            var card = ScriptableObject.CreateInstance<CardEntry>();
            card.cardId = id;
            card.cardName = name;
            card.grade = grade;
            card.cardType = type;
            card.existence = existence;
            card.baseCost = baseCost;
            card.upgradeCost = upgradeCost;
            card.keyword = keyword;
            card.upgradable = upgradable;
            card.baseEffects = new List<CardEffect>();
            card.upgradeEffects = new List<CardEffect>();
            card.baseAbility = new AbilityData();
            card.upgradeAbility = new AbilityData();
            return card;
        }

        private static void SaveAndAdd(CardEntry card, CardDatabase db)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath($"{CARD_DIR}/{card.cardName}.asset");
            AssetDatabase.CreateAsset(card, path);
            db.cards.Add(card);
        }
    }
}
