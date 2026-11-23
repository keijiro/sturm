#ifndef STURM_NOISE_INCLUDED
#define STURM_NOISE_INCLUDED

// 3D simplex noise with an analytic gradient, after Ashima Arts and Stefan
// Gustavson (MIT). The permutation runs modulo 289, so the field repeats
// every 289 units on each axis; time offsets wrap there without a seam.
//
// Unlike the original, the radial kernel is cut at r^2 = 0.5 instead of 0.6.
// With 0.6 a corner's contribution can still be nonzero where the point
// leaves its simplex, so the gradient jumps across cell faces (a finite
// difference check found jumps of up to 12 against an RMS of 2.7). Particles
// advected through such a field pile up along the faces and show straight
// edges. At 0.5 the kernel vanishes before the corner drops out.

float3 SimplexMod289(float3 x) { return x - floor(x / 289) * 289; }
float4 SimplexMod289(float4 x) { return x - floor(x / 289) * 289; }
float4 SimplexPermute(float4 x) { return SimplexMod289((x * 34 + 1) * x); }
float4 SimplexTaylorInvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

// Returns float4(gradient, value).
float4 SimplexNoiseGrad(float3 v)
{
    const float2 C = float2(1.0 / 6, 1.0 / 3);

    // First corner
    float3 i = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);

    // Other corners
    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);

    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - 0.5;

    // Permutations
    i = SimplexMod289(i);
    float4 p = SimplexPermute(SimplexPermute(SimplexPermute(
                   i.z + float4(0, i1.z, i2.z, 1)) +
                   i.y + float4(0, i1.y, i2.y, 1)) +
                   i.x + float4(0, i1.x, i2.x, 1));

    // Gradients: 7x7 points over a square, mapped onto an octahedron
    float4 j = p - 49 * floor(p / 49);
    float4 x_ = floor(j / 7);
    float4 y_ = floor(j - 7 * x_);

    float4 x = (x_ * 2 + 0.5) / 7 - 1;
    float4 y = (y_ * 2 + 0.5) / 7 - 1;
    float4 h = 1 - abs(x) - abs(y);

    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);

    float4 s0 = floor(b0) * 2 + 1;
    float4 s1 = floor(b1) * 2 + 1;
    float4 sh = -step(h, 0);

    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    float3 g0 = float3(a0.xy, h.x);
    float3 g1 = float3(a0.zw, h.y);
    float3 g2 = float3(a1.xy, h.z);
    float3 g3 = float3(a1.zw, h.w);

    float4 norm = SimplexTaylorInvSqrt(float4(dot(g0, g0), dot(g1, g1),
                                              dot(g2, g2), dot(g3, g3)));
    g0 *= norm.x;
    g1 *= norm.y;
    g2 *= norm.z;
    g3 *= norm.w;

    // Radial kernel m^4 with m = 0.5 - r^2. Its derivative is -8 m^3 x, and
    // the gradient has to be exact: the flow below relies on gradients being
    // curl free to stay divergence free.
    float4 m = max(0.5 - float4(dot(x0, x0), dot(x1, x1),
                                dot(x2, x2), dot(x3, x3)), 0);
    float4 m2 = m * m;
    float4 m3 = m2 * m;
    float4 m4 = m2 * m2;
    float4 px = float4(dot(x0, g0), dot(x1, g1), dot(x2, g2), dot(x3, g3));

    float3 grad = -8 * (m3.x * px.x * x0 + m3.y * px.y * x1 +
                        m3.z * px.z * x2 + m3.w * px.w * x3) +
                  m4.x * g0 + m4.y * g1 + m4.z * g2 + m4.w * g3;

    return 42 * float4(grad, dot(m4, px));
}

// Divergence-free flow from two scalar potentials. The cross product of two
// gradients equals curl(a grad b), so it is an exact curl field at the cost
// of two noise evaluations instead of the three a vector potential needs.
// The offsets decorrelate the potentials and animate them over time.
//
// With the 0.5 kernel the cross product has an RMS magnitude of 1.0 to 1.15
// for one to three octaves (measured over 200k random points), so callers can
// treat their amplitude as a typical acceleration in units per second squared.
float3 DivergenceFreeNoise(float3 p, float3 offset1, float3 offset2, uint octaves)
{
    float3 acc = 0;
    float amp = 1;
    for (uint i = 0; i < octaves; i++)
    {
        float3 g1 = SimplexNoiseGrad(p + offset1).xyz;
        float3 g2 = SimplexNoiseGrad(p + offset2).xyz;
        acc += cross(g1, g2) * amp;
        p *= 2.03;
        amp *= 0.5;
    }
    return acc;
}

#endif // STURM_NOISE_INCLUDED
