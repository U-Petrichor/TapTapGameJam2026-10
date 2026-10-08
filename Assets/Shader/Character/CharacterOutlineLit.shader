// 角色描边：反向外扩（Inverted Hull）双 Pass，只对用了本 Shader 的材质生效。
//
// 原理
// ----
// 同一个网格画两遍：
//   Pass 1「Outline」：Cull Front 只画背面，顶点在**裁剪空间**沿法线的屏幕投影方向外扩
//                     固定像素数，输出纯色 _OutlineColor，写深度。
//   Pass 2「ForwardLit」：直接 UsePass 复用 URP/Lit 的本体 Pass，画正面并写深度。
//
// 为什么"只画背面"最后会变成一圈线
// -------------------------------
// 封闭网格上，从相机穿过轮廓内部的每条射线都是"进正面、出背面"，
// 所以**背面三角形的投影覆盖范围 = 正面的轮廓**。
// 也就是说 Pass 1 单独看并不是一圈线，而是一整块比角色略大的实心色块。
//
//    Pass 1 覆盖 = S ⊕ w   （剪影 S 向外扩 w 像素）
//    Pass 2 覆盖 = S
//    存活像素   = (S ⊕ w) \ S = 宽度约 w 的一圈
//
// 覆盖范围是超集，所以"内部全被覆盖掉"在几何上不可能发生，一定剩下一圈。
//
// 内部是靠**深度测试**挖掉的，不是靠绘制顺序：只改了 positionCS.xy、没动 z/w，
// 所以壳的深度精确等于原来那个背面的深度，永远在自己正面的后面；
// 正面片元用 ZTest LEqual 必然通过并覆盖掉它。两个 Pass 顺序反过来结果一样。
// 顺序在这里只是让"先铺底、再覆盖"更直观，真正的保证是 LEqual + 两侧都写深度。
//
// 为什么是"恒定像素宽"
// --------------------
// 裁剪空间到屏幕要除以 w（透视除法）。偏移加在 positionCS.xy 上，屏幕位移 = 偏移 / w，
// 所以让 偏移 = 目标NDC位移 * w，w 被抵消，无论远近、模型缩放如何，屏幕宽度都恒定。
// 正交相机 w == 1，同一份代码自然成立。
//
// 关于蒙皮
// --------
// 顶点用的是 IN.positionOS / IN.normalOS。蒙皮网格由 Unity 在顶点着色器之前完成蒙皮
// （GPU 蒙皮或 CPU 蒙皮都是如此），所以这里拿到的已经是形变后的数据，不需要额外处理。
//
// 已知限制（本项目已确认接受）
// ---------------------------
// * 外扩方向来自每个顶点自己的法线，硬边/分离顶点的模型在棱角处可能出现裂缝。
//   缓解：把 _OutlineWidth 调小；或离线烘焙平滑法线到 TEXCOORD3 后改用它做方向。
// * "内轮廓"也会有线：手臂压在躯干前时，手臂的壳比躯干表面更近，躯干那一片会被深度
//   测试挡掉，于是在躯干上也留下手臂的描边。这是外扩法的默认行为，卡通渲染通常正是
//   想要它；若只要最外轮廓，需要改用 stencil（先写角色掩码，只在掩码外画描边）。
// * 薄部件（手指、头发片、细天线）在 _OutlineWidth 偏大时，两侧的壳会互相盖过部件本体，
//   看起来糊在一起 —— 这也是描边宽度不能随便调大的原因。
// * 只改 xy 不改 z，描边环的深度是背面深度，角色紧贴墙面时描边可能被墙提前切掉一点。
//
// 渲染管线相关
// ------------
// * 已确认本项目两条质量档都不是延迟渲染：
//     PC_Renderer  m_RenderingMode: 2 (Forward+)
//     Mobile_Renderer m_RenderingMode: 0 (Forward)
//   两者也都没有开 Depth Priming（m_DepthPrimingMode: 0 = Disabled），
//   所以描边不会被深度预渲染层"ZTest Equal 失败"整片切掉。
// * Outline Pass 用显式的 SRPDefaultUnlit（而不是 UniversalForward）：
//   URP 的 DrawObjectsPass Tag 列表是 { SRPDefaultUnlit, UniversalForward,
//   UniversalForwardOnly }，它在第一位，所以描边会先于本体绘制。
//   注意正确性并不依赖这个顺序（内部是深度测试挖掉的，见上），写明只是为了意图清楚。
// * 如果以后把 Renderer 的 Rendering Mode 切成 Deferred/Deferred+，SRPDefaultUnlit 这个
//   Tag 不会被任何 Pass 绘制，描边会整个消失。届时需要：Outline Pass 改成
//   LightMode = UniversalForwardOnly，并补一个 UsePass ".../Lit/GBUFFER"（本体走 GBuffer）。
// * 本体之外还补齐了 ShadowCaster / DepthOnly / DepthNormals / Meta，
//   它们来自 URP/Lit 且用的是**未外扩**的原始顶点，所以描边不会污染阴影、
//   深度预渲染和 SSAO（PC_Renderer 上挂着 SSAO，缺 DepthNormals 会出问题）。
// * 因为 Outline Pass 的 UnityPerMaterial 布局和 UsePass 进来的 Lit Pass 不一致，
//   本 Shader 不是 SRP Batcher 兼容的，会退回普通批处理。只描边一个角色，可以忽略；
//   本项目 GPU Resident Drawer 也是关闭的（m_GPUResidentDrawerMode: 0）。
//
// 参数
// ----
//   _OutlineOn    布尔开关（关键字，关掉时描边三角形直接退化成零面积，不产生像素）
//   _OutlineColor 描边颜色
//   _OutlineWidth 描边宽度，单位是屏幕像素，基准屏幕高 1080（见 OUTLINE_REF_HEIGHT）

