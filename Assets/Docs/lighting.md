# Lighting

This doc is about the Museum. The other four scenes each carry a single directional light and
Lobby has none at all, but **Dakon needed a pass of its own** after the colour space change —
see the last section.

Every number here is in `Assets/Scripts/Core/Editor/MuseumLightingBuilder.cs` — if you change
one, change both.

## The look

Evenly lit and readable everywhere: no black corners, no black ceilings, and the lanterns
reading as warm accents on top of that rather than as the only light in the building.

Getting there was not a matter of turning numbers up. The first rig lit its fixtures and
nothing else, and the reasons were structural — see "What was actually wrong" below.

## There is no global illumination on this target. At all.

This is the constraint everything else follows from, so it is worth stating bluntly.

| Option | Verdict |
|---|---|
| **Baked lightmaps** | Impossible. Verified across **all 975** mesh assets: `m_UVInfo` is only ever `5` (one UV channel) or `0` (none) — **zero have UV2**. And `m_IsReadable: 0` on **975 of 975**, so `Unwrapping.GenerateSecondaryUVSet` cannot even run without first rewriting every mesh. |
| **Adaptive Probe Volumes** | Hard-disabled on WebGL2 in the package: `ForwardLights.cs:544-548` gates `apvIsEnabled` on `GraphicsDeviceType.WebGPU` under `#if UNITY_WEBGL`. It would look correct in the Editor and render **nothing** in the build. Also needs compute shaders, which WebGL2 lacks. |
| **Legacy Light Probes** | Work, but light *dynamic* objects. They put no indirect light on static walls, which is exactly what was black. |

So the brightness is built entirely from **direct light, ambient, and reflection probes**. It
will never have the softness real bounce gives. Do not go looking for a setting that fixes
that — the honest fix would be a WebGPU target or regenerating UV2 on 975 meshes.

## The building

Measured from the floor slabs, not estimated. Useful because the fill layer is placed from
these numbers rather than from prop names.

| | |
|---|---|
| Footprint | **48 m (x 2.43→50.43) × 40 m (z −26.83→13.17)** |
| Storeys | 4 occupied, **5.00 m floor-to-floor** |
| Walkable floor heights | **1.60, 7.70, 12.70, 17.70** |
| Clear height | 4.6 m per storey (5.55 m on the ground floor) |
| Atrium void | **14 × 14 m** at (26.43, −6.83), open ~21 m to the roof |
| Room slots | 6 per storey. E/W are 17 × 14; the N/S halves are 24 × 13, split at x 26.43 |

**All glazing is on the south face** (z ≈ +13.2). The north, east and west perimeters are
solid plaster on every level, and there is no skylight or clerestory.

## What was actually wrong

None of this was a value that needed nudging:

| Problem | Why it mattered |
|---|---|
| The **entire ground-floor ring had zero lights** | All six `Int_Lampion` fixtures sit inside the 14×14 atrium footprint, leaving ~1,720 m² with nothing in it |
| **All ten exhibit accents were outside the building** | They aimed at `Podium_T1/T2_*`, every one of which is beyond the 48×40 plate |
| **Five of six L3 lanterns hung ~7 m above their floor** with range 11 | Put the floor at ~65% of the falloff; L3 was by far the darkest storey |
| **N/S rooms are 24 m long, lit by one range-11 light** | Far ends were 14–20 m away — physically unreachable |
| **The sun pointed the wrong way** | Yaw −30° → forward `(−0.32, −0.77, +0.56)`, shining from the north into the back of a solid wall. No daylight entered the building at all |
| **Nothing to reflect** | Zero probes; 181 renderers at 0.70–0.78 smoothness and 59 glass at ~0.95 all mirrored a bare blue sky, indoors |
| **Every material was `_Metallic: 0`** | Metals included — so brass and gold could not pick up an environment even once there was one |
| **ACES tonemapping** | Crushes shadows, widening the exact bright-pool/black-surround gap being complained about |

## Project-wide settings this depends on

