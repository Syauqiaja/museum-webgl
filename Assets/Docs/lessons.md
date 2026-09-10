# Lesson Plaques

The educational copy that stands in the museum itself: a world-space panel on the wall beside
each game's exhibit screen, paged with Q and E. Built for **all eighteen games**.

This doc covers where the text comes from, how it reaches the scene, and what to do to add
the next game. The look of the panel follows [ui-style.md](ui-style.md); the scene contents
it adds are listed in [scene-setup.md](scene-setup.md).

## Where the text comes from

`AMS-NAM.xlsx`, sheet `V03-OK` — a curriculum matrix authored by **Agus Muji Santoso**
(created 2026-05-07, last revised 2026-08-20), delivered outside the repo. It has one row
per game, rows 6–23, eighteen games:

> Dakon, Engklek, Cublak-cublak Suweng, Egrang, Gobak Sodor, Bentengan, Benthik, Gatheng,
> Bekelan, Lompat Tali, Dam-daman, Cirak, Dampar, Sluku-sluku Bathok, Jamuran, Bitingan,
> Ancak-ancak Alis, Blarak Sempal

Four of its columns are exhibit copy and are the ones shown:

| Col | Heading on the panel | What it is |
|---|---|---|
| G | `KONSEP SAINS` | The science the game demonstrates, pitched at Fase D |
| H | `SPORT SCIENCE` | Anatomy, joints, muscles, biomechanics of playing it |
| I | `ASAL-USUL` | Where the game comes from |
| J | `SENI & BUDAYA` | Philosophy, values, song lyrics |

Columns **E (`Pertanyaan Pemantik`) and F (`Pertanyaan Lanjutan`) are deliberately excluded**.
They are prompts a teacher asks a class — "Mengapa beberapa biji terasa lebih nyaman
dimainkan…" — and a plaque that asks a visitor a question it will never answer reads as
broken. If a guided-tour mode ever wants them, they are still in the spreadsheet.

The text is Indonesian and is shown **verbatim**, including its typography (`–`, `—`) and its
inconsistencies (`Anatomi Gerak :`, `Anatomi pada Bethik =`). It is someone else's authored
copy; tidying it silently would be editing a source we do not own.

## The pipeline

```
AMS-NAM.xlsx            Editor/LessonContent.cs        Resources/lessons/<key>.asset      the scene
(outside the repo)  ──►  const strings, verbatim   ──►  GameLessonData (ScriptableObject) ──► LessonPanel
                         authoring source               runtime source of truth
```

- **`Assets/Scripts/Core/Editor/LessonContent.cs`** — the copy as `const string`s. Editor-only:
  it is the authoring record, so a text change is a reviewable diff rather than an invisible
  edit to a binary asset.
- **`Assets/Resources/lessons/<key>.asset`** — one `GameLessonData` per game, with `gameKey`,
  `displayName` and a list of `LessonSection { heading, body }`. Written by the builder;
  editable by hand in the Inspector if someone needs a quick fix without the Editor scripts.
  A **list**, not four fields, because several rows in the matrix have gaps and a game with
  three sections must not render a blank fourth page.
- **`Museum/Rebuild UI/Lesson Panels`** (`MuseumLessonUIBuilder`) — writes the assets, then
  builds the plaques. Re-runnable: the contents are regenerated, the placement is adopted from
  whatever is already in the scene. It never reopens the Museum scene if it is already open —
  doing so would discard an unsaved nudge and then "adopt" the older pose off disk.

## What gets built in the scene

Everything lives under one scene-root container, the way the lighting builder keeps its output
under `Lighting (Generated)`:

```
Lessons (Generated)          identity transform — nothing inherits a scale
├── Dakon Lesson
│   └── Lesson Panel (Dakon)   world-space Canvas, 820×520 design units, LessonPanel
├── Egrang Lesson
│   └── …                      one pair per game, eighteen of them
```

