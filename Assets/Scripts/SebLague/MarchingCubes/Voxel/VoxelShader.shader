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

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
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

                float4 scaledPosition = mul(_ObjectToWorld, GetVoxelPosition(IN.positionOS, svInstanceID));
                OUT.positionCS = TransformObjectToHClip(scaledPosition);
                OUT.uv = TRANSFORM_TEX(IN.texcoord, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                return color;
            }
            ENDHLSL
        }
    }
}