Recorded also in [boundaries.md](boundaries.md).

| Setting | Where | Value | Why |
|---|---|---|---|
| Color space | `ProjectSettings/ProjectSettings.asset` | **Linear** | Correct light falloff |
| Lights use linear intensity | `ProjectSettings/GraphicsSettings.asset` | **on** | Must move with the color space |
| Rendering mode | `Assets/Settings/Mobile_Renderer.asset` | **Forward+** | Forward caps additional lights at **4 per object**; this rig has 71. Forward+ clusters instead and ignores that cap |

Constraints deliberately **not** changed, that the values are tuned around: additional-light
shadows are off pipeline-wide, soft shadows are off, render scale is 0.8, MSAA is off.

## The rig

**71 lights: one sun, 29 lanterns, 42 fill. Plus 25 reflection probes.**

### Sun

The scene's existing `Directional Light (1)` is adopted, never recreated.

| | |
|---|---|
| Rotation | **pitch 38°, yaw 180°** |
| Colour temperature | 6500 K |
| Intensity | 2.2 |
| Shadows | on, strength 0.55 |
| Culling mask | everything except layer 7 `MinimapOnly` |
| `RenderSettings.sun` | assigned to this light |

The yaw matters far more than the intensity. At the recovered −30° the sun shone from the
north, and since every window is on the south face it lit nothing inside. 180° rakes it through
the glazing and down the atrium — the cheapest daylight available in the project.

### Environment

| | |
|---|---|
| Ambient | **Trilight** `0.50, 0.48, 0.46` / `0.44, 0.41, 0.38` / `0.34, 0.31, 0.28` |
| Fog | on, ExponentialSquared, density 0.0025, colour `0.16, 0.13, 0.10` |
| Reflection intensity | **0.8** |

**The ground term is the one to watch.** It is what lights every slab underside — the surfaces
that read as "ceiling". At `0.15/0.12/0.09` they were near-black.

### Lanterns

One point light per fixture, realtime, no shadows.

| Rule | Count | Kelvin | Range | Intensity | Drop |
|---|---|---|---|---|---|
| name ends `_Lampion` | 18 | 2200 | 11 | 7 | 0.25 |
| name starts `Int_Lampion_` | 6 | 2200 | 14 | 9 | 0.3 |
| name starts `DEC_lampu_` | 4 | 2700 | 6 | 3 | 0.15 |
| `Lampu_Gantung_Atrium` | 1 | 2400 | 32 | 16 | 0.6 |

Each light is then **clamped to at most 3.9 m above its own storey** (`ClampToStorey`), because
the L3 lanterns model about 7 m up. The mesh stays where the artist put it; only the light moves.

### Fill

This is the layer that stops the museum reading as pools under lamps. Each of the six room
slots gets a soft wide fill on every storey — two for the 24 m N/S slots, since a 24 m room
cannot be covered from its centre — plus a column up the atrium shaft, which was empty for 21 m.

| | |
|---|---|
| Kelvin / intensity / range | 3600 K / 4.5 / 20 |
| Height above floor | 3.6 m |
| Count | **42** |

These replaced the ten podium accents, which pointed at objects outside the building.

### Reflection probes

**25 baked, box-projected**: one per room slot per storey, plus one tall probe for the atrium.
Resolution 128 (~128 KB compressed each). Blending and box projection are already enabled on the
RP asset, and all 979 renderers already carry `m_ReflectionProbeUsage: BlendProbes`, so no
per-renderer change was needed. WebGL2 allows 32 visible probes — this rig makes 25.

Box projection matters here because the rooms *are* boxes: it makes the reflection track the
walls instead of sitting at infinity.

**Creating the probes and capturing them are separate steps.** `Museum/Lighting/Build` creates
them; **`Museum/Lighting/Bake Reflection Probes`** captures them to `Assets/Scenes/Museum/*.exr`
via `Lightmapping.BakeReflectionProbe`. Until that runs, the probes fall back to the skybox.

### Materials

