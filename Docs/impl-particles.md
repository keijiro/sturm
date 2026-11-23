# Particles: where the arguments live

All under `Assets/Sturm/Runtime/Particles/`.

| Topic | File · symbol |
| :-- | :-- |
| One shared simulation and render path; GPU-only state | `ParticleEffect.cs` header |
| Cross-fade on preset switch, idle skip | `ParticleEffect.Emitting`, `IsIdle` |
| Time step clamp | `ParticleEffect.LateUpdate` |
| Uniforms re-set before every dispatch | `ParticleEffect.Simulate` |
| Update before emit | `ParticleEffect.Simulate` |
| Live gradient rebake and its cost | `ParticleEffect.BakeGradient` |
| Ring buffer instead of a free list | `ParticleSimulation.compute` header |
| Rejection sampling instead of a CDF | `ParticleSimulation.compute` `Emit` |
| Outward push at emission | `ParticleSimulation.compute` `Emit` |
| Raw camera color stored, mixed at render | `ParticleSimulation.compute` `Emit` |
| Drag as the smoke-to-sparks dial | `ParticleSimulation.compute` `Update` |
| Stray motion | `ParticleSimulation.compute` `StrayDrift` |
| Dead slots without a flag | `ParticleCommon.hlsl` `Particle` |
| Derived traits, stray selection by threshold | `ParticleCommon.hlsl` `SubRandom`, `IsStray` |
| Noise kernel cut at 0.5, not 0.6 | `Noise.hlsl` header |
| Two-potential curl noise; amplitude units | `Noise.hlsl` `DivergenceFreeNoise` |
| Energy-conserving depth of field; strays as lights | `ParticleRender.shader` `Vert` (point mode) |
| No depth of field for polygons | `ParticleRender.shader` `Vert` (shape mode) |
| Sparkle instead of a global gain | `ParticleRender.shader` `Sparkle` |
| Even polygon strokes | `ParticleRender.shader` `Frag` (shape mode) |
| Culling dead particles in the vertex stage | `ParticleRender.shader` `Vert` |
| What each tunable means, strays' purpose, capacity sizing | `ParticleProfile.cs` |

Cross-file rules (units, salts, formats) are in [conventions.md](conventions.md).
