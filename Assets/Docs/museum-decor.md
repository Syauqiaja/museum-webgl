# Museum Decor — frames, signage, themed bays, the lobby gallery

Everything the `Museum/Decor/*` menu puts into `Museum.unity`, on top of the building the
project shipped with. All of it is **generated**, the way the lighting is: each tool owns one
scene-root container, rebuilds it from scratch, and never touches a hand-placed object's
children. Added 2026-09-10 as part of the "make it look like an expensive game" pass; the
copy it shows comes from the curriculum sheet (see [lessons.md](lessons.md)).

## The menus, in the order they are meant to run

| Menu | Container | What it does |
|---|---|---|
| `Museum/Decor/Reimport Generated Models` | — | Re-applies `GeneratedModelPostprocessor` to every FBX under `Assets/Models/Generated/` (material remap, no read/write, no rig) |
| `Museum/Decor/Align Screens To Walls` | — | Snaps the eighteen video canvases flat to their walls (`MuseumScreenAligner`); the frame builder runs it first |
| `Museum/Decor/Build Screen Frames` / `Clear` | `Screen Frames (Generated)` | Teak bar + gold lip + backboard bezel around every screen (`MuseumScreenFrameBuilder`) |
| `Museum/Decor/Build Signage` / `Clear` | `Signage (Generated)` | Name plate above and curriculum board below every screen (`MuseumSignageBuilder`, copy from `GameSignageContent`) |
| `Museum/Decor/Report Viewing Blockers` | — | Lists, per screen, every renderer cutting a sight line from the viewing grid to the picture (`MuseumBlockerCleanup`) |
| `Museum/Decor/Clear Viewing Blockers` | — | Applies `MuseumBlockerCleanup.Fixes` — the hand-written table of props that were parked in front of a video — and re-reports |
| `Museum/Decor/Build Room Decor` / `Clear` | `Decor (Generated)` | Game-specific props in each of the eighteen bays (`MuseumRoomDecorBuilder`) |
| `Museum/Decor/Build Lobby Gallery` / `Clear` | `Lobby Gallery (Generated)` | The ground-floor exhibition of eight stations (`MuseumLobbyGalleryBuilder`) |

Then `Museum/Lighting/Bake Reflection Probes`, because the probes were baked before any of
this existed and the gong is metal.

Every builder opens the Museum scene through `MuseumScreenGeometry.OpenMuseum`, which refuses
to run over a *different* unsaved scene and does not reload the Museum if it is already open,
and registers every object it creates with Undo under `MuseumUIStyle.UndoLabel`. Running one
twice changes nothing.

### Placement is relative to the screens, not the world

