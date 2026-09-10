# Development Plan — Unity WebGL Client

Phase plan and honest status. Server-side rationale (stack choice, cost, hosting) lives in
the server repo's `dev-plan.md` and is not repeated. Each phase has an **exit criterion**:
a demonstrable in-browser fact, not a checklist.

## Where the project actually is

- Unity `6000.3.19f1`, URP `17.3.0`, Input System `1.19.0`, uGUI, AI Navigation, Colyseus
  Unity SDK `0.17.17`.
- Five scenes: `MainMenu`, `Museum`, `Lobby`, `Dakon`, `Egrang`. No Engklak.
- Both games are complete on both sides and have been played end to end against the
  deployed server. The client is live at `https://museum.fajrsyauqi.com`.
- **Blocking the exhibit today:** scene wiring lost in the 2026-08-19 recovery and the video
  CDN's 403. Neither is a code problem. MainMenu and the Lobby have since been repaired
  (`e11c10b`, `696d214`); the **Museum doorways**, the whole **Dakon view** and the eight
  **`SeedType` assets** are still empty — see [scene-setup.md](scene-setup.md).

---

## P0 — Tech validation ✅

Colyseus over WebSockets, proven in a real WebGL build rather than only in the Editor.
**Exit met:** two browser tabs create/join the same room and see shared state change.

## P1 — Core scaffolding & navigation ✅

Persistent bootstrap (`SessionData`, `ColyseusNetManager`, `SceneLoader`), `ServerConfig`,
`SceneReference`, faded async loads, the walkable museum hub with doorways.
**Exit met:** every scene reachable and returnable; nickname and connection survive loads.

## P2 — Dakon offline / hotseat ✅

Pure-C# `DakonBoard` implementing the v6 ruleset, `DakonView` rendering it, EditMode tests
covering setup, sweep, turn boundaries, endgame and ties.
**Exit met:** a full hotseat game to a win or tie with correct scoring —
*subject to the scene being rewired*.

## P3 — Dakon networking ✅

`NetDakonSession` over `DakonState`, `drop_seed` with pipelined drops, `drop_applied`-driven
animation, all error codes, `game_over`, and reconnect via the lobby handoff.
**Exit met:** two browsers play a full networked game; a mid-game refresh lands back in the
same seat and turn state.

## P4 — Lobby & room UI ✅ (two gaps)

Nickname on MainMenu, the generic Lobby scene, create/join by code, the slot list sized by
the request, host-only Start at 2+, the full failure-toast table, and the real
`ColyseusLobbyService` against the server (`FakeLobbyService` demoted to an Editor
convenience).

Still owed:

- **`?room=CODE` deep-link auto-join** — read the page URL on WebGL boot and skip straight
  to a join.
- **Reconnect into a room after a mid-game drop** — the token is cached and
  `Reconnect<T>()` exists; the "Reconnecting…" overlay and retry loop are not wired.
- **Play again** on either game. Online this is server work, not client work: the room locks
  at `startGame()` and never unlocks, so "again" can only mean a new room via the lobby.
  (Dakon's game-over panel now has its own exit.)

## P5 — WebGL build & hosting ✅

`Museum/Build/WebGL (Production|Development)` with endpoint and catalog validation, Brotli,
explicitly-thrown exceptions, and the rsync/nginx deployment — all verified end to end.
See [build-and-deploy.md](build-and-deploy.md).

**Open:** payload is ~119.5 MB. Texture import caps and crunch compression are the next
lever. And the video CDN 403 makes every museum screen a placeholder.

## P6 — Egrang ✅ / Engklak ⛔

Egrang is done: three-lane race, skill-check bar, three stilt profiles, server countdown,
prediction with server repair, results panel, offline mode, and both test suites.

Engklak remains **blocked on rules** — nothing exists on either side. See
[games/engklak.md](games/engklak.md).

## P7 — Resilience & polish (open)

- Cross-browser: Chrome / Safari / Firefox and mobile browsers.
- Poor-network behaviour: latency and loss against reconnect and desync.
- The reconnect overlay from P4.
- Kiosk fullscreen pass and final QA.
- **Exit:** all target browsers pass, and a simulated drop mid-match recovers without the
  player leaving the game scene.

---

## Immediate work queue

1. **Push this repo to a private remote.** It is local-only and was already lost once.
2. Rewire the Dakon view and the four Museum doorways
   ([scene-setup.md](scene-setup.md)); refill the eight `SeedType` assets.
3. Move the videos off ImageKit and repaste 16 URLs.
4. Turn `useFakeService` off on the Lobby canvas before any build.
5. Then P7.
