# UI Style

How UI gets built in this client. The rules below are extracted from `MainMenu.unity`
and `Dakon.unity` — they are not aspirational, they describe the system already in the
scenes. Follow them so new screens match without re-deriving numbers. Screen *flow*
lives in [ui-flow.md](ui-flow.md); this doc is the *look*.

All sizes and font sizes below are in **800×600 design units**.

## 1. Canvas

Every screen Canvas is set up identically. Copy these values exactly — a Canvas with a
different reference resolution makes every font size and rect size in this doc
meaningless on that screen.

| Setting | Value |
|---|---|
| Render Mode | Screen Space – Camera (assign the scene's camera) |
| UI Scale Mode | Scale With Screen Size |
| Reference Resolution | **800 × 600** |
| Screen Match Mode | Match Width Or Height, **Match 0.5** |
| Reference Pixels Per Unit | 100 |
| GraphicRaycaster | defaults (Ignore Reversed Graphics on, Blocking Objects None) |

One EventSystem per scene, using **`InputSystemUIInputModule`** — the project is on the
new Input System, so never add the legacy `StandaloneInputModule`.

## 2. Hierarchy shapes

Four composed shapes cover everything currently built. Reuse them rather than inventing
a fifth.

```
Screen                          Composed button
Canvas                          Play Button      (RectTransform + Button)
├── BG        Image, stretch    ├── Glow         Image + CanvasGroup + LoopFadeTween
├── <content groups>            ├── BG           Image (sliced) ← Button target graphic
└── <modals, last = on top>     └── Text (TMP)   stretched to fill

Modal                                            HUD chip
Pause Panel   Image (no sprite, scrim) +         Turns        Image (frame sprite)
              CanvasGroup + FadeTween +          └── Text (TMP)
              Button (transition None → close)
└── Panel     Image (frame) + CanvasGroup + PopupTween
    ├── Text (TMP)      title
    ├── <content>
    └── <buttons>
```

Naming: descriptive names with spaces (`Pause Panel`, `Back to Museum`, `Point P1`),
TMP objects named `Text (TMP)`. Modals sit last under the Canvas so they draw on top.

## 3. Anchoring

Anchors encode intent — pick the one that matches what the element *is*, not whatever
snapshot the editor produced.

| Element | Anchors | Notes |
|---|---|---|
| Background, modal scrim | stretch (0,0)–(1,1) | offsets 0, sizeDelta 0 |
| Corner utility (settings, help) | that corner, e.g. (1,1) | negative anchoredPos inset — never center-anchor |
| Bottom bar (Dakon hand) | bottom-stretch (0,0)–(1,0) | height in sizeDelta.y |
| Title, dialog, HUD chip | center (0.5,0.5) | |
| Label inside a button/panel | stretch | offsets 0 |

Pivot stays `0.5, 0.5` unless there's a reason to move it.

## 4. Typography

**TextMeshProUGUI only** — no legacy `UnityEngine.UI.Text` anywhere.

| Role | Font asset | Size | Color |
|---|---|---|---|
| Display / brand wordmark | `Assets/Fonts/CG-Regular SDF.asset`, Bold style | ~56 | `#F4EAD5` |
| Eyebrow / kicker | `Roboto-SemiBold SDF` | ~14, characterSpacing ~14 | `#C19F61` |
| Body / tagline | `Roboto-Regular SDF` | ~14 | `#D8C0A1` |
| Dialog title | `Roboto-Bold SDF` | 16–18 | `#FFFFFF` |
| Button label | `Roboto-Bold SDF` | 13.5–20 | `#FFFFFF` |
| Caption / metadata | `Roboto-Regular SDF` | 12 | `#D8C0A1` |
| HUD label | `Roboto-Bold SDF` | 10 | `#FFFFFF` |
| HUD secondary | `Roboto-Medium SDF` | 8 | `#FFFFFF` |

- **Auto-sizing off.** Pick a fixed size from the table; auto-size lets two screens
  silently disagree about what "a title" is.
- Center-aligned by default; left-align only for stat/label rows (Dakon `Point P1/P2`) and for
  paragraph copy — a centered four-line blurb is harder to read than a left-aligned one.
- **Caption size is for one-line metadata** (a cross-section spec, a count, a timestamp). Body
  size for the same string wraps in a narrow card and breaks the alignment across a row of them.
- No per-object material override — use the font asset's shared material.
- `raycastTarget = false` on decorative labels, including button labels.
- TMP's *project default* font is still LiberationSans, so **assign the Roboto/CG asset
  explicitly on every TMP component**. Don't rely on the default.

## 5. Palette

| Hex | Float RGBA | Role |
|---|---|---|
| `#C19F61` | 0.757, 0.624, 0.380, 1 | Gold — eyebrow/kicker text |
| `#F4EAD5` | 0.957, 0.918, 0.835, 1 | Cream — display wordmark |
| `#D8C0A1` | 0.847, 0.753, 0.631, 1 | Tan — body copy |
| `#FFFFFF` | 1, 1, 1, 1 | Default graphic tint, all UI labels |
| `#000000` @ 63% | 0, 0, 0, 0.631 | Modal scrim |
| `#989898` @ 35% | 0.594, 0.594, 0.594, 0.353 | Large container frame (hand panel) |
| `#FFFFFF` @ 26% | 1, 1, 1, 0.263 | Inner masked container frame |

Tint the neutral frame sprites with these colors; don't author new colored sprites.

**The two translucent tints are for containers over a flat background** — the Dakon hand panel over
its board, an inner rim inside an already-opaque panel. Over a live 3D scene they fail: the terrain
reads straight through the body copy and every card turns to mud. There, tint the frame `#FFFFFF`
(the sprite's own dark-brown fill, fully opaque) and let the scrim do the separating. Judge this by
looking at the Game view over the actual scene, not at the panel in isolation.

## 6. Frames and 9-slice

Two frame sprites, both 48px border, Image type **Sliced**:

- `Assets/Sprites/frame_default.png` — buttons, containers.
- `Assets/Sprites/frame_with_corner.png` — HUD chips, dialog panels.

Button skins: `Assets/Sprites/Buttons/Rectangle 2.png` (24px border) for the face,
`Rectangle 3.png` (100px border) for glow. Full-bleed art (`Main Menu BG.png`,
screenshots) uses Image type **Simple**.

**Corner radius is tuned with `Pixels Per Unit Multiplier`, not with new sprites.**
Higher multiplier = tighter corner. Reuse a rung of the existing ladder:

| Multiplier | Use |
|---|---|
| 1.0 | Dialog panel |
| 1.39 | Large popup |
| 1.66 | Medium dialog |
| 1.97 | Large container |
| 3.13 | Button |
| 5.05 | Small HUD chip |

## 6b. Full-screen panels over a live scene

A menu that covers gameplay (stick selection, pause, results) is read against whatever the camera
happens to be showing — sky, terrain, a moving character. Three rules keep it legible:

1. **Scrim first.** A stretched `Image` at `#000000` @ 63% under the content. It is the separation;
   the panels on top are not doing that job.
2. **Content panels are opaque.** Frames tinted `#FFFFFF` per §6 — never the translucent container
   tints over 3D.
3. **One tween group, not two nested dimmers.** The scrim root carries the `CanvasGroup` +
   `FadeTween`, and the content inside it carries its own `CanvasGroup` + `PopupTween`. Set the
   scrim's `FadeTween.toAlpha` to **1** when content lives inside the same group — the stock 0.6 is
   tuned for a backdrop with nothing in it, and leaves the content permanently at 60%.

The run UI the panel covers (HUD, timing bar, its buttons) belongs under one parent that ships
**inactive** and is switched on when the panel commits. That, not a flag on each widget, is what
stops input reaching gameplay through the panel.

## 6c. Padding, and the strip of many items

Two rules that only show up once real content lands in a frame:

1. **Nothing sits on the frame's bevel.** A label stretched to its container's rect touches the
   nine-slice's lit edge and reads as clipped. Inset it: **14 × 5** inside a HUD chip, **12 × 4**
   inside a button, **20** inside a dialog panel. This is padding, not a smaller font — shrinking
   the type to make room is how two screens end up disagreeing about §4.
2. **A row of N items scrolls; it does not shrink to fit.** When the count is content-driven
   (Dakon's fifteen-card hand), a `HorizontalLayoutGroup` with `childForceExpand` on divides one
   screen width between them and every item comes out a sliver. The shape that works:

   ```
   Frame          Image (sliced) + ScrollRect (horizontal only, vertical off)
   └── Viewport   Image + Mask          ← no layout group here: it fights the fitter
       └── Content  HorizontalLayoutGroup (childControl on, childForceExpand OFF)
                    + ContentSizeFitter (horizontal PreferredSize, vertical Unconstrained)
           └── Item prefab with a LayoutElement carrying its min/preferred size
   ```

   The item's size lives on its `LayoutElement`, not only in its rect — under a controlling
   layout group the rect is overwritten. Vertical fit stays Unconstrained, or the strip collapses
   to nothing the moment the last item leaves.

## 6d. World-space panels standing in the museum

A panel mounted on a prop (the lesson plaques, the video screens) is still authored in the
same 800×600 units — it just gets scaled down to whatever the prop measures instead of to the
screen. What changes:

| Setting | Value | Why |
|---|---|---|
| Render Mode | **World Space** | It is an object in the room, not an overlay |
| Rect size | authored in design units (820 × 520 for the lesson plaques) | So §4's font sizes still mean something |
| Transform scale | placed by hand, or sized against the neighbouring video frame | Placement is a judgement about the room, not a formula |
| Dynamic Pixels Per Unit | **3** | At 1 the text is mush; the canvas is metres wide, not pixels |
| GraphicRaycaster | **none** | See below |
| Offset from the surface | ~2 cm off the wall or prop | Anything less z-fights the mesh |
| Facing | set it in the scene and look through the Game view | Which side reads correctly is not obvious — check, do not derive it |

**Type runs ~2× the §4 table.** A world panel is read standing in a room, not at a desk: the
lesson plaques carry body at 25, tabs at 18, a 38 display title. Grow the *rect* with the first
step up, then stop — the panel occupies a fixed span of wall, so once that is set **font size is
the only dial that changes how big the copy actually is**; enlarging the rect at the same metres
makes the text smaller. It is paid for in pages, and paging here is a keypress, not a scroll.

**Height above the floor is a rule, not a per-panel judgement.** Which wall a panel hangs on is
authored; how high it hangs is not. Eyes are at the same height in every room, so measure from
the floor of the volume the visitor reads from — the lesson plaques' centres all sit 1.88 m
above it — rather than from whatever the neighbouring prop happens to be hung at.

**A section indicator is a strip of states, never a row of buttons.** Where a screen would put
tabs, a world panel puts something that only *looks* like tabs: the lesson plaques name their
four sections across the top, mark the one being read, and are stepped with the same Q/E as
everything else. The shape:

```
Tabs      HorizontalLayoutGroup, childAlignment LowerLeft, controlling HEIGHT but not width
          ↳ every tab shares a bottom edge on a divider rule; the active one grows upward
└── Tab   Image (frame_default at ChipPixelsPerUnitMultiplier)
    │     + LayoutElement (height: 33 idle / 40 active — the view rewrites this)
    │     + inner HorizontalLayoutGroup for padding + ContentSizeFitter (horizontal only)
    ├── Accent      2-unit Gold rule, top-anchored, ignoreLayout, active tab only
    └── Text (TMP)  Roboto-SemiBold, NoWrap
```

The active tab overlapping the divider is what makes the strip read as tabs rather than as a row
of chips. Let the fitter size each tab to its own label; forcing a common width leaves the short
headings swimming. The view owns the heights and tints, so a tab whose section has no content
can simply be switched off.

**Resize a fitted rect through its `LayoutElement`, never through `sizeDelta`.** A width driven
by a `ContentSizeFitter` and a hand-written `sizeDelta` rebuild in the wrong order: the fitter
measures zero and the whole strip collapses into a stack in the corner. It reads as a broken
layout group and is actually the write.

**No raycaster, no Buttons.** The museum is FPS mode with the cursor locked, so a world-space
Button is unclickable by construction. Interactive world panels are driven by the keyboard
from a trigger volume instead, and their controls are drawn as **keycaps** (§7's frame at
`ChipPixelsPerUnitMultiplier`, 34 × 28) rather than as buttons — a control that looks pressable
but is not is worse than one that reads as a key hint.

**Check facing by looking, not by reasoning.** UI is `Cull Off`, so a world canvas draws from
both sides and a wrong rotation gives you mirrored text rather than an invisible panel. Which
yaw is correct depends on the viewing side *and* the rotation together — the two mirrorings can
cancel. The museum's lesson panels sit at yaw 180 and read correctly. Put a camera where the
visitor stands and look.

World-space uGUI is unlit, so the panel keeps a constant brightness under the museum's 71
realtime lights. Do not push it to pure white: the project is Linear with bloom, and Cream at
~95% is as bright as copy should get.

## 7. Buttons

Transition **ColorTint** with the stock ColorBlock, unchanged: Normal `#FFFFFF`,
Highlighted `#F5F5F5`, Pressed `#C8C8C8`, Selected `#F5F5F5`, Disabled `#C8C8C8` @ 50%,
Color Multiplier 1, Fade Duration 0.1. Don't hand-tune per button — uniform feedback is
the whole point.

- Target graphic is the button's own `Image`, except in the composed-button shape where
  it is the child `BG`.
- Navigation stays **Automatic**; add explicit links only when keyboard nav is a stated
  requirement.
- Modal scrims are Buttons with transition **None** — invisible click-outside-to-close.

## 8. Motion

LeanTween is the tween library here. No DOTween.

**Every animated UI element gets a `CanvasGroup` paired 1:1 with a tween component**,
and the tween plays on `OnEnable` — callers only `SetActive(true)`, never poke the tween.

| Component | Use | Settings |
|---|---|---|
| [`Tweens/FadeTween.cs`](../Scripts/Tweens/FadeTween.cs) | Modal dim backdrop | in 0.25 / out 0.2, ignoreTimeScale |
| [`Tweens/PopupTween.cs`](../Scripts/Tweens/PopupTween.cs) | Dialog / result pop | fromScale 0.6, show 0.35 easeOutBack; toScale 0.7, hide 0.2 easeInBack; fadeRatio 0.6 |
| [`LoopFadeTween.cs`](../Scripts/LoopFadeTween.cs) | Infinite attention pulse (glow) | alpha 0.7→0.2, 1s, PingPong, loop -1 |
| [`Tweens/LeanTweenPlayModeReset.cs`](../Scripts/Tweens/LeanTweenPlayModeReset.cs) | Editor-only: resets LeanTween statics each Play | see the domain-reload note below |

**Not everything that moves is a tween.** Dakon's drop feedback (`DakonVignette`) pulses its
alpha from a plain coroutine, because it must fire on every drop of a match played in the
Editor — including the second Play after a recompile, where the domain-reload trap below
strands LeanTween. Its two tints, green `(0.25, 0.80, 0.35)` and red `(0.85, 0.20, 0.20)`, are
Egrang's `SkillCheckTrackColors.Default` full/fail pair: right and wrong are the same two
colours in both games.

Between-scene transitions go through `Assets/Scripts/Core/SceneLoader.cs` — don't
hand-roll scene fades.

**LeanTween vs. `DisableDomainReload`.** This project enters Play mode with domain reload off
(`EditorSettings.enterPlayModeOptions`). LeanTween's `init()` only builds its `~LeanTween`
updater when its static `tweens` array is null, but that GameObject dies with the Play session
while the array survives — so from the *second* Play after a recompile it holds tweens and no
updater, and nothing animates. Because `PopupTween.Show()` sets alpha 0 and a shrunken scale
before handing off to LeanTween, an active panel then never appears at all: the museum's Enter
prompt invisible, the lobby showing only its background, no error anywhere. A build or a
Multiplayer Play Mode virtual player is a separate process with a fresh domain and is fine,
which makes it look like a single broken client.
[`Tweens/LeanTweenPlayModeReset.cs`](../Scripts/Tweens/LeanTweenPlayModeReset.cs) clears the
statics on `SubsystemRegistration`; if you ever see a panel that is active but invisible, check
that it still exists before blaming the scene.

## 9. View scripts

- Views are **presentational only**: render supplied state, raise events, own nothing.
  See `DakonCard.cs`, `EgrangStickCard.cs`.
- Wire refs with `[SerializeField] private` + `[Header]`/`[Tooltip]`, assigned in the
  Inspector. `GetComponent` only as a documented fallback when the field is left empty.
- Namespaces `Museum.Core` / `Museum.Games.<Game>`; new view classes `sealed`.
- Repeated items (cards, rows) = prefab + container instantiated at runtime — see
  `Assets/Prefabs/Card.prefab` with `DakonView.handContainer`.
- Editor-generated UI is fine for big static layouts (see
  `Assets/Scripts/Games/Egrang/Editor/EgrangStickSelectionUIBuilder.cs`), but the
  generated result must obey every rule above — the 800×600 canvas especially.
- A generator **adopts what the scene already has** rather than emitting a rival: the Egrang builder
  wires the panel to an existing `SkillCheckBar` and disables that bar's canvas as the run root
  instead of generating a second timing bar. Re-running a generator should be safe on a scene
  someone else has been editing.
- The tween components live in the default assembly (LeanTween ships without an asmdef, so they
  cannot sit in one), and an asmdef cannot reference the default assembly. Editor generators inside
  an asmdef therefore add them via `Type.GetType("FadeTween, Assembly-CSharp")`.
- **A panel authored inactive runs `Awake` inside the call that shows it.** A view whose `Awake`
  hides its own root (the usual "start hidden" idiom) therefore closes itself in the same frame it
  was opened: state says shown, screen shows nothing. Guard the hide (`if (!IsShowing)`) and read
  the root through a lazy property so `Show` works before `Awake` has run. `EgrangResultsView` is
  the worked example; a view built in code is active from `AddComponent` onward, so only a test
  that starts the object inactive reproduces it (`EgrangResultsViewTests`).
- Generators share one style source. `Assets/Scripts/Games/Egrang/Editor/EgrangUIStyle.cs` holds the
  palette, font paths, nine-slice ladder and the `CreateUI`/`CreateButton`/`SetFrame`/`StyleText`
  helpers; builders pull it in with `using static`. Two generators with their own copy of the
  palette are two screens free to drift apart.

## 10. Checklist for a new screen

- [ ] Canvas: Screen Space – Camera, 800×600, Match 0.5, PPU 100.
- [ ] EventSystem uses `InputSystemUIInputModule`, not the legacy module.
- [ ] Every TMP component has its font asset explicitly assigned; auto-size off.
- [ ] All colors come from §5; no new colored sprites.
- [ ] Frames are `frame_default` / `frame_with_corner`, Sliced, PPU multiplier from §6.
- [ ] Buttons use stock ColorTint values; scrims use transition None.
- [ ] Anything that animates has a `CanvasGroup` + one tween component, plays on enable.
- [ ] Anchors match element role (§3); pivot 0.5,0.5.
- [ ] Text is inset from its frame, never stretched onto the bevel (§6c).
- [ ] A content-driven row scrolls at a fixed item size rather than force-expanding (§6c).
- [ ] Over a live 3D scene: scrim present, content frames opaque, `FadeTween.toAlpha` 1 (§6b).
- [ ] The gameplay UI a full-screen panel covers sits under one inactive parent (§6b).
- [ ] No `UnityEngine.UI.Text`.
