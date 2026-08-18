using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using LightMiniGame.PostProcessing;

namespace LightMiniGame.EditorTools
{
    /// <summary>
    /// 一键接入「信号干扰」全屏后处理效果的编辑器工具。
    ///
    /// 手工在 Renderer2D 的 Inspector 底部找 "Add Renderer Feature" 按钮比较麻烦
    /// （按钮在所有折叠区之后，容易被忽略），本工具直接用代码完成：
    ///   1. 往当前管线使用的 Renderer 上挂 InterferencesFeature（作为 sub-asset）
    ///   2. 在场景里建一个 Global Volume + Profile，并添加 InterferencesVolume override
    ///
    /// 菜单：Tools/后处理/信号干扰
    /// </summary>
    public static class InterferencesSetup
    {
        private const string MenuRoot = "Tools/后处理/信号干扰/";

        // ====================================================================
        // 步骤 1：挂 Renderer Feature
        // ====================================================================

        [MenuItem(MenuRoot + "1. 添加 Renderer Feature 到当前渲染器", false, 1)]
        public static void AddRendererFeature()
        {
            var rendererData = FindActiveRendererData();
            if (rendererData == null)
            {
                EditorUtility.DisplayDialog("失败",
                    "找不到当前 URP 管线使用的 Renderer 资产。\n请确认 Project Settings > Graphics 已指定 URP 管线资产。",
                    "确定");
                return;
            }

            var so = new SerializedObject(rendererData);
            var featuresProp = so.FindProperty("m_RendererFeatures");
            var mapProp = so.FindProperty("m_RendererFeatureMap");

            if (featuresProp == null)
            {
                EditorUtility.DisplayDialog("失败",
                    "无法访问渲染器的 Feature 列表（URP 内部字段名可能已变更）。",
                    "确定");
                return;
            }

            // 去重：已挂过就不再添加
            for (int i = 0; i < featuresProp.arraySize; i++)
            {
                var existing = featuresProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing is InterferencesFeature)
                {
                    EditorUtility.DisplayDialog("已存在",
                        $"'{rendererData.name}' 上已经挂了 Interferences Feature，无需重复添加。",
                        "确定");
                    Selection.activeObject = rendererData;
                    return;
                }
            }

            // 复刻 URP 官方 ScriptableRendererDataEditor.AddComponent 的流程
            var feature = ScriptableObject.CreateInstance<InterferencesFeature>();
            feature.name = nameof(InterferencesFeature);
            Undo.RegisterCreatedObjectUndo(feature, "Add Interferences Feature");

            if (EditorUtility.IsPersistent(rendererData))
                AssetDatabase.AddObjectToAsset(feature, rendererData);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;

            // GUID 映射表必须同步增长，否则 URP 重载后会丢引用
            if (mapProp != null)
            {
                mapProp.arraySize = featuresProp.arraySize;
                mapProp.GetArrayElementAtIndex(mapProp.arraySize - 1).longValue = localId;
            }

            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[信号干扰] 已把 InterferencesFeature 挂到 '{rendererData.name}'（{AssetDatabase.GetAssetPath(rendererData)}）。");
            Selection.activeObject = rendererData;
        }

        // ====================================================================
        // 步骤 2：建 Global Volume
        // ====================================================================

        [MenuItem(MenuRoot + "2. 在场景中创建 Global Volume", false, 2)]
        public static void CreateGlobalVolume()
        {
            // 已有带 InterferencesVolume 的 Volume 就直接选中
            var existing = Object.FindObjectsOfType<UnityEngine.Rendering.Volume>()
                .FirstOrDefault(v => v.profile != null && v.profile.Has<InterferencesVolume>());
            if (existing != null)
            {
                EditorUtility.DisplayDialog("已存在",
                    $"场景里已有配置了信号干扰的 Volume：'{existing.name}'。",
                    "确定");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var go = new GameObject("Global Volume - 信号干扰");
            Undo.RegisterCreatedObjectUndo(go, "Create Interferences Volume");

            var volume = go.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;

            // Profile 存成独立资产，方便版本管理
            const string dir = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets", "Settings");

            string profilePath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/InterferencesVolumeProfile.asset");
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);

            // 关键：VolumeProfile.Add<T>() 只创建实例并塞进 components 列表，
            // 但**不会**把它挂成 .asset 的 sub-asset，保存后组件就丢了（Inspector 显示 no overrides）。
            // 必须复刻官方 VolumeComponentListEditor.AddComponent 的流程手动挂载。
            var component = AddOverrideToProfile<InterferencesVolume>(profile);

            // 给一组"一眼能看出效果"的默认值，避免用户以为没生效
            component.intensity.overrideState = true;
            component.intensity.value = 1.0f;
            component.distortion.overrideState = true;
            component.distortion.value = 0.5f;
            component.offset.overrideState = true;
            component.offset.value = 2.0f;

            volume.sharedProfile = profile;

            EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(profilePath, ImportAssetOptions.ForceUpdate);

            Selection.activeGameObject = go;
            Debug.Log($"[信号干扰] 已创建 Global Volume，Profile 存于 {profilePath}，Intensity 已预设为 1。");
        }

