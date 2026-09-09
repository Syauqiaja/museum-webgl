# Mobile Browser Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Unity WebGL build fully playable in a mobile browser, with the visitor choosing their control scheme on a first screen in Bahasa.

**Architecture:** A `ControlScheme` value on the existing `SessionData` bootstrap singleton is the single source of truth; a browser probe only *recommends* one of the two picker buttons. The museum's `FPSController` gains plain script setters that a new touch input source pushes into, so the existing Input System path is untouched, and pointer lock is skipped entirely under the touch scheme. A project WebGL template stops the page scrolling and zooming under the player's thumbs.

**Tech Stack:** Unity 6000.3.19f1, URP, WebGL, Unity Input System, uGUI + TextMeshPro, NUnit via Unity Test Runner, an Emscripten `.jslib` plugin.

**Spec:** `docs/superpowers/specs/2026-09-08-mobile-support-design.md`

## Global Constraints

- **UI copy is Bahasa Indonesia.** Exact strings, verbatim: `Kamu main pakai apa?`, `HP / Layar Sentuh`, `Komputer`, `Disarankan`, `Putar HP kamu ke samping`, `Ketuk Interaksi`, `Lompat`, `Interaksi`.
- **Enum members are Bahasa where the spec says so:** `ControlScheme { Unknown, Sentuh, Desktop }`. `Sentuh` means touch.
- **The browser probe never decides.** `PlatformDetect` output may only drive the `Disarankan` hint. The visitor's tap sets `SessionData.Scheme`.
- **The scheme is not persisted.** No `PlayerPrefs` key for it — a museum kiosk serves a new visitor each time.
- **UnityEvent handler names are API** (CLAUDE.md). This plan adds `ChooseTouch` and `ChooseDesktop`. Never rename them; generated scenes wire them by name.
- **Generated schema is off-limits:** `Assets/Scripts/Net/Schema/*.cs` is not touched by any task here.
- **No new third-party package, no new networking dependency** (`Assets/Docs/boundaries.md`).
- **Game rules stay out of `MonoBehaviour` and out of scenes.** Nothing in this plan is a game rule; input shaping is not a rule, and no pool size, grab size, finish distance or stilt value is read or written.
- **Server:** unchanged. No message type, no room name, no schema.
- **Out of scope, do not do it:** quality settings, lighting, `webGLMemorySize`/`webGLMaximumMemorySize`, or any other phone performance tuning.

### Running the tests

The Editor holds a project lock, so batch mode needs the Editor closed. Either run from the
Test Runner window (Window → General → Test Runner), or:

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "/Users/mac/Documents/Works/unity/Museum Minigames" \
  -runTests -testPlatform EditMode -logFile - -quit
