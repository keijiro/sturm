Shader "Hidden/Sturm/SilhouetteFilter"
{
    Properties
    {
        _MainTex("Source", 2D) = "black" {}
    }

HLSLINCLUDE

#include "UnityCG.cginc"

sampler2D _MainTex;
float4 _MainTex_TexelSize;

// Blurred matte pyramid
sampler2D _Level0;
sampler2D _Level1;
sampler2D _Level2;
sampler2D _Level3;
sampler2D _Level4;
float4 _Level0_TexelSize;
float4 _Level1_TexelSize;
float4 _Level2_TexelSize;
float4 _Level3_TexelSize;
float4 _Level4_TexelSize;

float4 _ShapeWeights;
float4 _HaloWeights;
float4 _BodyWeights;
float3 _LightDirection;
float2 _LightAxis;
float3 _Color;
float _Depth;
float _LightIntensity;
float _GlowIntensity;
float _BodyColor;
float _EdgeFade;

void VertBlit(float4 position : POSITION,
              float2 texCoord : TEXCOORD0,
              out float4 outPosition : SV_Position,
              out float2 outTexCoord : TEXCOORD0)
{
    outPosition = UnityObjectToClipPos(position);
    outTexCoord = texCoord;
}

// Central difference in the level's own texel units. Every level has about
// the same blur radius in its own texels, so equal weights give each scale an
// equal peak slope at a straight edge.
float2 LevelGradient(sampler2D level, float4 texelSize, float2 uv)
{
    float2 dx = float2(texelSize.x, 0);
    float2 dy = float2(0, texelSize.y);
    return float2(tex2D(level, uv + dx).r - tex2D(level, uv - dx).r,
                  tex2D(level, uv + dy).r - tex2D(level, uv - dy).r) * 0.5;
}

// Cubed half-Lambert instead of a clamped dot product. A light behind the
// subject reaches only the grazing band of the pseudo-surface, and the clamp
// would cut that band off with a hard terminator. The wrapped response ramps
// smoothly from the lit side of the rim into the dark interior.
float KeyLight(float3 normal)
{
    float wrap = saturate(dot(normal, _LightDirection) * 0.5 + 0.5);
    return wrap * wrap * wrap;
}

// The matte carries no depth, so the blurred mattes stand in for an inflated
// surface: steep near the silhouette, where a real body turns away from the
// viewer, and flat where the blur saturates inside. Thin parts never saturate
// and stay grazing, as narrow cylinders would.
float3 SurfaceNormal(float2 uv, out float2 g2, out float2 g3)
{
    float2 g0 = LevelGradient(_Level0, _Level0_TexelSize, uv);
    float2 g1 = LevelGradient(_Level1, _Level1_TexelSize, uv);
    g2 = LevelGradient(_Level2, _Level2_TexelSize, uv);
    g3 = LevelGradient(_Level3, _Level3_TexelSize, uv);
    float2 slope = g0 * _ShapeWeights.x + g1 * _ShapeWeights.y +
                   g2 * _ShapeWeights.z + g3 * _ShapeWeights.w;
    return normalize(float3(-slope * _Depth, 1));
}

// Light on the camera image. Coarser levels than the rim's carry it from the
// lit side of the outline into the body. Only the light's direction in the
// image plane picks that side, since a light behind the subject would leave
// nothing but the grazing band. A normalized normal would flip across the
// middle of every part and draw a hard terminator there, so the slope itself
// gives the facing ratio. It is small away from the outline, hence the gain,
// and the cube fades it out gradually toward the interior.
float BodyLight(float2 uv, float2 g2, float2 g3)
{
    float2 g1 = LevelGradient(_Level1, _Level1_TexelSize, uv);
    float2 g4 = LevelGradient(_Level4, _Level4_TexelSize, uv);
    float2 slope = g1 * _BodyWeights.x + g2 * _BodyWeights.y +
                   g3 * _BodyWeights.z + g4 * _BodyWeights.w;
    float facing = saturate(dot(-slope, _LightAxis) * 20);
    return facing * facing * facing;
}

// Direction away from the body. The coarse levels keep it smooth far from
// the outline, where the fine ones are flat.
float2 OutwardDirection(float2 g2, float2 g3)
{
    float2 g = g2 + g3;
    return -g / max(length(g), 1e-5);
}

// Fades toward the frame border. A performer cut off by the camera frame
// would otherwise end in a hard straight edge on the plane, both in the
// image and in the emission.
float EdgeMask(float2 uv)
{
    float2 d = min(uv, 1 - uv);
    float2 m = saturate(d / max(_EdgeFade, 1e-4));
    return m.x * m.y;
}

// 9-tap binomial kernel folded into five bilinear samples
float4 Blur(float2 uv, float2 step)
{
    float4 acc = tex2D(_MainTex, uv) * 0.2270270270;
    acc += tex2D(_MainTex, uv + step * 1.3846153846) * 0.3162162162;
    acc += tex2D(_MainTex, uv - step * 1.3846153846) * 0.3162162162;
    acc += tex2D(_MainTex, uv + step * 3.2307692308) * 0.0702702703;
    acc += tex2D(_MainTex, uv - step * 3.2307692308) * 0.0702702703;
    return acc;
}

// A half-size target puts each bilinear sample at the shared corner of a 2x2
// source block, so a single tap is a box downsample.
float4 FragPrefilter(float4 position : SV_Position,
                     float2 texCoord : TEXCOORD0) : SV_Target
{
    return tex2D(_MainTex, texCoord).a;
}

float4 FragDownsample(float4 position : SV_Position,
                      float2 texCoord : TEXCOORD0) : SV_Target
{
    return tex2D(_MainTex, texCoord);
}

float4 FragBlurH(float4 position : SV_Position,
                 float2 texCoord : TEXCOORD0) : SV_Target
{
    return Blur(texCoord, float2(_MainTex_TexelSize.x, 0));
}

float4 FragBlurV(float4 position : SV_Position,
                 float2 texCoord : TEXCOORD0) : SV_Target
{
    return Blur(texCoord, float2(0, _MainTex_TexelSize.y));
}

// Rim-lit silhouette in linear HDR, in the matte's own image space
float4 FragComposite(float4 position : SV_Position,
                     float2 texCoord : TEXCOORD0) : SV_Target
{
    float2 uv = texCoord;
    float4 source = tex2D(_MainTex, uv);
    float matte = source.a;

    float2 g2, g3;
    float3 normal = SurfaceNormal(uv, g2, g3);
    float surface = KeyLight(normal) * _LightIntensity;

    // Outside the matte, the blurred levels give the halo. It takes the light
    // of the nearest silhouette, which is fully grazing there.
    float4 levels = float4(tex2D(_Level1, uv).r, tex2D(_Level2, uv).r,
                           tex2D(_Level3, uv).r, tex2D(_Level4, uv).r);
    float halo = dot(levels, _HaloWeights);
    float edge = KeyLight(float3(OutwardDirection(g2, g3), 0)) * _LightIntensity;
    float glow = halo * edge * _GlowIntensity;

    float intensity = lerp(glow, surface, matte);

    // Exponential saturation tints faint light and drives highlights to white.
    float3 rim = 1 - exp(-intensity * _Color);

    // The camera image keeps only its luminance and takes the light's hue,
    // matching the rim where its light is faint.
    float luma = dot(source.rgb, float3(0.2126, 0.7152, 0.0722));
    float3 body = luma * _Color * matte * BodyLight(uv, g2, g3) * _BodyColor;
    return float4((rim + body) * EdgeMask(uv), 1);
}

// Emission guide for the particle simulation, sampled with bilinear
// filtering at half the matte resolution.
// r: matte, gb: outward direction, a: rimness (0 facing the viewer, 1 where
// the pseudo-surface turns edge-on)
float4 FragEmission(float4 position : SV_Position,
                    float2 texCoord : TEXCOORD0) : SV_Target
{
    float2 uv = texCoord;
    float2 g2, g3;
    float3 normal = SurfaceNormal(uv, g2, g3);
    float matte = tex2D(_Level0, uv).r * EdgeMask(uv);
    // A straight edge peaks around 1 - n.z = 0.5 at the default depth, so
    // the doubling lets the outline reach full rimness.
    float rimness = saturate((1 - normal.z) * 2);
    return float4(matte, OutwardDirection(g2, g3), rimness);
}

ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PrefilterPass"
            HLSLPROGRAM
            #pragma vertex VertBlit
            #pragma fragment FragPrefilter
            ENDHLSL
        }

        Pass
        {
            Name "DownsamplePass"
            HLSLPROGRAM
            #pragma vertex VertBlit
            #pragma fragment FragDownsample
            ENDHLSL
        }

        Pass
        {
            Name "BlurHPass"
            HLSLPROGRAM
            #pragma vertex VertBlit
            #pragma fragment FragBlurH
            ENDHLSL
        }

        Pass
        {
            Name "BlurVPass"
            HLSLPROGRAM
            #pragma vertex VertBlit
            #pragma fragment FragBlurV
            ENDHLSL
        }

        Pass
        {
            Name "CompositePass"
            HLSLPROGRAM
            #pragma vertex VertBlit
            #pragma fragment FragComposite
            ENDHLSL
        }

        Pass
        {
            Name "EmissionPass"
            HLSLPROGRAM
            #pragma vertex VertBlit
            #pragma fragment FragEmission
            ENDHLSL
        }
    }
}
