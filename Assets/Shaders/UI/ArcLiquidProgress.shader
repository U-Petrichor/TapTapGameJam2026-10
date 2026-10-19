Shader "TapTapGameJam/UI/Arc Liquid Progress"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _LiquidColor ("Liquid", Color) = (1,.19,.055,1)
        _HighlightColor ("Highlight", Color) = (1,.79,.28,1)
        _Progress ("Progress", Range(0,1)) = .55
        _LiquidTime ("Animation time", Float) = 0
        _Arc ("Center / start / sweep", Vector) = (.5,.439,3.59538,3.00197)
        _Radii ("Inner / outer / wave / speed", Vector) = (.232,.34,.035,1)
        _SpriteUV ("Sprite UV", Vector) = (0,0,1,1)
        _Aspect ("Sprite aspect", Float) = .94466
        _Bubbles ("Bubbles", Float) = .8
        _Surge ("Surge", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 mask : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color, _TextureSampleAdd, _LiquidColor, _HighlightColor;
            float4 _ClipRect, _Arc, _Radii, _SpriteUV;
            float _Progress, _LiquidTime, _Aspect, _Bubbles, _Surge;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.color = v.color * _Color;
                float2 pixelSize = o.vertex.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                o.mask = float4(v.vertex.xy * 2 - rect.xy - rect.zw, .25 / (.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = (i.uv - _SpriteUV.xy) / max(_SpriteUV.zw - _SpriteUV.xy, .00001);
                float2 p = (uv - _Arc.xy) * float2(1, _Aspect);
                float radius = length(p);
                // Clockwise distance from the sealed lower-left entrance.
                float angle = atan2(p.y, p.x);
                float along = frac((_Arc.z - angle) / 6.2831853 + 1) * 6.2831853 / _Arc.w;
                float across = saturate((radius - _Radii.x) / max(.001, _Radii.y - _Radii.x));
                float t = _LiquidTime * _Radii.w;
                float wave = sin(across * 6.7 - t * 4.6) * .6 + sin(across * 12.3 + t * 6.1) * .27 + sin(across * 19 - t * 3.7) * .13;
                float ends = saturate(_Progress * 16) * saturate((1 - _Progress) * 16);
                float front = _Progress + wave * _Radii.z * ends * (1 + _Surge * 1.5);
                float aa = max(fwidth(along), .001);
                float fill = 1 - smoothstep(front - aa, front + aa, along);
                fill *= step(.0001, _Progress);
                if (_Progress > .9999) fill = 1;
                float head = (1 - smoothstep(.008, .065, abs(front - along))) * ends;
                float flow = sin(along * 39 - t * 4 + sin(across * 11 + t) * 1.5);
                float streak = pow(saturate(sin(across * 22 + sin(along * 18 - t * 3) * .7)), 12);
                float body = .65 + .26 * sin(across * 3.14159) + .07 * flow;
                float3 col = _LiquidColor.rgb * body;
                col = lerp(col, _HighlightColor.rgb, streak * .2 + head * .58);
                float rim = exp(-pow((across - .86) * 29, 2)) * .8 + exp(-pow((across - .12) * 25, 2)) * .28;
                col = lerp(col, _HighlightColor.rgb, saturate(rim));
                // Bubbles travel in polar coordinates, so their paths follow the pipe.
                float bubbles = 0;
                [unroll] for (int n = 0; n < 7; n++)
                {
                    float seed = n * 1.618;
                    float bx = frac(seed * .731 + t * (.055 + n * .004));
                    float by = .23 + .54 * (.5 + .5 * sin(seed * 7 + t * .8));
                    float2 d = float2((along - bx) * _Arc.w * max(radius, .24), (across - by) * (_Radii.y - _Radii.x));
                    float r = .004 + .0025 * frac(seed * 3.7);
                    float dist = length(d);
                    float edgeAA = max(fwidth(dist), .0006);
                    bubbles += (1 - smoothstep(edgeAA, edgeAA * 2, abs(dist - r))) * .65;
                }
                col = lerp(col, _HighlightColor.rgb, saturate(bubbles * _Bubbles));
                float fullPulse = smoothstep(.97, 1, _Progress) * (.5 + .5 * sin(t * 5)) * .14;
                col += _HighlightColor.rgb * fullPulse;
                fixed4 artwork = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                // The artwork also contains black comic dots outside the white pipe.
                // Only its white region is liquid; retain the original dots underneath.
                float pipe = artwork.a * smoothstep(.3, .8, dot(artwork.rgb, float3(.299,.587,.114)));
                fixed4 c = fixed4(col, pipe * fill * _LiquidColor.a) * i.color;
#ifdef UNITY_UI_CLIP_RECT
                float2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                c.a *= m.x * m.y;
#endif
#ifdef UNITY_UI_ALPHACLIP
                clip(c.a - .001);
#endif
                return c;
            }
            ENDCG
        }
    }
}