`MuseumScreenGeometry` reads every `Vid <Game>` group's canvas and reports a `ScreenFrame`:
centre, `Out` (the normal pointing into the room), `Along` (the wall's direction), width and
height. Frames, signage and room decor express every position in that frame — metres out from
the wall and along it — so re-hanging a screen re-hangs everything around it. The one thing
never used is `transform.position` of a building mesh: most of the museum's meshes carry their
placement in their vertices, and a prop's transform can sit twenty metres from where it renders.
`MuseumBlockerCleanup` and the decor builder both work from renderer bounds for that reason.

### The viewing corridor

`MuseumRoomDecorBuilder.CorridorHalfWidth` (2.1 m either side of the picture's centre line,
from `CorridorStart` 1.3 m out) is the strip a visitor watches from. The decor builder refuses
a slot inside it, and `Report Viewing Blockers` polices what the building itself left there:
eye points at `EyeHeight` 1.6 m, `NearStand` 2 m to `FarStand` 9 m from the wall, against a
grid on the picture. The last run reports **"every screen has a clear line of sight"**; the
two vitrines on Dam-daman and Dampar still graze a corner line from right at the rope, which is
below the picture and left alone.

## Materials

`MuseumDecorMaterials` is a palette of URP Lit materials under `Assets/Material/Decor/`,
created on demand and reused by asset path: `Frame_Wood`, `Frame_Gold`, `Backboard`,
`Teak_Light`, `Bamboo`, `Board_Cream`, `Iron`, `Stone`, `Cloth_Red`, `Cloth_Indigo`, `Leaf`,
`Terracotta`, `Coconut_Husk`, `Chalk_White`, `Shell_Ivory`, `Rubber`, plus the lobby's
`Petak_Cream` (emissive, for the hopscotch tiles). A hand-tweak to one of them recolours every
prop that uses it. Nothing richer than a flat colour lives here on purpose: the models are
low-poly and read at gallery distance, and a texture per prop is exactly the payload
[asset-budget.md](asset-budget.md) exists to prevent.

## The Blender pipeline

The 28 props under `Assets/Models/Generated/` were built through the Blender MCP bridge from a
script kept outside the repo (`C:\tmp\bprops.py` on the authoring machine). Each is a
hand-scaled low-poly model with its **materials named after the palette** (`Teak_Light`,
`Cloth_Red`, …); `GeneratedModelPostprocessor` matches on that name at import and swaps in the
shared asset, so an FBX never ships its own material. Read/write off, no rig, no animation.

```
Ancak_Tray  Bamboo_Rack  Bathok_Shells  Bekelan_Set  Bentengan_Fort_Indigo  Bentengan_Fort_Red
Benthik_Sticks  Bitingan_Bundle  Blarak_Frond  Cirak_Tiles  Dakon_Board  Damdaman_Board
Dampar_Table  Egrang_Stilts  Engklek_Court  Gatheng_Stones  Gobak_Field  Gobak_Flag  Gong_Stand
Gunungan_Wayang  Jamuran_Cluster  Kendhi_Pot  Lompat_Tali  Low_Table  Payung_Ceremonial
Plinth_Teak  Suweng_Cushion  Tikar_Mat
```

To add a prop: model it at metre scale with the origin on the floor, name its materials from
the palette, drop the FBX in the folder, then `Reimport Generated Models`. A material name that
is not in the palette is left as Blender exported it and logged.

## Room decor — one bay, one game

`MuseumRoomDecorBuilder.Bays` is a table keyed like `LessonContent` (`dakon`, `engklek`,
`cublak_cublak_suweng`, …). Each bay has a hero piece under the screen — always the game's own
object: a dakon board on a low table, the two forts flanking Bentengan, stilts for Egrang, a
chalked court for Engklek, a mushroom cluster for Jamuran — and a few pieces in the wings
(`L1`/`L2`, `R1`/`R2`, beyond the batik and plinths at |along| ≥ 4 m). Under-screen props are
capped at `UnderScreenMaxHeight` 1.25 m so nothing reaches the info board; anything at least
`ColliderMinHeight` 0.4 m tall gets a box collider so a visitor cannot walk through a gong
stand. Before committing, each prop's bounds are tested against the building's renderers and
nudged along `NudgeOffsets`, or dropped with a warning.

## The lobby gallery — lantai 1

The ground-floor lobby (floor at **y = 1.60**) had nothing in it but the reception desk, the
big gong and the gasing sculpture. `MuseumLobbyGalleryBuilder` turns it into an exhibition
about Javanese game culture with eight stations, four of them interactive. Everything is under
`Lobby Gallery (Generated)`; the lesson copy is `LobbyLessonContent` (editor-only, verbatim
authoring record like `LessonContent`) written to `Assets/Resources/lessons/lobby_<key>.asset`
and shown on the same `LessonPanel` the exhibit plaques use, via
`MuseumLessonUIBuilder.BuildPanelAt` with the eyebrow of each station instead of
`MATERI BELAJAR`.

| Station | Where (x, z) | Faces | Interaction |
|---|---|---|---|
| **Sambutan** (welcome) | (26.4, 5.3), behind the reception desk | −z | plaque only; two ceremonial payung flank it |
| **Gong** | the building's `Gong_Ageng_Lobby` at ≈(33.9, 19.5); plaque at (39.6, 7.2) | +x | `GongStation` — Enter swings the tabuh into the disc's centre and plays the gong |
| **Gasing** | around `Gasing_pedestal` ≈(29.4, −6.8); plaque at z −12.6 | −z | `GasingStation` — Enter spins the sculpture's body/shoulder/stem about the pedestal axis, decaying with `drag` |
| **Engklek court** | x 10, from z −3.5 north, 7 tiles + GUNUNG | — | `HopscotchCourse` — walk the tiles in order; each row lights, wrong row resets, reaching the gunung plays a fanfare |
| **Tembang** (songs) | stage at (10, −18) | +z | `SongStation` — Enter steps the lyric banner through `lines[]` with a tone per line |
| **Ragam** (the eighteen games) | (43, −8.2) | +z | plaque + table cluster of props |
| **Filosofi** | (43, −19.5) | −z | plaque + kendhi and gunungan on plinths |
| **Etika** | (46.2, 3.5) | +x | plaque + bamboo rack, blarak frond, ancak tray |

Plaque carpentry is the same stock as the screen bezels (`MuseumScreenFrameBuilder.Bar`): a
teak frame with a gold lip on two posts, panel centre at `ReadingHeight` 1.88 m above the
floor — the same eye rule as the exhibit plaques. Each plaque's reader volume is a floor
trigger (`Volume`), and every prop's footprint is checked against the building; the build log
ends with `built 8 stations … (0 footprint warning(s))`.

### The station scripts (`Museum.Core`, runtime)

- **`IInteractable`** — `Interact()` plus the hint text. `TouchInteractRouter` now holds a
  third slot, `CurrentInteractable`, beside the doorway prompt and the lesson reader; the
  Interaksi button and Enter act on it when no doorway is registered
  ([input-and-platform.md](input-and-platform.md)).
- **`GalleryStation`** (abstract) — trigger volume + `playerTag` + a 3D `hint` label that
  appears while the visitor stands inside; registers itself with the router on enter, reads
  Enter on desktop. Subclasses: `GongStation` (`mallet`, `swingAxis`, `swingDegrees`,
  `source`), `GasingStation` (`parts[]`, `axisPoint`, `kick`, `maxSpeed`, `drag`),
  `SongStation` (`lyric`, `idleText`, `lines[]`, `secondsPerLine`).
- **Shared with other visitors** (`MuseumInteractions`, see
  [networking.md](networking.md)): each station's id is in code (`StationId` — `gong`,
  `gasing`, `tembang`; the court is `engklek`), so no scene field carries it. Another visitor's
  press plays the same effect here (`OnRemoteInteract`), except the tembang, which plays the
  melody and leaves the lyric banner alone, and the court, which lights the petak and plays the
  chime/fanfare without touching this visitor's run or status line. `TuneSource` is **linear**,
  2.5 m → silent at 25 m (`GalleryStation.AudibleDistance`).
