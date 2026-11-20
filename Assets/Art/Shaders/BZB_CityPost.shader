// Blue Zone Bistro - city post-processing, one full-screen pass on the 3D city camera (City3DRenderer, added by
// Tools > Blue Zone Bistro > Generate Low-Poly City as a "Full Screen Pass" renderer feature with this material).
//   Outlines   dark lines where depth or normals jump (building rims, roof edges, trees), fading with distance.
//   Tilt-shift blur toward the top/bottom (and a little the sides): the "miniature model" look.
//   Grading    saturation, contrast and a cool/warm tint.
// Each part can be switched off at runtime (graphics panel): globals _BZB_Outline, _BZB_TiltShift, _BZB_Grade.
Shader "BlueZone/CityPost"
{
    Properties
    {
        [Header(Outlines)]
        _Thickness ("Line thickness (pixels)", Range(0.5, 3)) = 1.0
        _DepthSensitivity ("Depth sensitivity (relative jump)", Range(0.002, 0.2)) = 0.025
        _NormalSensitivity ("Normal sensitivity", Range(0.05, 1)) = 0.35
        _LineDarkness ("Line darkness", Range(0, 1)) = 0.4
        _LineFade ("Lines fade out at distance (m)", Float) = 1600
        [Header(Tilt shift)]
        _FocusCenter ("Focus band centre (0 bottom, 1 top)", Range(0, 1)) = 0.48
        _FocusWidth ("Sharp band half-height", Range(0, 0.5)) = 0.16
        _BlurRadius ("Max blur (pixels)", Range(0, 12)) = 5
        _SideBlur ("Side blur amount", Range(0, 1)) = 0.35
        [Header(Grading)]
        _Saturation ("Saturation", Range(0, 2)) = 1.0
        _Contrast ("Contrast", Range(0.5, 1.5)) = 1.04
        _Tint ("Tint (multiply)", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "CityPost"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _Thickness, _DepthSensitivity, _NormalSensitivity, _LineDarkness, _LineFade;
            float _FocusCenter, _FocusWidth, _BlurRadius, _SideBlur, _Saturation, _Contrast;
            float4 _Tint;
            float _BZB_Outline, _BZB_TiltShift, _BZB_Grade;   // 1 = on (set by BistroGraphics)

            half3 Scene(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }
            float EyeDepth(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half Edge(float2 uv)
            {
                float2 px = _Thickness * _ScreenSize.zw;
                float d0 = EyeDepth(uv);
                float d1 = EyeDepth(uv + float2(px.x, 0)), d2 = EyeDepth(uv - float2(px.x, 0));
                float d3 = EyeDepth(uv + float2(0, px.y)), d4 = EyeDepth(uv - float2(0, px.y));
                float depthJump = max(max(abs(d1 - d0), abs(d2 - d0)), max(abs(d3 - d0), abs(d4 - d0))) / max(d0, 0.001);
                float3 n0 = SampleSceneNormals(uv);
                float nd = 1.0 - min(min(dot(n0, SampleSceneNormals(uv + float2(px.x, 0))), dot(n0, SampleSceneNormals(uv - float2(px.x, 0)))),
                                     min(dot(n0, SampleSceneNormals(uv + float2(0, px.y))), dot(n0, SampleSceneNormals(uv - float2(0, px.y)))));
                half edge = saturate(step(_DepthSensitivity, depthJump) + step(_NormalSensitivity, nd));
                return edge * saturate(1.0 - d0 / max(1.0, _LineFade));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 color = Scene(uv);
                if (_BZB_TiltShift > 0.5 && _BlurRadius > 0.01)
                {
                    // Blur grows outside the sharp band (and slightly toward the sides). 12 taps on a golden-angle spiral.
                    float vertical = saturate((abs(uv.y - _FocusCenter) - _FocusWidth) / max(0.01, 0.5 - _FocusWidth));
                    float side = saturate(abs(uv.x - 0.5) * 2.0 - 0.4) * _SideBlur;
                    float amount = saturate(vertical * vertical + side * side);
                    float radius = _BlurRadius * amount;
                    if (radius > 0.3)
                    {
                        half3 sum = color; float count = 1;
                        [unroll] for (int i = 1; i <= 12; i++)
                        {
                            float a = i * 2.39996; float r = sqrt(i / 12.0) * radius;
                            sum += Scene(uv + float2(cos(a), sin(a)) * r * _ScreenSize.zw); count += 1;
                        }
                        color = sum / count;
                    }
                }
                if (_BZB_Outline > 0.5) color *= 1.0 - Edge(uv) * _LineDarkness;
                if (_BZB_Grade > 0.5)
                {
                    half luma = dot(color, half3(0.2126, 0.7152, 0.0722));
                    color = lerp(luma.xxx, color, _Saturation);
                    color = (color - 0.5) * _Contrast + 0.5;
                    color *= _Tint.rgb;
                }
                return half4(max(color, 0), 1);
            }
            ENDHLSL
        }
    }
}
