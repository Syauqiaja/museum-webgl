# Architecture — Client

How the client is put together and why. Rules live in [games/](games), wire protocol lives
in the server repo, screens live in [ui-flow.md](ui-flow.md); this is the skeleton the
three hang on.

```
[Browser: Unity WebGL build]

  Bootstrap object (DontDestroyOnLoad, created in MainMenu)
   ├── SessionData          nickname, playerId, seat (sessionId + reconnection token), last room code
   ├── ColyseusNetManager   the Colyseus Client; create / joinById / reconnect / leave
   └── SceneLoader          async scene loads behind a faded loading screen
        │
        ▼
  MainMenu ──► Museum ──doorway──► Lobby ──phase flips──► Dakon | Egrang
                (no room)          (real room)            (reconnects into that same room)
```

## The bootstrap singletons

Three components on one `Bootstrap` GameObject, all `DontDestroyOnLoad`, all following the
same duplicate rule: **destroy the component, never the GameObject.** A later scene that
carries its own Bootstrap would otherwise take the nickname and the seat down with the
duplicate — including singletons whose `Awake` had not run yet.

### `SessionData` — who this visitor is, for the whole visit

| Value | Lives | Why |
|---|---|---|
| `PlayerName` | PlayerPrefs `museum.session.playerName` | A tab refresh must not re-ask a museum visitor for their name. Always stored sanitised. |
| `PlayerId` | PlayerPrefs `museum.session.playerId` | Stable GUID minted on first run, sent as the `playerId` join option so the server can keep lifetime stats. **Stats only — never authorisation.** |
| `SessionId` | memory | The seat the server gave this connection. A game scene compares it against the state to know which side it plays. |
| `ReconnectionToken` | memory | Reclaims that seat within the server's 30 s window. |
| `RoomName` | memory | `dakon` / `egrang` — needed to reopen the right *typed* room on reconnect. |
| `LastRoomCode` | memory | Keeps a failed join's code in the field. |

Session id and token are **memory only, deliberately**. Both are issued per connection; a
stored copy would name a seat that no longer exists, and the client would try to reclaim it
instead of joining cleanly.

`ColyseusNetManager.PlayerName` is a passthrough to `SessionData`, not a second copy, so the
name typed on MainMenu is the same `displayName` a room sends three scenes later.

### `ColyseusNetManager` — the connection, and nothing else

Thin by design. It owns the `Client` built from `ServerConfig`, exposes
`CreateRoom<T>` / `JoinRoomById<T>` / `Reconnect<T>` / `Leave<T>`, stamps `displayName` into
every options dictionary, and caches the resulting seat into `SessionData`. It wires no
game callbacks — the caller (the lobby service, a net session) does that itself.

If no `ServerConfig` is assigned in the inspector it falls back to `Resources/ServerConfig`,
because generated scenes add the component from code and cannot pick an asset reference.

### `SceneLoader` — every scene change

`LoadScene(name|index)` runs an async load behind an instantiated loading-screen prefab:
fade in (0.3 s), wait for `progress >= 0.9`, hold for a 0.5 s minimum, activate, fade out.
It also unlocks and shows the cursor, which is what stops the museum's locked pointer from
following the player into a UI scene.

Scene names are constants in `SceneReference` so a loader call and Build Settings cannot
drift apart from a typo.

## Passing data into a scene

Unity's scene API has no parameter channel, so the one thing a scene must be *told* travels
in a static: `LobbyRequest.Pending` — `{ RoomName, MaxPlayers, GameScene, DisplayName }` —
set by the doorway immediately before loading the Lobby and consumed (and cleared) in
`LobbyController.Start`. Clearing it matters: a future entry point that forgets to set one
then lands on the visibly-wrong fallback instead of silently inheriting the last game's
request.

Everything else that must outlive a scene load lives on the Bootstrap object, not in
statics.

## Assemblies

