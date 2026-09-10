# Egrang — Rules and Client Mapping

Three players race on stilts down parallel lanes, walking by timing presses against a
sweeping skill-check bar. Rules are shared with the server's `docs/games/egrang.md`; the
wire contract is its `docs/protocol.md#egrang`. Server engine:
`src/games/egrang/EgrangRace.ts` + `src/rooms/EgrangRoom.ts`.

---

## The game

Each racer walks a lane by pressing Step when a sweeping cursor is over the right band of a
colour-coded bar. A well-timed press is a full step, a near-miss is a half step, a miss is a
stumble in place. First to the end of the lane takes first place; all three are ranked.

Before the race, each player picks one of three stilts. **The stilt changes only how hard
the timing is** — the width of the green band and the speed of the sweep. It never changes
what a step is worth. That is the educational hook: the three poles are described by their
cross-section (square / circle / isosceles triangle) and the player is expected to work out
which has the biggest bearing area, and therefore which is the steadiest.

## Track

| Thing | Value | Source |
|---|---|---|
| Lanes | 3, one per seat, identical | `EgrangRace.racers[3]` |
| Race length | **50 strides** | `finishUnits` / `DEFAULT_FINISH_UNITS` |
| Stride | **0.5 m** — so the lane is 25 m | `EgrangStepMover.stepLength` |
| Units on the wire | strides only — the server never sees world coordinates | — |

## Steps

| Outcome | `result` | Strides | On screen |
|---|---|---|---|
| **Fail** (red) | 0 | 0 | Stumble: a decaying sideways shake, ending exactly where it began |
| **Half** (yellow) | 1 | 1 | One stride |
| **Full** (green) | 2 | 2 | Two strides with a plant between them (`interStrideSettle`), not one long glide |

Banked strides clamp at `finishUnits` — a Full step across the line is not overshot.

## The stilts

Three `EgrangStickProfile` assets in `Assets/Data/Egrang/`, filled from `EgrangStickPresets`
in code so the difficulty spread is unit-tested and can still be retuned in the editor.

| Stilt | `shape` | Cross-section | Green half-width | Yellow width | Sweep | Green window per pass |
|---|---|---|---|---|---|---|
| **Persegi** | 0 | Square, side 8 cm → 64 cm² | 0.18 | 0.16 | 2.00 s | ~720 ms |
| **Lingkaran** | 1 | Circle, ⌀8 cm → 50,24 cm² | 0.09 | 0.10 | 1.25 s | ~225 ms |
| **Segitiga** | 2 | Isosceles, base & height 8 cm → 32 cm² | 0.05 | 0.055 | 0.85 s | ~85 ms |

Both knobs move with cross-sectional area, and they are spaced roughly a factor of three
apart at each step — anything subtler and the three poles play the same, which makes the
choice decoration. Zone tables are built from a **half-width about the centre**
(`red | yellow | green | yellow | red`), so a preset cannot be authored asymmetric by
accident; an off-centre green would make the bar unfair in one sweep direction.

The selection card prints the shape, the measurement and the area *formula* — never the
area itself and never a difficulty word. The player does the sum. `AreaText` is kept in the
preset as the answer, unshown.

## Start and countdown

The room auto-starts the moment its third seat fills, and the host may start with two. At
start the server stamps `startsAtMs = now + 15 s` and broadcasts `countdown`.

That 15 s is the **stilt-picking window, not a "get ready" beat**. The room usually starts
before a client has finished loading the Egrang scene, so it is the only time anyone gets to
look at the three poles. At zero, any racer who never picked is given the highlighted pole
(the selector pre-highlights the easiest, so it is always a real choice) and the run root
switches on.

No step counts before `startsAtMs`.

## Client-graded presses, server-bounded rate

**The client grades its own press.** The cursor sweeps a lap in 0.85–2.0 s; grading on
arrival would turn a green press into a yellow one on any real connection.

The server bounds the *rate* instead: a press within **500 ms** of the previous accepted one
is rejected. The bar's own lockout is **600 ms** and a full step's animation runs 1.12 s
(`2 × moveDuration + interStrideSettle` = 2 × 0.5 + 0.12), so no honest press is ever
refused.

Every number that decides the race — banked strides, places, winner, persisted score — is
the server's.

## Win condition

Reaching 50 strides takes the next free place (1, then 2, then 3). **The first racer home
ends the race for everyone** — nobody is held on a lane they can no longer win — and a race
whose last place is taken by someone other than the winner ends on that step instead.
Unplaced racers keep `place = 0` and their banked strides, which is their score.

## Edge cases

| Case | Behaviour |
|---|---|
| Step before the countdown ends, from an unseated client, out of range, or already placed | `invalid_move`, no state change |
| Step inside the 500 ms rate limit | `invalid_move` |
| `choose_stick` after the race is over, or after that racer banked a step | `invalid_move`, stored stilt unchanged |
| Racer leaves mid-race | They stop stepping; the server withdraws them so `allPlaced` no longer waits on them, and the survivors finish normally. Their frozen position stays on screen |
| Reconnect | `stepUnits` is synced state, so a returning client is *placed* from it rather than replaying steps |