        /// <summary>
        /// 往 VolumeProfile 里添加一个 override，并正确挂成 sub-asset。
        /// 不能直接用 profile.Add&lt;T&gt;()：那个 API 不做 AddObjectToAsset，保存后组件会丢失。
        /// </summary>
        private static T AddOverrideToProfile<T>(UnityEngine.Rendering.VolumeProfile profile)
            where T : UnityEngine.Rendering.VolumeComponent
        {
            // 已有就直接返回，避免重复
            if (profile.TryGet<T>(out var existingComp) && existingComp != null)
                return existingComp;

            var component = (T)ScriptableObject.CreateInstance(typeof(T));
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            component.name = typeof(T).Name;
            component.SetAllOverridesTo(false);

            Undo.RegisterCreatedObjectUndo(component, "Add Volume Override");

            // 挂成 sub-asset —— 这一步是 profile.Add<T>() 缺失的关键环节
            if (EditorUtility.IsPersistent(profile))
                AssetDatabase.AddObjectToAsset(component, profile);

            // 通过 SerializedObject 写 components 列表（保证 Undo 与序列化正确）
            var so = new SerializedObject(profile);
            var listProp = so.FindProperty("components");
            listProp.arraySize++;
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = component;
            so.ApplyModifiedProperties();

            return component;
        }

        // ====================================================================
        // 一键全流程 + 修复 + 诊断
        // ====================================================================

        [MenuItem(MenuRoot + "修复现有 Volume Profile（override 丢失时用）", false, 3)]
        public static void RepairExistingProfile()
        {
            var volumes = Object.FindObjectsOfType<UnityEngine.Rendering.Volume>();
            var target = volumes.FirstOrDefault(v => v.sharedProfile != null);

            if (target == null)
            {
                EditorUtility.DisplayDialog("失败",
                    "场景里找不到带 Profile 的 Volume。请先执行第 2 步创建。",
                    "确定");
                return;
            }

            var profile = target.sharedProfile;

            if (profile.Has<InterferencesVolume>())
            {
                EditorUtility.DisplayDialog("无需修复",
                    $"'{profile.name}' 里已经有信号干扰 override 了。",
                    "确定");
                Selection.activeObject = profile;
                return;
            }

            var component = AddOverrideToProfile<InterferencesVolume>(profile);
            component.intensity.overrideState = true;
            component.intensity.value = 1.0f;
            component.distortion.overrideState = true;
            component.distortion.value = 0.5f;
            component.offset.overrideState = true;
            component.offset.value = 2.0f;

            EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            string path = AssetDatabase.GetAssetPath(profile);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            Selection.activeObject = profile;
            Debug.Log($"[信号干扰] 已修复 '{profile.name}'，添加信号干扰 override 并设 Intensity=1。");
            EditorUtility.DisplayDialog("修复完成",
                $"已往 '{profile.name}' 添加信号干扰 override。\nIntensity 已设为 1，Game 视图应能看到效果。",
                "好");
        }

        [MenuItem(MenuRoot + "开启场景相机的 Post Processing（Game 视图没效果时用）", false, 4)]
        public static void EnableCameraPostProcessing()
        {
            var cams = Object.FindObjectsOfType<Camera>();
            if (cams.Length == 0)
            {
                EditorUtility.DisplayDialog("失败", "场景里找不到任何相机。", "确定");
                return;
            }

            var fixedCams = new List<string>();
            var alreadyOn = new List<string>();

            foreach (var cam in cams)
            {
                var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null)
                {
                    // 没有 URP 相机数据组件时补上（否则 postProcessEnabled 恒为 false）
                    camData = Undo.AddComponent<UniversalAdditionalCameraData>(cam.gameObject);
                }

                if (camData.renderPostProcessing)
                {
                    alreadyOn.Add(cam.name);
                    continue;
                }

                Undo.RecordObject(camData, "Enable Post Processing");
                camData.renderPostProcessing = true;
                EditorUtility.SetDirty(camData);
                fixedCams.Add(cam.name);
            }

            // 场景改动需标脏才能保存
            if (fixedCams.Count > 0)
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

