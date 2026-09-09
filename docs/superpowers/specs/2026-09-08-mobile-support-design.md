# Mobile browser support and control-scheme selection

**Date:** 2026-09-08
**Status:** Approved design, not yet implemented
**Scope:** Unity WebGL client only. No server change, no protocol change.

## Problem

The build runs in a mobile browser today only in the sense that it loads. It is not
playable there:

- `FPSController.Awake` sets `Cursor.lockState = CursorLockMode.Locked` and
  `HandleCursorLock` re-locks on click. Mobile browsers have no Pointer Lock API, so
  the museum has no look control, and `Move` is bound to WASD/arrows only — the museum
  is unnavigable on a phone.
- `SceneTriggerPrompt.Update` requires `Keyboard.current.enterKey`; `LessonReader.Update`
  requires Q/E. On a phone there is no way to enter a doorway or page a lesson plaque.
- `SkillCheckBar` binds its `Step` action to `<Keyboard>/space` and `<Gamepad>/buttonSouth`.
  Egrang cannot be stepped by touch.
- `webGLTemplate: APPLICATION:Default` with no `Assets/WebGLTemplates/` directory. The
  stock page lets the page scroll, pinch-zoom and double-tap-zoom, which steals the very
  gestures the game needs, and renders at the device's full pixel ratio.

What already works and must not be broken: every screen is uGUI, and all five build scenes
carry an `InputSystemUIInputModule` on their EventSystem, so taps already reach every Button.
Dakon in particular is played through `DakonCard`'s uGUI `Button` (`DakonCard.cs:58-62`) — not
by raycasting the 3D board, and `HoverClickRaycaster`/`Hoverable` are referenced by no scene in
the project at all. `TMP_InputField` opens the mobile keyboard on MainMenu and in the Lobby.

## Decisions taken

| Question | Decision |
| --- | --- |
| Support depth | Full play on a phone, museum included. |
| How platform is decided | The visitor chooses, on a dedicated first screen, in Bahasa. |
| Role of auto-detection | Advisory only — it marks one button "Disarankan". It never decides. |
| Museum controls | Left thumb joystick to move, drag anywhere on the right to look, tap buttons for Lompat and Interaksi. |
| Orientation | Landscape asked for via an overlay; fullscreen requested on the picker tap. |
| Phone performance tuning | Explicitly out of scope. See "Out of scope". |

## Architecture

### 1. The control-scheme seam

```
PlatformDetect (advisory)  ─┐
                            ├─→  Platform picker (MainMenu)  ─→  SessionData.Scheme  ─→  every scene
Visitor's tap (authoritative)┘
```

**`Museum.Core.ControlScheme`** — `enum { Unknown, Sentuh, Desktop }`, its own file in
`Assets/Scripts/Core/`.

**`SessionData.Scheme`** — the single source of truth, with `IsTouch => Scheme ==
ControlScheme.Sentuh`. `SessionData` is already the `DontDestroyOnLoad` bootstrap object,
so the choice survives every scene load without a new singleton. It is deliberately **not**
persisted to `PlayerPrefs`: this is a museum kiosk, and the next visitor may be on a
different device than the last.

**`Museum.Core.PlatformDetect`** — a static class with one member,
`bool LooksLikeTouchDevice()`.

- On `UNITY_WEBGL && !UNITY_EDITOR` it calls `MuseumIsTouchDevice()` from
  `Assets/Plugins/WebGL/PlatformDetect.jslib`, which returns 1 when the browser reports
  a coarse pointer (`matchMedia('(pointer:coarse)').matches`) **and**
  `navigator.maxTouchPoints > 0`, or when the user agent matches the mobile pattern.
  Requiring a coarse pointer keeps a touchscreen laptop on the desktop side.
- Everywhere else (Editor, tests) it falls back to
  `Application.isMobilePlatform || Touchscreen.current != null`.

The result is used for exactly one thing: putting a "Disarankan" marker on one of the two
picker buttons. A wrong guess costs the visitor nothing.

### 2. The picker screen

A new full-screen panel in the **existing MainMenu scene**, shown before the title/name
panel, rather than a new scene. A new scene would mean a new build-settings entry, a new
`SceneReference`, and another scene that can lose its inspector wiring; the panel gets the
same "separate first screen" reading for none of that cost.