| asmdef | Namespace | Contains | References |
|---|---|---|---|
| `Museum.Core` | `Museum.Core` | Bootstrap trio, `ServerConfig`, `SceneReference`, `LobbyRequest`, `PlayerNameRules`, `SceneTriggerPrompt`, video trio, `FollowCamera`, hover/raycast, the lobby-gallery stations (`IInteractable`, `GalleryStation` + `GongStation`/`GasingStation`/`SongStation`, `HopscotchCourse`/`HopscotchTile`, `ProceduralAudio`) | ColyseusSDK, TMP, uGUI, InputSystem |
| `Museum.Games.Dakon` | `Museum.Games.Dakon` | Pure model + view + session seam | TMP, uGUI (no Colyseus) |
| `Museum.Games.Egrang` | `Museum.Games.Egrang` | Race, racers, skill-check bar, stilts, views | TMP, uGUI, InputSystem (no Colyseus) |
| `Museum.Net` | `Museum.Net` | Generated schema mirror, `NetDakonSession`, `NetEgrangSession`, the two net bootstraps | ColyseusSDK, Core, Dakon, Egrang |
| `Museum.Lobby` | `Museum.Lobby` | Lobby model, `ILobbyService` + both implementations, controller, slot view | Core, Net, ColyseusSDK, TMP, uGUI |
| `*.Editor` | `*.EditorTools` | UI style + scene generators, WebGL build, video migration | their runtime asm |
| `*.Tests`, `*.Tests.PlayMode` | `*.Tests` | EditMode / PlayMode tests | their runtime asm |

**The dependency arrow only points one way: `Museum.Net` knows about the games; the games
know nothing about Colyseus.** That is why `IDakonSession` and `IEgrangSession` are declared
inside the *game* assemblies — they are the seam, not either side of it. Two scripts outside
any asmdef (`FPSController`, `FPSInputReader`, `VideoTriggerPlayer`, `MinimapOnlyObject`)
sit in `Assembly-CSharp` on purpose: scene instances serialise them by name, and moving them
would break every reference.

## Rules live outside MonoBehaviour

Every rule a game has is in a plain C# class with no `UnityEngine` dependency, so it can be
unit-tested without entering Play mode and can be diffed line-by-line against the server's
port:

- `DakonBoard`, `DakonConfig`, `Seed`, `DropResult` — the whole Dakon ruleset.
- `EgrangStep`, `SkillCheckZones`, `SkillCheckCursor`, `EgrangSeating`,
  `EgrangTrackProgress`, `EgrangStickPresets`, `EgrangRunSummary` — the whole Egrang
  ruleset except the animation.
- `PlayerNameRules`, `RoomCode`, `VideoUrlRules`, `LobbySlot`/`LobbyRoomSnapshot` — the
  small decisions that would otherwise hide inside a `MonoBehaviour`.

The server is a **Node reimplementation**, not shared code (see the server repo's
`src/games/`). Parity is by contract and by matching numbers, never by sharing a file.

## The session seam

Both games render an interface, not a source of truth:

```
DakonView  ──renders──►  IDakonSession  ◄──  LocalDakonSession (hotseat, rules run locally)
                                        ◄──  NetDakonSession   (server is authoritative)

EgrangRace ──drives───►  IEgrangSession ◄──  NetEgrangSession  (server is authoritative)
                                        ◄──  null              (offline: lane 1, local finish line)
```

The two implementations differ in exactly one way that matters: locally a move resolves the
instant it is made, online it is a *request* whose result arrives later. So the view never
reads a return value — it asks, and renders the event that comes back. That single rule is
what lets one view serve both modes with no `if (online)` in it.

Which mode a scene runs in is decided by a `[DefaultExecutionOrder(-100)]` bootstrap in the
scene (`DakonNetBootstrap`, `EgrangNetBootstrap`): if `SessionData` holds a reconnection
token for the matching room name, it reconnects and binds a net session in `Awake`; if not,
the view starts its offline game in `Start`. A failed reconnect logs a warning and falls
back to offline — a museum kiosk with no server still gets a game.

## Endpoint configuration

`Assets/Resources/ServerConfig.asset` is the single place a server URL is written down:

- `devEndpoint` — `ws://localhost:2567`, matching the server's `npm start`.
- `prodEndpoint` — `wss://api.museum.fajrsyauqi.com`. **Must** be `wss://`: the page is
  served over `https://` and browsers block mixed-content WebSockets.
