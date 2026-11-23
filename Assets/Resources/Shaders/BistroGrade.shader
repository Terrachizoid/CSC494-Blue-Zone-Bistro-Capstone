// File responsibility: 2D colour grade for UI artwork (scenery and guests). UI/Default plus, in order:
// a colour lookup table (scenery moods), tone curves (a 256x1 ramp baked from AnimationCurves, per channel), saturation, contrast, brightness, a colour tint
// that keeps brightness, and haze toward a fog colour. Values come from BistroColorGrade; Canvas masks still work.
// Lives in Resources so web builds always include it (Resources.Load in BistroColorGrade).
Shader "BlueZone/UIGrade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Lut ("Colour Lookup (256x16)", 2D) = "white" {}
        _LutAmount ("Lookup Amount", Range(0,1)) = 0
        _ToneRamp ("Tone Ramp", 2D) = "gray" {}
        _ToneAmount ("Tone Amount", Range(0,1)) = 0
        _Saturation ("Saturation", Float) = 1
        _Contrast ("Contrast", Float) = 1
        _Brightness ("Brightness", Float) = 1
        _GradeTint ("Grade Tint", Color) = (1,1,1,1)
        _GradeTintAmount ("Grade Tint Amount", Range(0,1)) = 0
        _Fog ("Fog", Range(0,1)) = 0
        _FogColor ("Fog Color", Color) = (0.8,0.8,0.8,1)

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
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _ToneRamp;
            sampler2D _Lut;
            half _LutAmount;

            // 16x16x16 lookup stored as 16 squares side by side (x = blue square + red, y = green), gamma-space values.
            half3 LutSample(half3 c)
            {
                #ifndef UNITY_COLORSPACE_GAMMA
                c = LinearToGammaSpace(c);
                #endif
                c = saturate(c);
                float b = c.b * 15.0;
                float b0 = floor(b);
                float b1 = min(b0 + 1.0, 15.0);
                float2 rg = float2(c.r * 15.0 + 0.5, c.g * 15.0 + 0.5);
                half3 s0 = tex2D(_Lut, float2((b0 * 16.0 + rg.x) / 256.0, rg.y / 16.0)).rgb;
                half3 s1 = tex2D(_Lut, float2((b1 * 16.0 + rg.x) / 256.0, rg.y / 16.0)).rgb;
                half3 o = lerp(s0, s1, b - b0);
                #ifndef UNITY_COLORSPACE_GAMMA
                o = GammaToLinearSpace(o);
                #endif
                return o;
            }
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            half _ToneAmount, _Saturation, _Contrast, _Brightness, _GradeTintAmount, _Fog;
            fixed4 _GradeTint, _FogColor;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                half3 c = saturate(color.rgb);
                c = lerp(c, LutSample(c), _LutAmount);
                half3 toned = half3(tex2D(_ToneRamp, float2(c.r, 0.5)).r,
                                    tex2D(_ToneRamp, float2(c.g, 0.5)).g,
                                    tex2D(_ToneRamp, float2(c.b, 0.5)).b);
                c = lerp(c, toned, _ToneAmount);
                const half3 luma = half3(0.299, 0.587, 0.114);
                half grey = dot(c, luma);
                c = lerp(grey.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                c *= _Brightness;
                half tintLuma = max(dot(_GradeTint.rgb, luma), 0.01);
                c = lerp(c, c * (_GradeTint.rgb / tintLuma), _GradeTintAmount);
                c = lerp(c, _FogColor.rgb, _Fog);
                color.rgb = saturate(c);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
