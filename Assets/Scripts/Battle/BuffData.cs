using UnityEngine;

/// <summary>
/// Buff 数据 —— 每个属性一个 ScriptableObject，Inspector 配置名称、图标和悬停说明。
/// </summary>
[CreateAssetMenu(menuName = "CardGame/Buff Data", fileName = "NewBuff")]
public class BuffData : ScriptableObject
{
    [Tooltip("Buff 名称")]
    public string buffName = "新Buff";

    [Tooltip("图标")]
    public Sprite icon;

    [Tooltip("属性类型")]
    public BuffAttributeType attributeType = BuffAttributeType.Strength;

    [Tooltip("该属性的最小值（实际应用值不低于此）")]
    public int minValue = 0;

    [TextArea(2, 4)]
    [Tooltip("悬停图标时显示的说明")]
    public string description;

    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(buffName))
        {
            string name = buffName.EndsWith("Buff") ? buffName.Substring(0, buffName.Length - 4) : buffName;
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return DefaultName(attributeType);
    }

    public string GetDescription()
    {
        return string.IsNullOrEmpty(description) ? DefaultDescription(attributeType) : description;
    }

    public static string DefaultName(BuffAttributeType type) => type switch
    {
        BuffAttributeType.Strength => "力量",
        BuffAttributeType.Dexterity => "敏捷",
        BuffAttributeType.Recovery => "回复",
        BuffAttributeType.CriticalChance => "暴击率",
        BuffAttributeType.CriticalDamage => "暴击伤害",
        BuffAttributeType.Fatigue => "疲惫",
        BuffAttributeType.ArmorBreak => "破甲",
        _ => "Buff"
    };

    public static string DefaultDescription(BuffAttributeType type) => type switch
    {
        BuffAttributeType.Strength => "每点力量使造成的攻击伤害 +1。",
        BuffAttributeType.Dexterity => "每点敏捷使获得的护甲 +1。",
        BuffAttributeType.Recovery => "回合开始时回复等同层数的生命。",
        BuffAttributeType.CriticalChance => "提高攻击打出暴击的概率。",
        BuffAttributeType.CriticalDamage => "提高暴击时的伤害倍率。",
        BuffAttributeType.Fatigue => "回合开始时受到等同层数的伤害（无视护甲），然后层数 -1。",
        BuffAttributeType.ArmorBreak => "已被剥离的护甲层数。部分效果可以按层数把护甲还回去。",
        _ => ""
    };
}
