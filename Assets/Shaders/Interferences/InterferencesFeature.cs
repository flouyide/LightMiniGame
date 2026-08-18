using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LightMiniGame.PostProcessing
{
    /// <summary>
    /// 全屏信号干扰效果的 Renderer Feature（团结引擎 1.9.3 / URP 14.2，经典 ScriptableRenderPass 写法）。
    ///
    /// 接入步骤：
    ///   1. 选中 Assets/Settings/Renderer2D.asset
    ///   2. Add Renderer Feature → Interferences Feature
    ///   3. 场景里新建 GameObject → Add Component → Volume（Mode 设为 Global）
    ///   4. 新建/指定 Volume Profile → Add Override → Light MiniGame/后处理/信号干扰
    ///   5. 勾选并调高 Intensity（默认 0 = 关闭）
    ///
    /// 注意：Volume 的 Intensity 为 0 时 Pass 直接跳过，不产生任何 Blit 开销。
    /// </summary>
    [DisallowMultipleRendererFeature("Interferences Feature")]
    public class InterferencesFeature : ScriptableRendererFeature
    {
        [Tooltip("效果插入时机。默认在后处理之后、渲染完成之前，覆盖全部画面内容（含 UI 之前的部分）")]
        [SerializeField]
        private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        [Tooltip("是否在场景视图相机上也应用（关闭可让 Scene 视图保持干净）")]
        [SerializeField]
        private bool applyToSceneView = true;

        [Tooltip("干扰效果的着色器。留空则按名称查找 'Hidden/PostProcessing/Interferences'" +
                 "（Hidden 着色器不出现在下拉列表，用 Project 窗口把 Interferences.shader 直接拖进这个字段）")]
        [SerializeField]
        private Shader effectShader;

        private InterferencesPass _pass;
        private Material _material;

        /// <summary>effectShader 未指定时的回退查找名（也是打包时的兜底）。</summary>
        private const string FallbackShaderName = "Hidden/PostProcessing/Interferences";

        public override void Create()
        {
            // 序列化字段优先；为空则按名称回退（兼容旧配置，也保证运行时打包环境可用）
            Shader shader = effectShader != null ? effectShader : Shader.Find(FallbackShaderName);

            if (shader == null)
            {
                Debug.LogError("[InterferencesFeature] 着色器未指定且按名称找不到 '" + FallbackShaderName + "'。" +
                               "请在 Renderer2D 的 Interferences Feature 面板里把 Interferences.shader 拖到 Effect Shader 字段。");
                _material = null;
                _pass = null;
                return;
            }

            // 材质只在「不存在」或「换了着色器」时重建，避免热重载后重复创建
            if (_material == null || _material.shader != shader)
            {
                CoreUtils.Destroy(_material);
                _material = CoreUtils.CreateEngineMaterial(shader);
            }

            _pass = new InterferencesPass(_material)
            {
                renderPassEvent = this.renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null) return;

            var cameraType = renderingData.cameraData.cameraType;

            // 只在游戏相机（以及可选的场景视图）上生效；反射探针/预览相机跳过
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
            if (cameraType == CameraType.SceneView && !applyToSceneView) return;

            // 后处理总开关关闭时不生效
            if (!renderingData.cameraData.postProcessEnabled) return;

            // Volume 未激活（Intensity == 0）时不入队，零开销
            var volume = VolumeManager.instance.stack.GetComponent<InterferencesVolume>();
            if (volume == null || !volume.IsActive()) return;

            _pass.Setup(volume);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;

            CoreUtils.Destroy(_material);
            _material = null;
        }

        // ====================================================================
        // Render Pass
        // ====================================================================

        private class InterferencesPass : ScriptableRenderPass, IDisposable
        {
            private readonly Material _material;
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Interferences");

            private InterferencesVolume _volume;
            private RTHandle _tempTarget;

            // Shader 属性 ID（避免每帧字符串哈希）
            private static readonly int IdIntensity            = Shader.PropertyToID("_Intensity");
            private static readonly int IdOffset               = Shader.PropertyToID("_Offset");
            private static readonly int IdDistortion           = Shader.PropertyToID("_Distortion");
            private static readonly int IdDistortionSpeed      = Shader.PropertyToID("_DistortionSpeed");
            private static readonly int IdDistortionDensity    = Shader.PropertyToID("_DistortionDensity");
            private static readonly int IdDistortionAmplitude  = Shader.PropertyToID("_DistortionAmplitude");
            private static readonly int IdDistortionFrequency  = Shader.PropertyToID("_DistortionFrequency");
            private static readonly int IdScanlines            = Shader.PropertyToID("_Scanlines");
            private static readonly int IdScanlinesDensity     = Shader.PropertyToID("_ScanlinesDensity");
            private static readonly int IdScanlinesOpacity     = Shader.PropertyToID("_ScanlinesOpacity");
            private static readonly int IdBrightness           = Shader.PropertyToID("_Brightness");
            private static readonly int IdContrast             = Shader.PropertyToID("_Contrast");
            private static readonly int IdGamma                = Shader.PropertyToID("_Gamma");
            private static readonly int IdHue                  = Shader.PropertyToID("_Hue");
            private static readonly int IdSaturation           = Shader.PropertyToID("_Saturation");
            private static readonly int IdBaseColor            = Shader.PropertyToID("_BaseColor");
            private static readonly int IdBaseColorBlend       = Shader.PropertyToID("_BaseColorBlend");

            public InterferencesPass(Material material)
            {
                _material = material;
                // 需要读取相机颜色，声明 Color 输入让 URP 保证 _CameraOpaqueTexture 可用性/正确的资源过渡
                ConfigureInput(ScriptableRenderPassInput.Color);
            }

            public void Setup(InterferencesVolume volume)
            {
                _volume = volume;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;   // 中间色彩缓冲不需要深度
                desc.msaaSamples = 1;       // 后处理阶段不做 MSAA

                RenderingUtils.ReAllocateIfNeeded(
                    ref _tempTarget,
                    desc,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_InterferencesTemp");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || _volume == null) return;

                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                if (source == null || _tempTarget == null) return;

                var cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    UpdateMaterial();

                    // 双缓冲：source → temp（过 shader）→ source（直拷）
                    // URP 不允许同一 RTHandle 同时作为读写目标，必须中转
                    Blitter.BlitCameraTexture(cmd, source, _tempTarget, _material, 0);
                    Blitter.BlitCameraTexture(cmd, _tempTarget, source);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            private void UpdateMaterial()
            {
                _material.SetFloat(IdIntensity,           _volume.intensity.value);
                _material.SetFloat(IdOffset,              _volume.offset.value);

                _material.SetFloat(IdDistortion,          _volume.distortion.value);
                _material.SetFloat(IdDistortionSpeed,     _volume.distortionSpeed.value);
                _material.SetFloat(IdDistortionDensity,   _volume.distortionDensity.value);
                _material.SetFloat(IdDistortionAmplitude, _volume.distortionAmplitude.value);
                _material.SetFloat(IdDistortionFrequency, _volume.distortionFrequency.value);

                _material.SetFloat(IdScanlines,           _volume.scanlines.value);
                _material.SetFloat(IdScanlinesDensity,    _volume.scanlinesDensity.value);
                _material.SetFloat(IdScanlinesOpacity,    _volume.scanlinesOpacity.value);

                _material.SetFloat(IdBrightness,          _volume.brightness.value);
                _material.SetFloat(IdContrast,            _volume.contrast.value);
                _material.SetFloat(IdGamma,               _volume.gamma.value);
                _material.SetFloat(IdHue,                 _volume.hue.value);
                _material.SetFloat(IdSaturation,          _volume.saturation.value);

                _material.SetColor(IdBaseColor,           _volume.baseColor.value);
                _material.SetFloat(IdBaseColorBlend,      _volume.baseColorBlend.value);
            }

            public void Dispose()
            {
                _tempTarget?.Release();
                _tempTarget = null;
            }
        }
    }
}
