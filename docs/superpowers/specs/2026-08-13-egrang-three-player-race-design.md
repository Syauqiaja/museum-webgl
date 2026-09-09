# Egrang — three-player networked race

Egrang is a three-player stilt race, but only one racer exists. The scene has three
lanes (`Player 1/2/3 Point`, each with its own `EgrangRaceTrack`) and a single
`Egrang Player` under lane 1. `EgrangRoom` on the server seats three clients and
then does nothing: the race itself is an explicit TODO, and `EgrangState` carries no
progress at all.

This design fills both halves — the two empty lanes on the client, and race
authority on the server — so three players race each other in one room.

Two repos are in scope:

- **Client** — `/Users/mac/Documents/Works/unity/Museum Minigames` (Unity, WebGL)
- **Server** — `/Users/mac/Documents/Works/nodejs/museum-minigames` (Node, Colyseus)

## What exists today

Client, all under `Assets/Scripts/Games/Egrang/`:

- `SkillCheckBar` sweeps a cursor, scores a press against `SkillCheckZones`, and
  raises `EgrangStepResult` (Fail / Half / Full). It binds the global
  `InputActionAsset` and calls `inputAsset.Enable()` in `OnEnable`.
- `EgrangStepMover` consumes that result and slides its own transform forward by
  `stepLength` per stride, with animator triggers per outcome.
- `EgrangRaceTrack` projects one `racer` Transform onto its start→finish line and
  raises `Finished` once. Nothing subscribes; there is no placement or win logic.
- `EgrangProgressView` finds its track with `FindFirstObjectByType`, which picks an
  arbitrary lane the moment three tracks exist.
- `EgrangStickSelector` picks one of three stilt profiles, each with its own zone
  widths and sweep time, and configures the bar before the run.
- `FollowCamera` on `Main Camera` has its target hard-wired to the lane-1 player.

Server:

- `EgrangRoom extends BaseGameRoom<EgrangState>`, `maxClients = 3`,
  `minPlayers = 2`. Seating, host, start, reconnect and idle cleanup are inherited.
  `messages` is empty; `onGameStart` is empty.
- `EgrangState` declares nothing beyond `BaseGameState` (`phase`, `hostSessionId`,
  `players: MapSchema<BasePlayer>` with `sessionId` / `displayName` / `seat` /
  `connected`).

## Authority model

The client grades its own press; the server owns progress and placement.

A press is graded against a cursor sweeping in 0.85–2.0 s depending on the stilt.
Grading on the server would mean grading against the cursor position at *arrival*,
so a 60 ms round trip turns a green press into a yellow one — roughly 10 % of the
track on the fastest stilt, and the museum's wifi is not better than that. Feel has
to be local.

So the server does not simulate the cursor. It bounds the **rate** of steps instead:
a press closer than 500 ms to the previous one is rejected outright (the bar's own
lockout is 600 ms; 100 ms is jitter grace). A patched client can therefore claim a
full step every 500 ms and gain at most about 2×, not unbounded speed — and every
number that decides the race (`stepUnits`, `place`, `winner`, the persisted score)
is the server's, never a client's.

## Distance units

Progress is counted in **half-steps**, not meters.

The client moves `transform.forward * stepLength` per stride and a Full step is
worth two of those, so half-steps are the one quantity both sides can agree on
without shipping lane geometry to Node. The server holds `finishUnits` — the race
length in half-steps — and the client converts back to world space with its own
`stepLength`. On joining, the client compares its lane's measured length against
`finishUnits * stepLength` and logs a warning on mismatch; the server's number
wins.

`EgrangStep.StepsFor` (Full = 2, Half = 1, Fail = 0) is reimplemented on the server.
This is deliberate duplication under the project's existing rule: the Node server
reimplements rules and parity holds by contract, not by shared code.

## Server design

### Schema — `src/rooms/schema/EgrangState.ts`

A parallel map keyed by session id, rather than re-typing `BaseGameState.players`.
Re-declaring the inherited `players` field with a subclass would move field indices
in a base shared with Dakon; a second map leaves the lobby schema untouched.

```ts
class EgrangRacer extends Schema {
  @type("uint8")   stick = 0;       // 0 persegi, 1 lingkaran, 2 segitiga
  @type("uint16")  stepUnits = 0;   // half-steps banked
  @type("uint8")   place = 0;       // 0 unfinished, else 1..3
  @type("boolean") ready = false;   // stick chosen
}

class EgrangState extends BaseGameState {
  @type({ map: EgrangRacer }) racers = new MapSchema<EgrangRacer>();
  @type("uint16") finishUnits = 0;
  @type("number") startsAtMs = 0;
}
```

`lastStepMs` is server-private (a `Map<string, number>` on the room), not synced —
clients have no use for it and it would be one more thing to keep honest.

### Messages — `src/rooms/EgrangRoom.ts`

`choose_stick { shape: 0 | 1 | 2 }`
: Allowed only while `phase === "waiting"`. Sets `racer.stick` and `ready = true`.
  Any other value is an `invalid_move` error and changes nothing.

