# UI Flow

Screen flow across scenes. Transitions reuse LeanTween via `LoopFadeTween.cs` for fades.
Visual rules (canvas setup, palette, typography, frames, buttons, motion) live in
[ui-style.md](ui-style.md) — this doc covers flow only.

> This describes how the flow **behaves when wired**. After the 2026-08-19 recovery the
> Museum doorways and the whole Dakon view are still unwired in their scenes, so parts of
> this flow are currently dead on arrival. See [scene-setup.md](scene-setup.md).

## Flow graph

```
Boot ─► MainMenu ─► Museum hub ─► game doorway ─► Lobby ─► game scene ─► Results ─┬─► Play Again
        (nickname)  (shared hall,   (SceneTrigger-  (create /   (per game)         └─► Return to Museum
                     presence room)  Prompt)         join /
                                                     host start)

        ?room=CODE deep-link ─► auto-JoinById ─► Lobby room panel   ← NOT YET IMPLEMENTED
```

Games are **not** launched from MainMenu. A player types a nickname, walks into the museum,
and walks up to the game they want; the doorway opens the shared Lobby scene and tells it
which room to use and which scene to load once the host starts. MainMenu has no per-game
buttons and no create/join at all — that walk is what makes the hub a hub rather than a menu
with a 3D background.

## Screens

### MainMenu (platform picker, then nickname)
- **First screen: the platform picker.** `Kamu main pakai apa?` with two buttons, `HP /
  Layar Sentuh` and `Komputer`, wired to the UnityEvent handlers `MainMenu.ChooseTouch` and
  `MainMenu.ChooseDesktop` — both are handler names and therefore API, like `CreateRoom` or
  `StartGame` below. A browser probe (`PlatformDetect.LooksLikeTouchDevice`) may only tag one
  button `Disarankan`; the tap is what actually sets the scheme on `SessionData`, which is
  memory-only and never written to PlayerPrefs, because the kiosk's next visitor may be
  holding a different kind of device. See [input-and-platform.md](input-and-platform.md).
  **Asked once per page load, not once per visit to the scene:** when `SessionData.Scheme`
  is already set (the visitor came back from a game), `MainMenu.Awake` skips the picker and
  shows the menu with the name field pre-filled. Before 2026-09-10 both were re-asked, which
  visitors reported as "after playing I have to log in again".
- **Second screen: the name field.** Nickname entry and nothing else (`MainMenu.cs`). The name is sanitized by
  `PlayerNameRules` (trim, collapse inner whitespace, 2–16 chars) and stored on
  `SessionData`, which survives scene loads; every room later sends it as `displayName`.
  It is also written to PlayerPrefs, so the field comes back pre-filled after a tab
  refresh and the name panel in the lobby stays the fallback it was meant to be.
- **Avatar row — `PILIH KARAKTER`.** Four portraits under the name field (Jawa, Bali, Bugis,
  Minang — `Assets/Sprites/Char Avatars`), each wired to `MainMenu.SelectAvatar(index)`
  (handler name, API). The chosen one wears the gold corner frame; **Jawa is selected until the
  visitor picks.** The tap is stored on `SessionData.PlayerAvatar` at once (PlayerPrefs, like the
  name) and every room sends it as the `avatar` join option, so the visitor appears as that
  character to others in the museum and on their Egrang lane.
- **Enter Museum** → load Museum scene (presence room only, no game seat). The button stays **disabled until the
  typed name is valid**, so nobody can reach a room unnamed.
- **Deep-link — not yet implemented.** The intent stands: on WebGL boot, read `?room=CODE`
  from the page URL (via `Application.absoluteURL` / jslib) and skip straight to auto-join
  in the Lobby. It waits on the real transport (see the Lobby note below).

