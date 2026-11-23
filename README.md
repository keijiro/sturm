# Sturm

A prototype of real-time visual effects for live concerts.

## Running

Open the project in Unity `6000.6.10f1` and play `Assets/Main.unity`. It runs on
macOS (Metal) only, since it uses RVM through Core ML.

Choose the input source from the dropdown at the bottom left of the screen. Besides
the camera (WebCamTexture), any MP4 files under `Assets/StreamingAssets` are listed
as test sources.

| Key | Action |
| :-- | :-- |
| `1`–`9` | Select an effect preset |
| `←` / `→` | Switch to the previous / next preset |
| `Tab` | Toggle the overlay UI |

When you switch presets, the old preset only stops emitting; its existing particles
stay until the end of their lifetimes, so the two crossfade naturally.

## Structure

```
InputSource ─▶ MatteGenerator (RVM) ─▶ Silhouette ─▶ ParticleEffect × N
 camera/MP4      RGB + matte           rim-lit plane    shared compute + render
                                       emission map
```

- **Silhouette** (`Runtime/Silhouette`) — Turns the matte into a rim-lit silhouette
  and draws it on a plane of height 1 centered at the origin. It also builds the
  emission map used for spawning particles (the matte, the outward normal of the
  contour, and how close each texel is to the contour).
- **ParticleEffect** (`Runtime/Particles`) — The GPU particle foundation shared by
  every effect. Spawning, motion update, death and drawing are done by one compute
  shader and one render shader, and particle data never leaves the GPU. All
  differences in look and behavior are expressed as parameters of a
  `ParticleProfile` asset.
- **EffectPreset** (`Runtime/App`) — The unit of selection. It runs its child
  ParticleEffects together as layers (e.g. Constellation = points + shapes) and
  sets the rim light color of the silhouette.
- **CameraDrift** (`Runtime/Camera`) — A camera that keeps looking at the origin
  while drifting slightly with noise.
- Light bloom is handled by URP's Bloom (`Assets/Sturm/PostProcess.asset`).

The specification, cross-file rules and maps to the design notes in the code
are under `Docs/`.

## Adding an effect

1. Create a profile with `Create > Sturm > Particle Profile` and tune its parameters.
2. Create a GameObject under `Effects` in the scene and add `EffectPreset` to it.
   Under it, place a child GameObject with `ParticleEffect` (set the Profile, and
   set Source to `Emitter`).
3. Register the preset in the Presets list of `AppController` on `App`.

## Rebuilding the scene

`AgentScripts/Build.cs` is a script that creates the initial profiles and builds
the scene. Run it through the Pipeline package.

```
unity command script run --file AgentScripts/Build.cs --entry Build.All --project-path <project>
```

It does not overwrite existing profiles, but it rebuilds `Main.unity` except for
the camera and the UI, so any manual changes to the scene will be lost.
