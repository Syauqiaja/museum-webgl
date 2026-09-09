# Testing

What is tested, where, and how to run it. The rule this project follows: **rules live in
pure C# and are tested there**; scenes are checked by playing them.

## Suites

| Assembly | Mode | Covers |
|---|---|---|
| `Museum.Core.Tests` | EditMode | `VideoUrlRules` — normalise (spaces only, idempotent), key sanitising, https/absolute validation |
| `Museum.Core.Tests.PlayMode` | PlayMode | `SessionData` — PlayerPrefs round-trip, sanitising on set, seat set/clear, minted `playerId` |
| `Museum.Games.Dakon.Tests` | EditMode | `DakonBoard` (setup, per-side type split, forced hole order and wrap, sweep scoring, turn and hand boundaries, short final draw, endgame, tie) and `DakonPile` |
| `Museum.Games.Egrang.Tests` | EditMode | `SkillCheckZones`, `SkillCheckCursor`, `SkillCheckTrackTexture`, `EgrangStickPresets`, `EgrangStickSolver`, `EgrangSeating`, `EgrangTrackProgress`, `EgrangRunSummary` |
| `Museum.Games.Egrang.Tests.PlayMode` | PlayMode | `EgrangRace`, `EgrangRacer`, `EgrangStepMover` snapping, `SkillCheckBar.Configure` |
| `Museum.Lobby.Tests` | EditMode | `LobbySlot`, `LobbyRoomSnapshot` (`CanStart`, `AmHost`, `IsFull`), `RoomCode`, `FakeLobbyService` |

All test asmdefs are `UNITY_INCLUDE_TESTS`-constrained, non-auto-referenced, and pull
`nunit.framework.dll` explicitly.

## Running them

- **Editor:** Window → General → Test Runner, EditMode and PlayMode tabs.
- **Headless:**

  ```bash
  Unity -batchmode -projectPath . -runTests -testPlatform EditMode  -logFile - -quit
  Unity -batchmode -projectPath . -runTests -testPlatform PlayMode  -logFile - -quit
  ```

- **Through MCP:** `run_tests` against the connected Editor instance.

## What is deliberately not tested

- **Colyseus sessions.** `NetDakonSession` / `NetEgrangSession` need a live room; their
  behaviour is verified by playing two clients against a local server. The seams they sit
  behind (`IDakonSession`, `IEgrangSession`) exist so the *view* logic is exercisable
  without them.
- **Scene wiring.** Nothing asserts that `DakonView.holeAnchors` is populated. That is what
  [scene-setup.md](scene-setup.md) is for, and why a wiring regression is invisible to the
  suite — see the recovery state noted there.
- **`FakeLobbyService` is a contract, not a mock.** It is where the lobby's rules are
  written down (host migration on leave, full-room rejection, name validation, code
  round-trip), and `FakeLobbyServiceTests` is what the real server room has to agree with.

## Cross-repo parity

The server reimplements every rule in TypeScript — no shared code. Parity is by **contract
and matching numbers**, enforced by keeping the two configs identical
(`DakonConfig.cs` ↔ `DakonConfig.ts`, `EgrangStickPresets.cs` ↔ the race's own constants)
and, where it matters most, by deterministic vectors: the Dakon board takes an explicit RNG
seed, so a seed → drops → expected outcome case can be run in both suites.

The server's own suite (`npm test`, mocha + `@colyseus/testing`, `DB_DISABLED=1`) covers
`DakonRoom`, `EgrangRoom`, `BaseGameRoom` and the two engines. Run it when changing anything
in this doc's "rules" column.

## Manual checks worth doing before a release

1. Two browser tabs: MainMenu → Museum → doorway → Lobby → create + join by code → play to a
   result, in both games.
2. Mid-match refresh on one tab — it should land back in the same seat (Dakon) or the same
   lane at the same distance (Egrang).
3. One tab closes mid-match — the other should get `game_over` (Dakon forfeit) or finish the
   race normally with the leaver frozen (Egrang).
4. Background a tab mid-race for ten seconds and bring it back — Egrang should re-ground
   every racer rather than showing them where the abandoned strides left them.
5. Walk every museum screen and confirm the video state (currently: placeholders, because
   the CDN 403s — see [architecture.md](architecture.md#video-configuration)).
