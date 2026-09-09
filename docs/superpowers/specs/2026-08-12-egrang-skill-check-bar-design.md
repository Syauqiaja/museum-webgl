# Egrang Skill-Check Bar — Design

Date: 2026-08-12
Scope: `Assets/Scripts/Games/Egrang/` (Egrang only — not a shared UI component)

## Problem

Egrang currently has only the stilt visual rig (`EgrangStick`, `EgrangStickSolver`):
it welds a stilt to animated hand/foot bones. There is no locomotion and no player
input. Walking on stilts should not be a held key — each step must be earned by a
timed input.

## Mechanic

A cursor oscillates left↔right across a horizontal track divided into colour-coded
zones. The player presses **Space**. The cursor's position at that instant decides
the outcome:

| Zone      | Result | Steps forward | Feedback         |
|-----------|--------|---------------|------------------|
| 🟢 Green  | `Full` | 2             | clean step       |
| 🟡 Yellow | `Half` | 1             | short step       |
| 🔴 Red    | `Fail` | 0             | shake, no fall   |

On press the cursor **freezes**, the result fires, a lockout runs while the step
plays, then the cursor **resumes from where it stopped** (it does not reset to an
edge). Presses during lockout are ignored. Cursor speed is constant — no difficulty
ramp.

## Architecture

Three layers, mirroring the existing `EgrangStickSolver` / `EgrangStick` split:
pure testable logic, a scene-facing MonoBehaviour shell, and a replaceable consumer.

```
Space (InputAction)
        |
   SkillCheckBar (MonoBehaviour)
        |   uses SkillCheckCursor + SkillCheckZones (pure C#)
        |
   UnityEvent<EgrangStepResult>
        |
   EgrangStepMover (thin, replaceable)
        |
   transform / Animator  ->  EgrangStick (existing)
```

### Layer 1 — pure C# (no `MonoBehaviour`, unit-tested)

**`EgrangStepResult.cs`**
`enum EgrangStepResult { Fail, Half, Full }` plus a static `StepsFor(result)`
returning `0 / 1 / 2`. The step count lives with the result so gameplay code never
re-derives the mapping.

**`SkillCheckZones.cs`**
An ordered collection of bands over the normalized track, each
`(float start, float end, EgrangStepResult result)`.

- `Evaluate(float t)` → `EgrangStepResult` for a position in `0..1`.
- `IndexOf(float t)` → which band covers that position, or `-1`. Same coverage rules;
  `Evaluate` is written in terms of it. Callers needing to tell apart two bands that
  share a result — the two yellows — want the index rather than the result.
- Bands are half-open `[start, end)`; the final band includes `1.0` so the right
  edge is never uncovered.
- Any position not covered by a band returns `Fail`. This is the deliberate
  fallback, so mis-authored zone data degrades to "the player missed" rather than
  throwing mid-game.
- `Validate()` reports out-of-order, overlapping, or inverted bands so the shell can
  warn once at startup instead of misbehaving silently every frame.

Zone boundaries are authored in the inspector as normalized `0..1` values to match
the sprite art. Logic never reads the sprites, so the art can be swapped or retuned
without code changes.

**`SkillCheckCursor.cs`**
Oscillator state, advanced by delta time.

- `Position` (`0..1`), `Direction` (`+1 / -1`), `IsFrozen`.
- `Advance(float dt)` moves by `dt / sweepSeconds` and reflects at both edges.
  Reflection loops, so a `dt` large enough to cross the track more than once still
  lands inside `0..1` with the correct direction — no escape, no NaN.
- `Freeze()` / `Resume()` — frozen `Advance` is a no-op.
- `Reset(position, direction)` for a deterministic start.

### Layer 2 — scene shell

**`SkillCheckBar.cs`** (`MonoBehaviour`)

Serialized fields:
- `RectTransform track` — defines pixel width for cursor placement.
- `RectTransform cursor` — the moving indicator.
- `float sweepSeconds` — time for one edge-to-edge pass.
- `float lockoutSeconds` — freeze duration after a press.
- `SkillCheckZones zones` — inspector-authored normalized bands.
- `Image trackImage` + `failColor` / `halfColor` / `fullColor` + `trackResolution` —
  the single track graphic and how its bands are baked.
- `InputActionAsset inputAsset` + action map/action names (default `Egrang` / `Step`),
  matching the existing `FPSInputReader` pattern.
- `UnityEvent<EgrangStepResult> onStepResult`.

Behaviour:
- `Update()` advances the cursor and writes
  `cursor.anchoredPosition.x = Mathf.Lerp(-w/2, +w/2, Position)` where `w` is the
  track width.
- On the action's `performed`: ignore if locked; otherwise freeze, evaluate,
  invoke `onStepResult`, run the lockout, resume.
- Subscribes in `OnEnable`, unsubscribes in `OnDisable` (same lifecycle as
  `FPSInputReader`).

