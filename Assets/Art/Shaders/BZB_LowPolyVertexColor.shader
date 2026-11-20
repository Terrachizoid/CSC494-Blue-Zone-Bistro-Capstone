// Blue Zone Bistro - city shading model (URP). Owen's lighting/shading work starts here.
//
//   final = albedo * (1 - F) * (indirectDiffuse + direct sun diffuse)      diffuse, view-independent
//         + F * indirectSpecular + direct sun specular                      specular, view-dependent
//   then fog.
//
//   albedo           vertex RGB (sRGB). (Vertex alpha = old gloss mask, now only used by the 2D-renderer preview pass.)
//   indirectDiffuse  baked lightmap (looked up by the mesh's lightmap UV) when the city has been baked,
//                    otherwise CityLighting's sky/ground hemisphere (looked up by the normal N). * Ka.
//   direct diffuse   Kd * sun colour * max(N.L, 0) * shadow   (sun = CityLighting's Sun, live).
//   material         each vertex carries a material id (UV0.x) picking a row of the table below:
//                    F0 reflectance, roughness (reflection blur), specular strength, shininess. Owen tunes these.
//   F                Schlick Fresnel: F0 + (1 - F0)(1 - N.V)^5 with the material's F0.
//   indirectSpecular reflection probe cubemap looked up along R = reflect(-V, N), blur level from roughness
//                    ("split sum" prefiltered mips). * Kr. Falls back to the hemisphere if reflections are off.
//   direct specular  material specular * Ks * (shininess + 8) / (8 pi) * sun colour * max(N.H, 0)^shininess * N.L * shadow
//                    (energy-normalised Blinn-Phong: a sharper highlight is also brighter). Not scaled by F,
//                    so sun glints on glossy surfaces stay visible from above.
//   AO               URP SSAO (if the renderer feature is on) darkens indirect light; baked AO is inside the lightmap.
//
// Global switches set by BistroGraphics (runtime settings / debug panel):
//   _BZB_DisableBakedGI   1 = ignore the lightmap, use the hemisphere (compare baked vs not)
//   _BZB_DisableReflections 1 = no probe reflections
//   _BZB_DebugView        0 final, 1 albedo, 2 indirect diffuse, 3 direct light, 4 specular/reflections,
//                         5 ambient occlusion, 6 normals, 7 shadow
// Passes: ForwardLit (3D renderer), Universal2D (the project's 2D renderer and scene view: hemisphere only),
// Meta (tells the lightmapper each surface's colour, so bounced light picks up colour), ShadowCaster, DepthOnly,
// DepthNormals (for SSAO). Facets are flat; two-sided.
Shader "BlueZone/LowPolyBlinnPhong"
{
    Properties
    {
        _Ka ("Indirect (ambient/baked) strength", Range(0, 2)) = 1.0
        _Kd ("Direct diffuse (Kd)", Range(0, 2)) = 0.9
        _Ks ("Direct specular multiplier (all materials)", Range(0, 4)) = 1.0
        _Kr ("Reflection multiplier (all materials)", Range(0, 2)) = 1.0
        _ShadowStrength ("Shadow strength", Range(0, 1)) = 0.75
        [Header(Material table  x F0 reflectance  y roughness  z specular  w shininess)]
        _M0 ("0 Plaster walls", Vector) = (0.04, 0.85, 0.15, 12)
        _M1 ("1 Glass windows", Vector) = (0.35, 0.08, 2.0, 220)
        _M2 ("2 Metal roofs/bands", Vector) = (0.55, 0.30, 1.2, 60)
        _M3 ("3 Stone/paving", Vector) = (0.04, 0.70, 0.25, 20)
        _M4 ("4 Foliage/grass", Vector) = (0.03, 0.60, 0.35, 16)
        _M5 ("5 Paint/tiles/boats", Vector) = (0.08, 0.40, 0.60, 48)
        _M6 ("6 Sand", Vector) = (0.03, 0.90, 0.10, 8)
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _Ka, _Kd, _Ks, _Kr, _ShadowStrength;
            float4 _M0, _M1, _M2, _M3, _M4, _M5, _M6;
        CBUFFER_END
        // From CityLighting (linear colours). Sun direction points from the surface toward the sun.
        float4 _BZB_SunDir;
        half4 _BZB_SunColor, _BZB_AmbientSky, _BZB_AmbientGround, _BZB_FogColor, _BZB_Tint;
        float4 _BZB_FogParams; // x start, y end, z on (1/0)
        // From BistroGraphics.
        float _BZB_DisableBakedGI, _BZB_DisableReflections, _BZB_DebugView;

        half3 Albedo(half4 vertexColor)
        {
            #if !defined(UNITY_COLORSPACE_GAMMA)
                return SRGBToLinear(vertexColor.rgb);
            #else
                return vertexColor.rgb;
            #endif
        }
        half3 Hemisphere(float y) { return lerp(_BZB_AmbientGround.rgb, _BZB_AmbientSky.rgb, saturate(y * 0.5 + 0.5)); }
        half3 Tint() { return _BZB_Tint.a > 0 ? _BZB_Tint.rgb : half3(1, 1, 1); }
        half3 ApplyFog(half3 color, float3 positionWS)
        {
            float fog = saturate((distance(_WorldSpaceCameraPos.xyz, positionWS) - _BZB_FogParams.x) / max(1.0, _BZB_FogParams.y - _BZB_FogParams.x)) * _BZB_FogParams.z;
            return lerp(color, _BZB_FogColor.rgb, fog);
        }
        half Fresnel(half f0, half nv) { return f0 + (1.0 - f0) * pow(1.0 - nv, 5.0); }
        float4 Material(float id)
        {
            int i = (int)round(id);
            return i == 1 ? _M1 : i == 2 ? _M2 : i == 3 ? _M3 : i == 4 ? _M4 : i == 5 ? _M5 : i == 6 ? _M6 : _M0;
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 extra : TEXCOORD0; float2 lightmapUV : TEXCOORD1; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR;
                float2 lightmapUV : TEXCOORD2; float material : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.lightmapUV = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                output.material = input.extra.x;
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 l = normalize(_BZB_SunDir.xyz);
                float3 h = normalize(l + v);
                half3 albedo = Albedo(input.color);
                float4 m = Material(input.material);     // x F0, y roughness, z specular, w shininess
                half nl = saturate(dot(n, l));
                half nv = saturate(dot(n, v));

                // Shadow (direct light only) and ambient occlusion (indirect light).
                half rawShadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(1.0, rawShadow, _ShadowStrength);
                half indirectAO = 1.0, directAO = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                    indirectAO = ao.indirectAmbientOcclusion; directAO = ao.directAmbientOcclusion;
                #endif

                // Indirect diffuse: baked lightmap if present, otherwise the hemisphere.
                half3 indirectDiffuse = Hemisphere(n.y);
                #if defined(LIGHTMAP_ON)
                    if (_BZB_DisableBakedGI < 0.5) indirectDiffuse = SampleLightmap(input.lightmapUV, n);
                #endif
                indirectDiffuse *= _Ka * indirectAO * Tint();

                // Fresnel weight of the specular part (only glossy surfaces reflect).
                half F = Fresnel(m.x, nv);

                // Indirect specular: reflection probe along R, blurred by roughness.
                float3 r = reflect(-v, n);
                half3 indirectSpecular = (_BZB_DisableReflections > 0.5) ? Hemisphere(r.y) : GlossyEnvironmentReflection(r, m.y, indirectAO);
                indirectSpecular *= _Kr;

                // Direct (live sun).
                half3 directDiffuse = _Kd * _BZB_SunColor.rgb * nl * shadow * directAO;
                half3 directSpecular = m.z * _Ks * (m.w + 8.0) / (8.0 * PI) * _BZB_SunColor.rgb * pow(saturate(dot(n, h)), m.w) * nl * shadow;

                half3 diffuse = albedo * (1.0 - F) * (indirectDiffuse + directDiffuse);
                half3 specular = F * indirectSpecular + directSpecular;
                half3 color = diffuse + specular;

                int view = (int)_BZB_DebugView;
                if (view == 1) return half4(albedo, 1);
                if (view == 2) return half4(indirectDiffuse, 1);
                if (view == 3) return half4(albedo * directDiffuse + directSpecular, 1);
                if (view == 4) return half4(specular, 1);
                if (view == 5) return half4(indirectAO.xxx, 1);
                if (view == 6) return half4(n * 0.5 + 0.5, 1);
                if (view == 7) return half4(rawShadow.xxx, 1);
                return half4(ApplyFog(color, input.positionWS), 1.0);
            }
            ENDHLSL
        }
        Pass
        {
            // The project's default renderer is 2D, which only draws this pass (scene view, previews). Hemisphere + sun, no shadows.
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert2D
            #pragma fragment frag2D
            struct Attributes2D { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings2D { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR; };
            Varyings2D vert2D(Attributes2D input)
            {
                Varyings2D output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS; output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS); output.color = input.color;
                return output;
            }
            half4 frag2D(Varyings2D input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 l = normalize(_BZB_SunDir.xyz);
                half3 albedo = Albedo(input.color);
                half F = input.color.a * 0.3 * Fresnel(0.04, saturate(dot(n, v)));
                half3 color = albedo * (1 - F) * (_Ka * Hemisphere(n.y) * Tint() + _Kd * _BZB_SunColor.rgb * saturate(dot(n, l)))
                            + F * _Kr * Hemisphere(reflect(-v, n).y);
                return half4(ApplyFog(color, input.positionWS), 1.0);
            }
            ENDHLSL
        }
        Pass
        {
            // Read by the lightmapper only: the surface colour that bounced light picks up.
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vertMeta
            #pragma fragment fragMeta
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            struct AttributesMeta { float4 positionOS : POSITION; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; half4 color : COLOR; };
            struct VaryingsMeta { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            VaryingsMeta vertMeta(AttributesMeta input)
            {
                VaryingsMeta output;
                output.positionCS = MetaVertexPosition(input.positionOS, input.uv1, input.uv2, unity_LightmapST, unity_DynamicLightmapST);
                output.color = input.color;
                return output;
            }
            half4 fragMeta(VaryingsMeta input) : SV_Target
            {
                MetaInput meta = (MetaInput)0;
                meta.Albedo = Albedo(input.color);
                meta.Emission = 0;
                return MetaFragment(meta);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