Written by the generator, not left as hand-edited `.mat` files.

| Material | Uses | Change | Why |
|---|---|---|---|
| `Material_0.007` | 24 lantern shades | emission `2.0, 1.1, 0.4` | Above 1 so it crosses the bloom threshold and the shade *glows* |
| `Material_0.011` | atrium chandelier | emission `1.6, 1.0, 0.45` | Its map is near-white beads; untinted it reads as a cool-white blob |
| `Brass` / `Gold` / `MAT_Stair_Steel` | 91 / 48 / 42 | `_Metallic` 0 → **0.9** | Metals the recovery left as dielectrics. Smoothness was already 0.70–0.78, so they simply could not show a reflection |
| `Andesite` | 27 | `_Smoothness` 0.05 → **0.25** | Cut stone that absorbed everything and stayed black under any fill |

**The other thirteen emissive materials are fine — do not "fix" them.** `Material_0.014`
(`RM_*_Batik`), `.029` (`_Bakul`), `.034` (`_Bench`) and `.035` (`_Wayang`) sit at
`_EmissionColor {1,1,1,1}`, which looks alarming, but each drives emission through a **black
`_EmissionMap`**. White × black = no emission.

## Post-processing

`Assets/Settings/MuseumProfile.asset`, on the scene's `Global Volume`: **Neutral** tonemapping,
Vignette 0.10, `postExposure +0.3`, WhiteBalance +8, and Bloom at threshold 1.15, intensity
0.45, scatter 0.6, tint `1, 0.85, 0.6`, **clamp 8**.

Tonemapping was ACES and is now Neutral — ACES crushes shadows, which is the opposite of what a
building with no bounce light needs.

The bloom threshold is load-bearing: the eighteen video screens are uGUI `RawImage` drawn with
the **unlit** UI/Default shader, so they ignore scene lighting and top out at white = 1.0. A
threshold of exactly 1.0 made every screen bloom. The clamp stops any one bright element
smearing across the frame without having to lower the threshold and lose the lantern glow.