**The track graphic is generated from the zone data, not authored by hand.** The
author supplies one `Image` with a plain white sprite. `BakeTrackGradient()` renders
a `trackResolution × 1` `Texture2D` from the zone table, wraps it in a `Sprite`, and
assigns it to that image, which stretches it across the full track. One sprite
therefore covers any width — which matters on WebGL, where the canvas scales to the
kiosk screen or an arbitrary browser window.

`SampleTrack(t)` is simply `ColorFor(zones.Evaluate(t))`: every pixel is drawn the
colour of the result a press there would score.

**Bands meet at hard edges — no blending.** The seam the player sees must be exactly
where the scoring changes; a soft ramp would show a boundary that is not real and
promise a precision the bar does not grade on. `FilterMode.Point` is what enforces
this, since bilinear filtering would smear each seam as the texture stretches to the
track's width. `trackResolution` therefore governs how precisely the edges land
rather than how smooth they look; 512 puts each edge within 0.2% of its true
position, and raising it costs nothing at runtime.

The baked texture and sprite are created by hand rather than loaded as assets, so
`ReleaseBakedGradient()` destroys the previous pair on every rebake and in
`OnDestroy`. Without that, editing in the inspector would leak one of each per
keystroke.

Baking runs in `Awake` and, in the editor, from `OnValidate` — deferred via
`EditorApplication.delayCall`, since creating assets during `OnValidate` itself is
not allowed. The deferred callback re-checks that the object still exists, because
an undo, a scene close, or a domain reload can land between the edit and the tick.

### Layer 3 — consumer

**`EgrangStepMover.cs`** (`MonoBehaviour`) — lives on the **player object**, next to
the `Animator` that drives the character (and therefore the stilts via
`EgrangStick`).

Listens to `onStepResult` and does two things per step: fire an animation trigger
and move the transform.

| Result | Animator trigger | Movement                                  |
|--------|------------------|-------------------------------------------|
| `Full` | `"Full"`         | two strides of `stepLength`, settling between |
| `Half` | `"Half"`         | one stride of `stepLength`                |
| `Fail` | `"Fail"`         | none — shake in place                     |

A full step is **not** one double-length slide. It is the half step's stride taken
twice: stride, hold for `interStrideSettle`, stride again. The player visibly plants
a foot in the middle, which is what makes it read as two steps rather than one long
one. Each stride re-reads `transform.forward`, so a character that turns mid-step
follows its new facing.

Movement is **code-driven, not root motion**. The clips are visual only; the script
lerps `transform.position` forward along `transform.forward` over `moveDuration`
per stride.
`Animator.applyRootMotion` is left off. This keeps step distance exact and tunable
in the inspector regardless of how the clips were authored, and works whether or
not root motion is baked in. Swapping to root motion later means deleting the lerp
and enabling the flag.

`Fail` runs a shake: decaying positional noise over `shakeDuration` at
`shakeAmplitude`, returning exactly to the start position. The player never falls.

Serialized: `Animator animator`, the three trigger names, `stepLength`,
`moveDuration`, `interStrideSettle`, `shakeDuration`, `shakeAmplitude`.

**Re-entrancy:** the mover assumes it is never re-triggered mid-step —
`SkillCheckBar.lockoutSeconds` must cover the longest case, a full step at
`2 × moveDuration + interStrideSettle`, and it is the single place that guards this.
The mover does not keep its own `isStepping` flag.

## Assembly changes

`Museum.Games.Egrang.asmdef` currently references only `Museum.Core`. Add:
- `UnityEngine.UI` (for `Image` / `RectTransform` UI usage)
- `Unity.InputSystem` (for `InputActionAsset`)

## Testing

Edit-mode tests in the existing `Museum.Games.Egrang.Tests` assembly, matching
`EgrangStickSolverTests`.

`SkillCheckZonesTests`
- Each band returns its own result; boundaries resolve to the band that starts there.
- `t = 0` and `t = 1` are both covered.
- A gap in the bands returns `Fail`.
- `IndexOf` tells apart two bands that share a result, and is negative in a gap.
- `Validate()` flags overlapping and inverted bands.

`SkillCheckCursorTests`
- Advancing forward reflects at `1.0` and reverses direction.
- Advancing backward reflects at `0.0`.
- A `dt` spanning several sweeps still ends inside `0..1` with the right direction.
- `Freeze()` makes `Advance` a no-op; `Resume()` continues from the frozen position
  in the frozen direction.

`SkillCheckBar` and `EgrangStepMover` are not unit-tested — they are thin shells
over the tested logic, verified in play mode.

## Out of scope

- Difficulty ramp / speed scaling with race progress.
- Race track, finish line, opponents, scoring.
- Networking. Per `CLAUDE.md` the P3 server is authoritative, but Egrang has no
  network layer yet; the timing evaluation is client-side for now. When authority
  lands, `SkillCheckZones.Evaluate` is the contract to re-implement server-side.
- Reuse by other mini-games. Egrang-only by explicit decision.
