# Egrang Stick Selection — Design

Date: 2026-08-13
Scope: `Assets/Scripts/Games/Egrang/` (selection + difficulty only)

## Problem

The Egrang scene drops the player straight onto one fixed timing bar
([2026-08-12-egrang-skill-check-bar-design.md](2026-08-12-egrang-skill-check-bar-design.md)).
The game concept calls for the player to pick one of three stilts first, and for that
pick to set the difficulty.

## Concept

Difficulty comes from the pole's cross-section — the larger the bearing surface, the
steadier the stilt. At a common 8 cm maximum width:

| Shape     | Example size          | Area      | Difficulty |
|-----------|-----------------------|-----------|------------|
| Persegi   | sisi 8 cm             | 64 cm²    | Mudah      |
| Lingkaran | diameter 8 cm         | 50,24 cm² | Sedang     |
| Segitiga  | alas 8 cm, tinggi 8 cm| 32 cm²    | Sulit      |

**The cards do not print that Difficulty column.** They state the specification — shape,
size, area formula, bearing area — and stop. Which pole is the forgiving one is the
player's read, the same judgement they would make picking up real stilts. A "Mudah" label
is also one more thing that can quietly drift out of step with the zone table beneath it.

In the existing mechanic that reads as two knobs, both moving with area:

| Shape     | Green band | Yellow (each side) | Sweep  | Green window |
|-----------|------------|--------------------|--------|--------------|
| Persegi   | 0.36       | 0.16               | 2.0 s  | ~720 ms      |
| Lingkaran | 0.18       | 0.10               | 1.25 s | ~225 ms      |
| Segitiga  | 0.10       | 0.055              | 0.85 s | ~85 ms       |

The green *window* — band width × sweep time, the size of the target in milliseconds — is
what the player actually feels, and it is spaced roughly three to one at each step. The
earlier tuning was under two to one and read as three near-identical sticks; with no
difficulty label on the cards, a difference that small makes the choice decoration.
`EgrangStickPresetTests` holds the floor at two to one, and holds the triangle at ≥60 ms so
that hard does not tip into random.

**Deliberately out of scope** (decided with the user):

- No step-distance reward for the harder sticks. The concept doc gives segitiga a
  higher top speed, so as it stands a hard stick is strictly worse in a race decided
  by time and there is no reason to pick it. Revisit when the race timer lands.
- No race timer, finish line, or fall penalty.
- No turning/tilt mechanic — no steering axis exists yet.
- No stick model swap. Profiles carry no prefab; the existing stilt rig is untouched.

## Architecture

```
EgrangStickPresets (static data, unit-tested)
        |  fills
EgrangStickProfile (ScriptableObject, one per shape)
        |
EgrangStickSelector (owns the choice)  --Bind-->  EgrangStickCard (view)
        |  Configure(profile)                          |  specification text only
        v
SkillCheckBar  ------------------->  SkillCheckTrackTexture (bakes the live track)
```

### `EgrangStickShape`

`enum { Persegi, Lingkaran, Segitiga }` — declared in difficulty order.

### `EgrangStickPresets` / `EgrangStickPreset`

The shipped tuning as plain C#: the specification strings (`ShapeText`, `SizeText`,
`FormulaText`, `AreaText`), `GreenHalfWidth`, `YellowWidth`, `SweepSeconds`,
`BuildZones()` which expands them into the five-band
`red | yellow | green | yellow | red` table, and `GreenWindowMilliseconds` — width ×
sweep, the only figure that says how hard a stick actually is, and what the spread test
asserts against.

Zone tables are described by half-widths about the centre rather than as five explicit
bands, so a preset **cannot be authored asymmetric by accident** — an off-centre green
would score the two sweep directions differently.

Tuning lives in code so the difficulty ordering is unit-tested; the assets are filled
from it and can then be retuned in the editor without a recompile.

### `EgrangStickProfile` (ScriptableObject)