`step { result: 0 | 1 | 2 }`
: Accepted only when `phase === "in_progress"`, `now >= startsAtMs`, the sender is
  seated, `place === 0`, `result` is in range, and `now - lastStepMs >= 500`.
  On accept: `stepUnits = min(stepUnits + StepsFor(result), finishUnits)`,
  `lastStepMs = now`, then broadcast. On reject: `sendError(client,
  "invalid_move", …)` and no state change — a rejected press is a no-op, not a
  rollback, because the client's local animation for it is cosmetic.

Broadcasts:

`step_taken { sessionId, result, stepUnits }`
: One per accepted press, to everyone including the sender.

`game_over { places, winner }`
: `places` is `{ [sessionId]: number }`, 0 for anyone who never finished.

### Lifecycle

`onGameStart` seats players by `player.seat` into a `seats: string[]`, the same way
`DakonRoom.onGameStart` does, so "you are player 2" matches the lobby slot. It
creates an `EgrangRacer` per seated session, sets `finishUnits` from the room
constant, and sets `startsAtMs = Date.now() + 3000` — the countdown gate that makes
three clients start together.

Reaching `finishUnits` assigns the next free `place` (1, then 2, then 3). The race
ends when every racer is placed, or 15 s after the first finisher, whichever comes
first; stragglers keep `place = 0` and their `stepUnits`. Ending calls the
inherited `persistMatchEnd("completed", …)` with the first-place session as winner,
then broadcasts `game_over`.

`scoreOf(sessionId)` returns that racer's `stepUnits`, so the persisted result row
records distance covered.

`onOpponentLeft` keeps the base forfeit bookkeeping. A departed racer simply stops
sending steps; the remaining players race on and the 15 s straggler timeout ends it.

## Client design

### New scripts — `Assets/Scripts/Games/Egrang/`

`EgrangRacer` (MonoBehaviour, one per lane, sits on `Player N Point`)
: Serialized `EgrangStepMover mover`, `EgrangRaceTrack track`, `int lane`.
  `ApplyStep(EgrangStepResult)` drives the existing stride animation.
  `SnapToUnits(int units)` places the racer at `start + forward * units *
  (stepLength / 2)` without animating — used on reconnect and on desync repair.
  `Progress` forwards the lane's own track reading.

`EgrangRace` (MonoBehaviour, scene root)
: Holds the three racers. Maps each seated session to a lane by `seat`, identifies
  the local seat by matching `SessionData.Instance.SessionId`, points
  `FollowCamera.Target` at the local racer, hands `EgrangProgressView` the local
  lane's track explicitly, and records placement for the finish text. This is the
  only object that knows which lane is "you".

`EgrangNetBootstrap` (MonoBehaviour)
: Mirrors `DakonNetBootstrap`. Takes the room from `RoomSession`, sends
  `choose_stick` when `EgrangStickSelector` commits, sends `step` on each bar press,
  and applies `step_taken` / `game_over` to `EgrangRace`.

### Input ownership

Only the local player gets a `SkillCheckBar`, and it stays exactly where it is — one
instance on the HUD. Remote lanes have no bar and no input binding, so nothing
contends over `inputAsset.Enable()`. Three bars would have all scored on the same
Space press; the fix is to never create them.

### Prediction and reconciliation

A local press animates immediately and is sent in the same frame. When the server
echoes that press back as `step_taken`, the client compares `stepUnits` against its
own banked count: equal means the prediction held and the echo is dropped; different
means the server rejected or clamped something, and the racer is repaired with
`SnapToUnits`. Remote `step_taken` messages call `ApplyStep` directly, so lanes 2
and 3 play the same stride animation the local player sees on their own racer.

### Offline fallback

With no `RoomSession` — the scene opened straight from the editor —`EgrangRace`
runs local-only: the bar wires to lane 1's mover, the other two lanes stand idle,
and no network calls are made. The current single-player dev loop keeps working
unchanged, which matters because every gameplay tweak is iterated that way.

### Scene changes — `Assets/Scenes/Egrang.unity`

- Copy the `Egrang Player` prefab instance under `Player 2 Point` and
  `Player 3 Point` at local `(0, 0, -3)`, matching lane 1.
- Delete the duplicate `EgrangStepMover`: the player carries one inside the prefab
  and a second added in the scene, and only the scene one is wired to the bar.
- Add `EgrangRacer` to each `Player N Point`, and `EgrangRace` plus
  `EgrangNetBootstrap` to a new scene root.
- Clear `EgrangProgressView`'s reliance on `FindFirstObjectByType`; it takes its
  track from `EgrangRace`.
- `FollowCamera.target` is no longer authored in the scene — `EgrangRace` sets it.

## Testing

Server (vitest, beside the existing room tests):

- A second `step` inside 500 ms is rejected and leaves `stepUnits` unchanged.
- Full / Half / Fail accumulate 2 / 1 / 0 units.
- `stepUnits` clamps at `finishUnits`; the first three finishers take places 1–3.
- `step` before `startsAtMs`, and from an unseated client, are both rejected.

Client (EditMode, existing `Museum.Games.Egrang.Tests` asmdef):

- `SnapToUnits` places a racer at the expected distance for a unit count.
- Seat → lane mapping is stable and identifies the local seat from a session id.

## Out of scope

Results/podium screen art, spectator camera, and mid-race disconnect scoring beyond
the base class's forfeit handling. The race ends and reports places; presenting that
handsomely is separate work.
