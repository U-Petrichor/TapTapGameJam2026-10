// 喷雾守卫的"雾壳"。挂在运行时生成的锥壳网格上（形状和判定锥完全同源）。
// 所有参数都由 GuardSprayView 用 MaterialPropertyBlock 每帧写：
//   _Sweep 0→1.2  雾从喷嘴往远端推
//   _Alpha 0→1    整体浓度
// 其余是静态的外观参数，在材质资源里调。
Shader "Jam/SprayCone"
{
    Properties
    {
        [MainColor] _Color ("Tint", Color) = (0.62, 1.0, 0.55, 1.0)
        _Alpha ("Alpha", Range(0, 1)) = 0.0
        _Sweep ("Sweep", Range(0, 1.2)) = 1.0
        _NoiseScale ("Noise Scale", Float) = 6.0
        _NoiseScroll ("Noise Scroll Speed", Float) = 2.5
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.55
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
            Name "SprayCone"
            Tags { "LightMode" = "UniversalForward" }

            // 透明雾：不写深度（不然自己叠自己的面会被裁掉），但保留深度测试，
            // 所以墙是会挡住雾的——和"墙会挡住判定"保持一致。
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Alpha;
                float  _Sweep;
                float  _NoiseScale;
                float  _NoiseScroll;
                float  _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                // x = 横向(角度) 0..1，y = 轴向(离喷嘴的距离) 0..1
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs position = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = position.positionCS;
                OUT.uv = IN.uv;
                return OUT;
            }

            // 便宜的值噪声：雾的纹理全靠它，不需要贴图
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float along = saturate(IN.uv.y);
                float across = abs(IN.uv.x - 0.5) * 2.0;

                // 轴向包络：喷嘴里侧稍淡（别糊住守卫自己），中段最浓，远端散掉
                float body = smoothstep(0.0, 0.15, along) * (1.0 - smoothstep(0.55, 1.0, along));

                // 横向柔边：锥的两侧化开，不要硬边
                float edge = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, across);

                // 两层沿轴向滚动的雾，打破"一整块半透明"的塑料感
                float2 noiseUV = float2(IN.uv.x * _NoiseScale,
                                        along * _NoiseScale * 0.55 - _Time.y * _NoiseScroll);
                float mist = ValueNoise(noiseUV) * 0.65 + ValueNoise(noiseUV * 2.3 + 17.0) * 0.35;
                mist = 0.45 + 0.55 * saturate(mist);

                // 喷出前沿：_Sweep 之前是雾，之后是空的，看起来就像从喷嘴推出去
                float front = 1.0 - smoothstep(_Sweep - 0.15, _Sweep, along);

                float alpha = body * edge * mist * front * _Alpha;
                return float4(_Color.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