### Museum hub
- Shared walkable scene. Other visitors appear as one of four costumed characters (Jawa,
  Bali, Bugis, Minang — the same one on every screen) idling, walking or running through
  the halls with their nickname overhead (`MuseumPresence` → server room `museum`); with no server it is walked alone,
  with no error shown. See [networking.md](networking.md#2-client-construction).
- **Going into a game does not leave the museum.** While a visitor is in the Lobby, Dakon or
  Egrang, the others see them standing idle at the doorway they used, tagged "Sedang bermain
  Dakon" / "Sedang bermain Egrang" under the name. Coming back — the Lobby's back button, a
  game's Kembali ke Museum, the results screen — they reappear **exactly where they went
  through the doorway**, facing the same way, not at the entrance (so the doorway's prompt and
  video come up again). Entering from MainMenu always starts at the entrance.
- **The gallery's gong, gasing, tembang and engklek court are shared**: another visitor's
  strike, spin, song or accepted step plays on your screen too, audible within 25 m. Your
  engklek run, the tembang's lyric banner, the videos and the lesson plaques stay yours.
- Each game has a doorway (`SceneTriggerPrompt`): walk in, press Enter. With `useLobby` on
  the doorway does **not** load the game — it fills in `LobbyRequest.Pending`
  (`roomName`, `maxPlayers`, its `sceneName` as the destination, and a `displayName` for the
  lobby title) and loads the Lobby.
- Dakon doorway: `dakon` / 2 seats / "Dakon" → `Dakon`. Egrang doorway: `egrang` / 3 seats /
  "Egrang" → `Egrang`. Each doorway is its exhibit's own video trigger volume on LT1
  (`Vid Dakon/Dakon Doorway`, `Vid Egrang/Cube`); the old placeholder `Egrang Doorway` beside
  Dakon is gone.
- **Lesson plaques are ambient, not a screen in this flow.** A `LessonPanel` is always
  rendered on its info-panel mesh; walking into its reading volume only makes `←`/`→` (or
  `Q`/`E`) page it. Nothing opens, nothing closes, no scene loads, and the player never
  leaves FPS control. See [lessons.md](lessons.md).

### Lobby scene (`Assets/Scenes/Lobby.unity`)

One generic scene serves every game; `LobbyRequest` is the only thing that differs.
`LobbyController` shows exactly one of three panels at a time:

- **Name panel** — *fallback only*. Shown when `SessionData` has no valid name, i.e. when a
  visitor somehow reached the Lobby without passing MainMenu. Not the normal path.
- **Entry panel** — a title reading `"<game> — <n> Pemain"` filled from the request's
  `displayName` and `maxPlayers`, then **Create Room**, or a room-code field + **Join Room**. The field is
  pre-filled with `SessionData.LastRoomCode` so a failed join keeps the code on screen.
  Input runs through `RoomCode.Sanitize` (uppercase; alphabet excludes 0/1/I/O/L).
  **Kembali ke Museum** (`BackToMuseum`) returns to the museum the visitor walked in from.
  `BackToMainMenu` still exists — it is a frozen handler name — but no generated scene wires
  it any more: MainMenu would re-run the picker and name field, which reads as a logout.
- **Room panel** — the room code (big, with a **copy** button), one slot row per seat, a
  host-only **Start**, a hint line under the seats, and **Leave** (`LeaveRoom` drops the
  seat and returns to the entry panel). The slot list is built to the request's `maxPlayers`,
  so Dakon shows 2 rows and Egrang 3. **Start** is visible only to the host and enabled
  only while the room is Waiting with **2 or more** players seated
  (`LobbyRoomSnapshot.CanStart`). The hint (`LobbyController.StartHintFor`) reads
  `Minimal 2 pemain untuk mulai — tunggu N pemain lagi` until then, and the number is
  `LobbyRoomSnapshot.MinPlayersToStart`, not copy. When the phase flips to `InProgress` the
  controller loads the request's game scene.

Rendering is snapshot-driven: every service event hands the controller a whole
`LobbyRoomSnapshot` and the whole panel is re-rendered from it. Nothing mutates a slot.

**Online, as built.** The lobby runs on `ColyseusLobbyService` against the Colyseus server
(`ws://localhost:2567` in dev, from `ServerConfig`). Create makes a private room whose code is
the server's; Join enters that code; Start is host-only and the server enforces it, answering a
refusal with `not_host` / `not_enough_players` / `already_started`. `FakeLobbyService` is still
there as an Editor convenience for working on the screen with no server running — flip
`Use Fake Service` on the Lobby canvas — but nobody can play together on it. **It is
currently switched on in `Lobby.unity`; turn it off before any build.**

### In-Game HUD
- Per game (see [games/dakon.md](games/dakon.md)). Turn indicator, scores, action prompts.
- **Egrang:** stick selection panel first (cards give shape, size and area formula only —
  no difficulty labels and no bearing area, so the player does the sum), under a **15-second
  countdown** and the room's roster by name. The countdown is the server's (`countdown`
  message); at zero the highlighted stilt is taken for anyone who never picked, and only
  then does the run start — picking early does not start it, because the server rejects
  steps before its own start instant.
- Then the run HUD — timing bar at the bottom, race progress strip at the top
  (`MULAI ──▮────── FINIS` with metres remaining), and a named roster with per-lane progress
  in the top left. Bar, strip and roster all live under one run root that ships inactive;
  `EgrangRace` switches it on when the countdown ends, so none of the three is on screen while
  stilts are still being picked (the countdown panel carries its own list of who is racing).
- **Pause (both games).** A gear in the top-right corner opens the same `Jeda` dialog in
  Dakon and Egrang: **Lanjutkan** (or a click on the scrim) closes it, **Kembali ke Museum**
  leaves. Neither game freezes — both are server-authoritative — so it is a menu over a live
  match. Egrang's also blocks the timing bar while it is up, because Space reaches the bar
  past the scrim. It is available from the stilt-picking window onward; once the results are
  up the gear sits under them and the results' own exit is the way out.

### Results (Finished state)
- From the `game_over` message. Winner / scores / tie, and **Return to Museum** (the hub
  the game was entered from). Play Again is not built for either game yet.
- **Egrang: built.** `EgrangResultsView` on the `Results Panel` under the `Egrang UI`
  canvas, generated by `Museum/Egrang/Build Results UI` and raised by `EgrangRace` on
  `RaceOver`. It shows the place ("Juara 2 dari 3"), the run time, the step tally
  (full / half / stumbles), the stick walked on, and one row per racer **by name** with the
  player's own row marked "(Kamu)". Offline — no session, so no `game_over` — the local
  finish line raises it instead, place 1 of 1.
  Its bottom button is **LANJUT**, not the exit: it raises the `After Game Panel`, the
  `egrang_after_game.jpeg` recap (the three stilts, cross-section comparison, how to win)
  over the results, and **Kembali ke Museum** sits inside that art, in its empty bottom
  band. That is the finished race's only way out; the recap has no click-outside close.
- **Dakon: built.** The `Game Over` panel in `Dakon.unity`, filled in by
  `DakonView.ShowGameOver` and given its layout by `DakonUIBuilder`. It names the winner and
  both scores with the players' registered names ("… menang!" / "Seri!"), and carries its own
  **Kembali ke Museum** button. The results panel is raised **before** the seed sweep animation
  runs, so a hiccup in the sweep never hides the scores.

## Failure / edge UX (handle explicitly — see [networking.md](networking.md) §3)

In the lobby every failure arrives as an `ILobbyService.Failed(code, message)` event, never
as a thrown exception, so there is one path for "the far side said no". Wording lives in
`LobbyError.MessageFor`. A failure while already seated leaves the player in the room and
the toast is the only feedback; only a failed create/join falls back to a panel — the name
panel for `name_invalid` (the entry panel has no name field, so it would be a dead end),
the entry panel otherwise. A create/join button is also inert while its own call is in
flight, so a double-click cannot open two rooms.

| Case | Trigger | UI |
|---|---|---|
| Room not found / expired | `JoinRoom` → `room_not_found` | Toast "Room not found", back to the lobby entry panel, keep the code in the field |
| Room full | `JoinRoom` → `room_full` | Toast "Room is full" |
| Name invalid | lobby name panel confirm, or a create/join with no name set → `name_invalid` | Toast "Enter a name (2–16 characters)", show the name panel |
| Server unreachable | `connection_failed` (today: every `ColyseusLobbyService` call) | Toast; back to the entry panel |
| Start refused | `not_host` / `not_enough_players` | Toast; stay in the room |
| Invalid move | `error` msg (per-game code) | Non-blocking toast; keep turn state |
| Disconnected | socket drop | "Reconnecting…" overlay, attempt `Reconnect` within window, then fall back to MainMenu — **not yet wired** |

Loading/transition fades between scenes use `LoopFadeTween` (CanvasGroup alpha).
