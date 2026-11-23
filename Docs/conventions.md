# Conventions

Rules that no single file owns. Each holds across several files, and breaking
one usually fails silently: a wrong picture rather than an error.

## Space

- The emitter plane is the `Silhouette` transform. It is one world unit high,
  as wide as the matte's aspect, centered on the transform, and faces the
  viewer along its negative z axis. The display quad, the particle emitter and
  the camera rig all assume this.
- Particle positions and velocities live in world space. Emission converts
  from the plane's local space once, at birth.
- Sizes in the point mode are pixels at 1080 lines and scale with the output
  height, so a look tuned on a laptop preview holds on the stage projector.
  Polygon shapes are the exception and are sized in world units.

## Light and color

- Everything is linear. The matte is created with an sRGB format so sampling
  linearizes it, and every color pushed from C# goes through `.linear`.
- Every stage draw is additive (`Blend One One`, no depth write) and writes
  zero alpha. Nothing depends on draw order, and layers can be added or
  removed freely.
- Values above one are intentional: they are what the URP bloom picks up.

## Frame order

1. `AppController.Update` hands the latest input frame to RVM.
2. `Silhouette.LateUpdate` (execution order −100) rebuilds the pyramid, the
   composite and the emission map from the current matte.
3. `ParticleEffect.LateUpdate` runs per effect: update, then emit, then draw.

## Hidden shader references

`ParticleEffect` and `Silhouette` keep their shaders in hidden serialized
fields. The values come from the script's default references in its `.cs.meta`,
which Unity copies when the component is added. Replacing or renaming one of
those shaders means updating the default references as well; existing scene
objects keep the old reference until reassigned.

## Per-particle randomness

A particle stores one random value. Every other per-particle trait is derived
from it with `SubRandom(random, salt)` (`ParticleCommon.hlsl`), so the
simulation and the renderer agree without storing more. Each trait needs a salt
of its own; sharing one would correlate two traits.

| Salt | Trait | Where |
| :-- | :-- | :-- |
| (raw value) | Stray selection | `IsStray`, both shaders |
| 1 | Intensity | `ParticleRender.shader` `ParticleColor` |
| 2 | Twinkle phase | 〃 |
| 3 | Twinkle rate | 〃 |
| 4 | Size | `ParticleRender.shader` `Vert` |
| 5 | Polygon spin | 〃 |
| 6 | Polygon start angle | 〃 |
| 7 | Polygon side count | 〃 |
| 8 | Sparkle phase | `ParticleRender.shader` `Sparkle` |
| 0x10000 + slot | Sparkle slot draw | 〃 |
| `0x5bd1e995` | Stray drift (seeds a stream, not `SubRandom`) | `ParticleSimulation.compute` `StrayDrift` |

## Format obligations

Pairs of files that must agree. The channel legends are kept next to the code
that writes them; this list only says who has to match whom.

| Contract | Producer | Consumers |
| :-- | :-- | :-- |
| Particle record (12 floats) | `Particle` in `ParticleCommon.hlsl` | `ParticleEffect.ParticleStride` |
| Emission map (r matte, gb outward direction, a rimness; half matte size) | `FragEmission` in `SilhouetteFilter.shader` | `Emit` in `ParticleSimulation.compute` |
| Matte (rgb linear camera color, a matte) | RVM via `AppController` | `SilhouetteFilter.shader` (alpha → pyramid, rgb → body light), `Emit` (rgb → particle color) |
| Color over life (128 × 1, linear, alpha is the fade) | `ParticleEffect.BakeGradient` | `ParticleColor` in `ParticleRender.shader` |
| Noise period 289 | `Noise.hlsl` | `ParticleEffect.NoisePeriod` |
| Pyramid of five levels, weights over four | `Silhouette.LevelCount`, `CumulativeWeights` | `_Level0`–`_Level4` and `*Weights` in `SilhouetteFilter.shader` |
| Filter pass indices | Pass order in `SilhouetteFilter.shader` | `*Pass` constants in `Silhouette.cs` |
| Kernel and property names | `ParticleSimulation.compute`, `ParticleRender.shader` | `ParticleEffect.ShaderIDs` and `FindKernel` calls |
| Six-vertex quad order (0, 1, 2) (3, 4, 5) | `QuadCorner` in `ParticleRender.shader`, `Vert` in `SilhouetteDisplay.shader` | Draw counts in `ParticleEffect.Draw`, `Silhouette.Draw` |
