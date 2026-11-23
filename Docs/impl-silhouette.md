# Silhouette: where the arguments live

All under `Assets/Sturm/Runtime/Silhouette/`.

| Topic | File · symbol |
| :-- | :-- |
| Origin in the unity-rvm-coreml hologram prototype | `Silhouette.cs` header |
| Running ahead of the particle effects | `Silhouette.LateUpdate` |
| Rebuilding the pyramid every frame | `Silhouette.UpdatePyramid` |
| Cumulative level weights and their falloff | `Silhouette.CumulativeWeights` |
| What each tunable means | `Silhouette.cs` properties |
| Single-tap box downsample | `SilhouetteFilter.shader` `FragPrefilter` |
| Equal slope per pyramid level | `SilhouetteFilter.shader` `LevelGradient` |
| Blurred mattes as an inflated surface | `SilhouetteFilter.shader` `SurfaceNormal` |
| Wrapped lighting instead of a clamp | `SilhouetteFilter.shader` `KeyLight` |
| Body light from the slope, not the normal | `SilhouetteFilter.shader` `BodyLight` |
| Halo outside the matte | `SilhouetteFilter.shader` `FragComposite` |
| Exponential saturation toward white | `SilhouetteFilter.shader` `FragComposite` |
| Fade at the frame border | `SilhouetteFilter.shader` `EdgeMask` |
| Rimness scaling | `SilhouetteFilter.shader` `FragEmission` |

The emission map is a contract with the particle simulation; see
[conventions.md](conventions.md#format-obligations).
