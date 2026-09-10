# Asset Budget — why the payload is 40.5 MB and how to keep it there

This is a museum kiosk that also has to load over a hotel-grade connection on a visitor's
phone. The build is a **download before it is a game**, so asset import settings are a
shipping constraint, not a preference. Read this before adding a model, a texture, a
terrain or a package.

Build sizes and the deploy runbook live in [build-and-deploy.md](build-and-deploy.md).

## Where it stands

| | 2026-09-09 | 2026-09-10 |
|---|---|---|
| `WebGL.data.br` | 135.5 MB | **33.4 MB** |
| `WebGL.wasm.br` | 9.0 MB | **6.9 MB** |
| total | 144.6 MB | **40.5 MB** |
| packed (uncompressed) | 1058 MB | — |
| of which Texture2D | 1024 MB | — |

## How to read the real numbers

Guessing from `du -sh Assets/*` is wrong — Unity only packs what the **enabled build scenes
depend on**, plus everything in `Assets/Resources/`. `Assets/BOKI/LowPolyNature/` is 705 MB
on disk and contributes ~8 MB to the build.

The build report is the only truth:

```bash
cp Library/LastBuild.buildreport Assets/LastBuild.buildreport   # not loadable in place
```

(and take the headline number from the `[BuildWebGL] Payload:` line in the log, never from
`ls` on `Builds/WebGL/Build/` — Brotli stalls mid-write at a plausible-looking size)

then in the Editor, `AssetDatabase.LoadAssetAtPath<BuildReport>(...)` and walk
`report.packedAssets[].contents[]`, summing `packedSize` by `type` and by `sourceAssetPath`.
Delete the copy afterwards — it is a 1 MB binary that does not belong in the project.

For "what does this scene actually pull in", `AssetDatabase.GetDependencies(scenePath, true)`
answers directly and is much faster than reasoning about prefab graphs.

## The rules

### Textures

- **Never leave a texture on `Uncompressed`.** This is what cost 1 GB. WebGL2 supports
  DXT1/DXT5/BC5 on every browser that can run the build at all; there is no fallback case
  that justifies RGBA32.
- **Crunch everything that ships.** Crunch is a *transport* codec — it decodes to DXT at
  load, so runtime memory is unchanged and only the download shrinks. That is exactly the
  trade this project wants. Quality 50 for colour, 65 for normal maps (crunch is blockier
  on normals).
- Caps: **1024** for terrain layer albedo/normal, **512** for prop normal maps, **1024**
  for everything else. Museum props are viewed from more than arm's length.
- **Four things must stay uncompressed**, and reimporting them at Compressed is a visible
  regression, not a saving:
  - TMP **SDF atlases** (`*SDF Atlas.png`) — block compression destroys the distance field
    and text goes fuzzy at every size.
  - `EmojiOne.png`, `DebugFont.png` — same reason.
  - `AreaTex.png`, `SearchTex.png`, `BayerMatrix.png` — lookup tables. A compressed LUT is
    a wrong LUT.
- `streamingMipmaps` off and `isReadable` off. Neither buys anything on this target and
  `isReadable` doubles the memory of every texture it is set on.

### Generated decor (2026-09-10)

The museum-decor pass ([museum-decor.md](museum-decor.md)) added **28 FBX props** under
`Assets/Models/Generated/` (≈1 MB on disk, low-poly, read/write off, no rig — enforced by
`GeneratedModelPostprocessor`), **17 flat URP Lit materials** under `Assets/Material/Decor/`
(no textures — `MuseumDecorMaterials` is a colour palette on purpose) and one texture,
`Assets/Texture2D/SegeraHadir.png` (the "Segera hadir" placeholder card, 1024 cap, crunched).
The lobby stations synthesise their sounds at runtime (`ProceduralAudio`), so no audio clips
ship for them. Eight more `GameLessonData` assets sit in `Resources/lessons/` (`lobby_*`) —
text only. Expect the payload to move by well under a megabyte; re-read the build report after
the next production build and update the table above.

### Visitor characters (2026-09-11)

The museum's remote visitors wear the four `Assets/Models/ASSET_NUSANTARA/1_Karakter` models
(via `Assets/Prefabs/Visitors/`). What ships: four skinned meshes of 18–22k vertices, four
1024² albedo textures (extracted from the FBXs to `1_Karakter/Textures/<char>/`, crunched at
quality 50 per the rules above), four materials, and three Humanoid clips. The seven
`2_Animasi/Anim_*.fbx` files are 9.4 MB each on disk because every one carries a full copy of
the Jawa mesh — only their clips are referenced, so the meshes never reach a build. Estimate:
2–3 MB added; confirm against the next build report.

### Terrain

Terrain "data" is mostly **textures wearing a `TerrainData` costume**. A `TerrainData`'s size
is dominated by its **alphamaps** (splat weights), one RGBA texture per four assigned layers:

```
alphamapResolution² × 4 bytes × ceil(layers / 4) × 1.33 (mips)
```

At 2048 with 12 layers that is **67 MB for one terrain**. At 512 with 4 layers it is 1.4 MB.

- **`alphamapResolution` is 512.** The terrains are 200 × 200 m, so that is 2.5 splat texels
  per metre — more than the layer tiling (5 m) can resolve. 2048 was 10 texels/m.
- **`baseMapResolution` is 512.**
- **Assigned layer count is the cost, not painted layer count.** A layer assigned and never
  painted still allocates its quarter of a splat map. The Museum terrain had 11 layers
  assigned and 2 with any weight.
- **`detailResolution` is 0 when `detailPrototypes` is empty.** Four of the five terrains
  were carrying a 1024 detail map for zero grass.
- `heightmapResolution` 513 is cheap (0.5 MB) — leave it alone.

### Terrain layers — one canonical set

