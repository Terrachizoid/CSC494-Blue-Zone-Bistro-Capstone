// Blue Zone Bistro - low-poly vertex-colour shader (URP). A deliberately small starting point for the art pass:
// flat facets come from the mesh (no shared vertices), colour comes from the vertices (sRGB, converted in linear
// projects), lighting is the main light (with shadows) plus ambient probe, and scene fog is applied.
// Two-sided: back faces flip their normal, so wall winding never matters. Extend freely (noise normals, ramps, rim).
Shader "BlueZone/LowPolyVertexColor"
{
    Properties
    {
        _AmbientStrength ("Ambient strength", Range(0, 2)) = 1.0
        _ShadowStrength ("Shadow strength", Range(0, 1)) = 0.65
    }
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _AmbientStrength;
                half _ShadowStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                half3 albedo = input.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    albedo = SRGBToLinear(albedo);
                #endif
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS) * _AmbientStrength;
                half3 color = albedo * (ambient + mainLight.color * diffuse * shadow);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
