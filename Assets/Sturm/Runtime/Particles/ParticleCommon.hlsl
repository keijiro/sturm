#ifndef STURM_PARTICLE_COMMON_INCLUDED
#define STURM_PARTICLE_COMMON_INCLUDED

// GPU particle record shared by the compute kernels and the render shader.
// Must match ParticleEffect.ParticleStride on the C# side.
//
// A slot is dead while age >= life. Freshly cleared slots (all zero) and
// emissions that found no body pixel are therefore dead without a flag.
struct Particle
{
    float3 position;
    float age;
    float3 velocity;
    float life;
    float3 color;  // Linear camera color at the emission point
    float random;  // Per-particle uniform random value in [0, 1)
};

bool IsAlive(Particle p)
{
    return p.age < p.life;
}

// PCG hash (Jarzynski and Olano, "Hash Functions for GPU Rendering")
uint PcgHash(uint v)
{
    uint state = v * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

// Advances the seed and returns a uniform value in [0, 1).
float NextRandom(inout uint seed)
{
    seed = PcgHash(seed);
    return (seed >> 8) * (1.0 / 16777216);
}

float3 RandomUnitVector(inout uint seed)
{
    float z = NextRandom(seed) * 2 - 1;
    float phi = NextRandom(seed) * 6.28318530718;
    float r = sqrt(saturate(1 - z * z));
    return float3(cos(phi) * r, sin(phi) * r, z);
}

// Independent per-particle values derived from the stored random value. The
// simulation and the renderer agree on them, so a trait such as being a
// stray never has to be stored.
float SubRandom(float r, uint salt)
{
    return (PcgHash(asuint(r) ^ PcgHash(salt)) >> 8) * (1.0 / 16777216);
}

// Strays are the lowest fraction of the random values. Selecting them by
// threshold keeps the count proportional to the ratio as it is tuned live.
bool IsStray(Particle p, float ratio)
{
    return p.random < ratio;
}

#endif // STURM_PARTICLE_COMMON_INCLUDED
