// 「群颚破壁」的裂纹覆盖层。
//
// 用法：由 WallFoodItem 在墙体 / 桌子的 MeshRenderer 下建一个同 mesh 的子物体，
//       材质用这一份，进度用 MaterialPropertyBlock 单独推（_EatProgress 0..1）。
//
// 关键点：
//   * 不用几何外扩来"贴"在墙面上 —— 墙体普遍是非等比缩放（Wall1 的 z = 0.46），
//     按法线推出去会被缩放拉歪。压面靠 Offset -1,-1 + ZTest LEqual。
//   * 裂纹是程序化的（fbm 的 0.5 等值线 = 天然分叉的裂缝形状），不需要任何贴图资产，
//     也不依赖 mesh 的 uv（墙角、不同朝向的面都不会被拉伸）。
//   * 进度只控制"阈值"：0 的时候阈值设到 1 以上，一条裂缝都不出现；
//     1 的时候阈值降下来，裂缝变粗变多，整面墙布满。
Shader "Jam/WallCrack"
{
    Properties
    {
        _CrackColor ("Crack Color", Color) = (0.04, 0.03, 0.02, 1)
        _CrackAlpha ("Crack Opacity", Range(0, 1)) = 0.95
        _CrackScale ("Crack Scale (world units)", Float) = 1.6
        _CrackSoft ("Crack Softness", Range(0.001, 0.5)) = 0.10
        _WarpStrength ("Warp Strength", Range(0, 1)) = 0.35

        _EatProgress ("Eat Progress (0..1)", Range(0, 1)) = 0
        _StartThreshold ("Threshold At 0%", Range(0, 1.5)) = 1.02
        _EndThreshold ("Threshold At 100%", Range(0, 1)) = 0.34

        _HaloColor ("Halo Color", Color) = (0.35, 0.30, 0.24, 1)
        _HaloAlpha ("Halo Opacity", Range(0, 1)) = 0.18
        _HaloWidth ("Halo Width", Range(0.01, 0.5)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
        }

        Pass
        {
            Name "WallCrack"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            // 负值的多边形偏移把覆盖层往摄像机方向拉一格，稳定压在墙体表面之上，不会 z-fighting
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CrackColor;
                float4 _HaloColor;
                float  _CrackAlpha;
                float  _CrackScale;
                float  _CrackSoft;
                float  _WarpStrength;
                float  _EatProgress;
                float  _StartThreshold;
                float  _EndThreshold;
                float  _HaloAlpha;
                float  _HaloWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                return OUT;
            }

            // ---- 3D 值噪声（Dave Hoskins 的 hash + 三线性插值）----
            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash13(i + float3(0.0, 0.0, 0.0));
                float n100 = Hash13(i + float3(1.0, 0.0, 0.0));
                float n010 = Hash13(i + float3(0.0, 1.0, 0.0));
                float n110 = Hash13(i + float3(1.0, 1.0, 0.0));
                float n001 = Hash13(i + float3(0.0, 0.0, 1.0));
                float n101 = Hash13(i + float3(1.0, 0.0, 1.0));
                float n011 = Hash13(i + float3(0.0, 1.0, 1.0));
                float n111 = Hash13(i + float3(1.0, 1.0, 1.0));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                float norm = 0.0;

                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    sum  += ValueNoise(p) * amp;
                    norm += amp;
                    p    *= 2.03;
                    amp  *= 0.5;
                }

                return sum / norm;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float progress = saturate(_EatProgress);

                // 世界空间取样：不依赖 mesh 的 uv，不同朝向的墙面密度一致
                float3 p = IN.positionWS * _CrackScale;

                // 低频扭曲，把规整的等值线掰成有机的走向
                float3 warp = float3(
                    ValueNoise(p * 0.37),
                    ValueNoise(p * 0.37 + 17.3),
                    ValueNoise(p * 0.37 + 41.7)) - 0.5;
                p += warp * (_WarpStrength * 2.0);

                // fbm 的 0.5 等值线 = 分叉的裂缝。取 1-|2n-1| 让等值线处接近 1
                float ridge = 1.0 - abs(2.0 * Fbm(p) - 1.0);

                // 进度越高阈值越低：裂缝从"一条都没有"长到"布满整面墙"
                float threshold = lerp(_StartThreshold, _EndThreshold, progress);
                float crack = smoothstep(threshold, threshold + _CrackSoft, ridge);

                // 裂缝周围的污损光晕，让裂口看起来有深度
                float halo = smoothstep(threshold - _HaloWidth, threshold, ridge) * _HaloAlpha;

                float alpha = max(crack * _CrackAlpha, halo);
                float3 rgb = lerp(_HaloColor.rgb, _CrackColor.rgb, crack);

                return float4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
