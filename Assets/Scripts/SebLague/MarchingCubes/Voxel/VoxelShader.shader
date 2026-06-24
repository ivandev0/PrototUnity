Shader "Custom/VoxelShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
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

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            StructuredBuffer<float3> voxels;
            uniform float4x4 _ObjectToWorld;

            float3 GetVoxelPosition(float3 meshPositionOS, uint svInstanceID)
            {
                uint instanceID = GetIndirectInstanceID(svInstanceID);
                return meshPositionOS + voxels[instanceID];
            }

            Varyings vert(Attributes IN, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionOS = GetVoxelPosition(IN.positionOS.xyz, svInstanceID);
                float3 positionWS = mul(_ObjectToWorld, float4(positionOS, 1.0)).xyz;
                float3 normalWS = normalize(mul((float3x3)_ObjectToWorld, IN.normalOS));

                OUT.positionWS = positionWS;
                OUT.normalWS = normalWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = TRANSFORM_TEX(IN.texcoord, _BaseMap);
                OUT.shadowCoord = TransformWorldToShadowCoord(positionWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                half3 normalWS = normalize(IN.normalWS);

                // Ambient/environment lighting.
                half3 litColor = SampleSH(normalWS);

                // Main light, including shadow attenuation.
                Light mainLight = GetMainLight(IN.shadowCoord);
                half mainNdotL = saturate(dot(normalWS, mainLight.direction));
                litColor += mainLight.color * mainNdotL * mainLight.shadowAttenuation;

                return half4(baseColor.rgb * litColor, baseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            StructuredBuffer<float3> voxels;
            uniform float4x4 _ObjectToWorld;
            float3 _LightPosition;

            float3 GetVoxelPosition(float3 meshPositionOS, uint svInstanceID)
            {
                uint instanceID = GetIndirectInstanceID(svInstanceID);
                return meshPositionOS + voxels[instanceID];
            }
            
            Varyings vert(Attributes IN, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                Varyings OUT;
                
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionOS = GetVoxelPosition(IN.positionOS.xyz, svInstanceID);
                float3 positionWS = mul(_ObjectToWorld, float4(positionOS, 1.0)).xyz;
                float3 normalWS = normalize(mul((float3x3)_ObjectToWorld, IN.normalOS));
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}