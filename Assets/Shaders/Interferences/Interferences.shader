// ============================================================================
// Interferences —— 全屏信号干扰后处理（团结引擎 1.9.3 / URP 14.2）
//
// 还原 "Glitches: Interferences" 的双层信号劣化：
//   Stage 1   · Distortion —— 循环扭曲：每周期 2 次明显撕裂脉冲 + 常态小扭曲（Simplex 噪声 + RGB 色差）
//   Stage 2   · Scanlines  —— 屏幕空间坐标调制的横向暗带（受噪声调制产生有机闪烁）
//   Stage 3   · Grading    —— 对比度 → 色相 → Gamma → 饱和度（标准图像处理顺序）
//   Stage 3.5 · BaseColor  —— 底色染色：保留扭曲画面亮度结构（纹理全量可见），只替换色度
//
// 关键设计：撕裂用 noise² 作为幅度，得到尖锐的间歇性撕裂而非平滑连续扭曲。
// ============================================================================
Shader "Hidden/PostProcessing/Interferences"
{
    HLSLINCLUDE

    #pragma target 3.0
    #pragma editor_sync_compilation

    // include 顺序必须与 URP 自带后处理（如 Bloom.shader）一致：
    // Blit.hlsl 内部用了 TEXTURE2D_X / SAMPLE_TEXTURE2D_X 等 XR 宏，
    // 而这些宏定义在 **URP 的** ShaderLibrary/Core.hlsl 里（不在 core 包）。
    // 所以 Core.hlsl 必须先于 Blit.hlsl，否则报 unrecognized identifier 'TEXTURE2D_X'。
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    // === 参数 ===
    float _Intensity;          // 总强度 [0,1]，0 = 关闭

    // === 循环扭曲：每周期 2 次明显扭曲脉冲，其余时间为小扭曲 ===
    float _CycleInterval;      // 循环间隔（秒）：每隔多久出现一轮明显扭曲

    // 明显扭曲（脉冲窗口期参数）
    float _BurstDistortion;    // 强度：最大横向 UV 位移 [0,5]
    float _BurstSpeed;         // 速度：噪声时间演化速率 [0,100]
    float _BurstFrequency;     // 频率：撕裂带纵向频率 [0.1,10]
    float _BurstOffset;        // 色差：R/B 通道像素错位量 [0,20]

    // 小扭曲（常态参数）
    float _IdleDistortion;     // 强度 [0,2]
    float _IdleSpeed;          // 速度 [0,100]
    float _IdleFrequency;      // 频率 [0.1,10]
    float _IdleOffset;         // 色差像素错位量 [0,20]

    float _Scanlines;          // 扫描线整体可见度 [0,1]
    float _ScanlinesDensity;   // 扫描线间距 [0,1]
    float _ScanlinesOpacity;   // 单根扫描线压暗强度 [0,1]

    float _Brightness;         // 亮度加法偏移 [-1,1]
    float _Contrast;           // 中间调对比扩张 [0,10]
    float _Gamma;              // 非线性色调映射（取倒数）[0.1,10]
    float _Hue;                // 色轮旋转 [0,1]
    float _Saturation;         // 饱和度 [0,2]

    float4 _BaseColor;         // 底色：染色模式的颜色（保留扭曲画面亮度结构，只替换色度）
    float _BaseColorBlend;     // 染色强度 [0,1]；扭曲纹理始终全量可见，不受此参数压缩

    // ------------------------------------------------------------------------
    // 2D Simplex 噪声（Ashima / Ian McEwan 实现，无纹理依赖）
    // ------------------------------------------------------------------------
    float3 Mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
    float2 Mod289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
    float3 Permute(float3 x)  { return Mod289((x * 34.0 + 10.0) * x); }

    float SimplexNoise(float2 v)
    {
        const float4 C = float4(0.211324865405187,   // (3-sqrt(3))/6
                                0.366025403784439,   // 0.5*(sqrt(3)-1)
                               -0.577350269189626,   // -1+2*C.x
                                0.024390243902439);  // 1/41

        // 第一个角
        float2 i  = floor(v + dot(v, C.yy));
        float2 x0 = v - i + dot(i, C.xx);

        // 其余两个角
        float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
        float4 x12 = x0.xyxy + C.xxzz;
        x12.xy -= i1;

        // 排列
        i = Mod289(i);
        float3 p = Permute(Permute(i.y + float3(0.0, i1.y, 1.0)) + i.x + float3(0.0, i1.x, 1.0));

        float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
        m = m * m;
        m = m * m;

        // 梯度
        float3 x  = 2.0 * frac(p * C.www) - 1.0;
        float3 h  = abs(x) - 0.5;
        float3 ox = floor(x + 0.5);
        float3 a0 = x - ox;

        // 归一化梯度隐式缩放
        m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);

        float3 g;
        g.x  = a0.x * x0.x  + h.x * x0.y;
        g.yz = a0.yz * x12.xz + h.yz * x12.yw;

        return 130.0 * dot(m, g);
    }

    // ------------------------------------------------------------------------
    // 色彩工具
    // ------------------------------------------------------------------------
    float Luminance601(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

    float3 RGB2HSV(float3 c)
    {
        float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
        float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
        float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
        float d = q.x - min(q.w, q.y);
        const float e = 1.0e-10;
        return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
    }

    float3 HSV2RGB(float3 c)
    {
        float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
        float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
        return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
    }

    // 色彩校正：对比度扩张 → 色相旋转 → Gamma → 饱和度
    float3 ApplyGrading(float3 color)
    {
        color = saturate(color + _Brightness);
        color = saturate((color - 0.5) * _Contrast + 0.5);

        float3 hsv = RGB2HSV(color);
        hsv.x = frac(hsv.x + _Hue);
        color = HSV2RGB(hsv);

        color = pow(max(color, 1.0e-5), 1.0 / _Gamma);

        float lum = Luminance601(color);
        color = lerp(lum.xxx, color, _Saturation);

        return saturate(color);
    }

    // ------------------------------------------------------------------------
    // 主体：干扰合成
    // ------------------------------------------------------------------------
    float4 FragInterferences(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = input.texcoord;
        float4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

        // ---- Stage 1: Distortion（循环模式）----
        // 周期包络：每 _CycleInterval 秒出现 2 个明显扭曲脉冲窗口（平方包络 → 尖锐间歇），
        // 窗口内用"明显扭曲"参数，其余时间用"小扭曲"参数，两套噪声按包络平滑混合。
        float T   = max(_CycleInterval, 0.1);
        float ct  = frac(_Time.y / T);                    // 周期内归一化进度 [0,1)
        float d1  = saturate(1.0 - abs(ct - 0.15) / 0.08); // 脉冲1 窗口（中心 0.15，半宽 0.08）
        float d2  = saturate(1.0 - abs(ct - 0.45) / 0.08); // 脉冲2 窗口（中心 0.45，半宽 0.08）
        float env = max(d1 * d1, d2 * d2);                 // 平方 → 峰值尖锐、间歇感强

        // 小扭曲（常态）：低强度撕裂
        float ti = _Time.y * _IdleSpeed;
        float n1i = SimplexNoise(float2(ti, uv.y * _IdleFrequency));
        float n2i = SimplexNoise(float2(ti * 0.5, uv.y * _IdleFrequency * 8.0));
        float shiftIdle = (n1i * n1i * _IdleDistortion + n2i * _IdleDistortion * 0.1) * sign(n1i) * 0.1;

        // 明显扭曲（脉冲期）：高强度撕裂
        float tb = _Time.y * _BurstSpeed;
        float n1b = SimplexNoise(float2(tb, uv.y * _BurstFrequency));
        float n2b = SimplexNoise(float2(tb * 0.5, uv.y * _BurstFrequency * 8.0));
        float shiftBurst = (n1b * n1b * _BurstDistortion + n2b * _BurstDistortion * 0.1) * sign(n1b) * 0.1;

        // 按包络混合位移与主噪声（n1mix 供扫描线调制复用）
        float shift = lerp(shiftIdle, shiftBurst, env);
        float n1 = lerp(n1i, n1b, env);

        // ---- 色差：全屏 R/B 通道横向反向偏移（与 shift 解耦，常驻生效）----
        // offset 按包络混合（脉冲期错位更大）；像素数语义，按屏幕宽度归一化，分辨率无关。
        float offsetNow = lerp(_IdleOffset, _BurstOffset, env);
        float chroma = (offsetNow * _ScreenParams.x) / max(_ScreenParams.x, 1.0);

        float2 uvR = float2(uv.x + shift + chroma, uv.y);
        float2 uvG = float2(uv.x + shift,          uv.y);
        float2 uvB = float2(uv.x + shift - chroma, uv.y);

        float3 distorted;
        distorted.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uvR)).r;
        distorted.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uvG)).g;
        distorted.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uvB)).b;

        // ---- Stage 2: Scanlines ----
        // 屏幕空间纵向坐标调制出交替横带，密度越高线越密
        float lineFreq = lerp(80.0, 900.0, _ScanlinesDensity);
        float scan = sin(uv.y * lineFreq);

        // 受撕裂噪声调制的不透明度 → 有机闪烁（而非死板固定纹路）
        float scanNoise = saturate(0.6 + 0.4 * abs(n1));
        float scanMask = saturate(scan * 0.5 + 0.5);
        float darken = _ScanlinesOpacity * scanNoise * _Scanlines * scanMask;

        distorted *= (1.0 - darken);

        // ---- Stage 3: Grading + 混合 ----
        float3 graded = ApplyGrading(distorted);

        // ---- Stage 3.5: 底色染色（亮度结构保留，不压扭曲纹理）----
        // 用扭曲画面的亮度（承载撕裂/色差/扫描线的全部明暗结构）乘以底色的"归一化色度"：
        // 无论 _BaseColorBlend 多大，扭曲纹理始终全量可见；blend 只控制染成底色的程度。
        float lum = Luminance601(graded);
        float3 baseChroma = _BaseColor.rgb / max(Luminance601(_BaseColor.rgb), 1.0e-3);
        graded = saturate(lerp(graded, lum * baseChroma, _BaseColorBlend));

        float3 final = lerp(original.rgb, graded, _Intensity);

        return float4(final, original.a);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off Blend Off

        Pass
        {
            Name "Interferences"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragInterferences
            ENDHLSL
        }
    }

    Fallback Off
}
