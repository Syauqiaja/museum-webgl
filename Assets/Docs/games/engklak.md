# Engklak — Not Built

**Status: not implemented anywhere, and blocked on rules.** This file exists so nobody
re-derives that from an empty folder.

Engklak (also written Engklek — the hopscotch-style game) was in the original scope
alongside Dakon and Egrang. Nothing of it exists today:

| Layer | State |
|---|---|
| Rules | **TODO** — never supplied. The server's `docs/games/engklak.md` is a stub of TODO headings |
| Server room | **Not registered.** `src/app.config.ts` registers `dakon` and `egrang` only |
| Protocol | Placeholder section in the server's `protocol.md`, all fields TODO |
| Unity scene | Does not exist |
| Unity scripts | Do not exist |
| Museum doorway | Does not exist — the exhibit's trigger carries a `ComingSoonNotice` ("Permainan Engklek segera hadir"), see [scene-setup.md](../scene-setup.md) |

The only trace in this repo is the exhibit footage: `engklek_G0JYK3Hz.mp4` plays on one of
the museum's screens as reference material, which is independent of whether the game is
playable.

## Do not

- Ship a menu entry, doorway or lobby request pointing at `engklak` — `RoomSession.Factories`
  has no entry for it, so the lobby fails loudly with "This build has no schema for the
  'engklak' room", and the server would reject the join anyway.
- Invent rules to unblock a scene. Both implementations have to agree, and inventing them
  client-side guarantees they will not.

## To unblock

1. The rules get written into the server's `docs/games/engklak.md` — objective, court
   layout, player count, turn or real-time model, inputs, win condition, edge cases.
2. The server's `protocol.md#engklak` section is filled in, an `EngklakState` schema and an
   `EngklakRoom` are added, and the room is registered.
3. The client regenerates `Assets/Scripts/Net/Schema/EngklakState.cs`, adds one line to
   `RoomSession.Factories`, and follows the same shape as the other two: pure-C# rules
   model → session interface → view, with an offline mode.
4. The `ComingSoonNotice` on `Vid LT1/Vid Engklek/Cube` is swapped for a `SceneTriggerPrompt`
   with `new LobbyRequest("engklak", <seats>, SceneReference.Engklak, "Engklak")`
   (`SceneWiringRepair.Doorways` + drop `MakeEngklekComingSoon`), the scene is added to
   Build Settings, and `SceneReference` gains its constant.

See [games/dakon.md](dakon.md) and [games/egrang.md](egrang.md) for the two worked examples
— a turn-based one and a real-time one.
