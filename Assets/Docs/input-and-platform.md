# Input and Platform

How a visitor's device is decided, where that choice lives, and the touch overlay it turns
on. Read this before touching `MainMenu`'s picker, the touch overlay generators, `FPSController`'s
input surface, or the WebGL template.

## The two schemes

`Museum.Core.ControlScheme` (`Assets/Scripts/Core/ControlScheme.cs`) is a three-value enum:

```csharp
public enum ControlScheme { Unknown = 0, Sentuh = 1, Desktop = 2 }
```

`Sentuh` is Bahasa for touch — floating joystick, drag-to-look, tap buttons. `Desktop` is
keyboard and mouse with pointer lock. `Unknown` is "nobody has chosen yet", and everything
that reads the scheme treats it as `Desktop`. That default is deliberate: a scene opened
straight from the Editor, or any code path that runs before `MainMenu` does, still gets
mouse-and-keyboard behaviour rather than a joystick nobody asked for.

## The visitor chooses; the browser only suggests

MainMenu's first screen is a picker, not the name field: heading `Kamu main pakai apa?`, two
buttons, `HP / Layar Sentuh` and `Komputer` (`MainMenu.cs`). The choice calls one of two
UnityEvent handlers:

- `MainMenu.ChooseTouch()` → `Scheme = ControlScheme.Sentuh`
- `MainMenu.ChooseDesktop()` → `Scheme = ControlScheme.Desktop`

Both are UnityEvent handler names and therefore API in the same sense as `CreateRoom` or
`StartGame` — see CLAUDE.md's list. Rename either and the picker's buttons silently stop
doing anything.

`Museum.Core.PlatformDetect.LooksLikeTouchDevice()` asks the browser (via
`Assets/Plugins/WebGL/MuseumPlatform.jslib`'s `MuseumIsTouchDevice`, which checks a coarse
pointer plus a real touch point, or a mobile user-agent as a fallback for pointer-media
queries that lie) whether the device looks like touch. `MainMenu` calls it once, when the
picker is shown, and the answer decides exactly one thing: which of the two buttons gets a
`Disarankan` ("recommended") tag next to it. It never sets `Scheme` itself. That boundary is
intentional — a phone that reports as desktop, or a touchscreen laptop the probe guesses
wrong about, would otherwise hand someone a control scheme they cannot use, and the museum
has no attendant standing by to fix it. The probe recommends; the tap decides.

Outside a WebGL player (Editor, tests) the jslib isn't linked, so `LooksLikeTouchDevice()`
falls back to `Application.isMobilePlatform || Touchscreen.current != null` — that fallback
is what the EditMode tests exercise, not the browser check.

The same tap also calls `PlatformDetect.RequestFullscreen()` (`MuseumRequestFullscreen` in
the jslib), because it is the first user gesture the page gets and the Fullscreen API only
works inside one. It fullscreens **`document.documentElement`**, never the canvas; a denial or
an unsupported browser (iPhone Safari has no Fullscreen API at all) is swallowed silently
rather than surfaced.

The element it picks is not cosmetic — see "Typing on a phone" below.

## Typing on a phone

