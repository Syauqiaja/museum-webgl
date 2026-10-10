# Networking — Colyseus Unity SDK

How the client talks to the server. The **contract** — message names, payload shapes, state
schemas, error codes — is owned by the server repo's `docs/protocol.md`. This is the wiring
guide. If a message is not in `protocol.md`, do not invent it client-side.

## 1. SDK

`io.colyseus.sdk` via UPM git URL
`https://github.com/colyseus/colyseus-unity-sdk.git#upm`, resolving **0.17.17**, matching
the server's `colyseus ^0.17`.

**Unpinned risk:** `#upm` tracks the branch head, so a later resolve can pull an 0.18 SDK and
break silently against a 0.17 server. Pin to a tag (`…colyseus-unity-sdk.git#0.17.17`) and
bump client and server together, deliberately.

## 2. Client construction

Never `new Client(...)` in a scene. `ColyseusNetManager` owns the one client and builds it
from `ServerConfig` ([architecture.md](architecture.md#endpoint-configuration)):

- dev `ws://localhost:2567`, prod `wss://api.museum.fajrsyauqi.com`
- the endpoint is host + port only — the Unity C# SDK has **no path setting**, so it can
  never be a subpath of the client host

The Museum scene creates no client at all.

## 3. Opening a room

Three flows exist on the server; the client uses two of them.

```csharp
// Create — always private. The server generates the shareable 6-char code.
Room<DakonState> room = await ColyseusNetManager.Instance.CreateRoom<DakonState>(
    "dakon", new Dictionary<string, object> { { "private", true }, { "playerId", id } });
string code = room.RoomId;

// Join by code.
Room<DakonState> room = await ColyseusNetManager.Instance.JoinRoomById<DakonState>(code, options);
```

`displayName` is added to every options dictionary by `ColyseusNetManager`. `playerId` is
added by the lobby service. **Public `joinOrCreate` is not used** — every room this client
makes is private and reachable only by its code, so pressing Create never drops a stranger
into your game.

The room must be opened as its **own generated state type**: `Room<T>` is generic and the
decoder addresses fields by index, so opening a Dakon room as the base class leaves the
decoder unable to place the game's fields. `RoomSession` (in `Museum.Lobby`) is the seam that
hides that generic from the lobby — one entry per game in its factory table:

```csharp
{ "dakon",  new RoomFactory<DakonState>() },
{ "egrang", new RoomFactory<EgrangState>() },
```

Adding a game is one line there and nothing else.

### Failures

Colyseus reports a refused create/join as an **exception whose text carries the reason**, so
`ColyseusLobbyService.MapOpenFailure` classifies by inspection:

| Text contains | Mapped to |
|---|---|
| `already_started` | `already_started` |
| `not found` / `expired` / `invalid room` | `room_not_found` |
| `locked` / `full` | `room_full` |
| anything else | `connection_failed` |

Unrecognised failures map to *connection failed*, not *not found* — telling a player their
code was wrong when the server is simply down sends them off to retype a correct code.

## 4. Reading state

State is authoritative and auto-diffed. Never mutate a local copy and expect it to sync,
and never poll `room.State` in `Update()`.

Both net sessions use whole-state `OnStateChange` rather than fine-grained schema callbacks,
because both states are small enough that folding the whole thing costs nothing:

```csharp
_room.OnStateChange += (_, __) => Apply();   // refresh the mirror, then raise one event
```

Each session keeps a **mirror** of what the view needs (seats, names, hand, holes, totals)
and raises a typed event describing *which kind* of change it was — `BoardReady`,
`HandChanged`, `StateChanged`, `SeatsChanged`. The view reads the session's properties from
inside those events, so the mirror is always refreshed first.

Two mirror rules exist because the synced maps lie by omission:

- **Seats only ever grow.** A player who leaves is removed from `state.players`; rebuilding
  seating from that map would slide the survivor into the leaver's seat and hand them the
  leaver's score. Once seen, a seat is kept.
- **Names are kept for the same reason.** The results panel names both players, and it is
  shown at exactly the moment an opponent is most likely to have closed their tab.

## 5. Sending

```csharp
room.Send("drop_seed",     new { seedId, holeIndex });   // Dakon
room.Send("step",          new { result });              // Egrang, 0|1|2
room.Send("choose_stick",  new { shape });               // Egrang, 0|1|2
room.Send("countdown_sync",new { });                     // Egrang
room.Send("start_game",    new { });                     // lobby, host only
```

Type strings and payload shapes must match the server's `protocol.md` exactly.

## 6. Server → client messages

| Message | Payload | Handled by |
|---|---|---|
| `error` | `{ code, message }` | lobby toast; `NetDakonSession` → `DropRejected`; `NetEgrangSession` logs a warning |
| `player_joined` / `player_left` | `{ sessionId }` | not consumed directly — the lobby re-renders from the players map |
| `drop_applied` | `{ seedId, holeIndex, scoringPlayer, category, typeId, turnEnded, gameOver }` | Dakon animation |
| `game_over` | Dakon `{ scores, winner }`, Egrang `{ places, winner }` | results panels |
| `step_taken` | `{ sessionId, result, stepUnits }` | Egrang stride replay / prediction check |
| `countdown` | `{ startsAtMs, remainingMs }` | Egrang picking window |

**Why Dakon has `drop_applied` at all:** state sync tells you what *is*; an animation needs
to know what *changed*. An earlier version reconstructed it by diffing patches and could not
survive a turn — the server refills the hand inside the same drop that empties it, so the
patch after a turn's last drop shows a *bigger* hand and the diff saw no drop. Any patch
carrying two drops broke it the same way. The server already computed the whole result when
it validated the move, so it says so.

**Why Egrang's countdown is milliseconds-remaining, not a wall-clock instant:** `startsAtMs`
is the server's clock. A kiosk with a badly-set clock subtracting its own time from it would
show whatever its error happens to be. The server computes the remainder instead — once on
broadcast, and again per client on `countdown_sync`, which is the request that actually
reaches a client that was still loading the scene when the broadcast went out.

## 7. Prediction, and where it is allowed

| Game | Predicted | Not predicted |
|---|---|---|
| Dakon | that a hole with a drop **in flight** is already sown (so a burst cannot aim two seeds at one hole) | nothing is drawn until `drop_applied` — the board on screen is always one the server agrees with |
| Egrang | the **whole stride**: the bar grades the press and the racer walks immediately | the banked count, the places, the winner — all server numbers |

Dakon's hole prediction exists so the player can click a whole hand without waiting a round
trip each time; Colyseus delivers one client's messages in order, so the server applies the
burst in the order it was predicted. A wrong guess is a refusal, not a divergence, and a
refusal clears the entire in-flight queue (the server stopped at the rejected drop, so
everything queued behind it was aimed one hole too far).

Egrang predicts because the cursor sweeps a lap in 0.85–2.0 s: grading on arrival would turn
a green press into a yellow one on any real connection. The server bounds the *rate* instead
— a press within 500 ms of the previous accepted one is rejected — and every `step_taken`
either confirms the local count or corrects it with `SnapToUnits`, which places the racer on
the server's distance with no slide.

## 8. Reconnect, and the lobby → game handoff

This is the single most load-bearing piece of client networking, and it is not obvious.

The lobby owns a live room that **dies with the lobby scene**, and Unity has no channel for
handing a live object across a scene load. So the handoff is a *reconnect*, not a handover:

1. `ColyseusNetManager` caches `RoomName`, `RoomId`, `SessionId` and `ReconnectionToken`
   into `SessionData` the moment the room opens.
2. The lobby loads the game scene **without leaving the room**. The dying socket looks to
   the server like a drop, so the seat sits in `allowReconnection` (30 s) instead of looking
   like a walkout.
3. The game scene's net bootstrap calls `ColyseusNetManager.Reconnect<T>()` in `Awake` and
   binds the resulting session — landing back in the **same seat on the same room**, not a
   second one.

`ColyseusLobbyService.Leave()` therefore runs only when the player actually walks out
(Leave / Back), never on the way into a game. It leaves *consented*, which frees the seat
immediately rather than holding it for the reconnect window — which is what "I walked out"
should mean to the other player.

The bootstrap also checks `SessionData.RoomName` against its own game and refuses to
reconnect into the wrong room type, falling back to offline with a warning.

A **mid-game** socket drop is not yet handled: there is no "reconnecting…" overlay and no
retry loop. The token is cached and `Reconnect<T>()` exists; wiring it is outstanding work.

## 9. Leaving

Call `ColyseusNetManager.Leave<T>(room)` (which awaits `room.Leave(consented)` and clears the
cached seat) on an explicit quit or return-to-lobby. Never just destroy the scene and let the
socket time out — that delays the server's `onLeave` and its idle-room cleanup.

## 10. Server facts the client depends on

Verified against the running server; the full list is in the server's
`docs/unity-integration.md`.

| Thing | Value |
|---|---|
| Endpoint | `wss://api.museum.fajrsyauqi.com` |
| Client host | `https://museum.fajrsyauqi.com` (same VPS) |
| Rooms | `dakon` (2 seats), `egrang` (3 seats, `minPlayers` 2) |
| Room code | 6 chars, alphabet `ABCDEFGHJKMNPQRSTUVWXYZ23456789` |
| Join options | `{ private?, displayName? (≤32), playerId? }` |
| Start | host `start_game`; auto-starts when the room fills |
| Reconnect window | 30 s |
| Idle room timeout | 5 min (still in `waiting`) |
| Start rejections | `not_host`, `not_enough_players`, `already_started` |

Note `RoomCode.GeneratedLength` on the client is **5**, not 6. That constant is used only by
`FakeLobbyService` to mint offline codes; `RoomCode.Sanitize` does not check length, so a
real 6-char code types in fine. It is a cosmetic mismatch in the fake, not a bug in the
join path.

Health checks:

```bash
curl -s https://api.museum.fajrsyauqi.com/hi
curl -s -X POST https://api.museum.fajrsyauqi.com/matchmake/joinOrCreate/dakon \
  -H 'Content-Type: application/json' -d '{}'
```
