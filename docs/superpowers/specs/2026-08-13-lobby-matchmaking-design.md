# Lobby / Matchmaking Bridge Scene — Design

**Date:** 2026-08-13
**Status:** approved, ready for implementation plan
**Relates to:** `Assets/Docs/dev-plan.md` P4, `Assets/Docs/ui-flow.md`, `Assets/Docs/networking.md`

## Problem

Egrang is a 4-player race. There is no screen where players pick a nickname, create or
join a room, and see who else is in it before the race starts. `Egrang.unity` currently
assumes a single local player.

## Scope

Build a **generic** `Lobby` scene that any minigame routes through, and wire Egrang
(4 slots) as its first consumer. Build it against an in-memory fake service so the UI,
slot rules and failure UX are complete and testable before the Node server has an
`egrang` room.

**In scope:** name fallback prompt, create room, join by code, 4-slot room view, host
start, leave, failure toasts, EditMode tests, `Lobby.unity`, MainMenu entry point.

**Out of scope (seams left, not built):** `?room=CODE` deep-link, reconnect, the real
`ColyseusLobbyService` implementation, and anything about Egrang race rules — the
server's `docs/games/egrang.md` is still TODO and gameplay must not be invented
client-side.

## Decisions

| Decision | Choice | Why |
|---|---|---|
| Scene scope | One generic `Lobby` scene, parameterized | Dakon and Engklek reuse it; no copies to refactor later |
| Transport | `ILobbyService` + `FakeLobbyService` now, Colyseus impl later | Server room does not exist; contract-first also tells the server repo what to build |
| Nickname entry | MainMenu, with a Lobby fallback panel | Matches `ui-flow.md`; fallback covers Museum launch points where MainMenu was skipped |
| Start rule | Host-only button, enabled at 2+ occupied slots | Testable without four humans; empty slots simply don't race |
| Authority | Client `CanStart` is UX only | Server re-validates; mirrors `networking.md` §4 |

## Architecture

```
Assets/Scripts/Core/
  SessionData.cs            persistent MonoBehaviour: PlayerName, LastRoomCode
  SceneReference.cs         + Lobby, + Egrang

Assets/Scripts/Lobby/
  LobbyRequest.cs           roomName, maxPlayers, gameScene  — plain class; the pending
                            instance is held on SessionData (persistent), set by the
                            launcher before loading Lobby and read in LobbyController.Start
  LobbyPhase.cs             Waiting | Starting | InProgress
  LobbySlot.cs              sessionId, displayName, isHost, IsEmpty     (pure C#)
  LobbyRoomSnapshot.cs      code, slots[], phase, mySessionId, hostSessionId, CanStart (pure C#)
  LobbyError.cs             code + message constants
  ILobbyService.cs
  FakeLobbyService.cs       in-memory, no network
  ColyseusLobbyService.cs   stub; throws NotImplemented until the server room lands
  LobbyController.cs        MonoBehaviour; binds service events to panels
  LobbySlotView.cs          one slot card (presentational)
  Tests/                    EditMode tests

Assets/Scenes/Lobby.unity
```

Pure-C# types (`LobbySlot`, `LobbyRoomSnapshot`, `FakeLobbyService`) carry no
`MonoBehaviour` and no Colyseus reference, per CLAUDE.md's engine-independence rule.

`SessionData` is a `DontDestroyOnLoad` singleton following the `SceneLoader` /
`ColyseusNetManager` pattern already in `Assets/Scripts/Core/`, and lives on the same
persistent object. `ColyseusNetManager.PlayerName` becomes a passthrough to it rather
than a second copy of the nickname.

`LobbyController` chooses its service from a `[SerializeField] bool useFakeService`
(default true) — the single place the fake-to-real swap happens.

### `ILobbyService`

```csharp
Task CreateRoom(string roomName, string displayName, int maxPlayers);
Task JoinRoom(string code, string displayName);
Task StartGame();
Task Leave();
event Action<LobbyRoomSnapshot> RoomUpdated;
event Action<string, string> Failed;   // (code, message)
```