Unity's WebGL soft keyboard is not a Unity thing at all: `TouchScreenKeyboard.Open`, which
`TMP_InputField` calls whenever `TouchScreenKeyboard.isSupported`, ends in
`JS_MobileKeyboard_Show` (the editor's `PlaybackEngines/WebGLSupport/BuildTools/lib/MobileKeyboard.js`).
That function appends a `<div>` holding a real `<input>` and an OK button to `document.body`,
pins it to the bottom of the page, and focuses it. The browser opens its keyboard because a
DOM input took focus. `isSupported` is `Module.SystemInfo.mobile`, which the loader sets from
`/Mobile|Android|iP(ad|hone)/.test(navigator.appVersion)` — true on Android Chrome and iPhone
Safari, false on iPadOS, which reports itself as a Mac.

Two rules follow from that div living in `document.body`, and breaking either one takes out
**every** text field in the build — MainMenu's name, the lobby's name and room code:

- **Fullscreen the document element, not the canvas.** A fullscreen element is the only thing
  the browser paints, and a `<canvas>` cannot hold DOM children, so `unityInstance.SetFullscreen(1)`
  leaves Unity's own keyboard bar outside the fullscreen subtree: invisible, unfocusable, no
  keyboard, no caret, nothing. Fullscreening `documentElement` keeps `body` — and therefore
  the bar — inside. This matters here specifically because the picker tap that enters
  fullscreen is the same tap that reveals the name field.
- **Nothing in the template may outrank the bar's stacking order.** It carries no `z-index` of
  its own, so any positioned element with one paints over it. `#rotate-overlay` is the reason
  the template has no `z-index` anywhere: document order alone already puts it over the canvas.

Neither rule is reachable from an EditMode test — both live in the browser, not in C#. They
are checked by opening the built page on a phone and tapping a text field: Unity's white input
bar has to appear at the bottom of the screen.

Behind the picker, MainMenu's usual screen — name entry and Play — is a list of objects
(`Title`, `Name Input`, `Play Button`) that `MainMenu` activates once a scheme is chosen, not
a single parent panel. That is because `MainMenuUIBuilder` **adopts** the hierarchy that
survived the 2026-08-19 recovery rather than emitting a rival one next to it; re-parenting
those objects under one new panel would undo that adoption. `BG` is not in the list — the
background stays visible under the picker.

## Where the choice lives

`SessionData.Scheme` (`ControlScheme`, default `Unknown`) and the derived
`SessionData.IsTouch` (`Scheme == ControlScheme.Sentuh`) are the single source of truth for
every touch-vs-desktop decision in the client. It lives on `SessionData`, the same
`DontDestroyOnLoad` object that carries the visitor's name, so it survives every scene load
between MainMenu and Results.

It is deliberately **not** written to PlayerPrefs, unlike the name field. This is a museum
kiosk: the next visitor to stand in front of a paused build may be holding a phone after the
last one used the keyboard, and a remembered scheme from browser storage would hand them the
wrong overlay before they ever see the picker. Every page load starts at `Unknown` and asks
again — but only the page load. Within one session the answer lives on the bootstrap object,
and `MainMenu.Awake` skips the picker whenever `Scheme` is already set, so a visitor coming
back from a game is not asked twice.

## The touch overlay

Every touch-only control lives under a GameObject carrying `Museum.Core.TouchOnly`, which
deactivates itself in `Awake` unless `SessionData.Instance.IsTouch` is true.
`Museum.Core.DesktopOnly` is its mirror, for HUD that only means something to a keyboard —
see "The KONTROL card" below. Nothing in a
scene needs to know what a `TouchOnly` object contains or query the scheme itself — the
component does it once, per overlay root, and the desktop scheme (and `Unknown`, since it
reads as desktop) simply never sees any of it.

The overlay's parts:

- **Floating joystick** (`TouchJoystick`) and **look area** (`TouchLookArea`) — movement and
  camera drag, feeding `Museum.Player.TouchInputSource`, which is the touch-scheme
  counterpart to the keyboard/mouse `FPSInputReader`. The stick **rests visible** at the
  bottom-left of its area, dimmed to `restAlpha` (0.4) through a `CanvasGroup` on the ring;
  it still floats to wherever the finger lands and returns home on release. It used to be
  hidden until touched, which made it a control a first-time visitor never learned was
  there — the museum has no attendant to say "drag the left side of the screen", so the
  control has to say it by being on screen.
- **Lompat** — the jump button.
- **Interaksi** — routed by `TouchInteractRouter.Interact()` to whatever the player is
  currently standing in front of (see below). **Shown only while a doorway or a gallery
  station is registered** (`CurrentPrompt` or `CurrentInteractable`).
- **‹ ›** — page-turn buttons for lesson plaques, routed by `TouchInteractRouter.PageNext()`
  / `PagePrev()`. **Shown only while a plaque is registered.**
- **Egrang's JALAN button** — `Egrang UI/Run Root/Skill Check Bar/Step Button`, calling
  `SkillCheckBar.Press()`, the same method the desktop build's Space-bound `Step` action
  calls. It is the only touch step: it lives under the run root, so it exists only during the
  race, on the same Screen Space – Camera canvas as every other Egrang button.

  There used to be a full-screen tap zone on its own **Screen Space – Overlay** canvas at
  `sortingOrder = -10`. The -10 never put it behind anything: uGUI ranks an Overlay canvas's
  raycaster by its `sortingOrder` and a Camera canvas's by `int.MinValue`, so the invisible
  zone won every tap on a phone — the stick cards, Lanjutkan and Kembali ke Museum all
  stepped the racer instead (2026-09-11). It and its generator were removed. **Never mix an
  Overlay canvas into a scene whose buttons live on a Camera canvas.**

`VirtualStickModel` and `LookDragModel` are the pure, EditMode-tested math behind the
joystick and the drag-to-look area — no `UnityEngine` dependency, same discipline as the game
rules. `ControlSchemeSwitch` is what a per-scene root uses to pick between the touch and
desktop input path at runtime.

### `TouchInteractRouter` — self-registration, not per-doorway wiring

`TouchInteractRouter` (`Assets/Scripts/Core/TouchInteractRouter.cs`) exposes three static
slots, `CurrentPrompt` (a `SceneTriggerPrompt`), `CurrentReader` (a `LessonReader`) and
`CurrentInteractable` (an `IInteractable` — the ground-floor gallery's gong, gasing and song
stations, see [museum-decor.md](museum-decor.md)). Doorways, lesson plaques and stations call
`Register`/`Unregister` on themselves as the player enters and leaves their trigger volume;
the Interaksi and ‹ › buttons just act on whichever is currently registered. `Interact()`
prefers a doorway over a station, so a station volume that overlaps a doorway can never eat
the doorway's press. On desktop the stations read the same **Enter** key as the doorways. Nobody wires an Interaksi button to a specific doorway, and no doorway
needs to know the overlay exists. That matters here specifically because a museum doorway
going unwired by hand is a failure this project has already lived through once (see
`scene-setup.md`'s Museum doorway table) — self-registration means adding a fifth doorway
later needs no new wiring step at all.

### What the doorway prompt says

The HUD carries **one** prompt panel, named `Enter`, that every doorway raises and lowers —
not one per door. It is two parts: a keycap chip and a scheme-neutral line reading
`Untuk memulai permainan ini`. `SceneTriggerPrompt.Awake` sets the chip, and only the chip:
**`ENTER` on desktop, `INTERAKSI` on touch** — the same word the overlay button carries, so
the popup names something the visitor can actually reach.

Until 2026-09-10 it said `ENTER` to everyone, because `promptLabel` was `None` on all four
doorways and the runtime line therefore never ran. That is the shape of failure to expect
here: the panel still opens and still reads plausibly, so nothing looks broken on desktop,
where the text happens to be right. `SceneWiringRepair.WireDoorways` now wires `promptLabel`
alongside `promptUI` so a repair run cannot leave it null again.

### The KONTROL card

The Museum HUD's `Tutorial` panel is a keyboard legend — WASD, the mouse, Q and E. None of
those exist under the touch scheme, so the panel carries `Museum.Core.DesktopOnly` and is
hidden there; the joystick, look area and Interaksi button are already on screen saying the
same thing by being visible. `MuseumUIBuilder.StyleControls` attaches the component, so a
rebuild keeps it.

### Controls that appear only when they do something

`Museum.Core.ShownWhenAvailable` gates the Interaksi and ‹ › buttons on the router actually
holding something for them to act on — a doorway for Interaksi, a plaque for the arrows.
A button that does nothing when tapped is worse than an absent one: the visitor who taps it
and gets no response has been told the control is broken.

It hides through a `CanvasGroup` (alpha 0, non-interactive, raycast-transparent) rather than
`SetActive(false)`, and the difference is load-bearing — a deactivated GameObject stops
receiving `Update`, so it could never notice the doorway it is waiting for and would never
come back. Going raycast-transparent also stops a hidden button eating taps meant for the
look area behind it. **Lompat is deliberately not gated:** jumping is always available, so its
button is always meaningful.

## `FPSController`'s input surface

`FPSController` (moved, with the rest of the player scripts, into a new `Museum.Player`
assembly so it could be unit-tested at all) exposes:

- `SetMoveInput(Vector2)`
- `AddLookDelta(Vector2)`
- `SetSprint(bool)`
- `PressJump()`
- `UsesCursorLock` (a bool property)

Both `FPSInputReader` (keyboard/mouse) and `TouchInputSource` (touch) drive the controller
through this surface rather than either one reaching past it into private state.
`AddLookDelta` is additive — `HandleMouseLook` reads and then clears the accumulator every
frame, so a touch drag and a mouse look could in principle both contribute to the same frame
without one clobbering the other.

Pointer lock is skipped entirely under the touch scheme (`UsesCursorLock` is false whenever
`SessionData.IsTouch`), because mobile browsers do not implement the Pointer Lock API at
all — trying to request it there is not a graceful no-op, it is a control scheme that never
engages.

## The two rebuild menu items

- **`Museum/Rebuild UI/Touch Controls`** (`TouchControlsUIBuilder`) — the joystick, look area,
  Lompat, Interaksi and ‹ › overlay used in the Museum scene (and anywhere else the player
  walks around in first person).
- **`Museum/Rebuild UI/Main Menu`** (`MainMenuUIBuilder`) — the platform picker and the name
  screen behind it.

All three refuse to run when the active scene has unsaved changes (`EditorSceneManager`'s
scene `isDirty`), the same guard `TouchControlsUIBuilder` uses: these are total generators
in the same sense as `LobbyUIBuilder` and `DakonUIBuilder` — they rebuild their piece of the
scene from scratch, so running one over unsaved hand-edits would silently discard them.

## The Wiraga WebGL template

`Assets/WebGLTemplates/Wiraga/` is a custom WebGL template (`index.html` + `style.css`),
copied into the build verbatim by Unity's own build pipeline — the `{{{ PRODUCT_NAME }}}`
style tokens in it are Unity's substitution syntax, not placeholders left for hand-editing.
It carries the rules a phone browser needs that the stock template doesn't:

- A viewport meta tag pinning `width=device-width, initial-scale=1, maximum-scale=1,
  user-scalable=no, viewport-fit=cover` so the page can't be pinch-zoomed or scrolled out
  from under the canvas.
- `touch-action: none` in `style.css`, so a drag on the look area or the joystick doesn't
  also scroll or refresh the page.
- A `devicePixelRatio` cap of 2 passed into the Unity loader config, so a high-density phone
  screen doesn't render at a resolution the WebGL build can't sustain.
- A `Putar HP kamu ke samping` ("turn your phone sideways") overlay for portrait orientation.
- `window.unityInstance`, set once the loader resolves — `MuseumRequestFullscreen` in the
  jslib depends on this global existing, since `SetFullscreen` is a method on the instance,
  not a free function.

`BuildWebGL.cs` sets `PlayerSettings.WebGL.template = "PROJECT:Wiraga"` itself at build time,
in code, rather than relying on whatever `ProjectSettings` has saved — the same reasoning as
the endpoint and video-catalog checks documented in `build-and-deploy.md`: a build produced
by the menu item is the one whose settings are actually verified.

## Known gap: phone performance is untuned

None of this work touched rendering cost. The Museum scene still carries 71 realtime lights
and 25 reflection probes (`lighting.md`), and `webGLMaximumMemorySize` is still 2048 — none
of it sized or profiled against an actual phone GPU or its memory ceiling. This was left out
on purpose rather than guessed at: a performance budget for a phone needs real device
measurements (frame time, memory headroom, thermal throttling under the museum's expected
session length), and none of that exists yet. Shipping a plausible-looking cap without
measuring it would just replace an honest "untested" with a false "tuned". Treat a phone
running warm or dropping frames in the Museum scene as expected until this gap is closed
with its own pass, not as a regression in this work.

See also: [ui-flow.md](ui-flow.md) for where the picker sits in the screen flow,
[scene-setup.md](scene-setup.md) for the generated overlay objects in each scene, and
[build-and-deploy.md](build-and-deploy.md) for the Wiraga template at build time.
