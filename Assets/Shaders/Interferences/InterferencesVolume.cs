using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LightMiniGame.PostProcessing
{
    /// <summary>
    /// 全屏信号干扰效果的 Volume 参数组件（团结引擎 1.9.3 / URP 14.2）。
    ///
    /// 用法：场景里建一个 Volume（Global 或 Local）→ 指定 Volume Profile
    ///      → Add Override → Light MiniGame/后处理/信号干扰。
    /// Intensity 为 0 时效果完全不执行（Pass 直接跳过，无性能开销）。
    ///
    /// 双层结构：
    ///   Distortion —— Simplex 噪声横向撕裂 + 色差偏移（信号丢失、电磁干扰）
    ///   Scanlines  —— 横向压暗条纹（CRT 显示器、老旧屏幕）
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("Light MiniGame/后处理/信号干扰 (Interferences)")]
    public sealed class InterferencesVolume : VolumeComponent, IPostProcessComponent
    {
        // ====================================================================
        // 信号
        // ====================================================================

        [Header("信号")]
        [Tooltip("效果总强度。0 = 完全关闭（不消耗性能）")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

        // ====================================================================
        // 循环扭曲（每周期 2 次明显扭曲脉冲，其余时间小扭曲）
        // ====================================================================

        [Header("循环扭曲")]
        [Tooltip("循环间隔（秒）：每隔多久出现一轮明显扭曲（每轮 2 次脉冲）")]
        public ClampedFloatParameter cycleInterval = new ClampedFloatParameter(8.0f, 0.5f, 60.0f);

        [Header("明显扭曲（脉冲期）")]
        [Tooltip("强度：脉冲期的最大横向 UV 位移，撕裂宽度")]
        public ClampedFloatParameter burstDistortion = new ClampedFloatParameter(1.0f, 0.0f, 5.0f);

        [Tooltip("速度：脉冲期噪声的时间演化速率，撕裂变化快慢")]
        public ClampedFloatParameter burstSpeed = new ClampedFloatParameter(40.0f, 0.0f, 100.0f);

        [Tooltip("频率：脉冲期撕裂带的纵向频率，值越大条带越细越多")]
        public ClampedFloatParameter burstFrequency = new ClampedFloatParameter(3.0f, 0.1f, 10.0f);

        [Tooltip("Offset：脉冲期 R/B 通道色差错位的像素数")]
        public ClampedFloatParameter burstOffset = new ClampedFloatParameter(4.0f, 0.0f, 20.0f);

        [Header("小扭曲（常态）")]
        [Tooltip("强度：常态下细微的横向 UV 位移")]
        public ClampedFloatParameter idleDistortion = new ClampedFloatParameter(0.15f, 0.0f, 2.0f);

        [Tooltip("速度：常态噪声的时间演化速率")]
        public ClampedFloatParameter idleSpeed = new ClampedFloatParameter(8.0f, 0.0f, 100.0f);

        [Tooltip("频率：常态撕裂带的纵向频率")]
        public ClampedFloatParameter idleFrequency = new ClampedFloatParameter(2.0f, 0.1f, 10.0f);

        [Tooltip("Offset：常态 R/B 通道色差错位的像素数")]
        public ClampedFloatParameter idleOffset = new ClampedFloatParameter(0.5f, 0.0f, 20.0f);

        // ====================================================================
        // 扫描线（Scanlines）
        // ====================================================================

        [Header("扫描线")]
        [Tooltip("扫描线叠加层的整体可见度")]
        public ClampedFloatParameter scanlines = new ClampedFloatParameter(0.75f, 0.0f, 1.0f);

        [Tooltip("扫描线间距。值越大线越密")]
        public ClampedFloatParameter scanlinesDensity = new ClampedFloatParameter(0.25f, 0.0f, 1.0f);

        [Tooltip("单根扫描线的压暗强度。受撕裂噪声调制产生有机闪烁")]
        public ClampedFloatParameter scanlinesOpacity = new ClampedFloatParameter(0.5f, 0.0f, 1.0f);

        // ====================================================================
        // 色彩校正
        // ====================================================================

        [Header("色彩校正")]
        [Tooltip("亮度：加法式亮度偏移")]
        public ClampedFloatParameter brightness = new ClampedFloatParameter(0.0f, -1.0f, 1.0f);

        [Tooltip("对比度：中间调对比扩张")]
        public ClampedFloatParameter contrast = new ClampedFloatParameter(1.0f, 0.0f, 10.0f);

        [Tooltip("Gamma：非线性色调映射（内部取倒数）")]
        public ClampedFloatParameter gamma = new ClampedFloatParameter(1.0f, 0.1f, 10.0f);

        [Tooltip("色相：色轮旋转")]
        public ClampedFloatParameter hue = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

        [Tooltip("饱和度：相对亮度的色彩浓度")]
        public ClampedFloatParameter saturation = new ClampedFloatParameter(1.0f, 0.0f, 2.0f);

        // ====================================================================
        // 底色
        // ====================================================================

        [Header("底色")]
        [Tooltip("底色：染色模式的颜色。保留扭曲画面的明暗结构（撕裂/色差/扫描线全量可见），只把色度染成此色")]
        public ColorParameter baseColor = new ColorParameter(Color.black, false);

        [Tooltip("染色强度。0 = 原色，1 = 完全染成底色；任何值下扭曲纹理的可见度不变")]
        public ClampedFloatParameter baseColorBlend = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

        // ====================================================================
        // IPostProcessComponent
        // ====================================================================

        /// <summary>强度大于 0 才需要渲染（Pass 依此跳过，避免无谓 Blit）。</summary>
        public bool IsActive() => intensity.value > 0.0f;

        /// <summary>URP 14 保留接口（是否仅在非 tile 架构生效），此效果无限制。</summary>
        public bool IsTileCompatible() => false;
    }
}