Content, in Bahasa, styled through `MuseumUIStyle` like the rest of the menu:

- Heading: `Kamu main pakai apa?`
- Button `HP / Layar Sentuh` → `ControlScheme.Sentuh`
- Button `Komputer` → `ControlScheme.Desktop`
- The detected one carries a small `Disarankan` tag.

`MainMenu` gains two UnityEvent handlers, `ChooseTouch` and `ChooseDesktop`. **These names
are API** — the MainMenu generator wires them by name, exactly like `ConfirmName` and
`StartGame`. Each writes `SessionData.Scheme`, calls `MuseumRequestFullscreen()`, hides the
picker panel and reveals the name panel. Fullscreen must be requested from inside a user
gesture, and this tap is the first one the page ever gets.

`MainMenuUIBuilder` builds and styles the panel and wires both handlers, so the screen is
regenerable after an inspector wipe.

### 3. Museum touch input

`FPSController` grows a script-facing surface beside its Input System callbacks:

```csharp
public void SetMoveInput(Vector2 move);
public void AddLookDelta(Vector2 delta);
public void SetSprint(bool held);
public void PressJump();
```

The four existing `OnMove` / `OnLook` / `OnSprint` / `OnJump` callbacks become one-line
delegates to these, so `FPSInputReader` and `FPSInput.inputactions` are untouched.
`AddLookDelta` accumulates into the same `lookInput` field the mouse writes, in the same
units (`<Mouse>/delta`, applied per frame without `Time.deltaTime`), so both schemes share
one sensitivity value and one pitch clamp.

Cursor handling becomes conditional: `Cursor.lockState = Locked` in `Awake` and the
re-lock in `HandleCursorLock` run only when `SessionData.Instance.Scheme ==
ControlScheme.Desktop`. This is the single change that makes the museum usable on a phone
at all.

**`TouchInputSource`** (`Assets/Scripts/Player/`) reads the overlay widgets each frame and
pushes into those setters. At `Awake`, a small `ControlSchemeSwitch` component sets
`FPSInputReader.enabled = !IsTouch` and `TouchInputSource.enabled = IsTouch`, so exactly
one source is live.

**Overlay** — `Assets/Prefabs/Touch Controls.prefab`, a screen-space canvas whose root
disables itself when the scheme is Desktop:

- `TouchJoystick` — left third. Origin floats to wherever the finger lands, dead zone,
  radius clamp, output normalized to the same `Vector2` range the WASD composite produces.
- `TouchLookArea` — right two-thirds, an `IDragHandler` that reports per-frame drag delta.
- `Lompat` button → `PressJump()`.
- `Interaksi` button → the interaction router (§4).

The math in both widgets lives in pure structs — `VirtualStickModel` and `LookDragModel` —
that take positions and return vectors, with no `MonoBehaviour` and no scene. That is what
makes the touch scheme testable in EditMode rather than by hand on a phone. These are
input shaping, not game rules, so `UnityEngine.Vector2` is fine in them; the rules ban in
CLAUDE.md is about Dakon/Egrang rules, which none of this touches.

An editor generator, `Museum/UI/Build Touch Controls`, builds the prefab in the style of
the existing builders.

### 4. Parity for the keyboard-only interactions

- **`SceneTriggerPrompt`**: `Enter()` changes from private to public. A
  `TouchInteractRouter` on the overlay tracks the prompt whose trigger the player is
  currently inside and forwards the Interaksi tap to it. The prompt label reads
  `Tekan Enter` under Desktop and `Ketuk Interaksi` under Sentuh.
- **`LessonReader`**: gains public `Prev()` / `Next()` that call the same `LessonPanel`
  methods the Q/E poll calls. The overlay shows ‹ › buttons while a reader is active. The
  keyboard poll is unchanged — note that the class comment currently justifies keyboard
  input by the locked cursor, and that comment must be updated in the same change.
- **Egrang**: `SkillCheckBar.Press()` is already public. The Egrang overlay adds a
  transparent full-width tap zone that calls it. `EgrangInput.inputactions` is not touched,
  so the Space binding is untouched for desktop players. Stick selection is already uGUI.
