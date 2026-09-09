# Museum Minigames — Unity WebGL client

Unity `6000.3.19f1` / URP / WebGL client for a museum exhibit of Indonesian traditional
games. Two games: **Dakon** (congklak, 2 players, turn-based) and **Egrang** (stilt race,
3 players, real-time). The authoritative server is a **separate repo** at
`/Users/mac/Documents/Works/nodejs/museum-minigames` (Node + TypeScript + Colyseus).

## Docs — read and update these

All project knowledge lives in [`Assets/Docs/`](Assets/Docs/README.md). Read the relevant
doc before starting; update it in the same change when the change alters what it describes.
`Assets/Docs/README.md` is the index.

The client↔server **contract** (messages, schemas, room names) and the per-game **rules** are
owned by the server repo's `docs/protocol.md` and `docs/games/*.md`. Never invent a message
type or a rule client-side; if it is not there, flag it so both repos change together.

## Non-negotiables

- **Server is authoritative.** The client sends input events and renders synced state. The
  only sanctioned prediction is one the server immediately confirms or corrects (Egrang's
  stride, Dakon's queued hole index) — it never becomes a score.
- **Rules live outside `MonoBehaviour`**, in pure C# with no `UnityEngine` dependency, and
  are **not scene-editable**. Pool size, grab size, finish distance and stilt tuning are
  rules, and a scene that could override them makes offline a different game from online.
- **Rules exist twice, in two languages.** `DakonBoard.cs` ↔ `DakonBoard.ts`,
  `EgrangStickPresets.cs` ↔ the server's race constants. Change one, change the other, and
  say so in both repos' docs.
- **`Assets/Scripts/Net/Schema/*.cs` is generated** by `schema-codegen` from the server's
  schema. The decoder addresses fields by index, so hand-edits produce garbled state rather
  than errors. Regenerate, never edit.
- **UnityEvent handler names are API** — generated scenes wire buttons by method name
  (`CreateRoom`, `JoinRoom`, `StartGame`, `LeaveRoom`, `CopyCode`, `ConfirmName`,
  `BackToMainMenu`, `ChooseTouch`, `ChooseDesktop`). Renaming one silently breaks a scene.
- **No absolute URLs in scenes.** Endpoints come from `ServerConfig`, media from
  `VideoCatalog`; scene objects carry keys.
- **No third-party networking vendor, no WebRTC/UDP.** WebSockets via the Colyseus Unity SDK
  only. See `Assets/Docs/boundaries.md`.
- **Museum lighting is generated, not hand-placed.** `Museum/Lighting/Build` owns the 71
  realtime lights, 25 reflection probes, the ambient gradient and the fog. The project is in
  **Linear** color space on a **Forward+** renderer because of it. There is **no GI on this
  target** — lightmaps need UV2 (0 of 975 meshes have it) and APV is disabled on WebGL2 — so
  do not go hunting for a bounce-light setting. See `Assets/Docs/lighting.md`.

## Layout

- `Assets/Scripts/Core/` — `Museum.Core`: bootstrap trio (`SessionData`,
  `ColyseusNetManager`, `SceneLoader`), config, doorways, streamed video.
- `Assets/Scripts/Lobby/` — the generic create/join lobby behind `ILobbyService`.
- `Assets/Scripts/Games/{Dakon,Egrang}/` — pure-C# rules + views + session seams.
- `Assets/Scripts/Net/` — generated schema, `NetDakonSession`, `NetEgrangSession`, the
  reconnect bootstraps.
- `Assets/Scripts/*/Editor/` — UI generators (`Museum/…` menu), WebGL build, video migration.
- `Assets/Scripts/*/Tests/` — EditMode and PlayMode suites.
- `Assets/WebGLTemplates/Wiraga/` — the custom WebGL template (viewport/touch rules, the
  portrait overlay) that `BuildWebGL` selects at build time; `Assets/Plugins/WebGL/` — the
  jslib backing `Museum.Core.PlatformDetect`. See `Assets/Docs/input-and-platform.md`.

## Commands

- Tests: Test Runner in the Editor, or
  `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -logFile - -quit`.
- Build: menu `Museum/Build/WebGL (Production)` or `-executeMethod
  Museum.Build.Editor.BuildWebGL.Production`. Deploy steps in
  `Assets/Docs/build-and-deploy.md`.
- Local server: `cd /Users/mac/Documents/Works/nodejs/museum-minigames && npm start`.

## Hazards in this working tree

- The project was destroyed on 2026-08-19 and rebuilt; **inspector values did not survive**.
  MainMenu, the Lobby, Egrang, the **Dakon view** and the eight **`SeedType` assets** have all
  been repaired. What is still unwired is **one of the four Museum doorways** (the
  `SceneTriggerPrompt` on `Vid LT2/Vid Engklek/Cube` — blank `sceneName`, `useLobby` off;
  Engklek is not a shipped game, so there is nothing to point it at) — see
  `Assets/Docs/scene-setup.md` before debugging a "broken" script.
- Play mode runs with **domain reload disabled**, which strands LeanTween's updater from the
  second Play onward — an active panel with alpha 0 and no error. `LeanTweenPlayModeReset.cs`
  handles it; see `Assets/Docs/ui-style.md` §8 before debugging "invisible UI".
- The repo is **local-only**. Push to a private remote before further work.
- Never run `rsync --delete` without both source and destination present — that is what
  destroyed it. **Deploys do not use `--delete` at all**: it clears the live `Build/` before
  the replacements land, and a dropped transfer leaves the site 404ing (2026-09-09).
  `chmod -R a+rX` runs after *every* build, not once — Unity rewrites the `.br` files as
  `600` each time and nginx 403s them. See `Assets/Docs/build-and-deploy.md`.
