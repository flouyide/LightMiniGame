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
        BuffAttributeType.DirtyWork => "脏活",
        BuffAttributeType.Heat => "热度",
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
        BuffAttributeType.ArmorBreak => "每层破甲使受到的攻击伤害绕过 1 点护甲。没有护甲时也能叠加。",
        BuffAttributeType.DirtyWork => "受到伤害时，额外受到 3×层数 点伤害。",
        BuffAttributeType.Heat => "打出攻击牌增加热度。热度达到 25 时过载，手牌费用 +1。",
        _ => ""
    };

    /// <summary>未配置 BuffData.icon 时的内置图标（编辑器下从工程路径加载）。</summary>
    public static Sprite LoadBuiltinIcon(BuffAttributeType type)
    {
#if UNITY_EDITOR
        string[] paths = type switch
        {
            BuffAttributeType.ArmorBreak => new[] { "Assets/Art/词条标/破甲.png", "Assets/Art/局内/破甲.png" },
            BuffAttributeType.Fatigue => new[] { "Assets/Art/局内/疲惫.png" },
            BuffAttributeType.Strength => new[] { "Assets/Art/局内/力量.png" },
            BuffAttributeType.DirtyWork => new[] { "Assets/Art/局内/脏活.png" },
            BuffAttributeType.Heat => new[] { "Assets/Art/局内/枪械师文件夹.png" },
            _ => System.Array.Empty<string>()
        };
        foreach (string path in paths)
        {
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
        }
#endif
        return null;
    }

    public static bool IsDebuff(BuffAttributeType type)
        => type == BuffAttributeType.Fatigue
        || type == BuffAttributeType.ArmorBreak
        || type == BuffAttributeType.DirtyWork;
}
