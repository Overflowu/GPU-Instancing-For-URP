Shader "Aperture/InstancingBoid"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "ShaderModel" = "4.5"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options procedural:BoidsInstancingSetup
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)

            StructuredBuffer<float4x4> _LocalToWorldMatrices;
            StructuredBuffer<uint> _VisibleIndices;

            void InstancingMatrices(out float4x4 objectToWorld, out float4x4 worldToObject)
            {
                float4x4 localToWorld = _LocalToWorldMatrices[_VisibleIndices[unity_InstanceID]];

                // transform matrix
                objectToWorld._11_21_31_41 = localToWorld._11_21_31_41;
                objectToWorld._12_22_32_42 = localToWorld._12_22_32_42;
                objectToWorld._13_23_33_43 = localToWorld._13_23_33_43;
                objectToWorld._14_24_34_44 = localToWorld._14_24_34_44;

                // inverse transform matrix (TODO: replace with a library implementation if/when available)
                float3x3 worldToObject3x3;
                worldToObject3x3[0] = objectToWorld[1].yzx * objectToWorld[2].zxy - objectToWorld[1].zxy * objectToWorld[2].yzx;
                worldToObject3x3[1] = objectToWorld[0].zxy * objectToWorld[2].yzx - objectToWorld[0].yzx * objectToWorld[2].zxy;
                worldToObject3x3[2] = objectToWorld[0].yzx * objectToWorld[1].zxy - objectToWorld[0].zxy * objectToWorld[1].yzx;

                float det = dot(objectToWorld[0].xyz, worldToObject3x3[0]);

                worldToObject3x3 = transpose(worldToObject3x3);

                worldToObject3x3 *= rcp(det);

                float3 worldToObjectPosition = mul(worldToObject3x3, -objectToWorld._14_24_34);

                worldToObject._11_21_31_41 = float4(worldToObject3x3._11_21_31, 0.0f);
                worldToObject._12_22_32_42 = float4(worldToObject3x3._12_22_32, 0.0f);
                worldToObject._13_23_33_43 = float4(worldToObject3x3._13_23_33, 0.0f);
                worldToObject._14_24_34_44 = float4(worldToObjectPosition, 1.0f);

            }

            void BoidsInstancingSetup()
            {
                InstancingMatrices(unity_ObjectToWorld, unity_WorldToObject);
            }
#else
            void BoidsInstancingSetup()
            {

            }
#endif

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 texcoord     : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                return color;
            }

            ENDHLSL
        }
    }
}
