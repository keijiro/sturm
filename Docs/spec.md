# Specification

Sturm turns a performer on stage into a particle-lit silhouette in real time.
Its output goes straight to the stage screen, so everything the operator
touches lives in a small overlay that can be hidden.

Target: macOS (Metal) on Unity 6000.6. Matting runs RVM through Core ML
(`jp.keijiro.rvm-coreml`), which ties the project to Apple platforms.

## Input

- Sources are listed as every camera WebCamTexture reports, then every MP4
  under `StreamingAssets` (recursively, ordinal order). Duplicate camera names
  are numbered.
- The selected source is remembered by display name, so a source that comes
  back after a reboot or a re-plug is picked again even if its index moved.
- MP4 sources loop. Video has no audio output.
- A source that fails (camera denied or silent, missing or undecodable video)
  shows its reason in the status line and leaves the stage empty; it never
  falls back to another source on its own.

## Matting and reset

- The matte is a fixed 1280 × 720 texture regardless of input size.
- RVM is recurrent. Its state is discarded, and the matte cleared, whenever
  the image stream is discontinuous: on a source switch, when a video loops,
  and when a source fails. Clearing the matte hides the silhouette and stops
  emission until the new stream produces a matte.

## Effects

- An effect preset is one or more particle layers plus a rim light color and
  brightness for the silhouette.
- Exactly one preset emits at a time. Switching is never a cut: the old
  preset's particles live out their lifetimes while the new one starts, and the
  rim light glides to the new values over about a second.
- Preset selection wraps around at both ends and is remembered across runs.
- Adding a look should need only a new `ParticleProfile` asset and a preset in
  the scene, never new shader code.

## Operator controls

| Input | Action |
| :-- | :-- |
| `1`–`9` | Select preset |
| `←` / `→` | Previous / next preset |
| `Tab` | Show or hide the overlay |
| Source dropdown | Switch input |
| Effect dropdown | Select preset |

The overlay sits at the bottom left and passes clicks through everywhere else.
Its status line shows RVM inference time and a smoothed frame time; an error
replaces the inference time with a dash and moves the message to the tooltip.

## Output

The picture is linear HDR. Light above one is meant to bloom, and the bloom is
URP's own (`Assets/Sturm/PostProcess.asset`), not part of the effects.
