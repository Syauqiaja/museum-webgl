# Boundaries — Client

Constraints that mirror the server's `docs/boundaries.md`. Do not violate one without a
direct decision from the user, recorded here.

## Do not introduce

- **No client-authoritative state** for anything that decides a match — moves, strides,
  scores, places, winners. The client sends input events (`room.Send`), renders synced
  state, and never mutates a local copy expecting it to sync. The one sanctioned exception
  is *prediction that the server immediately confirms or corrects*
  ([networking.md](networking.md#7-prediction-and-where-it-is-allowed)) — Egrang's stride
  animation and Dakon's hole guess. Neither ever becomes a score.
- **No third-party game-networking vendor** — no Photon, no Unity Gaming Services
  Lobby/Relay, no PlayFab. Transport is the Colyseus Unity SDK over WebSockets, nothing else.
- **No WebRTC or raw UDP.** WebGL cannot use raw sockets; `wss://` is the only transport and
  is sufficient for both games.
- **Don't invent protocol messages or game rules.** If a message is not in the server's
  `protocol.md`, or a rule is not in its `games/*.md`, stop and flag it so both repos change
  together. A rule invented on one side is a rule that will disagree with the other.
- **No recurring cost added casually.** One VPS plus the domain is the accepted baseline.
  A video CDN is the open question, not a licence to add SaaS generally.

## Conventions that are load-bearing

- **Rules live outside `MonoBehaviour`.** Pure C# classes with no `UnityEngine` dependency,
  so they are unit-testable without Play mode and diffable against the server's port.
- **Rules are not scene-editable.** Pool size, grab size, finish distance and stilt tuning
  are not exposed on views. A scene that could set its own would make an offline match a
  different game from an online one — which has already happened once (a scene dealing 60
  seeds against a server dealing 120).
- **No absolute URLs in scene objects.** Server endpoints come from `ServerConfig`, media
  URLs from `VideoCatalog`; scene objects carry keys.
- **Don't re-embed large media.** The museum footage streams. A 141 MB WebGL payload is
  unusable on museum wifi.
- **The Museum scene has no network.** No `ColyseusClient` there, ever.
- **Generated schema is generated.** `Assets/Scripts/Net/Schema/*.cs` comes from
  `schema-codegen` against the server's schema files. Hand-editing it produces state that
  decodes into the wrong fields with no error.
- **UnityEvent handler names are API.** Generated scenes wire buttons by method name
  (`CreateRoom`, `JoinRoom`, `StartGame`, `LeaveRoom`, `CopyCode`, `ConfirmName`,
  `BackToMuseum`, `BackToMainMenu`, `ChooseTouch`, `ChooseDesktop`, `SelectAvatar`). Renaming
  one silently breaks a scene.

## Decisions recorded

Project-wide settings changed by a direct decision, listed here because they affect every
scene rather than the one that needed them.

- **Linear color space** (2026-08-19), with `m_LightsUseLinearIntensity` on to match. Taken
  for the Museum lighting; it restyles MainMenu, Dakon and Egrang too, and all four were
  checked at the time. Reverting it means re-tuning every light in the project.
- **Forward+ on `Mobile_Renderer`** (2026-08-19). Forward caps additional lights at 4 per
  object, and the Museum has 39; under Forward its lanterns popped in and out as the visitor
  walked. Forward+ clusters them and ignores the cap.

Both are explained in [lighting.md](lighting.md).

## Out of scope unless asked

- Analytics, abuse handling, rate limiting beyond what the server already does.
- Kiosk hardware and browser provisioning.
- Per-venue LAN/offline builds.
- Localisation beyond the Indonesian strings already in the UI.
