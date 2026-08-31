using UnityEngine;

/// <summary>
/// 运行时从图集路径加载 Sprite。编辑器走 AssetDatabase；真机/包体从 Resources/RuntimeArt 读同名图，
/// 避免 AssetDatabase 在包体里不可用导致图标变白块。
/// </summary>
public static class RuntimeArt
{
    public static Sprite LoadSprite(string editorPath)
    {
        if (string.IsNullOrEmpty(editorPath)) return null;
#if UNITY_EDITOR
        var editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(editorPath);
        if (editorSprite != null) return editorSprite;
#endif
        string name = System.IO.Path.GetFileNameWithoutExtension(editorPath);
        if (string.IsNullOrEmpty(name)) return null;
        var sprite = Resources.Load<Sprite>("RuntimeArt/" + name);
        if (sprite != null) return sprite;
        var all = Resources.LoadAll<Sprite>("RuntimeArt/" + name);
        return all != null && all.Length > 0 ? all[0] : null;
    }
}