            string msg = fixedCams.Count > 0
                ? $"已为以下相机开启 Post Processing：\n{string.Join("\n", fixedCams.Select(n => "  · " + n))}\n\n请记得保存场景（Ctrl+S）。"
                : "所有相机的 Post Processing 本来就是开启状态。";

            if (alreadyOn.Count > 0 && fixedCams.Count > 0)
                msg += $"\n\n本来已开启的：{string.Join("、", alreadyOn)}";

            Debug.Log("[信号干扰] " + msg.Replace("\n", " "));
            EditorUtility.DisplayDialog("完成", msg, "好");
        }

        [MenuItem(MenuRoot + "让 UI 也受干扰影响（Canvas 改为 Screen Space - Camera）", false, 5)]
        public static void MakeUIAffected()
        {
            var cam = Camera.main ?? Object.FindObjectsOfType<Camera>().FirstOrDefault();
            if (cam == null)
            {
                EditorUtility.DisplayDialog("失败", "场景里找不到相机。", "确定");
                return;
            }

            var canvases = Object.FindObjectsOfType<Canvas>()
                .Where(c => c.isRootCanvas)
                .ToList();

            if (canvases.Count == 0)
            {
                EditorUtility.DisplayDialog("提示",
                    "当前场景里找不到 Canvas。\n\n" +
                    "你的 UI 大概是运行时动态生成的（来自 Prefab）。\n" +
                    "这种情况请改 Prefab 本身，或运行时用脚本设置：\n" +
                    "    canvas.renderMode = RenderMode.ScreenSpaceCamera;\n" +
                    "    canvas.worldCamera = Camera.main;\n" +
                    "    canvas.planeDistance = 10f;",
                    "确定");
                return;
            }

            var changed = new List<string>();
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
                    continue;

                Undo.RecordObject(canvas, "Change Canvas Render Mode");
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                // planeDistance 需在近远裁剪面之间，否则 UI 被裁掉不可见
                canvas.planeDistance = Mathf.Clamp(10f, cam.nearClipPlane + 0.01f, cam.farClipPlane - 1f);
                EditorUtility.SetDirty(canvas);
                changed.Add(canvas.name);
            }

            if (changed.Count > 0)
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

            string msg = changed.Count > 0
                ? $"已将以下 Canvas 改为 Screen Space - Camera：\n{string.Join("\n", changed.Select(n => "  · " + n))}\n\n" +
                  "现在 UI 也会被干扰效果影响。\n请保存场景（Ctrl+S）。"
                : "所有 Canvas 本来就是 Screen Space - Camera 模式。";

            Debug.Log("[信号干扰] " + msg.Replace("\n", " "));
            EditorUtility.DisplayDialog("完成", msg, "好");
        }

        [MenuItem(MenuRoot + "一键完成全部接入", false, 20)]
        public static void SetupAll()
        {
            AddRendererFeature();
            CreateGlobalVolume();
            EnableCameraPostProcessing();
            EditorUtility.DisplayDialog("完成",
                "信号干扰效果已接入。\n\n" +
                "在 Game 视图应该已能看到画面撕裂与扫描线。\n" +
                "选中场景里的 'Global Volume - 信号干扰' 即可调参。\n\n" +
                "若修改过相机设置，请保存场景（Ctrl+S）。",
                "好");
        }

        [MenuItem(MenuRoot + "诊断当前状态", false, 21)]
        public static void Diagnose()
        {
            var sb = new System.Text.StringBuilder();

            // 着色器
            var shader = Shader.Find("Hidden/PostProcessing/Interferences");
            sb.AppendLine(shader != null
                ? "[OK] 着色器已找到：Hidden/PostProcessing/Interferences"
                : "[缺失] 找不到着色器 Hidden/PostProcessing/Interferences —— 请确认 Interferences.shader 无编译错误");

            // 渲染器 + Feature
            var rendererData = FindActiveRendererData();
            if (rendererData == null)
            {
                sb.AppendLine("[缺失] 找不到当前管线使用的 Renderer 资产");
            }
            else
            {
                sb.AppendLine($"[OK] 当前渲染器：{rendererData.name}（{rendererData.GetType().Name}）");
                sb.AppendLine($"     路径：{AssetDatabase.GetAssetPath(rendererData)}");

                bool hasFeature = rendererData.rendererFeatures != null &&
                                  rendererData.rendererFeatures.Any(f => f is InterferencesFeature);
                sb.AppendLine(hasFeature
                    ? "[OK] InterferencesFeature 已挂载"
                    : "[缺失] 渲染器上没有 InterferencesFeature —— 执行菜单第 1 步");
            }

            // 管线后处理开关
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            sb.AppendLine(urp != null
                ? $"[OK] 当前管线资产：{urp.name}"
                : "[缺失] 未设置 URP 管线资产");

            // Volume
            var volumes = Object.FindObjectsOfType<UnityEngine.Rendering.Volume>();
            var withComp = volumes.Where(v => v.profile != null && v.profile.Has<InterferencesVolume>()).ToList();
            if (withComp.Count == 0)
            {
                sb.AppendLine($"[缺失] 场景中 {volumes.Length} 个 Volume，但没有一个配置了信号干扰 —— 执行菜单第 2 步");
            }
            else
            {
                foreach (var v in withComp)
                {
                    v.profile.TryGet<InterferencesVolume>(out var comp);
                    float val = comp != null ? comp.intensity.value : 0f;
                    bool ovr = comp != null && comp.intensity.overrideState;
                    sb.AppendLine($"[OK] Volume '{v.name}'：Global={v.isGlobal}, Intensity={val:F2}, Override={ovr}");
                    if (!ovr)
                        sb.AppendLine("     [注意] Intensity 的 Override 复选框未勾选，效果不会生效");
                    else if (val <= 0f)
                        sb.AppendLine("     [注意] Intensity 为 0，效果被主动关闭");
                }
            }

            // 相机后处理开关 —— Game 视图看不到效果最常见的原因
            // Scene 视图走 isSceneViewCamera 分支（只看工具栏后处理按钮），不读这个字段，
            // 所以会出现"Scene 有效果、Game 没效果"的现象。
            var cams = Object.FindObjectsOfType<Camera>();
            foreach (var cam in cams)
            {
                var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null)
                {
                    sb.AppendLine($"[缺失] 相机 '{cam.name}' 没有 Universal Additional Camera Data 组件，Game 视图不会有效果");
                    sb.AppendLine("       → 执行菜单「开启场景相机的 Post Processing」");
                }
                else if (!camData.renderPostProcessing)
                {
                    sb.AppendLine($"[缺失] 相机 '{cam.name}' 的 Post Processing 未勾选 —— 这就是 Game 视图看不到效果的原因");
                    sb.AppendLine("       （Scene 视图不受此开关影响，所以那边能看到）");
                    sb.AppendLine("       → 执行菜单「开启场景相机的 Post Processing」");
                }
                else
                {
                    sb.AppendLine($"[OK] 相机 '{cam.name}'：Post Processing 已开启");
                }
            }

            // Canvas 渲染模式 —— 相机后处理已开却仍"看不到效果"的元凶
            // Screen Space - Overlay 的 UI 由 m_DrawOverlayUIPass 在
            // RenderPassEvent.AfterRendering + offset（FinalBlit 之后）绘制，
            // 完全绕过后处理链，所以画面全是 UI 时会觉得效果没生效。
            var canvases = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToList();
            if (canvases.Count == 0)
            {
                sb.AppendLine("[信息] 当前场景无 Canvas（UI 可能是运行时动态生成的）");
                sb.AppendLine("       若 UI 用 Screen Space - Overlay，则 UI 不会被后处理影响（这是 URP 的既定行为）");
            }
            else
            {
                foreach (var c in canvases)
                {
                    if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        sb.AppendLine($"[注意] Canvas '{c.name}' 是 Screen Space - Overlay —— 该 UI **不会**被后处理影响");
                        sb.AppendLine("       Overlay UI 在 FinalBlit 之后才绘制，绕过整个后处理链");
                        sb.AppendLine("       → 想让 UI 也受影响，执行菜单「让 UI 也受干扰影响」");
                    }
                    else
                    {
                        sb.AppendLine($"[OK] Canvas '{c.name}'：{c.renderMode}，会被后处理影响");
                    }
                }
            }

            Debug.Log("=== 信号干扰效果诊断 ===\n" + sb);
            EditorUtility.DisplayDialog("诊断结果", sb.ToString(), "确定");
        }

        // ====================================================================
        // 工具
        // ====================================================================

        /// <summary>取当前 URP 管线资产实际使用的默认 Renderer 数据。</summary>
        private static ScriptableRendererData FindActiveRendererData()
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return null;

            // m_RendererDataList / m_DefaultRendererIndex 都是 internal，走 SerializedObject
            var so = new SerializedObject(urp);
            var listProp = so.FindProperty("m_RendererDataList");
            var idxProp = so.FindProperty("m_DefaultRendererIndex");
            if (listProp == null || listProp.arraySize == 0) return null;

            int idx = idxProp != null ? Mathf.Clamp(idxProp.intValue, 0, listProp.arraySize - 1) : 0;
            return listProp.GetArrayElementAtIndex(idx).objectReferenceValue as ScriptableRendererData;
        }
    }
}