- `useDevEndpoint` — currently **on**. `Museum/Build/WebGL (Production)` flips it for the
  duration of the build and restores it afterwards, so a production build cannot ship the
  dev endpoint and a build cannot silently break Play mode.

Never hardcode a URL in a scene object.

## Video configuration

The museum footage does not ship in the build. Embedding it produced a 141 MB
`WebGL.data.br`; streaming cut the payload to ~110 MB and means a clip is fetched only when
a visitor walks up to that screen.

- `Assets/Resources/VideoCatalog.asset` — one entry per video: `key` (the original asset
  filename, `DAKON.mp4`) and the full CDN `url`. **Scene objects carry only the key.**
- `VideoUrlRules` — pure C# normalise/validate. Normalisation encodes spaces only (the
  footage is named for humans: `Gobak Sodor.mp4`), and re-encoding wholesale would turn every
  existing `%20` into `%2520`. `https://` is required for anything that ships.
- `StreamedVideoScreen` — forces `VideoSource.Url`, prepares before playing, swaps the
  screen's `RawImage` to a placeholder while loading and on failure, retries twice with a
  2 s delay, and treats a prepare still running after 12 s as an error, because a
  CORS-tainted or stalled fetch on WebGL never raises `errorReceived` at all.
- `VideoTriggerPlayer` — the collider half: `RequestPlay` on enter, `RequestStop` on exit.
  Walking away and back *is* the retry the museum offers; the canvas is world-space with no
  event camera, so a Retry button would not reliably take clicks.
- `Museum/Build/WebGL (Production)` refuses to build if any catalog entry is empty or not
  `https://`.

**Live blocker:** the 16 catalog URLs point at `ik.imagekit.io/altara/…`, which returns
`403 Video transformations limit exceeded` on every one. Nothing plays until the plan
changes or the files move (VPS `/var/www/`, R2, Bunny). That is 16 URL edits and no code
change. Two screens (`Vid Sluku`, `Vid Jamuran`) have keys with no video by design and show
the placeholder; `Vid Bitingan` plays `Tok tok pyar.mp4` — names disagree, preserved
deliberately, unconfirmed whether it is a bug.

The CDN must send CORS headers covering the client origin, support HTTP Range
(`Accept-Ranges: bytes`), serve `Content-Type: video/mp4`, and hold faststart-muxed
H.264/AAC. Without Range the browser downloads the whole file before the first frame.

## Repo layout

```
Assets/
  Scenes/          MainMenu, Museum, Lobby, Dakon, Egrang
  Scripts/
    Core/          Museum.Core — bootstrap, config, doorways, video, camera, hover, gallery stations
      Editor/      UI generators, lesson/signage copy, lighting, the Museum/Decor/* builders
    Lobby/         Museum.Lobby — service seam, model, controller, slot view
    Games/Dakon/   model (pure C#) + view + sessions
    Games/Egrang/  race, racers, skill-check bar, stilts, HUD views
    Net/           Museum.Net — Schema/ (generated, do not hand-edit), net sessions, bootstraps
    Player/        FPS controller + input reader (Assembly-CSharp)
    Editor/        WebGL build, video migration
    Tweens/        FadeTween, PopupTween
  Data/Egrang/     three EgrangStickProfile assets
  Models/Generated/  28 Blender-built decor props (FBX), materials remapped at import
  Material/Decor/  the generated-decor palette (MuseumDecorMaterials)
  Resources/       ServerConfig, VideoCatalog, seeds/, lessons/, TMP
  Docs/            this documentation set
docs/              root-level: dakon-scene-setup.md, superpowers/{specs,plans}
```

`Assets/Scripts/Net/Schema/*.cs` is generated from the server's `src/rooms/schema/*.ts`
with `schema-codegen` (`@colyseus/schema` 4.0.27, namespace `Museum.Net.State`). The decoder
addresses fields **by index**, so a drift shows up as garbled state rather than an error —
regenerate on any server schema change, never hand-edit.

Server-side topology (VPS, nginx, TLS, PM2, MySQL) lives in the server repo's
`docs/architecture.md` and `docs/deployment.md`.