Never parent a panel to an exhibit prop: the info-panel meshes are unit boxes scaled
non-uniformly (`70 × 5 × 45` for Dakon) and would shear the canvas.

Plus **one component per game on an existing trigger**: a `LessonReader` on the
`VideoTriggerPlayer` volume that already starts that game's video, pointed at the panel. That
volume is the floor a visitor stands on to watch the screen, which is exactly where they stand
to read the panel beside it — so a lesson adds **no collider and no new geometry**. All
eighteen games already have one. For Dakon the same object is also the `Dakon Doorway`, so one
trigger serves three purposes; `Enter` (doorway) and `Q`/`E` (panel) do not collide.

Two consequences worth knowing: the volumes are large (Dakon's is 22.9 × 1.5 × 10 m), so the
keys go live well before the panel is close enough to read; and the reader shares a GameObject
the builder does not own, so a rebuild strips every `LessonReader` first — destroying the panel
roots would not remove them.

### What the panel looks like

```
Lesson Panel (Dakon)   820 × 520 design units, pad 32, dynamicPixelsPerUnit 3
├── Backdrop           RectMask2D
│   └── BG             Main Menu BG.png, Simple, white, overscanned 1.3× and centred
├── Frame              frame_with_corner, ContainerFrame tint
├── Title              CG-Regular 38, Gold, top-left        Eyebrow  MATERI BELAJAR, Medium 14
├── Divider            the rule the tabs stand on, y −122
├── Tabs               one per section — see below
├── Body               Roboto-Regular 25, Cream, TMP paging
└── Footer Divider · Q/E keycaps · Dots · Page Label · Hint
```

The surface is the **main menu's own batik art, fully opaque** — the plaque is read against a
lit interior, and a translucent fill there turns the terrain behind it into part of the copy
(ui-style.md §5). The art carries a baked `16 Permainan Tradisional Indonesia` caption along
its bottom edge, which belongs on the menu and not on a plaque; overscanning it 1.3× inside a
`RectMask2D` pushes that band past both edges.

Type runs about **2× the screen-UI table** in ui-style.md §4. These panels are read standing in
a gallery, not at a desk. The rect grew with the first step up (660 × 440 → 820 × 520), but the
plaque is a fixed 2.9 m of wall, so past that point **font size is the only dial that makes the
copy physically larger** — growing the rect at the same metres shrinks it. The price is pages,
and a page is a keypress.

### The tabs

The four sections are named across the top as a **tab strip**, active tab raised and Gold, the
rest dim and sunk into the panel. It is an *indicator, not a control*: the museum locks the
cursor, this canvas has no raycaster, and nothing here can be clicked. Q/E still step the flat
page list; the active tab follows whichever section the current page belongs to, so a visitor
can see at a glance what else the plaque holds and how far in they are.

Mechanically: a `HorizontalLayoutGroup` aligned `LowerLeft` that controls **both axes**, so
every tab shares a bottom edge on the divider and the active one grows *upward* (40 units
against 33). Widths are each tab's own preferred width — its inner padding group plus the
label — because the four headings are different lengths and a common width would leave
`ASAL-USUL` swimming; each tab's `LayoutElement` carries `flexibleWidth = 0` so the leftover
space is not shared out. `LessonPanel` owns the tints, the Gold accent rule and the two
heights, and hides the tab of any section that produced no pages: the matrix has gaps, and a
tab a visitor can never reach is worse than no tab.

**The strip measures the tabs; nothing else may write their width.** The widths used to come
from a per-tab `ContentSizeFitter` while the strip ran `childControlWidth = false`, and the two
rebuild in the wrong order: `SetLayout` runs parent→child, so the strip placed the tabs at their
default widths and the fitters resized them afterwards, with nothing re-marking the strip. In
the Editor a second rebuild — a recompile, an inspector poke — hid it; **in a build the first
pass is the only pass, and the four tabs came out overlapping in a stack at the panel's left
edge** (2026-09-10). Asking the strip for preferred widths instead is correct in one pass,
because `CalculateLayoutInputHorizontal` runs leaves-first. The same trap on the other axis:
**the runtime moves a tab's height through its `LayoutElement`, never through its rect** — a
hand-written `sizeDelta` on a laid-out rect collapses the strip the same way.

### Where the panel hangs

Placement is a judgement about the room — which wall, what it hangs beside, how big it wants
to be next to the video frame — so the builder does not own it. It picks a pose in this order:

1. **Adopted from the scene.** If a `Lesson Panel (<Game>)` already exists *anywhere* in the
   scene, its position and rotation are read back and reused verbatim. Placing one by hand is
   the intended workflow; rebuilding regenerates the *contents* and leaves the placement alone.
   The *scale* is re-derived from the old rect: what an author placed is a size on the wall, so
   when the design-unit rect changes — as it did when the type grew and the panel went
   660 × 440 → 820 × 520 — the metres are held and the scale moves to match. The rebuild log
   says so when it happens.
2. **Beside the game's exhibit screen.** First build only: same wall, same height, same
   rotation, `1.12×` the video frame's height, `0.4 m` of wall between them. A lesson is the
   reading companion to that footage, so it belongs in the same band of wall.

**How high it hangs is not part of that judgement.** Whichever pose wins, the panel's centre is
then dropped to **1.88 m above the floor of its reading volume** — the same collider the
`LessonReader` rides, because that is the ground a visitor actually stands on. Eyes are at the
same height in every room, so eighteen plaques at eighteen elevations read as a mistake rather
than as a choice. Dakon's was the hand-placed one; the other seventeen, derived from screens
hung at their own heights, sat 0.53 m above it until the rule was added. A game with no trigger
volume keeps its authored height — there is nothing to measure against.

The rotation is copied from the screen rather than derived — see below.

Dakon's is authored: **2.88 × 1.83 m at `(13.34, 9.58, −26.54)`**, 3.1 m to the left of the
`DAKON.mp4` frame (2.65 × 1.64 m) and a little below its centre line. The other seventeen were
derived from it and come out at **2.89 × 1.83 m**, ~3.1 m from their screen.

### Facing is adopted too

Placement includes which way it points, and that is authored like everything else. Dakon's
panel is at yaw 180 — its canvas +Z faces the outer wall — and it reads **correctly** from the
gallery floor. Verified by rendering the scene from a camera at standing height on `L1_N`, not
by reasoning about it: an earlier version of this builder "corrected" the yaw on the theory
that a canvas is only readable from its +Z side, and it was wrong. The two mirrorings (viewing
side and the 180° yaw) cancel.

If a panel ever does come out mirrored, turn it in the scene and rebuild — the builder will
adopt whatever you leave it at.

## Reading it

The museum is FPS mode. `FPSController` locks the cursor in `Awake` and re-locks it on any
click, so **there is no pointer** — no `Button`, no `GraphicRaycaster`, no crosshair click.
All paging is keyboard, and the panel carries no raycaster at all.

- The panel renders **always**, readable from across the room.
- Stepping into the doorway's trigger makes the keys live: the keycaps go Gold and the hint
  changes from `Dekati untuk membaca` to `Q E  ganti halaman`. Stepping out greys them again.
  Without a cursor, that tint is the only "this responds to you" signal a visitor gets.
- **Q** goes back a page, **E** goes forward. They sit under the hand already on WASD, they do
  not collide with movement, and they stay clear of a doorway's `Enter`, so a trigger that is
  both a doorway and a plaque stays unambiguous. The Museum's KONTROL card advertises them as
  a third row (`Q E — Ganti halaman`), written by the same builder.
- Paging **wraps**. An unattended exhibit should never present a control that does nothing.

Long sections are paginated by TMP itself (`TextOverflowModes.Page`) at a **fixed** font size.
Auto-shrinking Dakon's 1287-character Sport Science block would make it unreadable at standing
distance, and scrolling needs a pointer we do not have. `LessonPaging` (pure C#, tested in
`Assets/Scripts/Core/Tests/LessonPagingTests.cs`) flattens the per-section page counts into
the single order the visitor steps through. Dakon's four sections come to **ten pages** at the
current type size (1 · 4 · 1 · 4).

## All eighteen games

Every game in the spreadsheet is built. The mapping is by the `Vid <Game>` group in
`Museum.unity` — that group owns the exhibit screen the panel hangs beside and, through its
`VideoTriggerPlayer`, the volume that pages it.

| Game | Group | Pages | | Game | Group | Pages |
|---|---|---|---|---|---|---|
| Dakon | `Vid Dakon` | 10 | | Cirak | `Vid Cirak` | 10 |
| Engklek | `Vid Engklek` | 9 | | Dampar | `Vid Dampar` | 8 |
| Cublak-cublak Suweng | `Vid Cublak` | 9 | | Sluku-sluku Bathok | `Vid Sluku` | 12 |
| Egrang | `Vid Egrang` | 10 | | Jamuran | `Vid Jamuran` | 11 |
| Gobak Sodor | `Vid Gobak` | 7 | | Bitingan | `Vid Bitingan` | 8 |
| Bentengan | `Vid Bentengan` | 8 | | Ancak-ancak Alis | `Vid Ancak` | 10 |
| Benthik | `Vid Benthik` | 8 | | Blarak Sempal | `Vid Blarak` | 9 |
| Gatheng | `Vid Gatheng` | 8 | | Dam-daman | `Vid Damdaman` | 9 |
| Bekelan | `Vid Bekelan` | 8 | | Lompat Tali | `Vid Lompat` | 9 |

Most of these games have **no minigame** and never will — the panel is the whole exhibit for
them. The `*_ipanel` meshes scattered around the rooms are still bare props; the lessons hang
on the walls beside the screens instead, which is where visitors already stop.

### Changing the copy

The spreadsheet is the source. `LessonContent.cs` is **generated** from it, not typed: all 72
strings were checked back against the sheet character for character. To change a game's text,
edit `LessonContent.cs` directly and re-run the builder — but if the spreadsheet is revised,
regenerate rather than patching by hand, and re-run the same comparison.

Adding a nineteenth game means: a row in `LessonContent.All()` with its four strings, a
`Vid <Game>` group in the scene holding a screen and a trigger, then
`Museum/Rebuild UI/Lesson Panels`. No new scripts.

## The lobby plaques reuse the panel

The ground-floor gallery ([museum-decor.md](museum-decor.md)) shows eight more plaques on the
same `LessonPanel`, through `MuseumLessonUIBuilder.BuildPanelAt` — the same 820 × 520 rect,
tabs, Q/E paging and 1.88 m reading height, with the eyebrow `SELAMAT DATANG` on the welcome
plaque and `GALERI LANTAI DASAR` on the other seven instead of `MATERI BELAJAR`. Their copy is **not** from the spreadsheet: it
is `LobbyLessonContent.cs` (editor-only, same authoring-record discipline as `LessonContent`),
written to `Assets/Resources/lessons/lobby_<key>.asset` — `lobby_sambutan`, `lobby_gong`,
`lobby_gasing`, `lobby_engklek`, `lobby_tembang`, `lobby_ragam`, `lobby_filosofi`,
`lobby_etika`. They are built and placed by `Museum/Decor/Build Lobby Gallery`, not by
`Rebuild UI/Lesson Panels`, and their placement is a constant in the builder rather than
adopted from the scene. The `LessonReader` for each rides the station's own floor trigger.

## Fonts

The Roboto SDF assets are **dynamic with empty character tables**, rasterising from the
bundled TTF at runtime, and `TMP Settings.asset` configures **no fallback font**. Indonesian
is ASCII, but the copy contains `–` (en dash) and `—` (em dash). Roboto has both; anything it
lacks would render blank rather than as a missing-glyph box. Check new copy in a build, not
only in the Editor.
