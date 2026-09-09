# Museum Minigames — Unity WebGL client

Browser-playable exhibit of Indonesian traditional games for a museum. A visitor names
themselves, walks a 3D museum, watches reference footage on the exhibit screens, and steps
into a game against other people over the network.

Two games are built:

- **Dakon** — congklak/mancala, 2 players, turn-based. Also plays offline hotseat.
- **Egrang** — stilt race, 3 players, real-time timing. Also runs offline solo.

Unity `6000.3.19f1`, URP, WebGL only. Live at `https://museum.fajrsyauqi.com`.

## Documentation

**All of it lives in [`Assets/Docs/`](Assets/Docs/README.md)** — start at that index.

| | |
|---|---|
| What the product is | [`Assets/Docs/overview.md`](Assets/Docs/overview.md) |
| How it is built | [`Assets/Docs/architecture.md`](Assets/Docs/architecture.md) |
| Game rules | [`Assets/Docs/games/`](Assets/Docs/games) |
| Networking | [`Assets/Docs/networking.md`](Assets/Docs/networking.md) |
| Wiring a scene | [`Assets/Docs/scene-setup.md`](Assets/Docs/scene-setup.md) |
| Building / deploying | [`Assets/Docs/build-and-deploy.md`](Assets/Docs/build-and-deploy.md) |

Feature design specs and plans are historical records under `docs/superpowers/`.

## The other half

The authoritative game server is a **separate repo**:
`Documents/Works/nodejs/museum-minigames` (Node + TypeScript + Colyseus). It owns the rules,
matchmaking and persistence; this client sends input events and renders what it is told. Its
`docs/protocol.md` is the contract source of truth for both sides.

## Quick start

1. Open the project in Unity `6000.3.19f1`.
2. Run the server locally: `cd ../../nodejs/museum-minigames && npm start` (`ws://localhost:2567`,
   which is what `Assets/Resources/ServerConfig.asset` points at by default).
3. Play from `Assets/Scenes/MainMenu.unity` — MainMenu → Museum → doorway → Lobby → game.
4. Tests: Window → General → Test Runner (EditMode and PlayMode). See
   [`Assets/Docs/testing.md`](Assets/Docs/testing.md).

## Warnings

- **This repo is local-only and has been destroyed once** (2026-08-19, a mistyped
  `rsync --delete`). Push it to a private remote before doing further work.
- Several scenes are **unwired** after that recovery — see
  [`Assets/Docs/scene-setup.md`](Assets/Docs/scene-setup.md) before concluding something is
  broken in code.
