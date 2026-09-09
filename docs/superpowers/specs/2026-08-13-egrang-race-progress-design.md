# Egrang Race Progress — Design

Date: 2026-08-13
Scope: `Assets/Scripts/Games/Egrang/` (course reading + HUD strip only)

## Problem

The player walks forward with no idea how far they have come or how much is left. The
concept doc decides the race on time to the finish line, so "how far to the finish" is the
number the whole game is about, and nothing in the scene reports it.

## Concept

A start–finish strip across the top of the run HUD: the course flattened to a rail, the
player as a marker sliding along it, the ground covered filled in behind, and the metres
remaining spelled out above.

**A strip, not a rendered minimap.** The Egrang lane is straight and fenced on both sides —
a top-down camera into a render target would spend a second render pass restating a single
scalar. If a later game needs a real map (Engklek's grid, a branching course), that is a
different component, not a generalisation of this one.

**Deliberately out of scope:**

- No race timer, no placement, no results screen. `EgrangRaceTrack.Finished` fires once and
  the run keeps going; a track that froze the player mid-scene would be inventing a game
  rule nobody has specified yet.
- No fall penalty, no rival racers. The strip draws one racer because there is one.
- No automatic finish-line placement. The generator guesses 52 m down the player's forward
  axis, which is roughly where the lane's fences stop, and says so in the console.

## Architecture

```
EgrangTrackProgress (pure C#, unit-tested)   <-- the arithmetic
        ^  Evaluate(start, finish, position)
        |
EgrangRaceTrack (MonoBehaviour: two markers + the racer)
        ^  Progress
        |
EgrangProgressView (HUD strip: rail, fill, marker, readout)
```

### `EgrangTrackProgress`

A readonly struct plus a static `Evaluate`. Returns `Normalized`, `TravelledMeters`,
`RemainingMeters`, `TotalMeters` and `HasFinished`.

Three decisions worth keeping:

- **Projection onto the start→finish axis**, not distance-to-finish. The player wobbles
  sideways across the lane on every step, and a straight distance reading would count that
  drift as ground lost. Only movement along the course counts — which is also the only
  definition a race ranking can use.
- **Height is dropped.** Measured on the ground plane, so a rise in the terrain is not
  distance covered.
- **A zero-length course reads as finished, not `NaN`.** A finish marker still sitting on
  top of the start is the likely half-wired state, and a `NaN` would paint itself into the
  HUD.

No `MonoBehaviour` in sight, per the repo's standing rule: the authority server will need
each player's progress to rank them, and that has to be the same arithmetic the client
draws.

### `EgrangRaceTrack`

Start and finish as child transforms so the course is dragged, not typed. Exposes
`Progress`, `LengthMeters`, and a `Finished` event that fires once (`ResetRun()` re-arms
it). Gizmos draw the line and print its length in the Scene view — without them the markers
are two empties with no visible relationship.

### `EgrangProgressView`

Pure view. Places the marker by `anchoredPosition` about the rail's centre — the same
convention `SkillCheckBar` uses for its cursor — stretches a left-anchored fill, and writes
the readout through a format string (`{0}` metres left, `{1}` course length, `{2}` percent).
`Draw(progress)` is public so a test or a replay can drive the strip with no live track.

### `EgrangUIStyle` (editor)

The style helpers — palette, fonts, nine-slice ladder, `CreateUI`/`CreateButton`/`SetFrame`
— moved out of `EgrangStickSelectionUIBuilder` into a shared static class, pulled in with
`using static`. With a second generator in the repo, leaving them private would have meant
two copies of the palette free to drift apart.

### `EgrangRaceProgressUIBuilder`

Menu: **Museum → Egrang → Build Race Progress UI**.

Puts the strip under the run canvas — found via the existing `SkillCheckBar`, the same
adopt-what-exists rule the selection builder follows — so it hides with the rest of the run
UI while the selection panel is up. Refuses to build if there is no bar yet, rather than
inventing a second canvas.

An existing `EgrangRaceTrack` is returned untouched: by then its markers have been placed by
hand, and resetting them to a default would be the generator overwriting level design.

### `FollowCamera` (`Museum.Core`)

The camera rides behind the racer instead of watching the lane from a fixed point at the
start line — from there the player shrinks to nothing by the halfway mark, and the strip
above is the only thing still saying where they are.

- **Offset is captured from the scene**, not typed. `Capture Offset From Scene` on the
  component's context menu takes whatever framing was set up by eye. Framing is art
  direction; hand-entered offsets are how a placed shot gets lost.
- **Rotation is left alone.** The course is straight, so there is nothing to turn towards,
  and a camera that swings on every stumble makes people ill. `lookAtTarget` is there for a
  course that bends.
- **`LateUpdate`, not `Update`** — following in `Update` chases last frame's position and
  reads as permanent judder.
- `SmoothDamp` with a 0.35 s ease. It also swallows the 6 cm fail-shake, so a stumble
  wobbles the player and not the whole screen.

Shipped offset is `(0, 5.9, -9.5)` at the scene's authored 26.57° pitch. The camera's
original placement — 4.7 m back — was framed for a static shot of the start line and reads
too tight once it travels: the character covers the lane ahead and crowds the timing bar.

In `Museum.Core` rather than the Egrang assembly because nothing about it is Egrang's.

## Testing

- `EgrangTrackProgressTests` (EditMode, 10 cases) — start/half/finish readings; sideways
  drift and climbing do not count as progress; behind-the-start and past-the-finish clamp;
  `RemainingMeters` never goes negative anywhere along the course; a zero-length course
  gives 1 rather than `NaN`; a diagonal (3-4-5) course measures 50 m.

Verified in play mode at 34%, 82% and past the finish: fill, marker and readout track the
racer, and the readout flips to `FINIS` at the line.

## Note for whoever wires the finish

`Race Track/Finish Line` is a guess at 52 m. Drag it to the real end of the lane — the Scene
view gizmo prints the length as you move it.