Shader "Jam/CharacterOutlineLit"
{
    Properties
    {
        [Header(Outline)]
        [Toggle(_OUTLINE_ON)] _OutlineOn("Enable Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0.05, 0.05, 0.08, 1)
        _OutlineWidth("Outline Width (px @1080p)", Range(0.0, 20.0)) = 3.0

        [Header(Base)]
        // ------------------------------------------------------------------
        // 下面这一整段必须和 "Universal Render Pipeline/Lit" 的属性**完全同名同类型**。
        // 因为本体 Pass 是用 UsePass 从 URP/Lit 拉过来的，它按名字去 UnityPerMaterial
        // 里取参数；这里少声明一个，对应参数在材质上就是默认值 0（例如 _BaseMap 拿不到
        // 就会变黑）。修改时请和 Lit.shader 的 Properties 保持同步。
        // ------------------------------------------------------------------

        // Specular vs Metallic workflow
        _WorkflowMode("WorkflowMode", Float) = 1.0

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        // SRP batching compatibility for Clear Coat (Not used in Lit)
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ==================================================================
        // Pass 1：描边。必须排在 ForwardLit 之前。
        // ==================================================================
        Pass
        {
            Name "Outline"
            // SRPDefaultUnlit 是 URP DrawObjectsPass Tag 列表里的第一个，早于 UniversalForward。
            // 内部那片是靠在下的 ForwardLit 用深度测试挖掉的（见文件头说明），
            // 这里的顺序只是让"先铺底、再覆盖"更直观，不是正确性的前提。
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front          // 只画背面，被推出剪影的那部分才是可见描边
            ZWrite On           // 写深度，避免描边被后面的物体穿透
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma shader_feature_local _OUTLINE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 注意：这里的 UnityPerMaterial 布局和 UsePass 进来的 Lit Pass 不同，
            // 所以本 Shader 不参与 SRP Batcher（只描边一个角色，无影响）。
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _OutlineOn;
            CBUFFER_END

            // 宽度的基准屏幕高度。描边宽度按屏幕高度等比缩放，
            // 这样 1080p / 2K / 手机高 DPI 上看起来粗细一致。
            #define OUTLINE_REF_HEIGHT 1080.0

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings OutlineVert(Attributes IN)
            {
                Varyings OUT;

            #if defined(_OUTLINE_ON)
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);

                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 normalCS = TransformWorldToHClipDir(normalWS);

                // 宽度换算到当前屏幕：以 1080p 高度为基准等比缩放
                float  widthPx = _OutlineWidth * (_ScreenParams.y / OUTLINE_REF_HEIGHT);

                // 法线在屏幕上的投影方向，换算到"像素空间"后再归一化，
                // 否则屏幕非正方形时描边的横竖粗细会不一致。
                float2 dirPx = normalize(normalCS.xy * _ScreenParams.xy + 1e-6);

                // 像素位移 → NDC 位移（1 NDC = 半屏像素）→ 裁剪空间位移。
                // 乘 positionCS.w 抵消透视除法，得到与距离无关的恒定像素宽度。
                float2 offsetNDC = dirPx * widthPx * 2.0 / _ScreenParams.xy;
                positionCS.xy += offsetNDC * positionCS.w;

                OUT.positionCS = positionCS;
            #else
                // 关闭描边：把三个顶点收到同一个位置，三角形零面积、
                // 不光栅化也不写深度，等于这个 Pass 不存在。
                OUT.positionCS = float4(0.0, 0.0, 0.0, 1.0);
            #endif

                return OUT;
            }

            half4 OutlineFrag(Varyings IN) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1.0);
            }
            ENDHLSL
        }

        // ==================================================================
        // Pass 2：本体，直接复用 URP/Lit 的各 Pass。
        // UsePass 的 Pass 名必须大写（Unity 内部按大写索引）。
        // ShadowCaster / DepthOnly / DepthNormals / Meta 缺一不可：
        // 前者掉阴影，后三者会破坏深度预渲染与 SSAO。
        // ==================================================================
        UsePass "Universal Render Pipeline/Lit/FORWARDLIT"
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
        UsePass "Universal Render Pipeline/Lit/META"
    }

    // 老管线兜底：万一没跑 URP，至少不至于整个角色消失
    FallBack "Universal Render Pipeline/Lit"
}
