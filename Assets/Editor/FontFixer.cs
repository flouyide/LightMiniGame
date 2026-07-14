using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// 一键修复 TMP 中文字体问题：
/// 用 TMP 官方 API CreateFontAsset 从 simhei.ttf 新建一个干净的"动态"字体
/// （自动正确写入源字体 GUID + 图集纹理），并设为 TMP Settings 的 fallback，
/// 替换迁移后残留的孤儿/损坏字体引用（m_AtlasTextures has not been assigned 的根因）。
/// 菜单：Tools > 修复中文字体（新建动态字体）
/// </summary>
public static class FontFixer
{
    private const string FontPath = "Assets/Fonts/simhei.ttf";
    private const string OutPath = "Assets/Fonts/SimHeiDynamic.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    [MenuItem("Tools/修复中文字体（新建动态字体）")]
    public static void FixChineseFont()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[FontFixer] 找不到源字体: {FontPath}");
            return;
        }

        // 用官方 API 创建干净的动态字体：源字体 GUID、图集纹理、材质全部正确初始化
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(sourceFont);
        if (fa == null)
        {
            Debug.LogError("[FontFixer] CreateFontAsset 失败（FontEngine 无法加载 simhei.ttf）。" +
                           "请选中 simhei.ttf，在 Inspector 确认 Font Import Settings 已勾选 \"Include Font Data\" 后重试。");
            return;
        }
        fa.name = "SimHeiDynamic";

        // 删除旧的同名资产（保留 .meta 由引擎处理），再保存
        AssetDatabase.DeleteAsset(OutPath);
        AssetDatabase.CreateAsset(fa, OutPath);
        // 图集纹理与材质必须作为子资源保存，否则重载后 m_AtlasTextures 变 null
        if (fa.atlasTexture != null)
            //AssetDatabase.AddAssetToAsset(fa.atlasTexture, fa);
        if (fa.material != null)
            //AssetDatabase.AddAssetToAsset(fa.material, fa);
        EditorUtility.SetDirty(fa);

        // 把 TMP Settings 的 fallback 指向新字体（替换孤儿/损坏引用）
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if (settings != null)
        {
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_fallbackFontAssets");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = fa;
            so.ApplyModifiedProperties();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = fa;
        Debug.Log($"[FontFixer] 已创建干净的动态中文字体并设为 TMP fallback: {OutPath}\n" +
                  "接下来请执行：Tools > 重建全部 UI Prefab，然后运行游戏。");
    }
}