`Mobile_RPAsset.m_ColorGradingMode` was **HDR**, the natural pairing for an HDR bloom, and
is **LDR** as of 2026-09-10. HDR grading is the heaviest `UberPost` path and Windows browsers
translate it through ANGLE → D3D11, where it failed to compile at all. The colour buffer is
still HDR (`m_SupportsHDR: 1`), so bloom still sees values above 1.0 and the 1.15 threshold
still means what it says — but Neutral tonemapping now runs inside a 16³ LDR LUT, so
highlights above 1.0 clip harder than they used to. **If the lanterns look blown out, that is
this change, not the light rig.** See
[build-and-deploy.md](build-and-deploy.md#the-angle--d3d11-shader-budget).

**`MuseumProfile` used to be shared with Dakon.** It was duplicated to `DakonProfile.asset` and
Dakon repointed, so museum tuning cannot restyle the board.

## Rebuilding it

- **`Museum → Lighting → Build`** — idempotent, and safe to re-run on a scene someone else has
  been editing. Everything it creates lives under one `Lighting (Generated)` root, which it
  clears and rebuilds; anything hand-placed outside that root is untouched.
- **`Museum → Lighting → Bake Reflection Probes`** — captures the 25 probes. Slow, and it
  blocks the Editor's main thread while it runs.
- **`Museum → Lighting → Clear`** — removes the generated root, leaving the adopted directional
  light. It does not revert ambient/fog; re-run Build for those.

### Why a generator and not inspector values

The project was destroyed on 2026-08-19 and rebuilt from a deployed build; **no inspector value
survived**. A rig that exists only inside `Museum.unity` is a rig that is lost next time.

### Two traps worth knowing before you edit it

1. **Transforms lie.** 305 of the museum's 1052 transforms sit at the origin because their
   offset is baked into the mesh vertices — every `Podium_*` and every `RM_*_Plinth*` among
   them. Positions come from `Renderer.bounds`, which is world-space and correct either way.
2. **Matching renderers by name is not enough either.** The four `DEC_lampu_*` posts are empty
   parents whose mesh sits on a child called `Mesh_0.017`, so a renderer-name search silently
   skipped them and produced 25 lanterns instead of 29. The generator matches **transforms** and
   encapsulates the bounds of their children.

Also: **18 renderers are parked at y −99 to −160** (children of `Vid LT1/2/3`). Any automatic
bounds fitting must exclude them.

## Performance

71 realtime lights, no shadows on 70 of them, at render scale 0.8 under Forward+, plus 25
reflection probes. **Frame time on the museum's hardware has not been measured.** If it is a
problem, the levers in order are:

1. Drop the atrium fill lights and the second fill in each N/S slot (−16).
2. Shorten fill ranges — fewer clusters touched per tile.
3. Reduce `m_ShadowDistance` on `Mobile_RPAsset` from 50.
4. Halve the probe count by dropping to one per storey rather than one per slot.

All 979 renderers cast shadows, including 72 glass balustrade pieces. Turning that off for the
glass is free frame time nobody has taken.

## Known pre-existing defects, deliberately left alone

- The Minimap Camera is also tagged `MainCamera`.
- `Mobile_RPAsset.m_VolumeProfile` is a dangling guid pointing at no asset.
- `Assets/Shader/Museum_Hologram.shader` is a decompiled stub with no URP passes;
  `Trigger Hologram.mat` uses it on 19 objects.
- `QualitySettings` has a WebGL default of index 3 with only two levels defined.
- The main camera's culling mask does not exclude `MinimapOnly`, which `MinimapOnlyObject.cs`
  says it should. The lights do exclude it.

## Minimap markers

Every light here excludes layer 7 `MinimapOnly` — a map icon shaded by whichever lantern it
happens to stand under is not a map icon. That exclusion is only half the fix: `CreatePrimitive`
hands back the pipeline's default **Lit** material, so an excluded marker would receive nothing
but this scene's deliberately dark ambient and a red dot would render near-black.

`MinimapOnlyObject.cs` therefore loads `Assets/Resources/MinimapMarker.mat` (URP/Unlit) and
assigns it before tinting. It lives in `Resources` rather than on a serialized field so that it
cannot be lost the way this project's inspector values were.


## Dakon

The Linear switch left Dakon reading dark and muddy, and the fix is not the museum's fix,
because **Dakon is almost entirely UI**: one 3D `Plane` backdrop, and 17 `Image`s on a
`ScreenSpaceCamera` canvas. Two consequences follow, and both are easy to get wrong:

1. **Lights barely matter.** Only the backdrop plane is lit; the board, holes and seeds are
   sprites. Raising the directional light brightens the backdrop and nothing else.
2. **Post-processing does hit the board**, because a ScreenSpaceCamera canvas is composited
   before post. Exposure is a blunt instrument here — a first attempt at `postExposure +0.35`
   lifted the UI along with everything else and washed the whole board out to pale tan.

What it runs now, written by `DakonUIBuilder.ApplyEnvironment` (menu
`Museum → Rebuild UI → Dakon`) rather than left in the inspector:

| | |
|---|---|
| Directional light | 1.2, shadow strength 0.6 |
| Ambient | Trilight `0.36, 0.34, 0.32` / `0.30, 0.27, 0.24` / `0.20, 0.18, 0.15` |
| Reflection intensity | 0.6 |

Ambient moved off Skybox mode: the camera clears to a solid colour so the skybox is never
drawn, but it was still the ambient source, putting a dim blue probe over a warm wooden board.

`Assets/Settings/DakonProfile.asset` uses **Neutral** tonemapping, not the museum's ACES —
ACES desaturates, and on a flat close-up board it drains the wood and flattens the seeds.
Vignette 0.18, `postExposure +0.12`, saturation neutral.

MainMenu and Egrang were checked after the same change and needed nothing: Egrang is a bright
outdoor scene and MainMenu is dark by design.