```

Swap `EditMode` for `PlayMode` for the PlayMode suites. Add
`-testFilter "Museum.Core.Tests.VirtualStickModelTests"` to run one fixture. If the Editor is
open and MCP for Unity is connected, `run_tests` through MCP is the faster path and needs no
`-quit`.

---

### Task 1: The control scheme value

**Files:**
- Create: `Assets/Scripts/Core/ControlScheme.cs`
- Modify: `Assets/Scripts/Core/SessionData.cs`
- Test: `Assets/Scripts/Core/Tests/PlayMode/ControlSchemeTests.cs`

**Interfaces:**
- Consumes: `Museum.Core.SessionData` (existing `DontDestroyOnLoad` singleton, `SessionData.Instance`).
- Produces: `Museum.Core.ControlScheme` enum with members `Unknown`, `Sentuh`, `Desktop`; `SessionData.Scheme { get; set; }` of that type; `SessionData.IsTouch => Scheme == ControlScheme.Sentuh`.

- [ ] **Step 1: Write the failing test**

`Assets/Scripts/Core/Tests/PlayMode/ControlSchemeTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The scheme is per-visit memory on the bootstrap object. These tests build the object the
    /// way a scene does — AddComponent runs Awake — the same shape SessionDataTests uses.
    /// </summary>
    public class ControlSchemeTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("Bootstrap");

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private SessionData Bootstrap() => _go.AddComponent<SessionData>();

        [Test]
        public void Scheme_starts_unknown()
        {
            SessionData session = Bootstrap();
            Assert.AreEqual(ControlScheme.Unknown, session.Scheme);
        }

        [Test]
        public void IsTouch_is_false_until_the_visitor_picks_touch()
        {
            SessionData session = Bootstrap();
            Assert.IsFalse(session.IsTouch);

            session.Scheme = ControlScheme.Desktop;
            Assert.IsFalse(session.IsTouch);

            session.Scheme = ControlScheme.Sentuh;
            Assert.IsTrue(session.IsTouch);
        }

        [Test]
        public void Scheme_is_not_written_to_PlayerPrefs()
        {
            SessionData session = Bootstrap();
            session.Scheme = ControlScheme.Sentuh;

            // A kiosk serves a new visitor each time; a remembered scheme would be wrong for them.
            Assert.IsFalse(PlayerPrefs.HasKey("museum.session.controlScheme"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the PlayMode suite filtered to `Museum.Core.Tests.ControlSchemeTests`.
Expected: FAIL to compile — `ControlScheme` and `SessionData.Scheme` do not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Core/ControlScheme.cs`:

```csharp
namespace Museum.Core
{
    /// <summary>
    /// How the visitor is playing. Chosen by the visitor on MainMenu's first screen, never by
    /// the build: a browser probe can only recommend (see <see cref="PlatformDetect"/>), because
    /// a wrong guess would hand someone controls they cannot use.
    /// </summary>
    public enum ControlScheme
    {
        /// <summary>Nobody has chosen yet. Everything that reads this treats it as Desktop.</summary>
        Unknown = 0,

        /// <summary>Phone or tablet: on-screen joystick, drag to look, tap buttons.</summary>
        Sentuh = 1,

        /// <summary>Keyboard and mouse, with pointer lock.</summary>
        Desktop = 2,
    }
}
```

In `Assets/Scripts/Core/SessionData.cs`, add beside `PlayerId` (memory-only, so it goes in the
group the class comment describes as "memory only"):

```csharp
        /// <summary>
        /// The control scheme the visitor picked on MainMenu. Memory only, deliberately: this
        /// build runs as a museum kiosk, so the next visitor may not be on the device the last
        /// one was, and a remembered answer would be worse than asking.
        /// </summary>
        public ControlScheme Scheme { get; set; } = ControlScheme.Unknown;

        /// <summary>True only once the visitor has explicitly chosen touch.</summary>
        public bool IsTouch => Scheme == ControlScheme.Sentuh;
```

- [ ] **Step 4: Run tests to verify they pass**

Run the same filter. Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/ControlScheme.cs Assets/Scripts/Core/ControlScheme.cs.meta \
        Assets/Scripts/Core/SessionData.cs \
        Assets/Scripts/Core/Tests/PlayMode/ControlSchemeTests.cs \
        Assets/Scripts/Core/Tests/PlayMode/ControlSchemeTests.cs.meta
git commit -m "feat: control scheme on SessionData"
```

---

### Task 2: Browser probe and fullscreen request

**Files:**
- Create: `Assets/Plugins/WebGL/MuseumPlatform.jslib`
- Create: `Assets/Scripts/Core/PlatformDetect.cs`
- Test: `Assets/Scripts/Core/Tests/PlatformDetectTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `static bool Museum.Core.PlatformDetect.LooksLikeTouchDevice()` and
  `static void Museum.Core.PlatformDetect.RequestFullscreen()`.

**Note on the jslib:** the browser functions are `MuseumIsTouchDevice` and
`MuseumRequestFullscreen`. `MuseumRequestFullscreen` reads `window.unityInstance`, which
Task 13's template assigns; until then it falls through to the DOM API. Both are wrapped in
`try/catch` so a browser without the API is a silent no-op rather than an exception into wasm.

- [ ] **Step 1: Write the failing test**

`Assets/Scripts/Core/Tests/PlatformDetectTests.cs`:

```csharp
using NUnit.Framework;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// In the Editor the jslib does not exist, so these exercise the fallback path only. What the
    /// real browser reports is verified by hand on a phone — see the plan's manual pass.
    /// </summary>
    public class PlatformDetectTests
    {
        [Test]
        public void LooksLikeTouchDevice_answers_without_throwing_in_the_editor()
        {
            Assert.DoesNotThrow(() => PlatformDetect.LooksLikeTouchDevice());
        }

        [Test]
        public void LooksLikeTouchDevice_is_false_on_a_desktop_editor_with_no_touchscreen()
        {
            // The Editor on macOS has neither Application.isMobilePlatform nor a Touchscreen device.
            Assert.IsFalse(PlatformDetect.LooksLikeTouchDevice());
        }

        [Test]
        public void RequestFullscreen_is_a_no_op_off_the_web()
        {
            Assert.DoesNotThrow(PlatformDetect.RequestFullscreen);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run EditMode filtered to `Museum.Core.Tests.PlatformDetectTests`.
Expected: FAIL to compile — `PlatformDetect` does not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Plugins/WebGL/MuseumPlatform.jslib`:

```javascript
mergeInto(LibraryManager.library, {

  // 1 when the browser looks like a touch device. A coarse pointer AND a real touch point keeps
  // a touchscreen laptop out; the user-agent test catches phones whose pointer media query lies.
  MuseumIsTouchDevice: function () {
    try {
      var coarse = !!(window.matchMedia && window.matchMedia('(pointer: coarse)').matches);
      var points = (navigator.maxTouchPoints || 0) > 0;
      var mobileUA = /Android|iPhone|iPad|iPod|Mobile|Silk/i.test(navigator.userAgent || '');
      return ((coarse && points) || mobileUA) ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  // Must be called from inside a user gesture. iPhone Safari has no Fullscreen API at all, so a
  // silent no-op there is the correct outcome, not an error.
  MuseumRequestFullscreen: function () {
    try {
      if (window.unityInstance && window.unityInstance.SetFullscreen) {
        window.unityInstance.SetFullscreen(1);
        return;
      }
      var el = document.documentElement;
      var request = el.requestFullscreen || el.webkitRequestFullscreen;
      if (request) request.call(el);
    } catch (e) {
      // Denied or unsupported. The rotate overlay and the viewport rules carry it alone.
    }
  },

});
```

`Assets/Scripts/Core/PlatformDetect.cs`:

```csharp
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Museum.Core
{
    /// <summary>
    /// What the browser says about the device. Advisory only: the single caller is MainMenu's
    /// platform picker, and all it does with the answer is mark one button "Disarankan". The
    /// visitor's tap is what sets <see cref="SessionData.Scheme"/>.
    /// </summary>
    /// <remarks>
    /// Outside a WebGL player — the Editor, tests — the jslib is not linked, so the check falls
    /// back to Unity's own signals. That fallback is what the EditMode tests exercise.
    /// </remarks>
    public static class PlatformDetect
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int MuseumIsTouchDevice();
        [DllImport("__Internal")] private static extern void MuseumRequestFullscreen();
#endif

        /// <summary>True when the browser reports a touch device. Never authoritative.</summary>
        public static bool LooksLikeTouchDevice()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return MuseumIsTouchDevice() == 1;
#else
            return Application.isMobilePlatform || Touchscreen.current != null;
#endif
        }

        /// <summary>
        /// Asks the browser for fullscreen. Only meaningful inside a user gesture, which is why
        /// the picker's button tap is the one place that calls it. A no-op everywhere else.
        /// </summary>
        public static void RequestFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            MuseumRequestFullscreen();
#endif
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the same filter. Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Plugins Assets/Scripts/Core/PlatformDetect.cs* \
        Assets/Scripts/Core/Tests/PlatformDetectTests.cs*
git commit -m "feat: browser touch probe and fullscreen request"
```

---

### Task 3: Virtual stick geometry

**Files:**
- Create: `Assets/Scripts/Core/VirtualStickModel.cs`
- Test: `Assets/Scripts/Core/Tests/VirtualStickModelTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `readonly struct Museum.Core.VirtualStickModel`, constructed as
  `new VirtualStickModel(float radius, float deadZone)`, with
  `Vector2 Evaluate(Vector2 origin, Vector2 current)` returning a movement vector of magnitude
  0..1, and `Vector2 KnobOffset(Vector2 origin, Vector2 current)` returning the knob's pixel
  offset clamped to the radius.

- [ ] **Step 1: Write the failing test**

`Assets/Scripts/Core/Tests/VirtualStickModelTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The joystick's whole behaviour as arithmetic: no scene, no touch, no frame. This is the
    /// reason the touch scheme can be trusted without a phone in hand.
    /// </summary>
    public class VirtualStickModelTests
    {
        private const float Radius = 100f;
        private const float DeadZone = 10f;

        private static VirtualStickModel Stick() => new VirtualStickModel(Radius, DeadZone);

        [Test]
        public void A_drag_inside_the_dead_zone_produces_no_movement()
        {
            Vector2 move = Stick().Evaluate(Vector2.zero, new Vector2(5f, 0f));
            Assert.AreEqual(Vector2.zero, move);
        }

        [Test]
        public void A_drag_at_the_radius_produces_full_movement()
        {
            Vector2 move = Stick().Evaluate(Vector2.zero, new Vector2(Radius, 0f));
            Assert.AreEqual(1f, move.magnitude, 0.001f);
        }

        [Test]
        public void A_drag_past_the_radius_is_clamped_to_full_movement()
        {
            Vector2 move = Stick().Evaluate(Vector2.zero, new Vector2(Radius * 4f, 0f));
            Assert.AreEqual(1f, move.magnitude, 0.001f);
        }

        [Test]
        public void Direction_survives_the_clamp()
        {
            Vector2 move = Stick().Evaluate(Vector2.zero, new Vector2(0f, Radius * 3f));
            Assert.AreEqual(0f, move.x, 0.001f);
            Assert.Greater(move.y, 0.9f);
        }

        [Test]
        public void A_diagonal_drag_past_the_radius_stays_on_the_unit_circle()
        {
            Vector2 move = Stick().Evaluate(Vector2.zero, new Vector2(500f, 500f));
            Assert.AreEqual(1f, move.magnitude, 0.001f);
            Assert.AreEqual(move.x, move.y, 0.001f);
        }

        [Test]
        public void Movement_is_measured_from_where_the_finger_landed_not_from_the_screen_origin()
        {
            var origin = new Vector2(640f, 360f);
            Vector2 move = Stick().Evaluate(origin, origin + new Vector2(Radius, 0f));
            Assert.AreEqual(1f, move.magnitude, 0.001f);
        }

        [Test]
        public void The_knob_never_leaves_the_ring()
        {
            Vector2 knob = Stick().KnobOffset(Vector2.zero, new Vector2(900f, 0f));
            Assert.AreEqual(Radius, knob.magnitude, 0.001f);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run EditMode filtered to `Museum.Core.Tests.VirtualStickModelTests`.
Expected: FAIL to compile — `VirtualStickModel` does not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Core/VirtualStickModel.cs`:

```csharp
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// The geometry of an on-screen thumb stick, with no MonoBehaviour and no scene: given where
    /// a finger landed and where it is now, what movement does that mean, and where does the knob
    /// get drawn.
    /// </summary>
    /// <remarks>
    /// The origin is passed in rather than fixed because the stick floats — it appears wherever
    /// the thumb lands in its half of the screen, which is what stops a player having to look
    /// down to find it.
    ///
    /// This is input shaping, not a game rule: it decides how a gesture reads, never how far a
    /// player travels. Vector2 is UnityEngine's, which is fine here for the same reason.
    /// </remarks>
    public readonly struct VirtualStickModel
    {
        /// <summary>Screen pixels from the origin at which movement reaches full speed.</summary>
        public readonly float Radius;

        /// <summary>Screen pixels of slop before any movement is reported at all.</summary>
        public readonly float DeadZone;

        public VirtualStickModel(float radius, float deadZone)
        {
            Radius = Mathf.Max(1f, radius);
            DeadZone = Mathf.Clamp(deadZone, 0f, Radius - 1f);
        }

        /// <summary>
        /// Movement for this frame: direction from the origin, magnitude ramping 0 to 1 between
        /// the dead zone and the radius. Matches the range the WASD 2DVector composite produces,
        /// so <c>FPSController</c> needs no second speed scale.
        /// </summary>
        public Vector2 Evaluate(Vector2 origin, Vector2 current)
        {
            Vector2 offset = current - origin;
            float distance = offset.magnitude;
            if (distance <= DeadZone) return Vector2.zero;

            Vector2 direction = offset / distance;
            float travel = Mathf.InverseLerp(DeadZone, Radius, distance);
            return direction * travel;
        }

        /// <summary>Where to draw the knob relative to the ring's centre.</summary>
        public Vector2 KnobOffset(Vector2 origin, Vector2 current)
            => Vector2.ClampMagnitude(current - origin, Radius);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the same filter. Expected: 7 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/VirtualStickModel.cs* Assets/Scripts/Core/Tests/VirtualStickModelTests.cs*
git commit -m "feat: virtual stick geometry"
```

---

### Task 4: Look-drag accumulation

**Files:**
- Create: `Assets/Scripts/Core/LookDragModel.cs`
- Test: `Assets/Scripts/Core/Tests/LookDragModelTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `class Museum.Core.LookDragModel`, constructed as `new LookDragModel(float sensitivity)`,
  with `float Sensitivity { get; set; }`, `void AddDrag(Vector2 screenDelta)` and
  `Vector2 Consume()`.

**Why a class and not a struct:** it holds a per-frame accumulator that callers mutate through a
reference. A struct here would silently accumulate into a copy.

- [ ] **Step 1: Write the failing test**

`Assets/Scripts/Core/Tests/LookDragModelTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// Drag arrives as any number of pointer events per frame and must leave as exactly one
    /// look delta per frame, then be gone. Holding a stale value would spin the camera forever.
    /// </summary>
    public class LookDragModelTests
    {
        [Test]
        public void Drag_is_scaled_by_sensitivity()
        {
            var model = new LookDragModel(0.5f);
            model.AddDrag(new Vector2(10f, 4f));

            Vector2 look = model.Consume();
            Assert.AreEqual(5f, look.x, 0.001f);
            Assert.AreEqual(2f, look.y, 0.001f);
        }

        [Test]
        public void Several_drags_in_one_frame_sum()
        {
            var model = new LookDragModel(1f);
            model.AddDrag(new Vector2(3f, 0f));
            model.AddDrag(new Vector2(4f, 1f));

            Vector2 look = model.Consume();
            Assert.AreEqual(7f, look.x, 0.001f);
            Assert.AreEqual(1f, look.y, 0.001f);
        }

        [Test]
        public void A_frame_with_no_drag_produces_zero_not_the_last_value()
        {
            var model = new LookDragModel(1f);
            model.AddDrag(new Vector2(20f, 20f));
            model.Consume();

            Assert.AreEqual(Vector2.zero, model.Consume());
        }

        [Test]
        public void Sensitivity_can_be_changed_between_frames()
        {
            var model = new LookDragModel(1f) { Sensitivity = 2f };
            model.AddDrag(new Vector2(3f, 0f));

            Assert.AreEqual(6f, model.Consume().x, 0.001f);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run EditMode filtered to `Museum.Core.Tests.LookDragModelTests`.
Expected: FAIL to compile — `LookDragModel` does not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Core/LookDragModel.cs`:

```csharp
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Turns a stream of drag events into one look delta per frame.
    /// </summary>
    /// <remarks>
    /// The output is in the same units as <c>&lt;Mouse&gt;/delta</c> — pixels since the last frame,
    /// applied without <c>Time.deltaTime</c> — so the touch scheme and the mouse scheme share one
    /// sensitivity and one pitch clamp inside <c>FPSController</c>.
    ///
    /// <see cref="Consume"/> empties the accumulator on purpose. A drag reports nothing on a frame
    /// where the thumb did not move, and a model that kept its last value would spin the camera on
    /// a stationary finger.
    /// </remarks>
    public class LookDragModel
    {
        private Vector2 _pending;

        public LookDragModel(float sensitivity)
        {
            Sensitivity = sensitivity;
        }

        /// <summary>Screen pixels of drag to look-delta units.</summary>
        public float Sensitivity { get; set; }

        /// <summary>Adds one pointer event's drag. Called any number of times per frame.</summary>
        public void AddDrag(Vector2 screenDelta) => _pending += screenDelta * Sensitivity;

        /// <summary>Takes this frame's look delta and clears it.</summary>
        public Vector2 Consume()
        {
            Vector2 look = _pending;
            _pending = Vector2.zero;
            return look;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the same filter. Expected: 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/LookDragModel.cs* Assets/Scripts/Core/Tests/LookDragModelTests.cs*
git commit -m "feat: look drag accumulation"
```

---

### Task 5: A test-visible assembly for the player scripts

**Files:**
- Create: `Assets/Scripts/Player/Museum.Player.asmdef`
- Create: `Assets/Scripts/Player/Tests/PlayMode/Museum.Player.Tests.PlayMode.asmdef`
- Test: `Assets/Scripts/Player/Tests/PlayMode/FPSControllerAssemblyTests.cs`

**Interfaces:**
- Consumes: `Museum.Core` (Task 1).
- Produces: assembly `Museum.Player` containing `FPSController` and `FPSInputReader`, referencable
  by the test assembly `Museum.Player.Tests.PlayMode`.

**Why this task exists:** `FPSController` currently compiles into `Assembly-CSharp`, and an
assembly with an `.asmdef` cannot reference `Assembly-CSharp`. Without this, no test can see the
controller, and Tasks 6 and 7 would be untestable. Scene and prefab references survive because
Unity stores them by script GUID, not by assembly. `SceneWiringRepair.cs:193-204` already reaches
these two classes by `GetType().Name` through reflection, so it is unaffected.

- [ ] **Step 1: Write the failing test**

`Assets/Scripts/Player/Tests/PlayMode/FPSControllerAssemblyTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Museum.Player.Tests
{
    /// <summary>
    /// Guards the assembly split itself: the player scripts must stay somewhere a test can
    /// reference, and must keep seeing Museum.Core.
    /// </summary>
    public class FPSControllerAssemblyTests
    {
        [Test]
        public void The_controller_lives_in_the_player_assembly()
        {
            Assert.AreEqual("Museum.Player", typeof(FPSController).Assembly.GetName().Name);
        }

        [Test]
        public void The_player_assembly_can_see_the_core_assembly()
        {
            Assert.AreEqual(Museum.Core.ControlScheme.Unknown, default(Museum.Core.ControlScheme));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run PlayMode filtered to `Museum.Player.Tests.FPSControllerAssemblyTests`.
Expected: FAIL to compile — the test assembly does not exist, and `FPSController` is not
visible from any `.asmdef`.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Player/Museum.Player.asmdef`:

```json
{
    "name": "Museum.Player",
    "rootNamespace": "",
    "references": [
        "Museum.Core",
        "Unity.InputSystem",
        "Unity.TextMeshPro",
        "UnityEngine.UI"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Assets/Scripts/Player/Tests/PlayMode/Museum.Player.Tests.PlayMode.asmdef`:

```json
{
    "name": "Museum.Player.Tests.PlayMode",
    "rootNamespace": "Museum.Player.Tests",
    "references": [
        "Museum.Player",
        "Museum.Core",
        "Unity.InputSystem",
        "UnityEngine.UI",
        "UnityEngine.TestRunner"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`FPSController` and `FPSInputReader` keep the global namespace and their existing GUIDs — do
not move, rename or re-namespace either file.

- [ ] **Step 4: Run tests to verify they pass**

Run the same filter. Expected: 2 tests PASS.

Then, still in this step, open `Assets/Scenes/Museum.unity` in the Editor and confirm the
player object still shows `FPSController` and `FPSInputReader` with their inspector values —
not "Missing (Mono Script)". If anything reads as missing, stop: the assembly move is the
cause and the plan must be revisited rather than the values re-entered by hand.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Player/Museum.Player.asmdef* Assets/Scripts/Player/Tests
git commit -m "chore: give the player scripts their own assembly"
```

---

### Task 6: Script-facing input on FPSController, and conditional pointer lock

**Files:**
- Modify: `Assets/Scripts/Player/FPSController.cs:32-79` (Awake, Update, HandleCursorLock) and its `On*` callbacks at the end of the file
- Test: `Assets/Scripts/Player/Tests/PlayMode/FPSControllerInputTests.cs`

**Interfaces:**
- Consumes: `Museum.Core.SessionData.IsTouch`, `Museum.Core.ControlScheme` (Task 1); the
  `Museum.Player` assembly (Task 5).
- Produces on `FPSController`: `void SetMoveInput(Vector2 move)`, `void AddLookDelta(Vector2 delta)`,
  `void SetSprint(bool held)`, `void PressJump()`, and `bool UsesCursorLock { get; }`.

- [ ] **Step 1: Write the failing test**

`Assets/Scripts/Player/Tests/PlayMode/FPSControllerInputTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Museum.Core;

namespace Museum.Player.Tests
{
    /// <summary>
    /// The controller is the seam both schemes push through. These tests drive it by script only —
    /// no Input System devices — which is exactly how the touch source drives it in the build.
    /// </summary>
    public class FPSControllerInputTests
    {
        private GameObject _bootstrap;
        private GameObject _player;

        private SessionData GivenScheme(ControlScheme scheme)
        {
            _bootstrap = new GameObject("Bootstrap");
            SessionData session = _bootstrap.AddComponent<SessionData>();
            session.Scheme = scheme;
            return session;
        }

        private FPSController GivenPlayer()
        {
            _player = new GameObject("Player", typeof(Rigidbody), typeof(CapsuleCollider));
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(_player.transform, false);
            return _player.AddComponent<FPSController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.DestroyImmediate(_player);
            if (_bootstrap != null) Object.DestroyImmediate(_bootstrap);
            Cursor.lockState = CursorLockMode.None;
        }

        [UnityTest]
        public IEnumerator Look_delta_from_script_yaws_the_body()
        {
            GivenScheme(ControlScheme.Sentuh);
            FPSController controller = GivenPlayer();
            float before = controller.transform.eulerAngles.y;

            controller.AddLookDelta(new Vector2(10f, 0f));
            yield return null;

            Assert.AreNotEqual(before, controller.transform.eulerAngles.y);
        }

        [UnityTest]
        public IEnumerator Look_delta_is_consumed_so_the_camera_stops_when_the_thumb_does()
        {
            GivenScheme(ControlScheme.Sentuh);
            FPSController controller = GivenPlayer();

            controller.AddLookDelta(new Vector2(10f, 0f));
            yield return null;
            float afterDrag = controller.transform.eulerAngles.y;

            yield return null;
            Assert.AreEqual(afterDrag, controller.transform.eulerAngles.y, 0.001f);
        }

        [UnityTest]
        public IEnumerator Touch_scheme_never_locks_the_cursor()
        {
            GivenScheme(ControlScheme.Sentuh);
            FPSController controller = GivenPlayer();
            yield return null;

            Assert.IsFalse(controller.UsesCursorLock);
            Assert.AreNotEqual(CursorLockMode.Locked, Cursor.lockState);
        }

        [UnityTest]
        public IEnumerator Desktop_scheme_locks_the_cursor()
        {
            GivenScheme(ControlScheme.Desktop);
            FPSController controller = GivenPlayer();
            yield return null;

            Assert.IsTrue(controller.UsesCursorLock);
        }

        [UnityTest]
        public IEnumerator An_unchosen_scheme_behaves_as_desktop()
        {
            // Opening Museum.unity directly in the Editor never runs the picker.
            GivenScheme(ControlScheme.Unknown);
            FPSController controller = GivenPlayer();
            yield return null;

            Assert.IsTrue(controller.UsesCursorLock);
        }

        [UnityTest]
        public IEnumerator Move_input_from_script_reaches_the_rigidbody()
        {
            GivenScheme(ControlScheme.Sentuh);
            FPSController controller = GivenPlayer();

            controller.SetMoveInput(new Vector2(0f, 1f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Vector3 velocity = controller.GetComponent<Rigidbody>().linearVelocity;
            Assert.Greater(new Vector2(velocity.x, velocity.z).magnitude, 0.1f);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run PlayMode filtered to `Museum.Player.Tests.FPSControllerInputTests`.
Expected: FAIL to compile — `SetMoveInput`, `AddLookDelta` and `UsesCursorLock` do not exist.

- [ ] **Step 3: Write minimal implementation**

In `Assets/Scripts/Player/FPSController.cs`, add the using and the new surface. Add at the top:

```csharp
using Museum.Core;
```

Replace the cursor lines at the end of `Awake` (currently lines 54-55) with:

```csharp
        if (UsesCursorLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
```

Add above `Update`:

```csharp
    /// <summary>
    /// Pointer lock is a desktop-only mechanism: mobile browsers have no Pointer Lock API, and
    /// locking there would leave the player with no look control at all. An unmade choice — the
    /// museum scene opened directly in the Editor — reads as desktop, which is how it has always
    /// behaved.
    /// </summary>
    public bool UsesCursorLock =>
        SessionData.Instance == null || SessionData.Instance.Scheme != ControlScheme.Sentuh;
```

Replace `HandleCursorLock` (lines 70-79) with:

```csharp
    private void HandleCursorLock()
    {
        if (!UsesCursorLock) return;

        // WebGL/browsers drop pointer lock on focus loss and require a fresh user
        // gesture (click) to re-acquire it — otherwise look input silently stops.
        if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
```

At the end of `HandleMouseLook`, after `transform.Rotate(...)`, add:

```csharp
        // Consumed. The mouse re-sends a delta every frame it moves and a zero when it stops, so
        // clearing here changes nothing for it; touch adds deltas and depends on it.
        lookInput = Vector2.zero;
```

Replace the four `On*` callbacks at the end of the file with delegates onto the new surface:

```csharp
    public void OnMove(InputAction.CallbackContext context) => SetMoveInput(context.ReadValue<Vector2>());

    public void OnLook(InputAction.CallbackContext context) => AddLookDelta(context.ReadValue<Vector2>());

    public void OnSprint(InputAction.CallbackContext context) => SetSprint(context.ReadValueAsButton());

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed) PressJump();
    }

    /// <summary>Movement for this frame, in the same range the WASD composite produces.</summary>
    public void SetMoveInput(Vector2 move) => moveInput = move;

    /// <summary>
    /// Adds look delta in <c>&lt;Mouse&gt;/delta</c> units. Additive rather than assigned so several
    /// touch events in one frame sum instead of overwriting each other.
    /// </summary>
    public void AddLookDelta(Vector2 delta) => lookInput += delta;

    public void SetSprint(bool held) => isSprintPressed = held;

    public void PressJump() => isJumpPressed = true;
```

- [ ] **Step 4: Run tests to verify they pass**

Run the same filter. Expected: 6 tests PASS. Then run the whole PlayMode and EditMode suites
and confirm nothing else regressed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Player/FPSController.cs Assets/Scripts/Player/Tests/PlayMode/FPSControllerInputTests.cs*
git commit -m "feat: script input surface and scheme-aware pointer lock"
```

---

### Task 7: Touch widgets and the touch input source

**Files:**
- Create: `Assets/Scripts/Core/TouchOnly.cs`
- Create: `Assets/Scripts/Player/TouchJoystick.cs`
- Create: `Assets/Scripts/Player/TouchLookArea.cs`
- Create: `Assets/Scripts/Player/TouchInputSource.cs`
- Create: `Assets/Scripts/Player/ControlSchemeSwitch.cs`
- Test: `Assets/Scripts/Player/Tests/PlayMode/TouchInputTests.cs`

**Interfaces:**
- Consumes: `VirtualStickModel` (Task 3), `LookDragModel` (Task 4), `FPSController.SetMoveInput` /
  `AddLookDelta` / `PressJump` (Task 6), `SessionData.IsTouch` (Task 1).
- Produces:
  - `Museum.Core.TouchOnly` — a MonoBehaviour that deactivates its own GameObject unless the
    scheme is `Sentuh`. Used by every scene's overlay, in any assembly.
  - `TouchJoystick` with `Vector2 Value { get; }` and serialized `radius`, `deadZone`, `knob`.
  - `TouchLookArea` with `Vector2 ConsumeLook()` and serialized `sensitivity`.
  - `TouchInputSource` with serialized `controller`, `joystick`, `lookArea`, and public
    `void Jump()` and `void Interact()` for button wiring.
  - `ControlSchemeSwitch` with serialized `desktopBehaviours` and `touchBehaviours`.

- [ ] **Step 1: Write the failing test**

`Assets/Scripts/Player/Tests/PlayMode/TouchInputTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Museum.Core;

namespace Museum.Player.Tests
{
    /// <summary>
    /// Drives the touch widgets through the same uGUI event interfaces a real finger drives them
    /// through, and checks what reaches the controller.
    /// </summary>
    public class TouchInputTests
    {
        private GameObject _bootstrap;
        private GameObject _root;

        private SessionData GivenScheme(ControlScheme scheme)
        {
            _bootstrap = new GameObject("Bootstrap");
            SessionData session = _bootstrap.AddComponent<SessionData>();
            session.Scheme = scheme;
            return session;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_bootstrap != null) Object.DestroyImmediate(_bootstrap);
        }

        private static PointerEventData At(Vector2 position) =>
            new PointerEventData(EventSystem.current) { position = position };

        [Test]
        public void A_joystick_drag_past_the_dead_zone_reports_movement()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Joystick", typeof(RectTransform));
            var joystick = _root.AddComponent<TouchJoystick>();

            joystick.OnPointerDown(At(new Vector2(100f, 100f)));
            joystick.OnDrag(At(new Vector2(300f, 100f)));

            Assert.Greater(joystick.Value.x, 0.5f);
        }

        [Test]
        public void Releasing_the_joystick_zeroes_it()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Joystick", typeof(RectTransform));
            var joystick = _root.AddComponent<TouchJoystick>();

            joystick.OnPointerDown(At(new Vector2(100f, 100f)));
            joystick.OnDrag(At(new Vector2(300f, 100f)));
            joystick.OnPointerUp(At(new Vector2(300f, 100f)));

            Assert.AreEqual(Vector2.zero, joystick.Value);
        }

        [Test]
        public void A_look_drag_is_reported_once_then_gone()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Look", typeof(RectTransform));
            var look = _root.AddComponent<TouchLookArea>();

            look.OnDrag(new PointerEventData(EventSystem.current) { delta = new Vector2(12f, 0f) });

            Assert.Greater(look.ConsumeLook().x, 0f);
            Assert.AreEqual(Vector2.zero, look.ConsumeLook());
        }

        [Test]
        public void TouchOnly_hides_itself_under_the_desktop_scheme()
        {
            GivenScheme(ControlScheme.Desktop);
            _root = new GameObject("Overlay");
            _root.AddComponent<TouchOnly>();

            Assert.IsFalse(_root.activeSelf);
        }

        [Test]
        public void TouchOnly_stays_visible_under_the_touch_scheme()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Overlay");
            _root.AddComponent<TouchOnly>();

            Assert.IsTrue(_root.activeSelf);
        }

        [UnityTest]
        public IEnumerator The_source_feeds_joystick_and_look_into_the_controller()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Player", typeof(Rigidbody), typeof(CapsuleCollider));
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(_root.transform, false);
            var controller = _root.AddComponent<FPSController>();

            var joystickObject = new GameObject("Joystick", typeof(RectTransform));
            var joystick = joystickObject.AddComponent<TouchJoystick>();
            var lookObject = new GameObject("Look", typeof(RectTransform));
            var look = lookObject.AddComponent<TouchLookArea>();
            joystickObject.transform.SetParent(_root.transform, false);
            lookObject.transform.SetParent(_root.transform, false);

            var source = _root.AddComponent<TouchInputSource>();
            source.Configure(controller, joystick, look);

            joystick.OnPointerDown(At(new Vector2(100f, 100f)));
            joystick.OnDrag(At(new Vector2(100f, 400f)));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Vector3 velocity = controller.GetComponent<Rigidbody>().linearVelocity;
            Assert.Greater(new Vector2(velocity.x, velocity.z).magnitude, 0.1f);
        }

        [UnityTest]
        public IEnumerator ControlSchemeSwitch_enables_exactly_one_source()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Player", typeof(Rigidbody), typeof(CapsuleCollider));
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(_root.transform, false);
            _root.AddComponent<FPSController>();
            var touch = _root.AddComponent<TouchInputSource>();

            // The child Camera stands in for the desktop list: any Behaviour will do, and the real
            // one (FPSInputReader) needs an InputActionAsset this fixture has no reason to build.
            var desktop = camera.GetComponent<Camera>();

            var swap = _root.AddComponent<ControlSchemeSwitch>();
            swap.Configure(new Behaviour[] { desktop }, new Behaviour[] { touch });
            yield return null;

            Assert.IsTrue(touch.enabled);
            Assert.IsFalse(desktop.enabled);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run PlayMode filtered to `Museum.Player.Tests.TouchInputTests`.
Expected: FAIL to compile — none of the five types exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Core/TouchOnly.cs`:

```csharp
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Deactivates its own GameObject unless the visitor chose the touch scheme. Every touch
    /// overlay in every scene carries one, so no scene needs a script that knows what an overlay
    /// contains.
    /// </summary>
    /// <remarks>
    /// Lives in Museum.Core so Egrang, Dakon and the museum can all use it. An unmade choice
    /// hides the overlay, which is the right default: the museum opened directly in the Editor is
    /// a desktop session.
    /// </remarks>
    public class TouchOnly : MonoBehaviour
    {
        private void Awake()
        {
            bool touch = SessionData.Instance != null && SessionData.Instance.IsTouch;
            if (!touch) gameObject.SetActive(false);
        }
    }
}
```

`Assets/Scripts/Player/TouchJoystick.cs`:

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using Museum.Core;

/// <summary>
/// The floating thumb stick: it appears wherever the finger lands inside its rect, so the player
/// never has to look down to find it. All the geometry is in <see cref="VirtualStickModel"/>;
/// this class only turns uGUI events into calls on it and moves the knob graphic.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("Screen pixels from the touch-down point at which movement is at full speed.")]
    [SerializeField] private float radius = 120f;

    [Tooltip("Screen pixels of slop before any movement is reported.")]
    [SerializeField] private float deadZone = 12f;

    [Tooltip("Optional ring that moves to the touch-down point. Hidden until touched.")]
    [SerializeField] private RectTransform ring;

    [Tooltip("Optional knob drawn inside the ring.")]
    [SerializeField] private RectTransform knob;

    private VirtualStickModel _model;
    private Vector2 _origin;
    private bool _held;

    /// <summary>This frame's movement, magnitude 0..1. Zero while nothing is touching it.</summary>
    public Vector2 Value { get; private set; }

    private void Awake() => _model = new VirtualStickModel(radius, deadZone);

    private void OnDisable() => Release();

    public void OnPointerDown(PointerEventData eventData)
    {
        _held = true;
        _origin = eventData.position;
        Value = Vector2.zero;

        if (ring != null)
        {
            ring.gameObject.SetActive(true);
            ring.position = _origin;
        }
        MoveKnob(_origin);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_held) return;
        Value = _model.Evaluate(_origin, eventData.position);
        MoveKnob(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();

    private void Release()
    {
        _held = false;
        Value = Vector2.zero;
        if (ring != null) ring.gameObject.SetActive(false);
    }

    private void MoveKnob(Vector2 current)
    {
        if (knob == null) return;
        knob.position = _origin + _model.KnobOffset(_origin, current);
    }
}
```

`Assets/Scripts/Player/TouchLookArea.cs`:

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using Museum.Core;

/// <summary>
/// Drag anywhere in this rect to look around. Sits over the right side of the screen, behind
/// nothing: it is a full rect with a transparent graphic so uGUI will route drags to it.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TouchLookArea : MonoBehaviour, IDragHandler
{
    [Tooltip("Screen pixels of drag to look-delta units. Compared against FPSController's own mouse sensitivity.")]
    [SerializeField] private float sensitivity = 0.4f;

    private LookDragModel _model;

    private void Awake() => _model = new LookDragModel(sensitivity);

    public void OnDrag(PointerEventData eventData)
    {
        _model ??= new LookDragModel(sensitivity);
        _model.AddDrag(eventData.delta);
    }

    /// <summary>This frame's look delta, cleared as it is read.</summary>
    public Vector2 ConsumeLook()
    {
        _model ??= new LookDragModel(sensitivity);
        return _model.Consume();
    }
}
```

`Assets/Scripts/Player/TouchInputSource.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Pushes the touch overlay's widgets into <see cref="FPSController"/>'s script input surface —
/// the touch-scheme counterpart of <see cref="FPSInputReader"/>. Exactly one of the two is
/// enabled, by <see cref="ControlSchemeSwitch"/>.
/// </summary>
[DefaultExecutionOrder(-1)]
public class TouchInputSource : MonoBehaviour
{
    [SerializeField] private FPSController controller;
    [SerializeField] private TouchJoystick joystick;
    [SerializeField] private TouchLookArea lookArea;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<FPSController>();
    }

    /// <summary>Wires the source from script. The scene wires the same three through the inspector.</summary>
    public void Configure(FPSController target, TouchJoystick stick, TouchLookArea look)
    {
        controller = target;
        joystick = stick;
        lookArea = look;
    }

    private void Update()
    {
        if (controller == null) return;

        if (joystick != null) controller.SetMoveInput(joystick.Value);
        if (lookArea != null) controller.AddLookDelta(lookArea.ConsumeLook());
    }

    /// <summary>UnityEvent target for the Lompat button.</summary>
    public void Jump()
    {
        if (controller != null) controller.PressJump();
    }
}
```

`Assets/Scripts/Player/ControlSchemeSwitch.cs`:

```csharp
using UnityEngine;
using Museum.Core;

/// <summary>
/// Enables the input source that matches the visitor's chosen scheme and disables the other, so
/// only one thing is writing to <see cref="FPSController"/> at a time.
/// </summary>
/// <remarks>
/// An unmade choice reads as desktop, which keeps Museum.unity playable when it is opened
/// straight from the Editor without going through MainMenu.
/// </remarks>
[DefaultExecutionOrder(-2)]
public class ControlSchemeSwitch : MonoBehaviour
{
    [Tooltip("Behaviours live only under the keyboard-and-mouse scheme, e.g. FPSInputReader.")]
    [SerializeField] private Behaviour[] desktopBehaviours = new Behaviour[0];

    [Tooltip("Behaviours live only under the touch scheme, e.g. TouchInputSource.")]
    [SerializeField] private Behaviour[] touchBehaviours = new Behaviour[0];

    /// <summary>Wires the switch from script. The scene wires the same two arrays in the inspector.</summary>
    public void Configure(Behaviour[] desktop, Behaviour[] touch)
    {
        desktopBehaviours = desktop;
        touchBehaviours = touch;
        Apply();
    }

    private void Awake() => Apply();

    private void Apply()
    {
        bool touch = SessionData.Instance != null && SessionData.Instance.IsTouch;

        foreach (Behaviour behaviour in desktopBehaviours)
        {
            if (behaviour != null) behaviour.enabled = !touch;
        }

        foreach (Behaviour behaviour in touchBehaviours)
        {
            if (behaviour != null) behaviour.enabled = touch;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the same filter. Expected: 7 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/TouchOnly.cs* Assets/Scripts/Player/Touch*.cs* \
        Assets/Scripts/Player/ControlSchemeSwitch.cs* \
        Assets/Scripts/Player/Tests/PlayMode/TouchInputTests.cs*
git commit -m "feat: touch joystick, look area and input source"
```

---

### Task 8: Doorways and lesson plaques reachable by tap

**Files:**
- Modify: `Assets/Scripts/Core/SceneTriggerPrompt.cs:52-73`
- Modify: `Assets/Scripts/Core/LessonReader.cs:1-25` (class comment) and its `Update`
- Create: `Assets/Scripts/Core/TouchInteractRouter.cs`
- Test: `Assets/Scripts/Core/Tests/PlayMode/TouchInteractRouterTests.cs`

**Interfaces:**
- Consumes: `LessonPanel.Next()` / `LessonPanel.Prev()` (existing, `LessonPanel.cs:110-113`).
- Produces:
  - On `SceneTriggerPrompt`: `public void Enter()` (was private), `public bool PlayerInside { get; }`,
    and a serialized `TMPro.TMP_Text promptLabel` whose text is set in `Awake` to `Tekan Enter`
    under the desktop scheme and `Ketuk Interaksi` under touch.
  - `LessonReader` gains `public void Next()`, `public void Prev()`, `public bool PlayerInside { get; }`.
  - `Museum.Core.TouchInteractRouter` with `public void Interact()`, `public void PageNext()`,
    `public void PagePrev()`, and `public static TouchInteractRouter Active { get; }`.

**How the router finds its target:** prompts and readers register themselves with the router as
the player enters and leaves their trigger — no scene wiring per doorway, which matters because
one of the four museum doorways is already known to be unwired (CLAUDE.md). Registration is via
`TouchInteractRouter.Register(...)` / `Unregister(...)` static calls that no-op when no router
exists in the scene.

- [ ] **Step 1: Write the failing test**

`Assets/Scripts/Core/Tests/PlayMode/TouchInteractRouterTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The Interaksi and ‹ › buttons have no idea which doorway or plaque the player is standing
    /// at. The router is what closes that gap, and these tests are its contract.
    /// </summary>
    public class TouchInteractRouterTests
    {
        private GameObject _router;
        private GameObject _panelObject;

        [TearDown]
        public void TearDown()
        {
            if (_router != null) Object.DestroyImmediate(_router);
            if (_panelObject != null) Object.DestroyImmediate(_panelObject);
        }

        private TouchInteractRouter GivenRouter()
        {
            _router = new GameObject("Router");
            return _router.AddComponent<TouchInteractRouter>();
        }

        [Test]
        public void Paging_with_nothing_in_range_does_nothing_and_does_not_throw()
        {
            TouchInteractRouter router = GivenRouter();
            Assert.DoesNotThrow(router.PageNext);
            Assert.DoesNotThrow(router.PagePrev);
            Assert.DoesNotThrow(router.Interact);
        }

        [Test]
        public void A_registered_reader_receives_paging()
        {
            TouchInteractRouter router = GivenRouter();

            _panelObject = new GameObject("Reader", typeof(BoxCollider));
            var reader = _panelObject.AddComponent<LessonReader>();
            TouchInteractRouter.Register(reader);

            Assert.DoesNotThrow(router.PageNext);

            TouchInteractRouter.Unregister(reader);
            Assert.IsNull(TouchInteractRouter.CurrentReader);
        }

        [Test]
        public void Unregistering_the_reader_that_is_not_current_leaves_the_current_one_alone()
        {
            TouchInteractRouter router = GivenRouter();

            _panelObject = new GameObject("Reader", typeof(BoxCollider));
            var first = _panelObject.AddComponent<LessonReader>();
            var secondObject = new GameObject("Other", typeof(BoxCollider));
            var second = secondObject.AddComponent<LessonReader>();

            TouchInteractRouter.Register(first);
            TouchInteractRouter.Unregister(second);
            Assert.AreSame(first, TouchInteractRouter.CurrentReader);

            TouchInteractRouter.Unregister(first);
            Object.DestroyImmediate(secondObject);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run PlayMode filtered to `Museum.Core.Tests.TouchInteractRouterTests`.
Expected: FAIL to compile — `TouchInteractRouter` does not exist.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/Core/TouchInteractRouter.cs`:

```csharp
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// The overlay's Interaksi and ‹ › buttons, routed to whatever the player is standing in
    /// front of. Doorways and plaques register themselves as the player enters their trigger, so
    /// no doorway needs to be wired to the overlay by hand — which matters, because a museum
    /// doorway going unwired is a failure this project has already lived through.
    /// </summary>
    public class TouchInteractRouter : MonoBehaviour
    {
        /// <summary>The doorway the player is inside, or null.</summary>
        public static SceneTriggerPrompt CurrentPrompt { get; private set; }

        /// <summary>The plaque the player is standing at, or null.</summary>
        public static LessonReader CurrentReader { get; private set; }

        public static void Register(SceneTriggerPrompt prompt) => CurrentPrompt = prompt;

        public static void Register(LessonReader reader) => CurrentReader = reader;

        /// <summary>Clears the registration only if this is still the one registered.</summary>
        public static void Unregister(SceneTriggerPrompt prompt)
        {
            if (CurrentPrompt == prompt) CurrentPrompt = null;
        }

        /// <summary>Clears the registration only if this is still the one registered.</summary>
        public static void Unregister(LessonReader reader)
        {
            if (CurrentReader == reader) CurrentReader = null;
        }

        private void OnDestroy()
        {
            // Statics outlive a scene load; a stale prompt would be a destroyed object.
            CurrentPrompt = null;
            CurrentReader = null;
        }

        /// <summary>UnityEvent target for the Interaksi button.</summary>
        public void Interact()
        {
            if (CurrentPrompt != null) CurrentPrompt.Enter();
        }

        /// <summary>UnityEvent target for the › button.</summary>
        public void PageNext()
        {
            if (CurrentReader != null) CurrentReader.Next();
        }

        /// <summary>UnityEvent target for the ‹ button.</summary>
        public void PagePrev()
        {
            if (CurrentReader != null) CurrentReader.Prev();
        }
    }
}
```

In `Assets/Scripts/Core/SceneTriggerPrompt.cs`, add the using `using TMPro;`, add a serialized
field beside `promptUI`:

```csharp
    [Tooltip("The label inside promptUI. Its text depends on the control scheme, so it is set at runtime.")]
    [SerializeField] private TMP_Text promptLabel;
```

Add to `Awake`, after the existing `promptUI` line:

```csharp
        if (promptLabel != null)
        {
            bool touch = SessionData.Instance != null && SessionData.Instance.IsTouch;
            promptLabel.text = touch ? "Ketuk Interaksi" : "Tekan Enter";
        }
```

Register on enter and exit, and expose the state:

```csharp
    /// <summary>True while the player stands in this doorway's trigger.</summary>
    public bool PlayerInside => playerInside;
```

In `OnTriggerEnter`, after `playerInside = true;` add `TouchInteractRouter.Register(this);`
In `OnTriggerExit`, after `playerInside = false;` add `TouchInteractRouter.Unregister(this);`

Change `private void Enter()` to:

```csharp
    /// <summary>
    /// Walks through the doorway. Public because the touch overlay's Interaksi button reaches it
    /// through <see cref="TouchInteractRouter"/> — the keyboard is no longer the only way in.
    /// </summary>
    public void Enter()
```

In `Assets/Scripts/Core/LessonReader.cs`, replace the paragraph of the class comment that begins
"Keyboard rather than pointer on purpose." with:

```csharp
    /// Keyboard under the desktop scheme, where FPSController locks the cursor and a uGUI button
    /// on a world-space canvas could never be clicked. Under the touch scheme the cursor is never
    /// locked and the overlay's ‹ › buttons page the plaque instead, routed here by
    /// <see cref="TouchInteractRouter"/>. Both paths land on the same two methods.
```

Add to `LessonReader`:

```csharp
        /// <summary>True while the player stands in this plaque's trigger.</summary>
        public bool PlayerInside => _playerInside;

        /// <summary>
        /// Next page. Called by the E key and by the overlay's › button alike. Null-guarded because
        /// the router reaches these from a button, and a plaque with no panel assigned disables
        /// itself in Awake but is still a live reference.
        /// </summary>
        public void Next()
        {
            if (panel != null) panel.Next();
        }

        /// <summary>Previous page. Called by the Q key and by the overlay's ‹ button alike.</summary>
        public void Prev()
        {
            if (panel != null) panel.Prev();
        }
```

and change the two bodies inside `Update` from `panel.Prev();` / `panel.Next();` to `Prev();` /
`Next();`, and register in the trigger callbacks:

- In `OnTriggerEnter`, after `_playerInside = true;` add `TouchInteractRouter.Register(this);`
- In `OnTriggerExit`, after `_playerInside = false;` add `TouchInteractRouter.Unregister(this);`

- [ ] **Step 4: Run tests to verify they pass**

Run the same filter. Expected: 3 tests PASS. Run the whole EditMode and PlayMode suites too.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/TouchInteractRouter.cs* Assets/Scripts/Core/SceneTriggerPrompt.cs \
        Assets/Scripts/Core/LessonReader.cs Assets/Scripts/Core/Tests/PlayMode/TouchInteractRouterTests.cs*
git commit -m "feat: route doorway and plaque interaction to the touch overlay"
```

---

### Task 9: The museum touch overlay prefab and its generator

**Files:**
- Create: `Assets/Scripts/Core/Editor/TouchControlsUIBuilder.cs`
- Modify: `Assets/Scenes/Museum.unity` (via the generator)

The overlay is built directly into the scene, not saved as a prefab. An earlier draft of this
plan promised `Assets/Prefabs/Touch Controls.prefab`; that was dropped, because the wiring the
overlay needs (`TouchInputSource`'s references to the player's `FPSController`) is scene-specific
and a prefab cannot carry it, and because exactly one scene uses this overlay — Egrang builds its
own, and Dakon needs none.

**Interfaces:**
- Consumes: `TouchOnly`, `TouchJoystick`, `TouchLookArea`, `TouchInputSource`,
  `ControlSchemeSwitch`, `TouchInteractRouter` (Tasks 7-8), `MuseumUIStyle` helpers
  (`CreateUI`, `CreateButton`, `Stretch`, `Anchor`, `ReferenceResolution`, `UndoLabel`).
- Produces: menu item `Museum/Rebuild UI/Touch Controls`.

**Why the generator and not hand-wiring:** this project lost every inspector value once
(CLAUDE.md). Anything wired only by hand is a thing that has to be rebuilt from memory next time.

**Note:** `Museum.Core.Editor` cannot reference the `Museum.Player` assembly's types by name
unless it lists `Museum.Player` in its references — add `"Museum.Player"` to the `references`
array of `Assets/Scripts/Core/Editor/Museum.Core.Editor.asmdef` as part of this task.

- [ ] **Step 1: Write the generator**

`Assets/Scripts/Core/Editor/TouchControlsUIBuilder.cs`:

```csharp
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Builds the museum's touch overlay: a screen-space canvas holding the floating joystick, the
    /// look area, and the Lompat / Interaksi / ‹ › buttons. It hides itself under the desktop
    /// scheme through <see cref="TouchOnly"/>, so it is safe to leave in the scene always.
    ///
    /// Generated rather than hand-wired for the reason ui-style.md §9 gives: inspector values in
    /// this project have been lost once already, and a generator is how they come back.
    /// </summary>
    public static class TouchControlsUIBuilder
    {
        private const string ScenePath = "Assets/Scenes/Museum.unity";
        private const string OverlayName = "Touch Controls";

        [MenuItem("Museum/Rebuild UI/Touch Controls")]
        public static void Rebuild()
        {
            UnityEngine.SceneManagement.Scene scene =
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path == ScenePath
                    ? UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
                    : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                          ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == OverlayName) Object.DestroyImmediate(root);
            }

            GameObject overlay = Build();
            SceneManagerMove(overlay, scene);
            WirePlayer(scene, overlay);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("TouchControlsUIBuilder: touch overlay rebuilt in Museum.unity.");
        }

        private static void SceneManagerMove(GameObject go, UnityEngine.SceneManagement.Scene scene)
            => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        private static GameObject Build()
        {
            var overlay = new GameObject(OverlayName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TouchOnly));

            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the museum's own HUD canvases; the overlay must never be occluded by them.
            canvas.sortingOrder = 100;

            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            overlay.AddComponent<TouchInteractRouter>();

            BuildJoystick(overlay.transform);
            BuildLookArea(overlay.transform);
            BuildButtons(overlay.transform, overlay.GetComponent<TouchInteractRouter>());

            return overlay;
        }

        private static void BuildJoystick(Transform parent)
        {
            // Left third of the screen. A fully transparent Image is still a raycast target, which
            // is what makes an empty area of screen draggable.
            GameObject area = CreateUI("Joystick Area", parent, typeof(Image), typeof(TouchJoystick));
            RectTransform rect = area.GetComponent<RectTransform>();
            Anchor(rect, Vector2.zero, new Vector2(0.35f, 1f), new Vector2(0.5f, 0.5f));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var backdrop = area.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0f);
            backdrop.raycastTarget = true;

            GameObject ring = CreateUI("Ring", area.transform, typeof(Image));
            RectTransform ringRect = ring.GetComponent<RectTransform>();
            ringRect.sizeDelta = new Vector2(160f, 160f);
            var ringImage = ring.GetComponent<Image>();
            ringImage.sprite = FrameSprite();
            ringImage.color = new Color(Cream.r, Cream.g, Cream.b, 0.25f);
            ringImage.raycastTarget = false;

            GameObject knob = CreateUI("Knob", ring.transform, typeof(Image));
            RectTransform knobRect = knob.GetComponent<RectTransform>();
            knobRect.sizeDelta = new Vector2(64f, 64f);
            var knobImage = knob.GetComponent<Image>();
            knobImage.sprite = FrameSprite();
            knobImage.color = new Color(Gold.r, Gold.g, Gold.b, 0.65f);
            knobImage.raycastTarget = false;

            ring.SetActive(false);

            var stick = area.GetComponent<TouchJoystick>();
            var serialized = new SerializedObject(stick);
            serialized.FindProperty("ring").objectReferenceValue = ringRect;
            serialized.FindProperty("knob").objectReferenceValue = knobRect;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildLookArea(Transform parent)
        {
            GameObject area = CreateUI("Look Area", parent, typeof(Image), typeof(TouchLookArea));
            RectTransform rect = area.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.35f, 0f), Vector2.one, new Vector2(0.5f, 0.5f));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var backdrop = area.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0f);
            backdrop.raycastTarget = true;

            // Behind the buttons, so a tap on Lompat is a tap on Lompat and not a one-pixel drag.
            area.transform.SetAsFirstSibling();
        }

        private static void BuildButtons(Transform parent, TouchInteractRouter router)
        {
            GameObject jump = CreateButton("Lompat Button", parent, "Lompat", 140f, 64f, 18f);
            PlaceCorner(jump, new Vector2(1f, 0f), new Vector2(-110f, 100f));

            GameObject interact = CreateButton("Interaksi Button", parent, "Interaksi", 180f, 64f, 18f);
            PlaceCorner(interact, new Vector2(1f, 0f), new Vector2(-110f, 180f));
            UnityEventTools.AddPersistentListener(interact.GetComponent<Button>().onClick, router.Interact);

            GameObject prev = CreateButton("Halaman Sebelumnya", parent, "‹", 64f, 64f, 24f);
            PlaceCorner(prev, new Vector2(0.5f, 0f), new Vector2(-60f, 40f));
            UnityEventTools.AddPersistentListener(prev.GetComponent<Button>().onClick, router.PagePrev);

            GameObject next = CreateButton("Halaman Berikutnya", parent, "›", 64f, 64f, 24f);
            PlaceCorner(next, new Vector2(0.5f, 0f), new Vector2(60f, 40f));
            UnityEventTools.AddPersistentListener(next.GetComponent<Button>().onClick, router.PageNext);
        }

        private static void PlaceCorner(GameObject go, Vector2 anchor, Vector2 offset)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            Anchor(rect, anchor, anchor, new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = offset;
        }

        /// <summary>
        /// Points the player rig at the overlay: the touch source reads the widgets, and the switch
        /// decides which of the two sources is live.
        /// </summary>
        private static void WirePlayer(UnityEngine.SceneManagement.Scene scene, GameObject overlay)
        {
            FPSController controller = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                controller = root.GetComponentInChildren<FPSController>(true);
                if (controller != null) break;
            }

            if (controller == null)
            {
                Debug.LogWarning("TouchControlsUIBuilder: no FPSController in Museum.unity — " +
                                 "the overlay is built but nothing consumes it.");
                return;
            }

            var joystick = overlay.GetComponentInChildren<TouchJoystick>(true);
            var lookArea = overlay.GetComponentInChildren<TouchLookArea>(true);

            TouchInputSource source = controller.GetComponent<TouchInputSource>();
            if (source == null) source = Undo.AddComponent<TouchInputSource>(controller.gameObject);

            var sourceSerialized = new SerializedObject(source);
            sourceSerialized.FindProperty("controller").objectReferenceValue = controller;
            sourceSerialized.FindProperty("joystick").objectReferenceValue = joystick;
            sourceSerialized.FindProperty("lookArea").objectReferenceValue = lookArea;
            sourceSerialized.ApplyModifiedPropertiesWithoutUndo();

            var reader = controller.GetComponent<FPSInputReader>();

            ControlSchemeSwitch swap = controller.GetComponent<ControlSchemeSwitch>();
            if (swap == null) swap = Undo.AddComponent<ControlSchemeSwitch>(controller.gameObject);

            var swapSerialized = new SerializedObject(swap);
            FillArray(swapSerialized.FindProperty("desktopBehaviours"), new Object[] { reader });
            FillArray(swapSerialized.FindProperty("touchBehaviours"), new Object[] { source });
            swapSerialized.ApplyModifiedPropertiesWithoutUndo();

            // The Lompat button needs the source, which only exists once the player is found.
            Transform jump = overlay.transform.Find("Lompat Button");
            if (jump != null)
            {
                UnityEventTools.AddPersistentListener(jump.GetComponent<Button>().onClick, source.Jump);
                EditorUtility.SetDirty(jump.GetComponent<Button>());
            }

            EditorUtility.SetDirty(source);
            EditorUtility.SetDirty(swap);
        }
    }
}
```

- [ ] **Step 2: Add the assembly reference**

In `Assets/Scripts/Core/Editor/Museum.Core.Editor.asmdef`, add `"Museum.Player"` to `references`.
Expected before this: the generator does not compile, because `FPSController` is not visible.

- [ ] **Step 3: Run the generator and verify by hand**

In the Editor, run `Museum/Rebuild UI/Touch Controls`. Expected: log
`TouchControlsUIBuilder: touch overlay rebuilt in Museum.unity.`, a `Touch Controls` root object
in Museum.unity, and on the player object a `TouchInputSource` and a `ControlSchemeSwitch` with
their four references filled.

Then enter Play mode in Museum.unity. Expected: the overlay is **hidden** (no `SessionData`, so
the scheme is `Unknown`, which reads as desktop), and mouse-look still works exactly as before.

- [ ] **Step 4: Run the full test suites**

Run EditMode and PlayMode in full. Expected: everything that passed before still passes.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Editor/TouchControlsUIBuilder.cs* \
        Assets/Scripts/Core/Editor/Museum.Core.Editor.asmdef Assets/Scenes/Museum.unity
git commit -m "feat: generated touch overlay for the museum scene"
```

---

### Task 10: Egrang's step by tap

**Files:**
- Create: `Assets/Scripts/Games/Egrang/Editor/EgrangTouchUIBuilder.cs`
- Modify: `Assets/Scenes/Egrang.unity` (via the generator)

**Interfaces:**
- Consumes: `SkillCheckBar.Press()` (existing, public, `SkillCheckBar.cs:175`),
  `Museum.Core.TouchOnly` (Task 7).
- Produces: menu item `Museum/Rebuild UI/Egrang Touch`.

**Note:** `EgrangInput.inputactions` is not touched. The Space binding stays exactly as it is for
desktop players; the tap zone is a second way into the same public method.

- [ ] **Step 1: Write the generator**

`Assets/Scripts/Games/Egrang/Editor/EgrangTouchUIBuilder.cs`:

```csharp
using Museum.Core;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Games.Egrang.EditorTools
{
    /// <summary>
    /// Adds Egrang's touch step: a transparent full-screen button behind the HUD that calls the
    /// same <see cref="SkillCheckBar.Press"/> the Space key calls. Hidden under the desktop scheme
    /// by <see cref="TouchOnly"/>.
    /// </summary>
    /// <remarks>
    /// A full-screen zone rather than a small button because a stride is a rhythm action: the
    /// player is watching the bar, not their thumb. It sits at the back of the canvas so the
    /// stick-selection cards and the results panel still take their own taps first.
    /// </remarks>
    public static class EgrangTouchUIBuilder
    {
        private const string ScenePath = "Assets/Scenes/Egrang.unity";
        private const string OverlayName = "Egrang Touch";

        [MenuItem("Museum/Rebuild UI/Egrang Touch")]
        public static void Rebuild()
        {
            UnityEngine.SceneManagement.Scene scene =
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path == ScenePath
                    ? UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
                    : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                          ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            SkillCheckBar bar = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bar = root.GetComponentInChildren<SkillCheckBar>(true);
                if (bar != null) break;
            }

            if (bar == null)
            {
                Debug.LogError("EgrangTouchUIBuilder: no SkillCheckBar in Egrang.unity — nothing to tap.");
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == OverlayName) Object.DestroyImmediate(root);
            }

            var overlay = new GameObject(OverlayName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TouchOnly));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(overlay, scene);

            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Behind the HUD: the cards and the results panel must win a shared tap.
            canvas.sortingOrder = -10;

            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            GameObject zone = CreateUI("Tap Zone", overlay.transform, typeof(Image), typeof(Button));
            Stretch(zone.GetComponent<RectTransform>());

            var image = zone.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            var button = zone.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            UnityEventTools.AddPersistentListener(button.onClick, bar.Press);

            EditorUtility.SetDirty(button);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("EgrangTouchUIBuilder: tap zone wired to SkillCheckBar.Press.");
        }
    }
}
```

- [ ] **Step 2: Check the editor assembly can see what it needs**

`Assets/Scripts/Games/Egrang/Editor/Museum.Games.Egrang.Editor.asmdef` must list
`Museum.Games.Egrang`, `Museum.Core` and `Museum.Core.Editor` in `references`. Add whichever are
missing. Expected before this: the generator does not compile.

- [ ] **Step 3: Run the generator**

In the Editor, run `Museum/Rebuild UI/Egrang Touch`. Expected: log
`EgrangTouchUIBuilder: tap zone wired to SkillCheckBar.Press.` and an `Egrang Touch` root object
whose `Tap Zone` button's `onClick` lists `SkillCheckBar.Press`.

- [ ] **Step 4: Verify the desktop path is untouched**

Enter Play mode in Egrang.unity. Expected: the overlay is hidden (scheme `Unknown`), and Space
still steps the bar. Run the full EditMode and PlayMode suites; expected: no regressions.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Games/Egrang/Editor/EgrangTouchUIBuilder.cs* \
        Assets/Scripts/Games/Egrang/Editor/Museum.Games.Egrang.Editor.asmdef Assets/Scenes/Egrang.unity
git commit -m "feat: Egrang step by tap"
```

---

### Task 11: Guard uGUI touch across every scene

**Files:**
- Create: `Assets/Scripts/Core/Tests/EventSystemModuleTests.cs`
- Modify (only if the check fails): the scene whose EventSystem is wrong

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: no runtime API. A test that fails if any build scene's EventSystem stops using
  `InputSystemUIInputModule`.

**What was actually verified, and why this task looks nothing like the spec's Dakon paragraph:**
the spec assumed Dakon is played by tapping 3D holes through `HoverClickRaycaster`. It is not.
Dakon's moves come from `DakonCard`, a uGUI `Button` (`DakonCard.cs:58-62`), and
`HoverClickRaycaster`/`Hoverable` are referenced by **no scene in the project** — they are unused
museum infrastructure. So Dakon needs no layer or collider work at all.

What Dakon and every other uGUI screen do depend on is the EventSystem's input module. The project
runs `activeInputHandler: 2` (both backends), and all five build scenes currently carry
`InputSystemUIInputModule` (script guid `01614664b831546d2ae94a42149d80ac`). Touch already routes
to every Button through it. The real risk is silent regression: someone adds an EventSystem the
default way, gets a `StandaloneInputModule`, and taps stop reaching uGUI on the day the backend is
narrowed to Input System only. That is what this test pins down.

- [ ] **Step 1: Write the failing test**

`Assets/Scripts/Core/Tests/EventSystemModuleTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Museum.Core.Tests
{
    /// <summary>
    /// Every screen a finger touches — Dakon's cards, the lobby, the menus, Egrang's tap zone —
    /// reaches uGUI through the EventSystem's input module. Only InputSystemUIInputModule routes
    /// touch under the Input System backend, so a scene that loses it loses touch silently.
    /// </summary>
    /// <remarks>
    /// Opens each build scene in the Editor rather than at play time: this is a wiring fact about
    /// the scene asset, and an EditMode check reports the scene by name when it breaks.
    /// </remarks>
    public class EventSystemModuleTests
    {
        private static IEnumerable<string> BuildScenePaths()
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) yield return scene.path;
            }
        }

        [Test]
        public void Every_build_scene_routes_uGUI_through_the_input_system_module(
            [ValueSource(nameof(BuildScenePaths))] string scenePath)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                EventSystem eventSystem = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    eventSystem = root.GetComponentInChildren<EventSystem>(true);
                    if (eventSystem != null) break;
                }

                Assert.IsNotNull(eventSystem, $"{scenePath} has no EventSystem — no uGUI input at all.");
                Assert.IsNotNull(eventSystem.GetComponent<InputSystemUIInputModule>(),
                    $"{scenePath}'s EventSystem has no InputSystemUIInputModule. A StandaloneInputModule " +
                    "does not route touch under the Input System backend, so every button in this scene " +
                    "would stop responding on a phone.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it compiles and reports per scene**

Run EditMode filtered to `Museum.Core.Tests.EventSystemModuleTests`.
Expected before the asmdef reference below: FAIL to compile — `Museum.Core.Tests` cannot see
`UnityEditor` or `UnityEngine.InputSystem.UI`.

Add `"Unity.InputSystem"` to the `references` array of
`Assets/Scripts/Core/Tests/Museum.Core.Tests.asmdef`. It already has `"includePlatforms": ["Editor"]`,
so `UnityEditor` is available without a further reference.

- [ ] **Step 3: Run the test again**

Run the same filter. Expected: 5 cases (one per enabled build scene), all PASS — MainMenu, Museum,
Dakon, Lobby and Egrang all carry `InputSystemUIInputModule` today.

- **If a scene fails:** open it, select its EventSystem, and use the inspector's
  "Replace with InputSystemUIInputModule" button that the Input System package offers on a
  `StandaloneInputModule`. Save the scene and re-run.

- [ ] **Step 4: Confirm Dakon by hand**

Enter Play mode in Dakon.unity and click a card. Expected: the move plays as before. This is a
mouse click, but the module that delivers it is the same one that delivers a tap, which is the
whole point of the test above.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Tests/EventSystemModuleTests.cs* Assets/Scripts/Core/Tests/Museum.Core.Tests.asmdef
git commit -m "test: pin every build scene to the Input System UI module"
```

---

### Task 12: The platform picker on MainMenu

**Files:**
- Modify: `Assets/Scripts/Core/MainMenu.cs`
- Modify: `Assets/Scripts/Core/Editor/MainMenuUIBuilder.cs`
- Modify: `Assets/Scenes/MainMenu.unity` (via the builder)
- Test: `Assets/Scripts/Core/Tests/PlayMode/MainMenuPickerTests.cs`

**Interfaces:**
- Consumes: `SessionData.Scheme` (Task 1), `PlatformDetect.LooksLikeTouchDevice()` and
  `PlatformDetect.RequestFullscreen()` (Task 2).
- Produces on `MainMenu`: `public void ChooseTouch()`, `public void ChooseDesktop()` —
  **both are API, wired by name**; plus `public void Configure(GameObject picker, GameObject[] menuObjects, GameObject touchHint, GameObject desktopHint)`
  for tests and the builder.

**Why the menu objects are a list and not a parent panel:** MainMenu's existing hierarchy —
`BG`, `Title`, `Name Input`, `Play Button` as siblings under `Canvas` — survived the asset loss
intact, and `MainMenuUIBuilder`'s doc comment is explicit that it adopts that hierarchy rather
than emitting a rival one. Re-parenting them under a new panel would be exactly the rival
hierarchy it refuses to build. So the picker is a new sibling drawn on top, and the objects it
covers are hidden by reference.

- [ ] **Step 1: Write the failing test**

`Assets/Scripts/Core/Tests/PlayMode/MainMenuPickerTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The first thing a visitor touches. What matters is that their tap — not the browser's
    /// guess — is what ends up on SessionData, and that the menu behind it appears afterwards.
    /// </summary>
    public class MainMenuPickerTests
    {
        private GameObject _bootstrap;
        private GameObject _menuObject;
        private GameObject _picker;
        private GameObject _title;

        private SessionData GivenSession()
        {
            _bootstrap = new GameObject("Bootstrap");
            return _bootstrap.AddComponent<SessionData>();
        }

        private MainMenu GivenMenu()
        {
            _menuObject = new GameObject("MainMenu");
            _picker = new GameObject("Platform Panel");
            _title = new GameObject("Title");

            var menu = _menuObject.AddComponent<MainMenu>();
            menu.Configure(_picker, new[] { _title }, null, null);
            return menu;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in new[] { _menuObject, _picker, _title, _bootstrap })
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void The_picker_is_shown_and_the_menu_hidden_until_a_choice_is_made()
        {
            GivenSession();
            GivenMenu();

            Assert.IsTrue(_picker.activeSelf);
            Assert.IsFalse(_title.activeSelf);
        }

        [Test]
        public void Choosing_touch_writes_the_touch_scheme()
        {
            SessionData session = GivenSession();
            MainMenu menu = GivenMenu();

            menu.ChooseTouch();

            Assert.AreEqual(ControlScheme.Sentuh, session.Scheme);
        }

        [Test]
        public void Choosing_desktop_writes_the_desktop_scheme()
        {
            SessionData session = GivenSession();
            MainMenu menu = GivenMenu();

            menu.ChooseDesktop();

            Assert.AreEqual(ControlScheme.Desktop, session.Scheme);
        }

        [Test]
        public void A_choice_reveals_the_menu_and_dismisses_the_picker()
        {
            GivenSession();
            MainMenu menu = GivenMenu();

            menu.ChooseDesktop();

            Assert.IsFalse(_picker.activeSelf);
            Assert.IsTrue(_title.activeSelf);
        }

        [Test]
        public void Choosing_without_a_SessionData_does_not_throw()
        {
            // MainMenu.unity opened straight from the Editor has no bootstrap object.
            MainMenu menu = GivenMenu();
            Assert.DoesNotThrow(menu.ChooseTouch);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run PlayMode filtered to `Museum.Core.Tests.MainMenuPickerTests`.
Expected: FAIL to compile — `MainMenu.Configure`, `ChooseTouch` and `ChooseDesktop` do not exist.

- [ ] **Step 3: Write minimal implementation**

Replace `Assets/Scripts/Core/MainMenu.cs` with:

```csharp
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// MainMenu's two screens: the platform picker the visitor sees first, and the name-and-play
    /// menu behind it.
    /// </summary>
    /// <remarks>
    /// The picker exists because no browser probe is trustworthy enough to hand someone controls
    /// they cannot use. <see cref="PlatformDetect"/> only decides which of the two buttons wears
    /// the "Disarankan" tag; the tap decides the rest.
    ///
    /// The menu is hidden by a list of objects rather than by one parent panel because MainMenu's
    /// hierarchy survived the 2026-08-19 asset loss intact and MainMenuUIBuilder adopts it rather
    /// than replacing it (ui-style.md §9). Re-parenting those objects would undo that.
    ///
    /// <c>ChooseTouch</c>, <c>ChooseDesktop</c> and <c>GoToMuseum</c> are wired into the scene by
    /// name and are API (CLAUDE.md). Do not rename them.
    /// </remarks>
    public class MainMenu : MonoBehaviour
    {
        [Tooltip("The full-screen 'Kamu main pakai apa?' panel. Shown first, dismissed on a choice.")]
        [SerializeField] private GameObject platformPanel;

        [Tooltip("Everything behind the picker — Title, Name Input, Play Button. Hidden until a choice is made.")]
        [SerializeField] private GameObject[] menuObjects = new GameObject[0];

        [Tooltip("The 'Disarankan' tag on the touch button. Shown only when the browser says touch.")]
        [SerializeField] private GameObject touchHint;

        [Tooltip("The 'Disarankan' tag on the desktop button. Shown only when the browser says desktop.")]
        [SerializeField] private GameObject desktopHint;

        private void Awake()
        {
            ShowPicker();
        }

        /// <summary>Wires the screen from script. The builder writes the same four references.</summary>
        public void Configure(GameObject picker, GameObject[] menu, GameObject touchTag, GameObject desktopTag)
        {
            platformPanel = picker;
            menuObjects = menu ?? new GameObject[0];
            touchHint = touchTag;
            desktopHint = desktopTag;
            ShowPicker();
        }

        /// <summary>UnityEvent target — API, wired by name. Do not rename.</summary>
        public void ChooseTouch() => Choose(ControlScheme.Sentuh);

        /// <summary>UnityEvent target — API, wired by name. Do not rename.</summary>
        public void ChooseDesktop() => Choose(ControlScheme.Desktop);

        /// <summary>UnityEvent target — API, wired by name. Do not rename.</summary>
        public void GoToMuseum()
        {
            SceneLoader.Instance.LoadScene(SceneReference.Museum);
        }

        private void ShowPicker()
        {
            if (platformPanel != null) platformPanel.SetActive(true);
            SetMenuVisible(false);

            bool touch = PlatformDetect.LooksLikeTouchDevice();
            if (touchHint != null) touchHint.SetActive(touch);
            if (desktopHint != null) desktopHint.SetActive(!touch);
        }

        private void Choose(ControlScheme scheme)
        {
            if (SessionData.Instance != null) SessionData.Instance.Scheme = scheme;

            // Fullscreen must be asked for inside a user gesture, and this tap is the first one the
            // page ever gets. Denied or unsupported is fine — PlatformDetect swallows it.
            PlatformDetect.RequestFullscreen();

            if (platformPanel != null) platformPanel.SetActive(false);
            SetMenuVisible(true);
        }

        private void SetMenuVisible(bool visible)
        {
            foreach (GameObject go in menuObjects)
            {
                if (go != null) go.SetActive(visible);
            }
        }
    }
}
```

Then in `Assets/Scripts/Core/Editor/MainMenuUIBuilder.cs`, add a `StylePlatformPicker(canvas, scene)`
call inside `Rebuild()` immediately after `StyleBackground(canvas);`, and add the method:

```csharp
        /// <summary>
        /// Builds the platform picker as a sibling drawn over the menu, and points MainMenu at both
        /// it and the objects it covers. Re-runnable: an existing panel is replaced.
        /// </summary>
        private static void StylePlatformPicker(Transform canvas, UnityEngine.SceneManagement.Scene scene)
        {
            Transform existing = canvas.Find("Platform Panel");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject panel = CreateUI("Platform Panel", canvas, typeof(Image));
            Stretch(panel.GetComponent<RectTransform>());
            var scrim = panel.GetComponent<Image>();
            scrim.sprite = null;
            scrim.color = Scrim;
            scrim.raycastTarget = true;
            panel.transform.SetAsLastSibling();

            AddLabel(panel.transform, "Heading", "Kamu main pakai apa?", DisplayFont, 32f, Cream,
                     new Vector2(0f, 90f), new Vector2(600f, 60f), new Vector2(0.5f, 0.5f));

            GameObject touch = CreateButton("Touch Button", panel.transform, "HP / Layar Sentuh", 260f, 64f, 18f);
            RectTransform touchRect = touch.GetComponent<RectTransform>();
            Anchor(touchRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            touchRect.anchoredPosition = new Vector2(-150f, 0f);

            GameObject desktop = CreateButton("Desktop Button", panel.transform, "Komputer", 260f, 64f, 18f);
            RectTransform desktopRect = desktop.GetComponent<RectTransform>();
            Anchor(desktopRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            desktopRect.anchoredPosition = new Vector2(150f, 0f);

            GameObject touchHint = HintTag(touch.transform);
            GameObject desktopHint = HintTag(desktop.transform);

            MainMenu menu = FindMenu(scene);
            if (menu == null)
            {
                Debug.LogWarning("MainMenuUIBuilder: no MainMenu component — the picker is built but unwired.");
                return;
            }

            UnityEventTools.AddPersistentListener(touch.GetComponent<Button>().onClick, menu.ChooseTouch);
            UnityEventTools.AddPersistentListener(desktop.GetComponent<Button>().onClick, menu.ChooseDesktop);

            var serialized = new SerializedObject(menu);
            serialized.FindProperty("platformPanel").objectReferenceValue = panel;
            serialized.FindProperty("touchHint").objectReferenceValue = touchHint;
            serialized.FindProperty("desktopHint").objectReferenceValue = desktopHint;

            // Everything the picker covers. BG stays on: it is the screen's backdrop, not menu chrome.
            var covered = new System.Collections.Generic.List<Object>();
            foreach (string name in new[] { "Title", "Name Input", "Play Button" })
            {
                Transform found = canvas.Find(name);
                if (found != null) covered.Add(found.gameObject);
                else Debug.LogWarning($"MainMenuUIBuilder: no '{name}' under Canvas to hide behind the picker.");
            }
            FillArray(serialized.FindProperty("menuObjects"), covered.ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(menu);
        }

        /// <summary>The small "Disarankan" tag under a picker button. Toggled at runtime by MainMenu.</summary>
        private static GameObject HintTag(Transform button)
        {
            TMP_Text tag = AddLabel(button, "Disarankan", "Disarankan", MediumFont, 12f, Gold,
                                    new Vector2(0f, -46f), new Vector2(260f, 20f), new Vector2(0.5f, 0.5f));
            return tag.gameObject;
        }
```

- [ ] **Step 4: Run tests to verify they pass, then run the builder**

Run the PlayMode filter. Expected: 5 tests PASS.

Then run `Museum/Rebuild UI/Main Menu` in the Editor and enter Play mode on MainMenu.unity.
Expected: the picker covers the menu, one button wears `Disarankan`, tapping either dismisses the
picker and reveals `Wiraga` / `Nama kamu` / `Mulai`, and `Mulai` still loads the museum.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/MainMenu.cs Assets/Scripts/Core/Editor/MainMenuUIBuilder.cs \
        Assets/Scripts/Core/Tests/PlayMode/MainMenuPickerTests.cs* Assets/Scenes/MainMenu.unity
git commit -m "feat: platform picker as MainMenu's first screen"
```

---

### Task 13: The Wiraga WebGL template

**Files:**
- Create: `Assets/WebGLTemplates/Wiraga/index.html`
- Create: `Assets/WebGLTemplates/Wiraga/style.css`
- Modify: `Assets/Scripts/Editor/BuildWebGL.cs:167-182` (`ApplyPlayerSettings`)

**Interfaces:**
- Consumes: `MuseumRequestFullscreen` in the jslib (Task 2), which reads `window.unityInstance`
  — this template is what assigns it.
- Produces: a template selectable as `PROJECT:Wiraga`, set by the build script.

**Note on `Assets/WebGLTemplates/`:** files under this folder are copied verbatim into the build
and are not imported as assets, so no `.meta` discipline and no code lives here. `{{{ ... }}}`
tokens are substituted by Unity at build time.

- [ ] **Step 1: Write the template**

`Assets/WebGLTemplates/Wiraga/index.html`:

```html
<!DOCTYPE html>
<html lang="id">
  <head>
    <meta charset="utf-8">
    <title>{{{ PRODUCT_NAME }}}</title>
    <!-- user-scalable=no is what stops a double-tap zooming the board mid-move.
         viewport-fit=cover puts the canvas under the notch instead of beside it. -->
    <meta name="viewport"
          content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover">
    <meta name="mobile-web-app-capable" content="yes">
    <meta name="apple-mobile-web-app-capable" content="yes">
    <link rel="stylesheet" href="style.css">
  </head>
  <body>
    <div id="unity-container">
      <canvas id="unity-canvas" tabindex="-1"></canvas>
      <div id="unity-loading">
        <div id="unity-progress-bar"><div id="unity-progress-fill"></div></div>
      </div>
    </div>

    <div id="rotate-overlay">
      <p>Putar HP kamu ke samping</p>
    </div>

    <script>
      var canvas = document.querySelector("#unity-canvas");
      var loading = document.querySelector("#unity-loading");
      var fill = document.querySelector("#unity-progress-fill");

      // A 3x phone screen otherwise asks the GPU for roughly nine times the pixels of a 1x one.
      var pixelRatio = Math.min(window.devicePixelRatio || 1, 2);

      function fitCanvas() {
        canvas.style.width = window.innerWidth + "px";
        canvas.style.height = window.innerHeight + "px";
      }
      fitCanvas();
      window.addEventListener("resize", fitCanvas);
      window.addEventListener("orientationchange", fitCanvas);

      var config = {
        dataUrl: "Build/{{{ DATA_FILENAME }}}",
        frameworkUrl: "Build/{{{ FRAMEWORK_FILENAME }}}",
        codeUrl: "Build/{{{ CODE_FILENAME }}}",
        streamingAssetsUrl: "StreamingAssets",
        companyName: {{{ JSON.stringify(COMPANY_NAME) }}},
        productName: {{{ JSON.stringify(PRODUCT_NAME) }}},
        productVersion: {{{ JSON.stringify(PRODUCT_VERSION) }}},
        devicePixelRatio: pixelRatio
      };

      var script = document.createElement("script");
      script.src = "Build/{{{ LOADER_FILENAME }}}";
      script.onload = function () {
        createUnityInstance(canvas, config, function (progress) {
          fill.style.width = (100 * progress) + "%";
        }).then(function (instance) {
          // MuseumRequestFullscreen in MuseumPlatform.jslib looks for this.
          window.unityInstance = instance;
          loading.style.display = "none";
        }).catch(function (message) {
          loading.textContent = message;
        });
      };
      document.body.appendChild(script);
    </script>
  </body>
</html>
```

`Assets/WebGLTemplates/Wiraga/style.css`:

```css
html, body {
  margin: 0;
  padding: 0;
  width: 100%;
  height: 100%;
  background: #12100e;
  color: #f4ead5;
  font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;

  /* The three rules that keep the page still under a player's thumbs: no scrolling,
     no browser gesture handling on the canvas, no rubber-band overscroll. */
  overflow: hidden;
  touch-action: none;
  overscroll-behavior: none;
}

#unity-container {
  position: fixed;
  inset: 0;
}

#unity-canvas {
  display: block;
  width: 100%;
  height: 100%;
  background: #12100e;
  /* A long press on a canvas otherwise offers to select or save it. */
  -webkit-touch-callout: none;
  -webkit-user-select: none;
  user-select: none;
}

#unity-loading {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
}

#unity-progress-bar {
  width: 60%;
  max-width: 320px;
  height: 6px;
  background: rgba(244, 234, 213, 0.15);
  border-radius: 3px;
  overflow: hidden;
}

#unity-progress-fill {
  width: 0;
  height: 100%;
  background: #c19f61;
}

/* Pure DOM, so it shows before the wasm has loaded — the visitor is told to rotate while
   the download is still running rather than after it. */
#rotate-overlay {
  display: none;
}

@media (orientation: portrait) and (max-width: 900px) {
  #rotate-overlay {
    position: fixed;
    inset: 0;
    z-index: 10;
    display: flex;
    align-items: center;
    justify-content: center;
    background: #12100e;
    font-size: 20px;
    text-align: center;
    padding: 24px;
  }
}
```

- [ ] **Step 2: Point the build at it**

In `Assets/Scripts/Editor/BuildWebGL.cs`, inside `ApplyPlayerSettings`, beside the existing
`PlayerSettings.WebGL.*` assignments:

```csharp
            // The stock template lets the page scroll and pinch-zoom, which eats the gestures the
            // touch scheme needs. Set here rather than left in ProjectSettings for the same reason
            // ServerConfig is an asset: a lost project setting should not change what ships.
            PlayerSettings.WebGL.template = "PROJECT:Wiraga";
```

- [ ] **Step 3: Build and verify the page**

Run `Museum/Build/WebGL (Production)`. Expected: the build succeeds and the output `index.html`
contains `Putar HP kamu ke samping` and `devicePixelRatio`.

```bash
grep -c "Putar HP kamu ke samping" <build output dir>/index.html
```

Expected: `1`.

- [ ] **Step 4: Verify in a desktop browser**

Serve the build and open it. Expected: the game loads and plays exactly as before, the page does
not scroll, and no rotate overlay appears at desktop width.

- [ ] **Step 5: Commit**

```bash
git add Assets/WebGLTemplates Assets/Scripts/Editor/BuildWebGL.cs
git commit -m "feat: Wiraga WebGL template with mobile viewport rules"
```

---

### Task 14: Verify on a real phone

**Files:** none. This task changes nothing; it is the gate before the documentation lands.

**Interfaces:**
- Consumes: everything above.
- Produces: a pass/fail list. Any failure becomes a fix in the task that owns it, not a patch here.

- [ ] **Step 1: Deploy the build**

Follow `Assets/Docs/build-and-deploy.md`. Open `ssh museumvps` in a real terminal first — the
deploy key has a passphrase and rsync needs the ControlMaster session. Never run `rsync --delete`
without both source and destination present.

- [ ] **Step 2: Walk the touch path on a phone**

Open the deployed URL on a phone and check each of these:

- [ ] Held in portrait, the overlay reads `Putar HP kamu ke samping`.
- [ ] Rotating to landscape dismisses it and the canvas fills the screen.
- [ ] The first screen reads `Kamu main pakai apa?` with `HP / Layar Sentuh` marked `Disarankan`.
- [ ] Tapping it reveals `Wiraga`, `Nama kamu` and `Mulai`; typing a name opens the phone keyboard.
- [ ] In the museum: the left thumb moves, dragging on the right looks around, and the page itself never scrolls or zooms under either.
- [ ] Standing in a doorway shows `Ketuk Interaksi`, and the Interaksi button enters it.
- [ ] At a lesson plaque, ‹ and › page it.
- [ ] Dakon: tapping a card plays the move.
- [ ] Egrang: tapping anywhere steps, and the skill-check bar responds.

- [ ] **Step 3: Walk the desktop path on a laptop**

- [ ] The picker marks `Komputer` as `Disarankan`.
- [ ] Choosing it gives pointer lock, WASD, mouse look, Enter at doorways, Q/E at plaques, Space in Egrang — all exactly as before this work.

- [ ] **Step 4: Record what failed**

Write down every unchecked box with the task number that owns it. Fix in that task's files, re-run
that task's tests, and re-verify. Do not proceed to Task 15 with an unchecked box and no note.

- [ ] **Step 5: Commit any fixes**

```bash
git commit -m "fix: <what the phone pass turned up>"
```

---

### Task 15: Documentation

**Files:**
- Create: `Assets/Docs/input-and-platform.md`
- Modify: `Assets/Docs/README.md`, `Assets/Docs/ui-flow.md`, `Assets/Docs/scene-setup.md`,
  `Assets/Docs/build-and-deploy.md`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: everything above.
- Produces: no code.

- [ ] **Step 1: Write the new doc**

`Assets/Docs/input-and-platform.md`, covering:

- The two schemes and the enum's Bahasa member names, and that `Unknown` reads as desktop so a
  scene opened straight from the Editor still works.
- That the visitor chooses on MainMenu's first screen, that `PlatformDetect` may only set the
  `Disarankan` tag, and why: a wrong guess would hand someone controls they cannot use.
- That the scheme lives on `SessionData` and is deliberately not persisted.
- The overlay's parts — floating joystick, look area, Lompat, Interaksi, ‹ ›, Egrang's tap zone —
  and that `TouchOnly` is what hides each of them under the desktop scheme.
- `TouchInteractRouter`: why doorways and plaques register themselves rather than being wired.
- That `ChooseTouch` and `ChooseDesktop` are UnityEvent handler names and therefore API.
- The three menu items that rebuild all of it: `Museum/Rebuild UI/Touch Controls`,
  `Museum/Rebuild UI/Egrang Touch`, `Museum/Rebuild UI/Main Menu`.
- The template's mobile rules and that `BuildWebGL` sets `PROJECT:Wiraga`.
- The known gap: phone performance is untuned, and why that was left out.

- [ ] **Step 2: Update the existing docs**

- `Assets/Docs/README.md` — index line for `input-and-platform.md`.
- `Assets/Docs/ui-flow.md` — the picker as MainMenu's first screen; add `ChooseTouch` and
  `ChooseDesktop` to the list of handler names that are API.
- `Assets/Docs/scene-setup.md` — the `Touch Controls` object in Museum.unity, the `Egrang Touch`
  object in Egrang.unity, the `Platform Panel` in MainMenu.unity, and that all three are generated.
- `Assets/Docs/build-and-deploy.md` — the `Wiraga` template, what it does for mobile, and that the
  build script sets it rather than ProjectSettings.

- [ ] **Step 3: Update CLAUDE.md**

Add `ChooseTouch` and `ChooseDesktop` to the "UnityEvent handler names are API" list, and add one
line to Layout for `Assets/WebGLTemplates/` and `Assets/Plugins/WebGL/`.

- [ ] **Step 4: Verify the docs match the code**

Re-read each claim against the file it describes. Every menu item named must exist; every method
named as API must exist with that exact spelling.

- [ ] **Step 5: Commit**

```bash
git add Assets/Docs CLAUDE.md
git commit -m "docs: control schemes, touch overlay and the Wiraga template"
```

---

## Notes for the executor

- **Never edit `Assets/Scripts/Net/Schema/*.cs`.** Nothing in this plan needs to.
- **`Assets/WebGLTemplates/` content is copied verbatim,** so `{{{ ... }}}` tokens there are
  Unity's, not placeholders for you to fill.
- **If a scene object named in a task is missing,** stop and report it rather than creating a
  rival hierarchy. One museum doorway is already known to be unwired (CLAUDE.md), which is not a
  bug for this plan to fix.
- **Both repos' docs change together** only when the contract changes. Nothing here touches the
  contract, so the server repo is not edited.