---

## Client mapping

Scene `Assets/Scenes/Egrang.unity`, room `egrang`, 3 seats, `minPlayers` 2. Entered from
the museum's Egrang doorway with
`new LobbyRequest("egrang", 3, SceneReference.Egrang, "Egrang")`.

### Lanes are seat-absolute

`EgrangSeating` maps `sessionId → lane` from the server's seat numbers, **identically on
every screen**: lane 0 is `Player 1 Point` on every client, not "the local player's lane".
`EgrangRace` is the one object that knows which lane is local; on seating it points the
`FollowCamera` and the progress HUD at that racer. Nothing about drawing another racer's
lane differs from drawing your own — only the camera and the input are local.

**Empty lanes are hidden online.** Once seats have arrived, a lane nobody sat down in has its
walker (`EgrangRacer.Body`, the `Egrang Player` object: model, stilts and plate) switched
off, so a two-player race shows two walkers. Lane roots and track markers stay. A racer who
leaves mid-race stays visible (seating never unassigns), matching the frozen-position rule
above. Offline, lanes 2–3 remain as scenery.

**Nameplates belong to their racer.** `EgrangRace` takes each lane's plate from under that
lane's racer, and `EgrangRace.nameplates` is only a fallback for racers built in code. The
recovered scene shipped that array as lanes 3, 1, 2, which put every name over someone else's
walker (fixed 2026-09-10). The host read as the right-hand racer, "the opponent" was
whichever lane nobody drove, and the top-left roster disagreed with the labelled avatars.

### Driven by messages, not schema callbacks

Unlike Dakon, the running race is not rendered off state-field callbacks:

| Source | Drives |
|---|---|
| `step_taken` `{ sessionId, result, stepUnits }` | `StepTaken` — replays that lane's stride, or confirms/corrects the local prediction |
| `countdown` `{ startsAtMs, remainingMs }` | `CountdownChanged` — the picking window; requested with `countdown_sync` on bind and on regaining focus |
| `game_over` `{ places, winner }` | `RaceOver` — stops local input and raises the results panel |
| `state.players` (whole-state patch) | `Seats` / `SeatsChanged` and `DisplayNameOf` |
| `state.racers[].stepUnits` | `StepUnitsOf` — places a reconnecting client's racers the first time seats resolve (on a fresh race every value is 0, so that snap is a no-op), and fills the distance on each results row |
| `state.racers[].stick` | `StickOf` — names every racer's stilt on the results panel, not only the local one |
| `state.finishUnits` | `FinishUnits` — the "34/50 langkah" denominator on a results row |
| `error` | Logged as a warning |

`state.racers[].place` and `startsAtMs` are synced but not read by client code — places
arrive in `game_over`, and the countdown arrives pre-computed in milliseconds. The finish
line the local racer animates towards is scene geometry (`EgrangRacer.HasFinishedLane`);
the finish that decides the race is the server's stride count.

The run clock reported on the results panel is **local**: started when the countdown reaches
zero, stopped when the local racer crosses. It is the one number on the panel that is not the
server's, and it is reported for the local player only — there is no per-racer time in the
protocol.

### What the results panel shows

`game_over` reaches every client, so all three see the same panel at the same moment. The
top block is this player's own run (place, local clock, full/half/fail tally, chosen stilt);
below it one row per racer — name, place or "tidak selesai", that racer's stilt and how far
they got, e.g. `34/50 langkah`. Stilt and distance come from `state.racers`, so a racer who
never crossed still gets a truthful row.

`Results Panel` is authored **inactive** in the scene, which means `EgrangResultsView.Awake`
does not run until `Show` turns the object on — inside `Show`. An `Awake` that hides the panel
unconditionally therefore closes the results in the same frame the race opens them: filled in,
`IsShowing` true, nothing on screen. `Awake` hides only when `IsShowing` is false, and every
read of the root goes through the lazy `Panel` property so `Show` works before `Awake` ever
runs. A view built in code is active from `AddComponent` onward and never hits this, which is
why only `EgrangResultsViewTests` catches it.

### Prediction and repair

A local press animates immediately: `SkillCheckBar` grades it, `EgrangRace.ReportLocalStep`
tallies it, `EgrangRacer.ApplyStep` banks and plays it, and the same grade is sent as `step`.
When the matching `step_taken` arrives it either **confirms** (counts match — nothing
happens, replaying it would double the stride) or **corrects**: `SnapToUnits` places the
racer exactly on the server's banked distance with no slide, and banks the count that
clamped position actually represents.

Local presses stop being fed into that path once the race is over or once the local racer
has crossed its own finish line — past that point the server sends no echo to confirm
against, so without the guard the racer would keep walking past the line.

Remote lanes never predict: each `step_taken` simply plays the same stride animation a local
press would have played.