Every UI refresh is driven by `RoomUpdated` carrying a whole snapshot. The controller
never mutates slots locally — the same discipline the real synced schema will require,
so swapping in `ColyseusLobbyService` changes no UI code.

### Slot rules

- `slots` always has `maxPlayers` entries; unoccupied ones render "Waiting…".
- Join fills the lowest-index empty slot.
- Joining a full room fails with `room_full`; joining an unknown code fails with
  `room_not_found`.
- If the host leaves, host migrates to the lowest-index remaining occupied slot.
- `CanStart => phase == Waiting && OccupiedCount >= 2 && mySessionId == hostSessionId`.

## Screen flow

```
NamePanel        (shown only when SessionData.PlayerName is empty)
   ↓ confirm — name trimmed, 2–16 chars, else `name_invalid`
EntryPanel       [Create Room]  [code field + Join Room]  [Back]
   ↓ create                      ↓ join — uppercased, 0/O/1/I filtered per server code scheme
RoomPanel        room code (large + copy button), 4 slot cards, [Start] (host, ≥2), [Leave]
   ↓ phase → InProgress
SceneLoader.LoadScene(request.gameScene)
```

Panels are sibling objects on one canvas switched with `SetActive`; no scene loads
between them. `Back` from EntryPanel returns to MainMenu. `Leave` from RoomPanel calls
`service.Leave()` and returns to EntryPanel.

MainMenu gains a nickname input (writing `SessionData.PlayerName`) and a Play-Egrang
button that sets `LobbyRequest { roomName = "egrang", maxPlayers = 4, gameScene =
SceneReference.Egrang }` and loads `Lobby`.

`"egrang"` as a room name is unconfirmed against the server's `protocol.md`
(`Assets/Docs/networking.md` §3 lists it as an open item). It lives in one place —
the `LobbyRequest` built by the launcher — so confirming it later is a one-line change.

## Failures

`Failed(code, message)` raises a non-blocking toast and returns to EntryPanel with the
typed code preserved, matching the table in `ui-flow.md`.

| Code | Message | Trigger |
|---|---|---|
| `room_not_found` | "Room not found" | unknown / expired code |
| `room_full` | "Room is full" | 5th join attempt |
| `name_invalid` | "Enter a name (2–16 characters)" | client-side validation |
| `connection_failed` | "Can't reach the server" | transport error (real impl only) |

`FakeLobbyService` exposes a way to force each of these, so the failure UX is
exercisable before a server exists.

## UI

Follows `Assets/Docs/ui-style.md` in full: 800×600 canvas at Match 0.5,
`InputSystemUIInputModule`, TMP with explicit font assets, palette from §5,
`frame_default` / `frame_with_corner` sliced frames, stock ColorTint buttons,
`CanvasGroup` + one tween per animated element. Slot cards are a prefab instantiated
into a container, following `DakonView.handContainer` / `EgrangStickCard`. The lobby is
a flat 2D screen, not a panel over a live 3D scene, so §6b's scrim rules do not apply.

## Testing

EditMode only, under `Assets/Scripts/Lobby/Tests/`, matching the existing
`Assets/Scripts/Games/*/Tests/` layout:

- `LobbySlot` — empty vs occupied.
- `LobbyRoomSnapshot.CanStart` — false at 1 player, true at 2 for host, false for
  non-host, false once phase leaves `Waiting`.
- `FakeLobbyService` — create yields a code and seats the creator as host; join fills
  the next slot and raises `RoomUpdated`; a 5th join raises `room_full`; leave frees the
  slot; host leaving migrates host; unknown code raises `room_not_found`.

No PlayMode tests. `LobbyController` stays thin enough to verify by playing the scene.

## Follow-ups (separate tasks)

1. `ColyseusLobbyService` + `Schema` classes, once the server's `egrang` room and
   `protocol.md` lobby state exist. Confirm the room-name string then.
2. `?room=CODE` deep-link auto-join.
3. Reconnect via the cached `ReconnectionToken`.
4. Egrang gameplay networking — blocked on server rules.
