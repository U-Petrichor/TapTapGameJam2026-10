// 信息素可视化。挂在网格物体本身（Plane 或 Quad）上，和 GridManager 同物体。
//
// 坐标约定（和 GridManager 完全一致，见 GridManager.cs 顶部的说明）：
//   网格覆盖的世界矩形由 C# 用 Renderer.bounds 算出来，以 _GridOriginWS / _GridSpanWS 传进来。
//   格子 (0,0) 在 (min.x, min.z) 角，x 沿世界 +X，y 沿世界 +Z。
//   这里一律不用 mesh 的 uv：Plane 的 uv 在 XZ 平面、Quad 的 uv 在 XY 平面，两者 v 的朝向
//   还不一样，用 positionWS 反算可以完全绕开这些差异，换 mesh 也不用改 shader。
Shader "Jam/PheromoneOverlay"
{
    Properties
    {
        [MainTexture] _PheroTex ("Pheromone Field (R=Food G=Home B=Player A=Danger)", 2D) = "black" {}
        _GridOriginWS ("Grid Origin WS (min.x, min.z)", Vector) = (-5, -5, 0, 0)
        _GridSpanWS ("Grid Span WS (size.x, size.z)", Vector) = (10, 10, 0, 0)
        _GridSize ("Grid Size (cols, rows)", Vector) = (10, 10, 0, 0)
        [Toggle] _ShowPheromones ("Show Pheromones", Float) = 1
        [Toggle] _ShowGridLines ("Show Grid Lines", Float) = 1
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
            Name "PheromoneOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_PheroTex);
            SAMPLER(sampler_PheroTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _GridOriginWS;
                float4 _GridSpanWS;
                float4 _GridSize;
                float  _ShowPheromones;
                float  _ShowGridLines;
            CBUFFER_END

            // ---- 四路信息素的颜色，写死在这里 ----
            static const float3 C_FOOD   = float3(0.20, 1.00, 0.35); // 绿
            static const float3 C_HOME   = float3(0.25, 0.55, 1.00); // 蓝
            static const float3 C_PLAYER = float3(1.00, 0.90, 0.20); // 黄
            static const float3 C_DANGER = float3(1.00, 0.20, 0.15); // 红

            static const float3 C_GRID          = float3(0.85, 0.95, 1.00);
            static const float  GRID_LINE_ALPHA = 0.30;

            struct Attributes
            {
                // 故意不声明 uv：这个 shader 不依赖 mesh 的 uv
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

            // 世界 XZ → 网格 UV：0 = 网格左下角，1 = 右上角
            float2 WorldToGridUV(float3 positionWS)
            {
                return (positionWS.xz - _GridOriginWS.xy) / max(_GridSpanWS.xy, 1e-5);
            }

            // 在网格 UV 域数格子：gridUV 落在整数上就是格线。
            // 用 fwidth 把线宽换算成"像素"单位，近处远处一样粗，也不会摩尔纹。
            float GridLineMask(float2 gridUV)
            {
                float2 g  = gridUV;
                float2 px = fwidth(g);
                float2 d  = abs(frac(g - 0.5) - 0.5) / max(px, 1e-6); // 到最近格线的像素距离
                return 1.0 - saturate(min(d.x, d.y) * 1.4);           // 约 1.4 像素宽
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float2 gridUV = WorldToGridUV(IN.positionWS);

                // 两个开关，step 是无分支的 0/1
                float showPhero = step(0.5, _ShowPheromones);
                float showGrid  = step(0.5, _ShowGridLines);

                // ---- 信息素层：四个通道各乘一个写死的颜色，相加叠加 ----
                // 纹理尺寸 = 网格尺寸，所以 uv 直接就是归一化的网格坐标，格子中心落在 texel 中心
                float4 p = SAMPLE_TEXTURE2D(_PheroTex, sampler_PheroTex, saturate(gridUV)) * showPhero;
                float3 rgb = saturate(p.r * C_FOOD + p.g * C_HOME + p.b * C_PLAYER + p.a * C_DANGER);
                float  alpha = max(max(p.r, p.g), max(p.b, p.a));

                // ---- 网格线层：压在信息素上面 ----
                // 注意：变量不能叫 line，line 是 HLSL 保留字（几何着色器图元类型）
                float lineMask = GridLineMask(gridUV * _GridSize.xy) * showGrid;
                rgb   = lerp(rgb, C_GRID, lineMask);
                alpha = max(alpha, lineMask * GRID_LINE_ALPHA);

                // 只输出自己的颜色和透明度，不掺地板色
                return float4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
