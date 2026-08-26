using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 词条 → 小图标映射。放到 Resources/CardEditor/KeywordIconLibrary 后全局生效；
/// 也可拖到卡牌 prefab 的 CardDisplay.keywordIconLibrary 上覆盖。
/// </summary>
[CreateAssetMenu(menuName = "CardEditor/Keyword Icon Library", fileName = "KeywordIconLibrary")]
public class KeywordIconLibrary : ScriptableObject
{
    public const string ResourcePath = "CardEditor/KeywordIconLibrary";

    [Serializable]
    public class Entry
    {
        public KeywordType keyword;
        public Sprite icon;
    }

    [Tooltip("每个词条对应一张小图标；未配置时卡面用首字占位")]
    public List<Entry> icons = new List<Entry>();

    public Sprite GetIcon(KeywordType flag)
    {
        if (icons == null || flag == KeywordType.None) return null;
        for (int i = 0; i < icons.Count; i++)
        {
            var e = icons[i];
            if (e != null && e.keyword == flag && e.icon != null)
                return e.icon;
        }
        return null;
    }

    public static KeywordIconLibrary Load() => Resources.Load<KeywordIconLibrary>(ResourcePath);
}
