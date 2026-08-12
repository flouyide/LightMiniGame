using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class EnemyAssetStore
{
    public const string EnemyFolder = "Assets/ScriptableObjects/Enemies";

    public static List<EnemyConfig> LoadAll()
    {
        if (!AssetDatabase.IsValidFolder(EnemyFolder))
        {
            var guids = AssetDatabase.FindAssets("t:EnemyConfig");
            var all = new List<EnemyConfig>(guids.Length);
            foreach (var g in guids)
            {
                var c = AssetDatabase.LoadAssetAtPath<EnemyConfig>(AssetDatabase.GUIDToAssetPath(g));
                if (c != null) all.Add(c);
            }
            return all;
        }
        var inFolder = AssetDatabase.FindAssets("t:EnemyConfig", new[] { EnemyFolder });
        var list = new List<EnemyConfig>(inFolder.Length);
        foreach (var g in inFolder)
        {
            var c = AssetDatabase.LoadAssetAtPath<EnemyConfig>(AssetDatabase.GUIDToAssetPath(g));
            if (c != null) list.Add(c);
        }
        return list;
    }

    public static List<string> GetSubDirectories()
    {
        var result = new List<string>();
        if (!AssetDatabase.IsValidFolder(EnemyFolder)) return result;
        foreach (var path in Directory.GetDirectories(EnemyFolder))
        {
            var rel = "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/');
            result.Add(rel);
        }
        return result.OrderBy(p => p).ToList();
    }

    public static EnemyConfig Create(string desiredName = "NewEnemy")
    {
        EnsureFolder(EnemyFolder);
        var asset = ScriptableObject.CreateInstance<EnemyConfig>();
        asset.enemyName = string.IsNullOrEmpty(desiredName) ? "NewEnemy" : desiredName;
        var path = AssetDatabase.GenerateUniqueAssetPath($"{EnemyFolder}/{asset.enemyName}.asset");
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<EnemyConfig>(path);
    }

    public static EnemyConfig Duplicate(EnemyConfig src)
    {
        if (src == null) return null;
        EnsureFolder(EnemyFolder);
        var srcPath = AssetDatabase.GetAssetPath(src);
        var copy = Object.Instantiate(src);
        copy.enemyName = src.enemyName + " (副本)";
        var dstPath = AssetDatabase.GenerateUniqueAssetPath(srcPath);
        AssetDatabase.CreateAsset(copy, dstPath);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<EnemyConfig>(dstPath);
    }

    public static bool Delete(EnemyConfig cfg)
    {
        if (cfg == null) return false;
        var path = AssetDatabase.GetAssetPath(cfg);
        if (string.IsNullOrEmpty(path)) return false;
        if (!EditorUtility.DisplayDialog("删除敌人配置", $"确定删除「{cfg.enemyName}」？\n{path}", "删除", "取消"))
            return false;
        return AssetDatabase.DeleteAsset(path);
    }

    public static void Ping(EnemyConfig cfg)
    {
        if (cfg != null) EditorGUIUtility.PingObject(cfg);
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        var parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        var leaf = Path.GetFileName(assetPath);
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(assetPath))
            AssetDatabase.CreateFolder(parent, leaf);
    }
}