`Assets/TerrainLayer/*.terrainlayer` is **the** layer set. It points at
`Assets/Texture2D/Ground_*`. A second, byte-identical set exists at
`Assets/BOKI/LowPolyNature/Terrain/layers/` pointing at
`Assets/BOKI/LowPolyNature/Textures/ground/` — those source textures are the uncompressed
originals, and a terrain that references even *one* BOKI layer drags all of them into the
build. **Never assign a BOKI layer to a shipped terrain.**

`maskMapTexture` is **null on every layer**. Neither terrain material
(`Assets/Material/TerrainLit.mat`, and BOKI's) has the `_MASKMAP` keyword, so the mask maps
were 11 textures rendered by nothing. They were plain AO maps, not URP mask maps
(R metallic / G AO / B height / A smoothness), so wiring them up would be wrong anyway.

### Packages

`Assets/Resources/` ships **whole, always**, whether or not anything references it. That is
how `Assets/Resources/sentis/` (19 MB) and `com.unity.ai.inference`'s compute shaders
(8.9 MB) were in a build with zero `Unity.Sentis` or `InferenceEngine` references anywhere.

Removed on 2026-09-10, all verified to have no user in `Assets/`:

`com.unity.ai.inference`, `com.unity.ai.assistant`, `com.unity.ai.navigation`,
`com.unity.animation.rigging`, `com.unity.visualscripting`, `com.unity.recorder`,
`com.unity.timeline`, `com.unity.multiplayer.center`,
`com.unity.multiplayer.center.quickstart`, `com.unity.multiplayer.playmode`,
`com.unity.collab-proxy`, and the `vr`, `xr`, `cloth`, `vehicles`, `physics2d`, `tilemap`,
`adaptiveperformance`, `androidjni`, `unityanalytics`, `vectorgraphics` engine modules.

Before adding a package, check it against [boundaries.md](boundaries.md) and check whether
it drops anything into `Resources/`.

### Player settings

| Setting | Value | Why |
|---|---|---|
| Managed stripping level | **High** | 9.0 → 6.9 MB of wasm, with `stripEngineCode` on |
| IL2CPP code generation | **OptimizeSize** | Faster download beats marginal throughput here |
| IL2CPP compiler config | **Master** | Slowest build, smallest output |
| `stripEngineCode` | **on** | |
| Compression | **Brotli**, no decompression fallback | nginx's `Content-Encoding: br` is mandatory — see [build-and-deploy.md](build-and-deploy.md) |
| Exceptions | Explicitly thrown only | |

`BuildWebGL.cs` sets the endpoint and template in code at build time; the stripping and
codegen settings above live in `ProjectSettings.asset` and are **not** re-asserted per build.
If a Unity upgrade resets them, the payload silently doubles.

**High stripping requires [`Assets/link.xml`](../link.xml).** The Colyseus SDK creates schema
state (`Activator.CreateInstance`, `GetField`) and message payloads (its MsgPack deserializer)
by reflection, which the linker cannot see. Without the file the WebGL build strips the payload
classes' constructors and unread fields: the lobby still works, but Dakon and Egrang stall on
their first message — and the Editor, which never strips, cannot reproduce it (2026-09-10).
The file keeps `ColyseusSDK`, `colyseus.nativewebsocket`, `Museum.Net` and `Museum.Core`
whole. **A new payload or schema type outside those two assemblies needs its assembly added.**

## Dead weight still on disk

Not in the build — nothing in the five enabled scenes depends on it — but it is in git and
in every clone. **Deleting any of it changes the payload by zero bytes.** Do it for clone
time, not download time, delete through the Editor so the `.meta` files go too, and push to
a remote first — [the repo is still local-only](../../CLAUDE.md).

Cleared on 2026-09-10 (commit `4530073`): `BOKI/.../Terrain/data/` (610 MB of demo
terrains) and `BOKI/.../Scenes/` (the DemoScene that was their only referent, plus its
lightmaps), plus the six orphan `Assets/TerrainData/` terrains. `Assets/BOKI` went
705 MB → 83 MB; `Assets/TerrainData/` holds only the five live terrains, 10.5 MB in total.

Still there:

| Path | Size | Referenced by |
|---|---|---|
| `Assets/BOKI/LowPolyNature/Textures/ground/` | 37 MB | nothing — the shipped terrains use `Assets/Texture2D/Ground_*` via `Assets/TerrainLayer/` |
| `Assets/BOKI/LowPolyNature/Audio/` | 31 MB | nothing in a build scene |
| `Assets/BOKI/LowPolyNature/Textures/leaves/` | 7.5 MB | nothing in a build scene |

`Assets/BOKI/LowPolyNature/Terrain/layers/` should go with the ground textures — those two
exist only as the uncompressed twin of the canonical layer set, and keeping them around is
how a future terrain edit reintroduces 550 MB by picking the wrong layer in the inspector.

What the Museum genuinely uses from BOKI: `Models/cliff_1.fbx`, `cliff_2.fbx`,
`mountain_1.fbx`, their prefabs, `Materials/gradient_color_palate.mat`,
`Textures/boki_colors.png`.

## What this cost visually

- The Egrang terrain went from 12 painted layers to **4** (grass, sand, dirt,
  grass-with-green-leaves). The six dropped layers held **1.8 % of total splat weight**
  between them — the largest was `Ground_Sand_Rocks` at 1.5 % — and their weight was
  renormalised into the surviving layers rather than left as holes.
- Splat edges are softer: the transition between two ground types now resolves over 40 cm
  instead of 10 cm. At the player's eye height on stilts this is not visible; in a top-down
  screenshot it is.
- Crunch at quality 50 shows on large flat gradients. Nothing in the Museum is a large flat
  gradient except `Main Menu BG.png`, which sits behind UI.