**Focus loss is repaired explicitly.** A backgrounded WebGL tab freezes `Update` and every
coroutine while the socket keeps receiving, so each queued `step_taken` stops the last
stride before it draws a frame and only the final one animates. The banked count still ends
up correct, so the mismatch check never trips — `OnApplicationFocus` therefore re-grounds
every racer in the server's `stepUnits` and re-asks for the countdown rather than trusting
the local one, which ran on unscaled time while the tab was frozen.

### Scene composition

- `Egrang Race` — the one coordinator. Wired to three `EgrangRacer` lane roots, the follow
  camera, the progress strip, the bar, the stick selector, the results panel, the countdown
  strip, the roster and three nameplates.
- `EgrangRacer` (per lane) — pairs a track, a mover and a seat; counts strides locally so an
  echo can confirm or correct without measuring the transform back into units.
- `EgrangStepMover` — plays the step. Movement is **code-driven** (`applyRootMotion` off), so
  a stride is exactly `stepLength` whatever the clips were authored to do.
- `SkillCheckBar` — cursor sweep, zone evaluation, lockout, and the **baked** track texture:
  its `onStepResult` is subscribed **in code** by `EgrangRace` and must have **no persistent
  UnityEvent target in the scene**. A call wired straight to a mover drives that lane on every
  client regardless of seating — see `scene-setup.md`.
  the red/yellow/green strip is generated from the zone table, so the picture cannot drift
  out of step with the numbers actually scored against. Bands meet at hard edges.
- `EgrangStick` (×2 per racer) — a two-pivot follower: the footplate is welded to the foot
  bone, the grip slides along the shaft to reach the hand. Animation is authored on the
  character, never on the stick. `Rebind(hand, foot)` points it at another body's bones.
- `EgrangRacerBody` (on each `Egrang Player`) — dresses the walker in its player's chosen
  character (`IEgrangSession.AvatarOf` ← `players[].avatar`; offline, lane 1 wears
  `SessionData.PlayerAvatar`), called from `EgrangRace.DrawRoster` on every seat change.
  **Jawa is the authored body** (`Armature` + `char1`, Generic, `Anim_Egrang`), and **that
  rig keeps playing whatever the walker wears** — the Animator, its controller and its egrang
  clips are never touched, so every lane runs the real egrang step, half step and fall.
  Any other character is instantiated as a child `Body (Char_…)` beside the authored
  skeleton, the authored `char1` renderer is hidden, and every `LateUpdate` (order 50, before
  the stilts' 100) the authored pose is copied onto it through two `HumanPoseHandler`s (the
  Jawa model's avatar → the chosen model's), then the body is shifted so its lower sole sits at
  the authored one's height — where the footplates are. Both stilts are re-pointed at the new
  hands and feet. Retargeting the clips was tried first and dropped: the Generic clips key
  every bone's position and scale (another body is stretched to Jawa's proportions), and a
  Humanoid bake played on a Humanoid Animator lost the height that puts the walker on the
  stilts — Unity treats a generated clip's own body curve as its "original" root, so the
  bodies stood ~0.9 m into the ground. Wired by `Museum/Egrang/Wire Scene References`
  (stilts + the four `Char_*.fbx`; Jawa's is required, its avatar reads the authored pose).
- `EgrangStickSelector` — owns the choice; the bar knows nothing about selection. The bar
  and player rig live under an **inactive** `runRoot`, which is what stops a press from
  reaching the bar through the panel without the bar needing a "not started" state.
- `Pause Panel` + `EgrangPauseMenu` — Dakon's pause menu (gear top-right → `Jeda`,
  `Lanjutkan`, `Kembali ke Museum`), authored directly in the scene. It is a menu,
  **not a time freeze**: the race is real-time and its clock is the server's, so the other
  racers keep walking and the countdown keeps running. What it does stop is input — while the
  panel is active `SkillCheckBar.InputBlocked` drops every press (Space, JALAN, the touch tap
  zone) without starting a lockout. The exit is `EgrangRace.BackToMuseum`, the same exit as
  Dakon's: the held seat is cleared, and the room is not left with consent, so mid-race the
  dropped socket is the withdrawal described under Edge cases.

### Offline mode

With no session bound, `EgrangRace` takes lane 1, runs its own 15 s picking window
(`offlineCountdownSeconds`), and ends the race at the local finish line with place 1 of 1 —
there is no `game_over` to wait for. This is the Editor's iteration mode and the fallback
for a kiosk with no server.

### Errors

Every rejection arrives as `error` `{ code: "invalid_move", message }` and is currently only
logged. Nothing in the race UI surfaces a refused step — an accepted-looking press that the
server dropped is repaired silently by the next `step_taken` mismatch.

## Tests

- EditMode (`Tests/`): `SkillCheckZonesTests`, `SkillCheckCursorTests`,
  `SkillCheckTrackTextureTests`, `EgrangStickPresetTests`, `EgrangStickSolverTests`,
  `EgrangSeatingTests`, `EgrangTrackProgressTests`, `EgrangRunSummaryTests`.
- PlayMode (`Tests/PlayMode/`): `EgrangRaceTests`, `EgrangRacerTests`,
  `EgrangStepMoverSnapTests`, `SkillCheckBarConfigureTests`, `EgrangPauseMenuTests`.