- **`HopscotchCourse` / `HopscotchTile`** — the tiles are triggers with a `face` renderer and
  a `litColor`; the course tracks the `rows[]` index reached and drives the `status` label
  (`idleText` → `Petak N` → `doneText`).
- **`ProceduralAudio`** — synthesises the gong, the spinning-top whir, the song tones and the
  fanfare as `AudioClip`s at runtime. No audio files ship for the lobby.

The hints and the hopscotch readout are **3D `TextMeshPro` labels, not canvases**, built at
0.1 scale — font size 10 is ≈ 0.1 m of line height, so the hints are size 9, the court readout
8, the lyric banner 11. They were first built at 20–26 and filled the room.

### Verifying it

Play mode, teleport the player rig with `rb.position = …; rb.linearVelocity = Vector3.zero;
rb.WakeUp();` — without the wake-up the trigger enters never fire and every station looks
dead. Then Enter at each station and walk the court. Verified 2026-09-10: gong clip plays and
the head reaches the disc plane (head centre at n·p 19.60 against the disc at 19.49, head
0.18 m thick), the gasing body rotates, the lyric advances, the court reports `Sampai gunung!`,
no exceptions in the log.

## What is deliberately not here

- No new audio, texture or font assets; no package. The bezels, plaques and stations are
  primitives, palette materials and the generated FBXs.
- No colliders on wall dressing (frames, signage): they would catch the screens' trigger
  raycasts. Colliders only on things a visitor could walk into.
- No hand-placed position that a rebuild would lose. If a station wants moving, change the
  constant in the builder and rebuild.
