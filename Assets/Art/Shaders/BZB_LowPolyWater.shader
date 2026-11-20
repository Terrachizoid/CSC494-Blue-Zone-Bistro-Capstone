// Blue Zone Bistro - water (URP). Same lighting model as the city shader, plus:
//   - directional waves: four Gerstner-style waves (different lengths and angles around one wind direction) moving
//     over time; only the shading normal moves (the surface stays flat), so it is cheap;
//   - shallow water and foam: the generator puts a band along every shore whose UV0.y runs 0 at the shore -> 1
//     offshore. Colour goes from Shallow Colour to the deep vertex colour across it, and foam lines roll toward the
//     shore near 0 (UV0.x = material id: 9 shore band; 7 ponds = no foam);
//   - SSR (screen-space reflections): march the reflected ray across the screen against the depth buffer and take
//     the colour of what it hits from the opaque texture. Fades with ray length, near screen edges and when nothing
//     is hit, falling back to the reflection probe;
//   - Fresnel: more mirror-like at grazing angles.
// Drawn after the opaque city (queue Transparent, but writes depth and does not blend), because SSR reads the
// finished opaque image (_CameraOpaqueTexture) and depth (_CameraDepthTexture). BistroCity3D turns both on for the
// city camera. SSR assumes a perspective camera.
// Globals from BistroGraphics: _BZB_SSR (on/off), _BZB_SSRSteps, _BZB_SSRMaxDistance, _BZB_SSRThickness,
// _BZB_DisableReflections, _BZB_DebugView (4 = reflections only, 8 = SSR hit mask).
Shader "BlueZone/LowPolyWater"
{
    Properties
    {
        _Ka ("Indirect strength", Range(0, 2)) = 1.0
        _Kd ("Direct diffuse", Range(0, 2)) = 0.6
        _Ks ("Sun glint (Ks)", Range(0, 8)) = 3.0
        _Shininess ("Glint shininess", Range(1, 1024)) = 400
        _Kr ("Reflection strength", Range(0, 2)) = 1.0
        _F0 ("Fresnel F0", Range(0, 1)) = 0.02
        _Roughness ("Probe blur", Range(0, 1)) = 0.08
        _WaveStrength ("Wave strength (steepness)", Range(0, 1)) = 0.35
        _WaveScale ("Longest wavelength (m)", Range(1, 80)) = 18
        _WaveSpeed ("Wave speed", Range(0, 4)) = 1.0
        _WaveDirection ("Wind direction (degrees)", Range(0, 360)) = 200
        _ShallowColor ("Shallow colour", Color) = (0.42, 0.72, 0.74, 1)
        _FoamColor ("Foam colour", Color) = (0.96, 0.97, 0.95, 1)
        _FoamWidth ("Foam band width (0-1 of shore band)", Range(0.02, 0.6)) = 0.22
        _FoamStrength ("Foam strength", Range(0, 1)) = 0.85
        _ShadowStrength ("Shadow strength", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-100" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Ka, _Kd, _Ks, _Shininess, _Kr, _F0, _Roughness, _WaveStrength, _WaveScale, _WaveSpeed, _WaveDirection, _ShadowStrength;
                half4 _ShallowColor, _FoamColor; half _FoamWidth, _FoamStrength;
            CBUFFER_END
            float4 _BZB_SunDir;
            half4 _BZB_SunColor, _BZB_AmbientSky, _BZB_AmbientGround, _BZB_FogColor, _BZB_Tint;
            float4 _BZB_FogParams;
            float _BZB_DisableReflections, _BZB_DebugView, _BZB_SSR, _BZB_SSRSteps, _BZB_SSRMaxDistance, _BZB_SSRThickness;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 extra : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR; float2 extra : TEXCOORD2; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS; output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS); output.color = input.color;
                output.extra = input.extra;
                return output;
            }

            half3 Hemisphere(float y) { return lerp(_BZB_AmbientGround.rgb, _BZB_AmbientSky.rgb, saturate(y * 0.5 + 0.5)); }
            half3 Tint() { return _BZB_Tint.a > 0 ? _BZB_Tint.rgb : half3(1, 1, 1); }

            // Wave normal: sum of four directional waves (Gerstner-style slopes). Each wave: direction d, wavenumber
            // k = 2 pi / length, deep-water speed w = sqrt(9.8 k), amplitude from steepness. Slopes add up; the
            // surface itself stays flat (shading only).
            float3 WaveNormal(float3 positionWS)
            {
                float base = radians(_WaveDirection);
                float2 p = positionWS.xz;
                float2 slope = 0;
                float lengths[4] = { 1.0, 0.61, 0.37, 0.23 };
                float angles[4] = { 0.0, 0.42, -0.55, 0.9 };
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float lambda = max(0.5, _WaveScale * lengths[i]);
                    float k = 6.2831853 / lambda;
                    float w = sqrt(9.8 * k) * _WaveSpeed;
                    float2 d = float2(cos(base + angles[i]), sin(base + angles[i]));
                    float amplitude = _WaveStrength * 0.5 / k;           // steepness * lambda / (4 pi)
                    slope += d * (k * amplitude) * cos(k * dot(d, p) - w * _Time.y);
                }
                return normalize(float3(-slope.x, 1.0, -slope.y));
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }
            // Foam near the shore: a solid edge at the waterline plus lines that roll in toward it, broken up by noise.
            half Foam(float shore, float3 positionWS)
            {
                if (shore >= _FoamWidth) return 0;
                float t = shore / _FoamWidth;                                     // 0 at the waterline -> 1 at the band edge
                float breakup = Noise(positionWS.xz * 0.35 + _Time.y * 0.15);
                float lines = frac(t * 3.0 + _Time.y * 0.25 * _WaveSpeed + breakup * 0.35);
                float stripe = smoothstep(0.0, 0.12, lines) * (1.0 - smoothstep(0.12, 0.3, lines));
                float edge = 1.0 - smoothstep(0.0, 0.18, t);
                return saturate(edge + stripe * (1.0 - t) * step(0.45, breakup)) * _FoamStrength;
            }

            // Screen-space reflection. Returns rgb = hit colour, a = confidence (0 = missed, use the probe).
            half4 TraceSSR(float3 positionWS, float3 r)
            {
                int steps = clamp((int)_BZB_SSRSteps, 4, 128);
                float maxDistance = max(1.0, _BZB_SSRMaxDistance);
                float thickness = max(0.05, _BZB_SSRThickness);
                float stepLength = maxDistance / steps;
                float t = stepLength * 0.5;
                [loop] for (int i = 0; i < steps; i++)
                {
                    float3 p = positionWS + r * t;
                    float4 clip = TransformWorldToHClip(p);
                    if (clip.w <= 0) break;
                    float2 uv = float2(clip.x, clip.y * _ProjectionParams.x) / clip.w * 0.5 + 0.5;   // same as ComputeScreenPos
                    if (any(uv < 0.0) || any(uv > 1.0)) break;                   // left the screen: nothing to reflect
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                    float rayEye = clip.w;                                         // eye depth of the ray point (perspective)
                    float behind = rayEye - sceneEye;
                    // Hit when the ray has just passed behind a surface. Allow at least one step of depth, or a
                    // ray stepping metres at a time jumps through walls without ever counting as a hit.
                    if (behind > 0.0 && behind < max(thickness, stepLength * (1.5 + i * 0.06)))
                    {
                        // Refine: halve back toward the surface a few times for a sharper reflection.
                        float lo = t - stepLength * (1.0 + i * 0.04), hi = t;
                        [unroll] for (int k = 0; k < 4; k++)
                        {
                            float mid = (lo + hi) * 0.5;
                            float4 c2 = TransformWorldToHClip(positionWS + r * mid);
                            float2 uv2 = float2(c2.x, c2.y * _ProjectionParams.x) / c2.w * 0.5 + 0.5;
                            if (c2.w - LinearEyeDepth(SampleSceneDepth(uv2), _ZBufferParams) > 0.0) { hi = mid; uv = uv2; } else lo = mid;
                        }
                        float2 edge = min(uv, 1.0 - uv);
                        half edgeFade = saturate(min(edge.x, edge.y) * 10.0);     // fade near screen borders
                        half distanceFade = 1.0 - (float)i / steps;                // fade with ray length
                        return half4(SampleSceneColor(uv), edgeFade * distanceFade);
                    }
                    t += stepLength * (1.0 + i * 0.04);                            // slightly longer steps further out
                }
                return half4(0, 0, 0, 0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = WaveNormal(input.positionWS);
                int matId = (int)round(input.extra.x);
                float shore = matId == 9 ? input.extra.y : 1.0;                // 0 at the shore, 1 offshore
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 l = normalize(_BZB_SunDir.xyz);
                float3 h = normalize(l + v);
                half3 albedo = input.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    albedo = SRGBToLinear(albedo);
                #endif
                albedo = lerp(_ShallowColor.rgb, albedo, smoothstep(0.0, 1.0, shore));
                half nl = saturate(dot(n, l)), nv = saturate(dot(n, v));
                half shadow = lerp(1.0, MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS)), _ShadowStrength);
                half F = _F0 + (1.0 - _F0) * pow(1.0 - nv, 5.0);

                float3 r = reflect(-v, n);
                half3 probe = (_BZB_DisableReflections > 0.5) ? Hemisphere(r.y) : GlossyEnvironmentReflection(r, _Roughness, 1.0);
                half4 ssr = half4(0, 0, 0, 0);
                if (_BZB_SSR > 0.5 && _BZB_DisableReflections < 0.5) ssr = TraceSSR(input.positionWS, r);
                half3 reflection = lerp(probe, ssr.rgb, ssr.a) * _Kr;

                half3 diffuse = albedo * (1.0 - F) * (_Ka * Hemisphere(n.y) * Tint() + _Kd * _BZB_SunColor.rgb * nl * shadow);
                half3 glint = _Ks * _BZB_SunColor.rgb * pow(saturate(dot(n, h)), _Shininess) * shadow;
                half3 color = diffuse + F * reflection + glint;
                half foam = Foam(shore, input.positionWS);
                color = lerp(color, _FoamColor.rgb * (_Ka * Hemisphere(1.0) + _Kd * _BZB_SunColor.rgb * shadow), foam);

                int view = (int)_BZB_DebugView;
                if (view == 4) return half4(F * reflection + glint, 1);
                if (view == 8) return half4(ssr.aaa, 1);
                float fog = saturate((distance(_WorldSpaceCameraPos.xyz, input.positionWS) - _BZB_FogParams.x) / max(1.0, _BZB_FogParams.y - _BZB_FogParams.x)) * _BZB_FogParams.z;
                return half4(lerp(color, _BZB_FogColor.rgb, fog), 1.0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert2D
            #pragma fragment frag2D
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            half4 _BZB_AmbientSky;
            struct A { float4 positionOS : POSITION; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            V vert2D(A input) { V o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.color = input.color; return o; }
            half4 frag2D(V input) : SV_Target
            {
                half3 c = input.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    c = SRGBToLinear(c);
                #endif
                return half4(lerp(c, _BZB_AmbientSky.rgb, 0.25), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
