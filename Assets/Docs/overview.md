# Overview — Unity WebGL Client

Browser-playable collection of Indonesian traditional games, shown on museum kiosks and
served publicly from the same URL. A visitor names themselves, walks a 3D museum, watches
the exhibit footage, and steps into a game. Multiplayer games are matched by a short room
code and run on an authoritative Colyseus server (separate repo).

## Audience and platform

- **Museum kiosks** — the same public URL, opened full-screen in a kiosk browser
  (Chrome `--kiosk`). Needs venue internet; there is no offline build today.
- **Public / home players** — same server, same URL, same rooms.
- **Platform: WebGL only.** WebGL cannot use raw UDP, so the only transport is WebSockets
  (`wss://` in production, because the page itself is served over `https://`).
- **Language of the UI: Indonesian.** "Giliran kamu", "Pemain 2", "Menunggu…",
  "Juara 2 dari 3". Code identifiers and docs stay English.

## The five scenes

| Scene | File | Networked | Role |
|---|---|---|---|
| **MainMenu** | `Assets/Scenes/MainMenu.unity` | Bootstrap only | Nickname entry, then **Enter Museum**. No game buttons, no create/join. |
| **Museum** | `Assets/Scenes/Museum.unity` | Room `museum` (presence) | Shared walkable exhibition hub: FPS movement, video screens, doorways into the games. Other visitors show as named avatars (`MuseumPresence`); walked alone when no server is reachable. |
| **Lobby** | `Assets/Scenes/Lobby.unity` | Room `dakon` / `egrang` | One generic create/join-by-code screen serving every game, parameterised by `LobbyRequest`. |
| **Dakon** | `Assets/Scenes/Dakon.unity` | Room `dakon` (2 seats) | Congklak/mancala board game. Also plays offline hotseat when there is no seat to reclaim. |
| **Egrang** | `Assets/Scenes/Egrang.unity` | Room `egrang` (3 seats) | Stilt race. Also runs offline in lane 1 when there is no seat to reclaim. |

Build Settings order: `MainMenu`, `Museum`, `Dakon`, `Lobby`, `Egrang` — all five enabled.

There is **no Engklak scene**, and no `engklak` room exists on the server. See
[games/engklak.md](games/engklak.md) before adding a menu entry for it.

## The player's path

```
MainMenu ──► Museum ──doorway──► Lobby ──host starts / room fills──► game scene
(nickname)   (walk, watch,       (create or join                    (Dakon | Egrang)
              choose)             by 6-char code)                    reconnects into
                                                                     the seat it already has
```

Games are never launched from a menu button. Walking up to the exhibit *is* the entry, which
is what makes the hub a hub instead of a menu with a 3D backdrop. The doorway
(`SceneTriggerPrompt`) parks a `LobbyRequest` — room name, seats, destination scene, the
game's display name — and loads the Lobby; the Lobby loads the game scene once the room
starts. Full screen-by-screen detail: [ui-flow.md](ui-flow.md).

## The games

| | Dakon | Egrang |
|---|---|---|
| Players | 2 | 3 (host may start with 2) |
| Shape | Turn-based, deterministic | Real-time, timing-based |
| Room | `dakon` | `egrang` |
| Match length | 6 hands of 10 seeds — 3 turns each | 50 strides (25 m), 15 s picking window |
| Offline mode | Hotseat on one screen | Solo in lane 1 |
| Rules | [games/dakon.md](games/dakon.md) | [games/egrang.md](games/egrang.md) |

Both are fully implemented on both sides and playable end to end.

## Content

- **Museum environment** — `museum_wiraga_full.fbx` plus BOKI LowPolyNature and
  ASSET_NUSANTARA sets, terrain, and a navmesh-free rigidbody FPS controller.
- **Reference footage** — 16 videos of the traditional games play on the hub's screens.
  They do **not** ship in the build; each screen carries a *key* and streams the file from a
  CDN listed in `Assets/Resources/VideoCatalog.asset`. See
  [architecture.md](architecture.md#video-configuration) — and note the live blocker there:
  the current ImageKit URLs return `403 Video transformations limit exceeded`, so nothing
  plays until the files move hosts.
- **Seed species** — eight `SeedType` assets in `Assets/Resources/seeds/` supply Dakon's
  card art and 3D seeds.
- **Stilt profiles** — three `EgrangStickProfile` assets in `Assets/Data/Egrang/` supply
  Egrang's three difficulties.

## Status, honestly

Built and working: bootstrap and scene flow, nickname identity, the museum hub, the generic
lobby against the real server, Dakon online + hotseat, Egrang online + offline, both results
screens, the WebGL build script, and the VPS deployment.

Not built: Engklak (no rules, no room, no scene), `?room=CODE` deep-link auto-join, a
"reconnecting…" overlay for a mid-game socket drop, play-again on either game, and an exit
button on Dakon's game-over panel. Video delivery is blocked on the CDN plan, not on code.
See [dev-plan.md](dev-plan.md) for the phase view.

## Recovery note

On 2026-08-19 this project was deleted by a mistyped `rsync --delete` and rebuilt from
Claude Code transcripts plus an AssetRipper pass over the deployed WebGL build. Two
consequences survive in the tree and matter when something looks wrong: **MonoBehaviour
inspector values were not recoverable** (MainMenu, the Lobby and Egrang have been repaired;
the Museum doorways, the Dakon view and the seed assets have not — see
[scene-setup.md](scene-setup.md)), and some scene script references still point at ripper
stub GUIDs for package scripts. The repo is local-only; push it to a private remote before
doing further work.