Identity (`shape`), the card specification (`displayName`, `shapeText`, `sizeText`,
`formulaText`, `areaText`) and the difficulty (`zones`, `sweepSeconds`). No difficulty
label and no stability meter: both were display-only summaries of the zone table, free to
contradict it, and both told the player what the cards are meant to leave them to work
out. `Reset()` and a context-menu **Apply Shape Preset** fill everything from the preset —
offered because changing an existing asset's shape would otherwise leave the previous
shape's specification and numbers in place, which is the one way this data can quietly lie
to the player.

### `SkillCheckTrackTexture` + `SkillCheckTrackColors`

The gradient bake, lifted out of `SkillCheckBar` when the cards previewed their zone
tables. The cards no longer show a preview — a picture of the green band is exactly the
difficulty tell the specification-only card withholds — so the bar is the only caller
today. It stays split out because the bake is worth testing on its own, and a results
screen or a tutorial would want the same strip. `Release(ref Sprite)`
disposes the sprite and its texture — these are made by hand, so nothing else collects
them.

### `SkillCheckBar` changes

- `Configure(EgrangStickProfile)` — copies the profile's zone table (the bar owns its
  own list, so it can never write back into a shipped asset), takes its sweep time,
  sends the cursor back to the left edge, clears the lockout, rebakes. Callable before
  `Awake`: the profile is held and adopted when the bar wakes, which is the normal path
  because the bar sits under an inactive run root.
- Cursor starts at the **edge**, not the centre — starting on green would hand the
  player a free full step.
- Colour fields collapse into one `SkillCheckTrackColors`; the bake is delegated.
- Exposes `Profile`, `Zones`, `SweepSeconds`.
- Inspector-authored zones remain the fallback when nothing configures the bar, so the
  bar still works standalone and the existing tests stay valid.

### `EgrangStickCard`

Pure view. `Bind(profile, onSelect)` fills in the four specification lines and wires the
button. A null profile hides the card, so a scene laid out for three sticks survives being
given two.

### `EgrangStickSelector`

Owns the flow. Binds cards, hides `runRoot` at startup, pre-highlights the first stick
(the widest cross-section — a player who only presses Start gets the forgiving pole), and
on commit: `bar.Configure(profile)` → `runRoot` active → panel hidden →
`onStickSelected` fires (the hook for the future race timer and networking).

With `startButton` assigned, a card click only highlights and the button commits —
right for a kiosk, where a stray tap should not start the race. With it empty, the card
click commits directly. Commit is ignored after the first, since reconfiguring the bar
mid-run would move the goalposts on a player already walking.

**The bar and player live under `runRoot`, inactive in the scene.** That, not a new flag
on the bar, is what stops Step being pressed through the panel.

## Testing

- `EgrangStickPresetTests` — every preset validates, is symmetric about 0.5, scores the
  centre green and both edges red; green width, forgiving width and sweep time all order
  by cross-sectional area; **the green window at least doubles at each step down** and
  stays ≥60 ms on the triangle. The symmetry check samples off the 0.01 grid on purpose:
  bands are half-open, so a sample landing exactly on an edge scores Full below and Half
  above whatever the table says, which is the interval convention rather than a defect.
- `SkillCheckTrackTextureTests` — every baked pixel matches `Evaluate` at that position,
  seams land where scoring changes, point filtering and clamped resolution hold,
  `Release` is idempotent. Pixels are compared with `Color`'s tolerant `==`, not NUnit's
  exact equality: the strip is `RGBA32`, so `Color.yellow` reads back one ulp off and an
  exact comparison fails on every non-red pixel.
- `SkillCheckBarConfigureTests` (PlayMode) — `Configure` swaps the scored table, works
  before `Awake`, re-parks the cursor, and warns instead of wiping zones on null. These
  live in their own **`Museum.Games.Egrang.Tests.PlayMode`** assembly with no
  `includePlatforms`; a `[UnityTest]` inside the Editor-only test asmdef runs as an
  EditMode test, where `Awake` never fires and every assertion about the bar's state
  fails for the wrong reason.
