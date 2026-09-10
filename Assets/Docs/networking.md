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

The Museum scene uses the same client for **presence**: `MuseumPresence` (on the player rig)
`JoinOrCreate`s the server's `museum` room straight on `ColyseusNetManager.Client` — not via
its create/join helpers, which would record it as the seat `SessionData` holds — sends `move`
`{ x, y, z, yaw }` at most 10×/s, and spawns a `MuseumVisitorAvatar` per other entry in
`state.visitors`, with their name overhead. The body is one of the four
`Assets/Models/ASSET_NUSANTARA/1_Karakter` characters (Jawa L, Bali P, Bugis P, Minang L), as
prefab variants in `Assets/Prefabs/Visitors/` scaled to 1.7 m. Which one a visitor wears is
picked from a hash of their session id — random across visitors, identical on every client.
All four are **Humanoid** (each its own avatar) and share `Anim_MuseumVisitor`, a 1D blend on
`Speed` over the `2_Animasi` clips: `Idle` 0, `Walk` 0.80 m/s, `Run` 1.50 m/s (the ground speeds
the clips imply at that scale), with `Run` sped up past that, capped at 3.5×. The clips are
in-place: `Anim_*.fbx` import as Humanoid *Copy From Other Avatar* (Jawa), Loop Time on, root
rotation/height/XZ baked into the pose — as `ASSET_NUSANTARA/BACA_DULU.md` prescribes.

**The idle is not `Anim_Idle.fbx`.** That file's legs are the egrang stance: the right leg holds
`Anim_Egrang`'s planted pose (Upper Leg Front-Back 0.48, Lower Leg Stretch 0.88) and the left leg
is frozen at its step pose (−0.25 / 0.15), with the body rolled ~7° — the idle action in
`_Sumber/blender/karakter_animasi.blend` never keyed the legs, so the export took them from the
egrang action. The blend's `Idle` slot plays `Assets/AnimationClip/Visitor Idle Stand.anim`
instead: `Anim_Idle`'s arms, spine, head and breathing, with both legs set to the characters'
rest pose (left/right averaged), `RootQ` levelled, the foot IK-goal curves dropped, and root
height set so the soles sit on the model's origin. Once the `.blend` is fixed and re-exported,
point the blend back at `Anim_Idle` and delete the derived clip.

**Walk and Run are derived too.** The Meshy `Anim_Walk`/`Anim_Run` hold the arms out near
horizontal (Arm Down-Up ≈ +0.26 walking, +0.45 running, where hanging is −1.16) — on every
character, Jawa included. On Minang, the one character modelled in an A-pose, that tore the
mesh: its jacket and sleeves are skinned for hanging arms and ballooned as the walk lifted
them. Skinning and retargeting are sound on all four (bind poses exact; identical muscles land
every body's bones within a few degrees of Jawa's), so the fix is the clips, not the rigs:
`Assets/AnimationClip/Visitor Walk.anim` and `Visitor Run.anim` are the FBX clips with only the
arm curves moved, swing kept: Arm Down-Up lowered from the near-horizontal source (walk mean
−0.25, run −0.20 — a toon walk, arms held clear of the body) and Arm Front-Back re-centred to
0.40 (the source swung forward-biased at 0.65, which carried each forward hand across the belly
of these wide chibi torsos). Arms hung fully down (−1.0) clipped through the body. The idle
opens them slightly (Arm Down-Up mean −0.88). Their Y root is "Based
Upon: Feet" (set on the FBX import too), so with the idle every clip keeps the soles on the
model's origin and `MuseumVisitorAvatar` drops the body exactly 1 m. If the
`visitorCharacters` array is lost the avatar falls back to a tinted copy of the player
capsule; `Museum/Rebuild UI/Wire Scene References` re-wires it from the folder.

Remote visitors move by **snapshot interpolation**, not by chasing the newest position: each
received position is stamped with its arrival time and the avatar is drawn
`MuseumVisitorAvatar.InterpolationDelay` (0.2 s — two report intervals) in the past, between
the positions either side of that moment. Chasing the newest position made avatars lurch and
stop between packets. The report rate (`MuseumPresence.SendHz`, 10) and the delay move
together; the server's 20 Hz patch only forwards. Avatars **face their direction of travel**,
not the `yaw` they are sent: that yaw is the visitor's first-person camera, and a body that
followed it spun whenever they looked around. It orients a first sighting only; after that the
body turns (0.12 s smoothing) toward where it is moving above 0.3 m/s and keeps that heading
when it stops. With Multiplayer Play Mode, a virtual player
started before a script change keeps running the old code until it is restarted — a visitor
drawn as a capsule there is usually that, not a lost reference. The room is left when the scene unloads; up to 3 rejoins
are tried if it drops; no server means the museum is walked alone. Contract: server
`docs/protocol.md#exhibition-museum-scene`.

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
| Dakon | the **target hole** of a queued drop (one past the last drop the server accepted) | nothing is drawn until `drop_applied` — the board on screen is always one the server agrees with |
| Egrang | the **whole stride**: the bar grades the press and the racer walks immediately | the banked count, the places, the winner — all server numbers |

Dakon's hole prediction exists so the player can click a whole hand without waiting a round
trip each time; Colyseus delivers one client's messages in order, so the server applies the
burst in the order it was predicted. A wrong guess is a refusal, not a divergence, and a
refusal clears the entire in-flight queue (the server stopped at the rejected drop, so
everything queued behind it was aimed one hole too far).

It counts forward from the last drop `drop_applied` reported — anyone's drop, since both
players walk the same ring — and falls back to the synced `nextHoleIndex` only when nothing
anchors it: the first drop of a turn, or after a refusal. Anchoring on `nextHoleIndex`
instead is the bug that shipped: `drop_applied` is broadcast the instant a drop is applied,
while the patch moving `nextHoleIndex` follows on the room's patch interval, so a tap inside
that window aimed at the hole that had just been filled and was refused. On a LAN the window
is a millisecond; over wss from a phone it is a round trip, which is why it looked
mobile-only. `Assets/Scripts/Net/DakonHolePrediction.cs` owns the rule and is tested in
EditMode without a room.

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
