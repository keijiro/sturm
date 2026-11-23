Shader "Hidden/Sturm/ParticleRender"
{
    Properties
    {
        _ColorOverLife("Color Over Life", 2D) = "white" {}
    }

HLSLINCLUDE

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "ParticleCommon.hlsl"

StructuredBuffer<Particle> _Particles;

TEXTURE2D(_ColorOverLife);
SAMPLER(sampler_ColorOverLife);

float3 _BaseColor;
float _SourceTint;
float _SourceLuminance;
float _SourceAdd;
float _Intensity;
float _IntensityRandomness;
float _Twinkle;
float _TwinkleRate;

float _SparkleDensity;
float _SparkleGain;
float _SparkleRate;
float _SparkleRays;

// Point mode. Sizes are in pixels at 1080 lines and scale with the screen.
float _PointRadius;
float _SizeRandomness;
float _Aperture;
float _MaxCoc;
float3 _FocusPoint;
float _FocusOffset;
float _StrayRatio;
float _StrayMaxCoc;
float _StrayBrightness;

// Shape mode. Sizes are in world units.
float _ShapeSize;
float _ShapeSpin;
float _ShapeLineWidth;

// Quad corners for two triangles: (0, 1, 2) and (3, 4, 5)
float2 QuadCorner(uint index)
{
    float x = (index == 1 || index == 2 || index == 4) ? 1 : -1;
    float y = (index == 2 || index == 4 || index == 5) ? 1 : -1;
    return float2(x, y);
}

// Brightness of glint rays relative to the flaring core
static const float RayLevel = 0.5;

// The three source color weights are described on ParticleProfile. Added
// light skips the gradient's color but not its alpha, which is the fade.
float3 ParticleColor(Particle p)
{
    float t = saturate(p.age / p.life);
    float4 grad = SAMPLE_TEXTURE2D_LOD(_ColorOverLife, sampler_ColorOverLife,
                                       float2(t, 0.5), 0);

    // Perceptual luminance, doubled so that mid gray leaves it unchanged
    float3 source = p.color;
    float luma = sqrt(max(dot(source, float3(0.2126, 0.7152, 0.0722)), 0)) * 2;
    float3 tint = lerp(1, source, _SourceTint);
    float level = lerp(1, luma, _SourceLuminance);
    float3 color = _BaseColor * grad.rgb * tint * level + source * _SourceAdd;

    float bright = 1 - _IntensityRandomness * SubRandom(p.random, 1);
    float phase = SubRandom(p.random, 2) * 6.28318530718;
    float rate = _TwinkleRate * (0.5 + SubRandom(p.random, 3));
    float twinkle = 1 - _Twinkle * (0.5 + 0.5 * sin(_Time.y * rate + phase));
    return color * grad.a * _Intensity * bright * twinkle;
}

// Glints. Time is cut into slots per particle, and in a small share of them
// the particle flares up for a moment, like a facet turning through the
// light. Lifting a few particles far above the rest makes the cloud glitter
// brighter without raising it as a whole, which would only saturate where
// particles pile up. Returns the flare envelope in [0, 1].
float Sparkle(Particle p)
{
    float t = _Time.y * _SparkleRate + SubRandom(p.random, 8);
    float slot = floor(t);
    if (SubRandom(p.random, 0x10000u + (uint)slot) >= _SparkleDensity) return 0;

    // Smooth bump over the slot, narrowed by the eighth power to a brief
    // flash with soft ends
    float x = frac(t);
    float e = 4 * x * (1 - x);
    e *= e;
    e *= e;
    return e * e;
}

// Dead slots collapse to a single off-screen point, so all six vertices are
// identical and the rasterizer drops the triangles.
void Vert(uint vertexID : SV_VertexID,
          out float4 outPosition : SV_Position,
          out float2 outCoord : TEXCOORD0,
          out float4 outColor : COLOR,
          out float4 outParams : TEXCOORD1)
{
    Particle p = _Particles[vertexID / 6];
    float2 corner = QuadCorner(vertexID % 6);

    outPosition = float4(-2, -2, 0, 1);
    outCoord = 0;
    outColor = 0;
    outParams = 0;
    if (!IsAlive(p)) return;

    float sparkle = Sparkle(p);
    float3 color = ParticleColor(p) * (1 + _SparkleGain * sparkle);

#ifdef _SHAPE_MODE

    // View-aligned polygon outline with a fixed world size. Depth of field
    // is not simulated here: a defocused outline would need its own
    // convolution, and the shapes read as graphic accents anyway.
    float size = _ShapeSize * (1 - _SizeRandomness * SubRandom(p.random, 4));
    float spin = _ShapeSpin * (SubRandom(p.random, 5) - 0.5) * 2;
    float angle = SubRandom(p.random, 6) * 6.28318530718 + spin * p.age;
    float2 cs = float2(cos(angle), sin(angle));
    float2 offset = float2(corner.x * cs.x - corner.y * cs.y,
                           corner.x * cs.y + corner.y * cs.x) * size;

    float3 view = TransformWorldToView(p.position);
    view.xy += offset;
    outPosition = TransformWViewToHClip(view);
    outCoord = corner;
    outColor = float4(color, 1);
    outParams = float4(3 + floor(SubRandom(p.random, 7) * 3), 0, 0, 0);

#else

    // Thin-lens circle of confusion, normalized so that _Aperture is the
    // diameter in pixels of a point at infinity. Every particle keeps its
    // total energy, so the disc dims as it grows and a defocused particle
    // turns into a faint bokeh instead of a bright blob.
    //
    // Strays are lights rather than specks of dust. They travel far from the
    // focal plane, so they get a larger size limit, and their discs keep a
    // floor brightness instead of fading out. Their rarity is what bounds the
    // fill rate.
    float scale = _ScreenParams.y / 1080;
    float4 clip = TransformWorldToHClip(p.position);
    float depth = clip.w;
    if (depth <= _ProjectionParams.y) return;

    float focus = max(-TransformWorldToView(_FocusPoint).z + _FocusOffset, 1e-3);
    float coc = 0.5 * _Aperture * abs(depth - focus) / depth;

    bool stray = IsStray(p, _StrayRatio);
    float maxCoc = stray ? _StrayMaxCoc : _MaxCoc;

    float r0 = _PointRadius * scale *
               (1 - _SizeRandomness * SubRandom(p.random, 4));
    float r = max(r0, min(coc * scale, maxCoc * scale));
    float energy = (r0 * r0) / (r * r);
    if (stray) energy = max(energy, _StrayBrightness);

    // Glint rays belong to a sharp image of the flare, so they retract as
    // the disc defocuses.
    float rays = _SparkleRays * scale * sparkle * saturate(2 - r / r0);

    // One extra pixel holds the antialiased edge.
    float extent = max(r, rays) + 1;
    clip.xy += corner * extent * 2 / _ScreenParams.xy * clip.w;

    outPosition = clip;
    outCoord = corner;
    outColor = float4(color * energy, 1);
    outParams = float4(extent, r, rays, 0);

#endif
}

float4 Frag(float4 position : SV_Position,
            float2 coord : TEXCOORD0,
            float4 color : COLOR,
            float4 params : TEXCOORD1) : SV_Target
{
#ifdef _SHAPE_MODE

    // Regular polygon outline inscribed in a circle of radius 0.9. The
    // distance is measured along the nearest edge normal, which keeps the
    // stroke width even along each edge.
    float sides = params.x;
    float sector = 6.28318530718 / sides;
    float a = atan2(coord.y, coord.x);
    float d = cos(floor(0.5 + a / sector) * sector - a) * length(coord);
    float stroke = abs(d - 0.9 * cos(3.14159265359 / sides));
    float aa = fwidth(d);
    float alpha = saturate((_ShapeLineWidth * 0.5 - stroke) / aa + 0.5);
    return float4(color.rgb * alpha, 0);

#else

    // Pixel-space coverage of a disc of radius params.y
    float2 q = coord * params.x;
    float alpha = saturate(params.y - length(q) + 0.5);

    // Four one-pixel rays of length params.z, tapering toward their tips
    if (params.z > 0)
    {
        float2 a = abs(q);
        float2 taper = saturate(1 - a / params.z);
        taper *= taper;
        float2 width = saturate(1 - a);
        alpha += (taper.x * width.y + taper.y * width.x) * RayLevel;
    }

    return float4(color.rgb * alpha, 0);

#endif
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
            Name "ParticlePass"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ _SHAPE_MODE
            ENDHLSL
        }
    }
}