- **Dakon**: no code change. Its moves come from uGUI Buttons, which the Input System UI module
  already delivers taps to. What the plan adds instead is a regression guard: a test asserting
  every build scene's EventSystem still carries `InputSystemUIInputModule`, since a
  `StandaloneInputModule` would kill touch in that scene silently.

### 5. WebGL template

`Assets/WebGLTemplates/Wiraga/` — `index.html`, `style.css`, `thumbnail.png`.

- `<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover">`
- `html, body { overflow: hidden; touch-action: none; overscroll-behavior: none; }` — this
  is what stops the page from scrolling and pinch-zooming under the player's thumbs.
- Canvas resized to `window.innerWidth/innerHeight` on `resize` and `orientationchange`.
- `config.devicePixelRatio = Math.min(window.devicePixelRatio, 2)` — a 3x phone screen
  otherwise renders roughly twice the pixels the GPU can afford.
- A portrait overlay, `Putar HP kamu ke samping`, driven by
  `@media (orientation: portrait)` in CSS. Being pure DOM, it shows before the wasm loads.
- `MuseumRequestFullscreen()` in the jslib calls `unityInstance.SetFullscreen(1)`, falling
  back to `document.documentElement.requestFullscreen()`, both inside `try/catch`. On
  iPhone Safari there is no Fullscreen API; the call is a silent no-op and the rotate
  overlay plus the viewport rules carry the experience alone.

`BuildWebGL.cs` sets `PlayerSettings.WebGL.template = "PROJECT:Wiraga"` at build time, so
the template survives a lost project setting the way `ServerConfig` survives a lost
inspector value.

## Testing

**EditMode**

- `VirtualStickModel`: dead zone rejects a sub-threshold drag; output clamps to magnitude 1
  at and beyond the radius; direction is preserved through the clamp.
- `LookDragModel`: delta scales by sensitivity; pitch accumulates and clamps at
  `minYRotation` / `maxYRotation`; a frame with no drag produces zero, not the last value.
- `PlatformDetect`: the non-WebGL fallback path returns a value and does not throw when no
  `Touchscreen` is present.

**PlayMode**

- `Scheme == Sentuh`: `FPSInputReader` disabled, `TouchInputSource` enabled,
  `Cursor.lockState != Locked`, overlay root active.
- `Scheme == Desktop`: the inverse, and `Cursor.lockState == Locked`.
- `SetMoveInput` and `AddLookDelta` move and rotate the controller the same way the
  equivalent Input System callbacks do.
- `SceneTriggerPrompt.Enter()` called while inside the trigger loads the same destination
  the Enter key loads.

**By hand, on a real phone** — the parts no test can cover: the rotate overlay, the
fullscreen request, that the page does not scroll under a drag, and one full pass through
MainMenu → museum → doorway → lobby → Dakon and → Egrang.

## Documentation

- New `Assets/Docs/input-and-platform.md`: the two schemes, where the choice is made and
  stored, what `PlatformDetect` is and is not allowed to decide, the overlay's widgets, and
  the template's mobile rules.
- `Assets/Docs/ui-flow.md`: the picker as the first screen; `ChooseTouch` / `ChooseDesktop`
  added to the list of UnityEvent handler names that are API.
- `Assets/Docs/scene-setup.md`: the Touch Controls prefab and its per-scene wiring.
- `Assets/Docs/build-and-deploy.md`: the `Wiraga` template and that the build script sets it.
- `Assets/Docs/README.md`: index entry for the new doc.
- `LessonReader`'s class comment: its "keyboard because the cursor is locked" rationale is
  now conditional on the Desktop scheme.

## Out of scope

- **Phone performance.** The museum runs 71 realtime lights and 25 reflection probes on a
  Forward+ Linear setup, and `webGLMaximumMemorySize` is 2048 MB. Both are likely to hurt
  on a mid-range phone and iOS Safari respectively. Tuning them is a separate piece of work
  with its own measurements; this design does not touch quality settings, lighting or
  memory.
- Server and protocol: unchanged.
- Localisation beyond the Bahasa strings named here.
- Persisting the scheme between visits.
