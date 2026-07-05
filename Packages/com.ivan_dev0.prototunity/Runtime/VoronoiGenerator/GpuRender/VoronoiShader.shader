Shader "Custom/VoronoiShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
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
            #define UNITY_VERTEX_INPUT_INSTANCE_ID uint instanceID : SV_InstanceID;

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "VoronoiShaderUtils.hlsl"

            Varyings vert(Attributes input, uint svInstanceID : SV_InstanceID, uint vertexId : SV_VertexID)
            {
                InitIndirectDrawArgs(0);
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = GetVoxelPosition(input.positionOS.xyz, svInstanceID, vertexId);
                float3 normalWS = GetVoxelNormal(input.normalOS, svInstanceID, vertexId);
                output.instanceID = GetIndirectInstanceID(svInstanceID);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(TransformWorldToObject(positionWS));
                VertexNormalInputs normalInput = GetVertexNormalInputs(TransformWorldToObject(normalWS),
                                                                       input.tangentOS);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.normalWS = normalInput.normalWS;
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz,
                    GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
                output.positionWS = vertexInput.positionWS;
                output.positionCS = vertexInput.positionCS;
                output.shadowCoord = GetShadowCoord(vertexInput);

                return output;
            }

            float4 GetColor(uint instanceID)
            {
                uint idOfCell = idsToRender[instanceID].id;
                uint width = colorSize.x, height = colorSize.y;
                int x = idOfCell % width;
                int y = (idOfCell / width) % height;
                int z = idOfCell / (width * height);
                half4 color = colors.Load(int4(x, y, z, 0));
                return color;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseColor;
                LitPassFragment(IN, baseColor);
                return half4(baseColor.rgb * GetColor(IN.instanceID), baseColor.a);
                // return half4(GetColor(IN.instanceID).rgb, 1);
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
            
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "VoronoiShaderUtils.hlsl"
            
            float3 _LightPosition;
            float3 _LightDirection;

            float4 GetShadowPositionHClip(Attributes input, uint svInstanceID : SV_InstanceID, uint vertexId : SV_VertexID)
            {
                float3 positionWS = GetVoxelPosition(input.positionOS.xyz, svInstanceID, vertexId);
                float3 normalWS = normalize(GetVoxelNormal(input.normalOS, svInstanceID, vertexId));

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                positionCS = ApplyShadowClamping(positionCS);
                return positionCS;
            }


            Varyings vert(Attributes input, uint svInstanceID : SV_InstanceID, uint vertexId : SV_VertexID)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
               
                output.positionCS = GetShadowPositionHClip(input, svInstanceID, vertexId);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }
}