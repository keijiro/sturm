Shader "Hidden/Sturm/SilhouetteDisplay"
{
    Properties
    {
        _MainTex("Silhouette", 2D) = "black" {}
    }

HLSLINCLUDE

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

float4x4 _LocalToWorld;
float2 _Size;
float _Brightness;

// Procedural quad from SV_VertexID: two triangles over the emitter plane
void Vert(uint vertexID : SV_VertexID,
          out float4 outPosition : SV_Position,
          out float2 outTexCoord : TEXCOORD0)
{
    float x = (vertexID == 1 || vertexID == 2 || vertexID == 4) ? 1 : 0;
    float y = (vertexID == 2 || vertexID == 4 || vertexID == 5) ? 1 : 0;
    float3 local = float3((float2(x, y) - 0.5) * _Size, 0);
    float3 world = mul(_LocalToWorld, float4(local, 1)).xyz;
    outPosition = TransformWorldToHClip(world);
    outTexCoord = float2(x, y);
}

float4 Frag(float4 position : SV_Position,
            float2 texCoord : TEXCOORD0) : SV_Target
{
    float3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, texCoord).rgb;
    return float4(c * _Brightness, 0);
}

ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DisplayPass"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
