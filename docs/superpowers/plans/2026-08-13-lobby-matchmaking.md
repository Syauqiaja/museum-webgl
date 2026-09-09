# Lobby / Matchmaking Bridge Scene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a generic `Lobby` scene where players enter a nickname, create or join a room by code, see 4 player slots fill up, and the host starts the game — wired for Egrang first.

**Architecture:** UI talks to an `ILobbyService` and re-renders from whole `LobbyRoomSnapshot` objects it never mutates. `FakeLobbyService` implements that interface in memory so everything is playable and unit-testable before the Node server has an `egrang` room; `ColyseusLobbyService` lands later behind the same interface. Slot/host/start rules are pure C# with no `MonoBehaviour` and no Colyseus reference.

**Tech Stack:** Unity 6000.3.19f1, uGUI + TextMeshPro, Input System, LeanTween, Colyseus Unity SDK 0.17.17, Unity Test Framework (EditMode, NUnit).

**Spec:** `docs/superpowers/specs/2026-08-13-lobby-matchmaking-design.md`

## Global Constraints

- **Unity `6000.3.19f1`.** WebGL target. Do not upgrade packages.
- **Namespaces:** `Museum.Core` for `Assets/Scripts/Core/`, `Museum.Lobby` for `Assets/Scripts/Lobby/`, `Museum.Lobby.Tests` for its tests, `Museum.Core.EditorTools` for `Assets/Scripts/Core/Editor/`.
- **New view classes are `sealed`** (`Assets/Docs/ui-style.md` §9).
- **UI rules are not optional** — every screen follows `Assets/Docs/ui-style.md`: Canvas Screen Space–Camera, reference resolution **800×600**, Match **0.5**, Reference Pixels Per Unit **100**; `InputSystemUIInputModule` only (never `StandaloneInputModule`); **TextMeshProUGUI only**, no `UnityEngine.UI.Text`, font asset explicitly assigned on every TMP component, auto-size off; colors only from §5 (Gold `#C19F61`, Cream `#F4EAD5`, Tan `#D8C0A1`, White `#FFFFFF`, Scrim `#000000` @ 63%); frames are `Assets/Sprites/frame_default.png` / `frame_with_corner.png`, Image type **Sliced**, corner radius via Pixels Per Unit Multiplier from the §6 ladder; buttons use the stock ColorTint block unchanged; anything animated gets a `CanvasGroup` + exactly one tween component that plays on `OnEnable`.
- **Pure-C# rule (CLAUDE.md):** game/lobby state classes live outside `MonoBehaviour` so the same logic can move server-side.
- **Never invent server protocol.** Room-name strings and code schemes are owned by the server repo's `protocol.md`. `"egrang"` is used in exactly one place (the `LobbyRequest` the launcher builds) so it is a one-line change when confirmed.
- **Tests are EditMode only**, under a `Tests/` folder with its own asmdef, mirroring `Assets/Scripts/Games/Egrang/Tests/`.
- **Running tests:** use the MCP tool `mcp__UnityMCP__run_tests` with `mode: "EditMode"` and a `test_filter` naming the fixture (e.g. `Museum.Lobby.Tests.LobbySlotTests`). CLI fallback when the Editor is not attached:
  `"/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath "$(pwd)" -runTests -testPlatform EditMode -testFilter "Museum.Lobby.Tests.*" -logFile -`
- **Commit after every task.** Conventional Commits.

---

### Task 1: Player-name rules + `SessionData`

Nickname must survive scene loads and be validated the same way everywhere. Validation is a pure static class so it can be tested without entering Play mode; `SessionData` is the persistent holder.

**Files:**
- Create: `Assets/Scripts/Core/PlayerNameRules.cs`
- Create: `Assets/Scripts/Core/SessionData.cs`
- Create: `Assets/Scripts/Core/Tests/Museum.Core.Tests.asmdef`
- Test: `Assets/Scripts/Core/Tests/PlayerNameRulesTests.cs`
- Modify: `Assets/Scripts/Core/SceneReference.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Museum.Core.PlayerNameRules.MinLength` (int, 2), `MaxLength` (int, 16), `static string Sanitize(string raw)`, `static bool IsValid(string raw)`. `Museum.Core.SessionData.Instance` (static), `SessionData.PlayerName` (string property, sanitized on set), `SessionData.LastRoomCode` (string), `SessionData.PendingLobbyRequest` (object-typed slot is NOT used — it is typed `Museum.Lobby.LobbyRequest` and added in Task 4). `Museum.Core.SceneReference.Lobby` = `"Lobby"`, `SceneReference.Egrang` = `"Egrang"`.

- [ ] **Step 1: Create the test assembly definition**

Create `Assets/Scripts/Core/Tests/Museum.Core.Tests.asmdef`:

```json
{
    "name": "Museum.Core.Tests",
    "rootNamespace": "Museum.Core.Tests",
    "references": [
        "Museum.Core",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
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

- [ ] **Step 2: Write the failing test**

Create `Assets/Scripts/Core/Tests/PlayerNameRulesTests.cs`:

```csharp
using NUnit.Framework;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The nickname is typed by museum visitors on a shared kiosk, so it gets trimmed and
    /// length-checked in one place — MainMenu and the lobby's fallback prompt must agree.
    /// </summary>
    public class PlayerNameRulesTests
    {
        [Test]
        public void Sanitize_TrimsSurroundingWhitespace()
        {
            Assert.That(PlayerNameRules.Sanitize("  Budi  "), Is.EqualTo("Budi"));
        }

        [Test]
        public void Sanitize_CollapsesInnerWhitespaceToSingleSpaces()
        {
            Assert.That(PlayerNameRules.Sanitize("Budi\t\t Santoso"), Is.EqualTo("Budi Santoso"));
        }

        [Test]
        public void Sanitize_TruncatesToMaxLength()
        {
            string sanitized = PlayerNameRules.Sanitize(new string('a', 40));

            Assert.That(sanitized.Length, Is.EqualTo(PlayerNameRules.MaxLength));
        }

        [Test]
        public void Sanitize_NullBecomesEmpty()
        {
            Assert.That(PlayerNameRules.Sanitize(null), Is.EqualTo(string.Empty));
        }

        [Test]
        public void IsValid_RejectsTooShortAndAcceptsAtMinimum()
        {
            Assert.That(PlayerNameRules.IsValid("A"), Is.False, "one character is below the minimum");
            Assert.That(PlayerNameRules.IsValid("  "), Is.False, "whitespace only");
            Assert.That(PlayerNameRules.IsValid(null), Is.False);
            Assert.That(PlayerNameRules.IsValid("Ab"), Is.True, "exactly the minimum");
        }

        [Test]
        public void IsValid_AcceptsAnOverlongNameBecauseSanitizeTruncatesIt()
        {
            Assert.That(PlayerNameRules.IsValid(new string('a', 40)), Is.True);
        }
    }
}
```

- [ ] **Step 3: Run the test and confirm it fails**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_filter: "Museum.Core.Tests.PlayerNameRulesTests"`.
Expected: compile error / failure — `PlayerNameRules` does not exist.

- [ ] **Step 4: Write `PlayerNameRules`**

Create `Assets/Scripts/Core/PlayerNameRules.cs`:

```csharp
using System.Text;

namespace Museum.Core
{
    /// <summary>
    /// The one place a typed nickname is cleaned and checked. Pure C# and free of
    /// UnityEngine so both the MainMenu field and the lobby's fallback prompt can share it
    /// and it can be unit-tested without entering Play mode.
    ///
    /// Validity is judged on the *sanitized* string: an overlong name is not an error, it is
    /// truncated. Only a name that is still too short after trimming is rejected.
    /// </summary>
    public static class PlayerNameRules
    {
        public const int MinLength = 2;
        public const int MaxLength = 16;

        /// <summary>Trim, collapse runs of whitespace to single spaces, truncate to <see cref="MaxLength"/>.</summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(raw.Length);
            bool pendingSpace = false;

            foreach (char c in raw)
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(c);

                if (builder.Length == MaxLength)
                {
                    break;
                }
            }

            return builder.ToString();
        }

        /// <summary>True when <see cref="Sanitize"/> leaves at least <see cref="MinLength"/> characters.</summary>
        public static bool IsValid(string raw) => Sanitize(raw).Length >= MinLength;
    }
}
```

- [ ] **Step 5: Run the test and confirm it passes**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_filter: "Museum.Core.Tests.PlayerNameRulesTests"`.
Expected: 6 tests PASS.

- [ ] **Step 6: Write `SessionData`**

Create `Assets/Scripts/Core/SessionData.cs`:

```csharp
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Persistent per-visit state that must outlive a scene load: the nickname and the last
    /// room code typed. Lives on the same DontDestroyOnLoad bootstrap object as
    /// <see cref="SceneLoader"/> and <see cref="ColyseusNetManager"/>, and follows the same
    /// singleton pattern.
    ///
    /// This is deliberately dumb storage — no networking, no validation policy of its own
    /// beyond running names through <see cref="PlayerNameRules"/> so a bad value can never be
    /// stored in the first place.
    /// </summary>
    public class SessionData : MonoBehaviour
    {
        public static SessionData Instance { get; private set; }

        private string _playerName = string.Empty;

        /// <summary>Nickname, always stored sanitized. Empty until the player enters one.</summary>
        public string PlayerName
        {
            get => _playerName;
            set => _playerName = PlayerNameRules.Sanitize(value);
        }

        /// <summary>True once a usable nickname has been entered; drives the lobby's fallback prompt.</summary>
        public bool HasPlayerName => _playerName.Length >= PlayerNameRules.MinLength;

        /// <summary>Last room code the player created or typed, so a failed join keeps it on screen.</summary>
        public string LastRoomCode { get; set; } = string.Empty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
```

- [ ] **Step 7: Add the new scene names**

Modify `Assets/Scripts/Core/SceneReference.cs` — add two constants alongside the existing ones:

```csharp
        public const string Lobby = "Lobby";
        public const string Egrang = "Egrang";
```

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Core/PlayerNameRules.cs* Assets/Scripts/Core/SessionData.cs* Assets/Scripts/Core/SceneReference.cs Assets/Scripts/Core/Tests
git commit -m "feat: add player-name rules and persistent SessionData"
```

---

### Task 2: Lobby state model (`LobbyPhase`, `LobbySlot`, `LobbyRoomSnapshot`)

The immutable picture of a room that the UI renders. Pure C#, no UnityEngine, no Colyseus — this is the part that will later be built from a synced schema instead of from the fake, without the UI noticing.

**Files:**
- Create: `Assets/Scripts/Lobby/Museum.Lobby.asmdef`
- Create: `Assets/Scripts/Lobby/LobbyPhase.cs`
- Create: `Assets/Scripts/Lobby/LobbySlot.cs`
- Create: `Assets/Scripts/Lobby/LobbyRoomSnapshot.cs`
- Create: `Assets/Scripts/Lobby/Tests/Museum.Lobby.Tests.asmdef`
- Test: `Assets/Scripts/Lobby/Tests/LobbySlotTests.cs`
- Test: `Assets/Scripts/Lobby/Tests/LobbyRoomSnapshotTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Museum.Lobby.LobbyPhase` (`Waiting`, `Starting`, `InProgress`); `LobbySlot` with `SessionId`, `DisplayName`, `IsHost`, `IsEmpty`, `static LobbySlot Empty`, `static LobbySlot Occupied(string sessionId, string displayName, bool isHost)`; `LobbyRoomSnapshot(string code, IReadOnlyList<LobbySlot> slots, LobbyPhase phase, string mySessionId, string hostSessionId)` with properties `Code`, `Slots`, `Phase`, `MySessionId`, `HostSessionId`, `OccupiedCount`, `IsFull`, `AmHost`, `CanStart`, and `const int MinPlayersToStart = 2`.

- [ ] **Step 1: Create the two assembly definitions**

Create `Assets/Scripts/Lobby/Museum.Lobby.asmdef`:

```json
{
    "name": "Museum.Lobby",
    "rootNamespace": "Museum.Lobby",
    "references": [
        "Museum.Core",
        "UnityEngine.UI",
        "Unity.TextMeshPro",
        "Unity.InputSystem"
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

Create `Assets/Scripts/Lobby/Tests/Museum.Lobby.Tests.asmdef`:

```json
{
    "name": "Museum.Lobby.Tests",
    "rootNamespace": "Museum.Lobby.Tests",
    "references": [
        "Museum.Lobby",
        "Museum.Core",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
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

- [ ] **Step 2: Write the failing tests**

Create `Assets/Scripts/Lobby/Tests/LobbySlotTests.cs`:

```csharp
using NUnit.Framework;

namespace Museum.Lobby.Tests
{
    /// <summary>
    /// A slot is either a seated player or a visible "Waiting…" placeholder. The room always
    /// shows all four, so emptiness has to be a first-class state rather than a null.
    /// </summary>
    public class LobbySlotTests
    {
        [Test]
        public void Empty_HasNoSessionIdAndIsEmpty()
        {
            LobbySlot slot = LobbySlot.Empty;

            Assert.That(slot.IsEmpty, Is.True);
            Assert.That(slot.SessionId, Is.Empty);
            Assert.That(slot.IsHost, Is.False);
        }

        [Test]
        public void Occupied_CarriesNameAndHostFlag()
        {
            LobbySlot slot = LobbySlot.Occupied("s1", "Budi", isHost: true);

            Assert.That(slot.IsEmpty, Is.False);
            Assert.That(slot.SessionId, Is.EqualTo("s1"));
            Assert.That(slot.DisplayName, Is.EqualTo("Budi"));
            Assert.That(slot.IsHost, Is.True);
        }
    }
}
```

Create `Assets/Scripts/Lobby/Tests/LobbyRoomSnapshotTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace Museum.Lobby.Tests
{
    /// <summary>
    /// CanStart is the whole start rule in one expression, so the button is never more than a
    /// view of it. The server re-checks the same thing — this is UX, not authority.
    /// </summary>
    public class LobbyRoomSnapshotTests
    {
        static LobbyRoomSnapshot Room(int occupied, LobbyPhase phase = LobbyPhase.Waiting,
            string mySessionId = "s0")
        {
            var slots = new List<LobbySlot>();
            for (int i = 0; i < 4; i++)
            {
                slots.Add(i < occupied
                    ? LobbySlot.Occupied($"s{i}", $"P{i}", isHost: i == 0)
                    : LobbySlot.Empty);
            }

            return new LobbyRoomSnapshot("ABCDE", slots, phase, mySessionId, "s0");
        }

        [Test]
        public void OccupiedCount_CountsOnlySeatedPlayers()
        {
            Assert.That(Room(2).OccupiedCount, Is.EqualTo(2));
            Assert.That(Room(4).OccupiedCount, Is.EqualTo(4));
        }

        [Test]
        public void IsFull_OnlyWhenEverySlotIsTaken()
        {
            Assert.That(Room(3).IsFull, Is.False);
            Assert.That(Room(4).IsFull, Is.True);
        }

        [Test]
        public void CanStart_IsFalseForTheHostAlone()
        {
            Assert.That(Room(1).CanStart, Is.False);
        }

        [Test]
        public void CanStart_IsTrueForTheHostAtTwoPlayers()
        {
            Assert.That(Room(2).CanStart, Is.True);
        }

        [Test]
        public void CanStart_IsFalseForANonHostEvenWithEnoughPlayers()
        {
            Assert.That(Room(3, mySessionId: "s1").CanStart, Is.False);
        }

        [Test]
        public void CanStart_IsFalseOncePhaseHasLeftWaiting()
        {
            Assert.That(Room(4, LobbyPhase.Starting).CanStart, Is.False);
            Assert.That(Room(4, LobbyPhase.InProgress).CanStart, Is.False);
        }

        [Test]
        public void AmHost_ComparesMySessionIdWithTheHostSeat()
        {
            Assert.That(Room(2).AmHost, Is.True);
            Assert.That(Room(2, mySessionId: "s1").AmHost, Is.False);
        }
    }
}
```

- [ ] **Step 3: Run the tests and confirm they fail**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_filter: "Museum.Lobby.Tests"`.
Expected: compile error — `LobbySlot` / `LobbyRoomSnapshot` / `LobbyPhase` do not exist.

- [ ] **Step 4: Write the model**

Create `Assets/Scripts/Lobby/LobbyPhase.cs`:

```csharp
namespace Museum.Lobby
{
    /// <summary>
    /// Room lifecycle as the lobby screen cares about it. <see cref="Starting"/> exists before
    /// there is anything to put in it: when the server drives a countdown, it lands here rather
    /// than forcing a new state through every switch in the UI.
    /// </summary>
    public enum LobbyPhase
    {
        Waiting,
        Starting,
        InProgress
    }
}
```

Create `Assets/Scripts/Lobby/LobbySlot.cs`:

```csharp
namespace Museum.Lobby
{
    /// <summary>
    /// One seat in the room — a seated player or a visible empty chair. The room always renders
    /// every seat, so "empty" is a state rather than a missing entry: a null would make the four
    /// cards jump around as people join and leave.
    ///
    /// Pure C# by design (CLAUDE.md): no MonoBehaviour, no Colyseus type, so the same shape can
    /// be built from a synced schema later without touching the UI.
    /// </summary>
    public readonly struct LobbySlot
    {
        public string SessionId { get; }
        public string DisplayName { get; }
        public bool IsHost { get; }

        private LobbySlot(string sessionId, string displayName, bool isHost)
        {
            SessionId = sessionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            IsHost = isHost;
        }

        public bool IsEmpty => string.IsNullOrEmpty(SessionId);

        public static LobbySlot Empty => new LobbySlot(string.Empty, string.Empty, false);

        public static LobbySlot Occupied(string sessionId, string displayName, bool isHost) =>
            new LobbySlot(sessionId, displayName, isHost);
    }
}
```

Create `Assets/Scripts/Lobby/LobbyRoomSnapshot.cs`:

```csharp
using System.Collections.Generic;

namespace Museum.Lobby
{
    /// <summary>
    /// The whole room in one immutable value. Every UI refresh renders one of these from scratch
    /// rather than patching what is on screen — the same discipline the authoritative synced
    /// schema will demand (Assets/Docs/networking.md §4), adopted now so swapping the fake
    /// service for the real one changes no view code.
    /// </summary>
    public sealed class LobbyRoomSnapshot
    {
        /// <summary>Below this, starting would put a lone player on a race track.</summary>
        public const int MinPlayersToStart = 2;

        public string Code { get; }
        public IReadOnlyList<LobbySlot> Slots { get; }
        public LobbyPhase Phase { get; }
        public string MySessionId { get; }
        public string HostSessionId { get; }

        public LobbyRoomSnapshot(string code, IReadOnlyList<LobbySlot> slots, LobbyPhase phase,
            string mySessionId, string hostSessionId)
        {
            Code = code ?? string.Empty;
            Slots = slots ?? new List<LobbySlot>();
            Phase = phase;
            MySessionId = mySessionId ?? string.Empty;
            HostSessionId = hostSessionId ?? string.Empty;
        }

        public int OccupiedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Slots.Count; i++)
                {
                    if (!Slots[i].IsEmpty)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsFull => OccupiedCount == Slots.Count;

        public bool AmHost => MySessionId.Length > 0 && MySessionId == HostSessionId;

        /// <summary>
        /// Whether *this* client should offer a Start button. Client-side only — the server
        /// validates the same rule; this exists so the button isn't a trap.
        /// </summary>
        public bool CanStart => Phase == LobbyPhase.Waiting && AmHost && OccupiedCount >= MinPlayersToStart;
    }
}
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_filter: "Museum.Lobby.Tests"`.
Expected: 9 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Lobby
git commit -m "feat: add pure-C# lobby room model"
```

---

### Task 3: Room-code rules, error codes, and the service interface

The contract everything else is written against, plus the one piece of typed-input cleanup the join field needs.

**Files:**
- Create: `Assets/Scripts/Lobby/RoomCode.cs`
- Create: `Assets/Scripts/Lobby/LobbyError.cs`
- Create: `Assets/Scripts/Lobby/ILobbyService.cs`
- Test: `Assets/Scripts/Lobby/Tests/RoomCodeTests.cs`

**Interfaces:**
- Consumes: `LobbyRoomSnapshot` (Task 2).
- Produces: `Museum.Lobby.RoomCode.Alphabet` (string), `RoomCode.GeneratedLength` (int, 5), `static string Sanitize(string raw)`, `static bool IsPlausible(string raw)`, `static string Generate(System.Random random)`. `Museum.Lobby.LobbyError.RoomNotFound`/`RoomFull`/`NameInvalid`/`ConnectionFailed`/`NotHost`/`NotEnoughPlayers` (string consts) and `static string MessageFor(string code)`. `Museum.Lobby.ILobbyService` with `Task CreateRoom(string roomName, string displayName, int maxPlayers)`, `Task JoinRoom(string code, string displayName)`, `Task StartGame()`, `Task Leave()`, `event Action<LobbyRoomSnapshot> RoomUpdated`, `event Action<string, string> Failed`, `LobbyRoomSnapshot Current { get; }`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Scripts/Lobby/Tests/RoomCodeTests.cs`:

```csharp
using System;
using NUnit.Framework;

namespace Museum.Lobby.Tests
{
    /// <summary>
    /// Codes get read aloud across a museum room and typed on a kiosk, so the alphabet excludes
    /// the characters people confuse (0/O, 1/I/L) and the input field forgives case, spaces and
    /// dashes. Characters outside the alphabet cannot appear in a real code, so they are dropped
    /// rather than guessed at.
    /// </summary>
    public class RoomCodeTests
    {
        [Test]
        public void Alphabet_ExcludesTheConfusableCharacters()
        {
            foreach (char c in "01IOL")
            {
                Assert.That(RoomCode.Alphabet, Does.Not.Contain(c.ToString()), $"'{c}' is confusable");
            }
        }

        [Test]
        public void Sanitize_UppercasesAndStripsSeparators()
        {
            Assert.That(RoomCode.Sanitize(" a b-c d e "), Is.EqualTo("ABCDE"));
        }

        [Test]
        public void Sanitize_DropsCharactersOutsideTheAlphabet()
        {
            Assert.That(RoomCode.Sanitize("AB0OD"), Is.EqualTo("ABD"));
            Assert.That(RoomCode.Sanitize("!!!"), Is.Empty);
        }

        [Test]
        public void Sanitize_NullBecomesEmpty()
        {
            Assert.That(RoomCode.Sanitize(null), Is.EqualTo(string.Empty));
        }

        [Test]
        public void IsPlausible_RequiresANonEmptySanitizedCode()
        {
            Assert.That(RoomCode.IsPlausible("abcde"), Is.True);
            Assert.That(RoomCode.IsPlausible("0OIL"), Is.False, "every character was dropped");
            Assert.That(RoomCode.IsPlausible(""), Is.False);
        }

        [Test]
        public void Generate_ProducesCodesOfTheRightLengthFromTheAlphabet()
        {
            string code = RoomCode.Generate(new Random(1234));

            Assert.That(code.Length, Is.EqualTo(RoomCode.GeneratedLength));
            foreach (char c in code)
            {
                Assert.That(RoomCode.Alphabet, Does.Contain(c.ToString()));
            }
        }
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_filter: "Museum.Lobby.Tests.RoomCodeTests"`.
Expected: compile error — `RoomCode` does not exist.

- [ ] **Step 3: Write `RoomCode`**

Create `Assets/Scripts/Lobby/RoomCode.cs`:

```csharp
using System;
using System.Text;

namespace Museum.Lobby
{
    /// <summary>
    /// Room-code alphabet and input cleanup. The real codes are generated **by the server** —
    /// this exists so the join field forgives how people actually type (lowercase, spaces,
    /// dashes) and so <see cref="FakeLobbyService"/> can mint codes of the same shape offline.
    ///
    /// The alphabet drops 0/O and 1/I/L because a code is read off a screen and typed by someone
    /// else across the room. Characters outside it cannot occur in a real code, so
    /// <see cref="Sanitize"/> drops them instead of inventing a mapping: there is no honest
    /// answer to "which letter did they mean by O".
    ///
    /// Length is not validated here — the server owns the code scheme
    /// (Assets/Docs/networking.md "Open items"), so the client only checks that something usable
    /// is left after cleanup and lets the server reject the rest.
    /// </summary>
    public static class RoomCode
    {
        public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

        /// <summary>Length <see cref="Generate"/> mints. Mirrors the server's 5–6 char scheme (CLAUDE.md).</summary>
        public const int GeneratedLength = 5;

        /// <summary>Uppercase, then keep only alphabet characters.</summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(raw.Length);

            foreach (char c in raw)
            {
                char upper = char.ToUpperInvariant(c);
                if (Alphabet.IndexOf(upper) >= 0)
                {
                    builder.Append(upper);
                }
            }

            return builder.ToString();
        }

        /// <summary>True when anything survives <see cref="Sanitize"/> — worth sending to the server.</summary>
        public static bool IsPlausible(string raw) => Sanitize(raw).Length > 0;

        /// <summary>Offline stand-in for the server's generator. Only <see cref="FakeLobbyService"/> uses it.</summary>
        public static string Generate(Random random)
        {
            var builder = new StringBuilder(GeneratedLength);

            for (int i = 0; i < GeneratedLength; i++)
            {
                builder.Append(Alphabet[random.Next(Alphabet.Length)]);
            }

            return builder.ToString();
        }
    }
}
```

- [ ] **Step 4: Run the test and confirm it passes**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_filter: "Museum.Lobby.Tests.RoomCodeTests"`.
Expected: 6 tests PASS.

- [ ] **Step 5: Write the error codes**

Create `Assets/Scripts/Lobby/LobbyError.cs`:

```csharp
namespace Museum.Lobby
{
    /// <summary>
    /// Failure codes the lobby can raise, and the player-facing text for each. The first four
    /// mirror the shared server codes in <see cref="Museum.Core.ErrorCode"/> and the failure
    /// table in Assets/Docs/ui-flow.md; the last two are client-side guards that never reach the
    /// wire.
    ///
    /// Codes, not sentences, cross the service boundary — the wording lives here so it is changed
    /// in one place and can be localised later.
    /// </summary>
    public static class LobbyError
    {
        public const string RoomNotFound = "room_not_found";
        public const string RoomFull = "room_full";
        public const string NameInvalid = "name_invalid";
        public const string ConnectionFailed = "connection_failed";
        public const string NotHost = "not_host";
        public const string NotEnoughPlayers = "not_enough_players";

        public static string MessageFor(string code)
        {
            switch (code)
            {
                case RoomNotFound: return "Room not found";
                case RoomFull: return "Room is full";
                case NameInvalid: return "Enter a name (2–16 characters)";
                case ConnectionFailed: return "Can't reach the server";
                case NotHost: return "Only the host can start";
                case NotEnoughPlayers: return "Need at least 2 players";
                default: return "Something went wrong";
            }
        }
    }
}
```

- [ ] **Step 6: Write the service interface**

Create `Assets/Scripts/Lobby/ILobbyService.cs`:

```csharp
using System;
using System.Threading.Tasks;

namespace Museum.Lobby
{
    /// <summary>
    /// Everything the lobby screen can ask of a room, with no Colyseus type in sight. Two
    /// implementations: <see cref="FakeLobbyService"/> (in memory, playable and testable before
    /// the server has an `egrang` room) and ColyseusLobbyService (later, once the server's
    /// protocol.md defines the lobby state).
    ///
    /// Contract:
    /// - Every successful call raises <see cref="RoomUpdated"/> with a whole new snapshot.
    ///   Callers render from that snapshot and never mutate one.
    /// - Every failure raises <see cref="Failed"/> with a <see cref="LobbyError"/> code and does
    ///   NOT raise <see cref="RoomUpdated"/>. Failures are reported through the event rather than
    ///   thrown, so the UI has one path for "the server said no" whether it came from a local
    ///   guard or the wire.
    /// </summary>
    public interface ILobbyService
    {
        /// <summary>Latest snapshot, or null before a room is created or joined.</summary>
        LobbyRoomSnapshot Current { get; }

        event Action<LobbyRoomSnapshot> RoomUpdated;

        /// <summary>(code, message) — code is a <see cref="LobbyError"/> constant.</summary>
        event Action<string, string> Failed;

        /// <summary>Host flow. The room code is generated on the far side and arrives in the snapshot.</summary>
        Task CreateRoom(string roomName, string displayName, int maxPlayers);

        /// <summary>Join an existing room by its shareable code.</summary>
        Task JoinRoom(string code, string displayName);

        /// <summary>Host-only. Moves the room to <see cref="LobbyPhase.InProgress"/>.</summary>
        Task StartGame();

        /// <summary>Explicit leave — never just drop the connection (Assets/Docs/networking.md §8).</summary>
        Task Leave();
    }
}
```

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Lobby
git commit -m "feat: add room-code rules, lobby error codes and ILobbyService"
```

---

### Task 4: `FakeLobbyService` and `LobbyRequest`

The in-memory room. This is what makes the whole screen playable and every failure path exercisable with no server running, and it doubles as the written contract the Node room must satisfy.

**Files:**
- Create: `Assets/Scripts/Lobby/LobbyRequest.cs`
- Create: `Assets/Scripts/Lobby/FakeLobbyService.cs`
- Test: `Assets/Scripts/Lobby/Tests/FakeLobbyServiceTests.cs`

**Interfaces:**
- Consumes: `ILobbyService`, `LobbyError`, `RoomCode`, `LobbyRoomSnapshot`, `LobbySlot`, `LobbyPhase` (Tasks 2–3).
- Produces: `Museum.Lobby.LobbyRequest` with `RoomName`, `MaxPlayers`, `GameScene` (all get-only, set via constructor `LobbyRequest(string roomName, int maxPlayers, string gameScene)`) and `static LobbyRequest Pending` (static slot the launcher writes and the lobby reads). `Museum.Lobby.FakeLobbyService` implementing `ILobbyService`, plus test/dev-only helpers `string AddSimulatedPlayer(string displayName)`, `void RemoveSimulatedPlayer(string sessionId)`, `void ForceNextFailure(string errorCode)`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Scripts/Lobby/Tests/FakeLobbyServiceTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace Museum.Lobby.Tests
{
    /// <summary>
    /// The fake is not a throwaway mock — it is the behaviour the Node room has to match, written
    /// down and executable. These tests are therefore the lobby contract: seat assignment, the
    /// full-room rejection, host migration when the host walks away.
    /// </summary>
    public class FakeLobbyServiceTests
    {
        FakeLobbyService _service;
        List<LobbyRoomSnapshot> _snapshots;
        List<string> _errors;

        [SetUp]
        public void SetUp()
        {
            _service = new FakeLobbyService(seed: 1234);
            _snapshots = new List<LobbyRoomSnapshot>();
            _errors = new List<string>();
            _service.RoomUpdated += s => _snapshots.Add(s);
            _service.Failed += (code, _) => _errors.Add(code);
        }

        void CreateRoom() => _service.CreateRoom("egrang", "Budi", 4).GetAwaiter().GetResult();

        [Test]
        public void CreateRoom_SeatsTheCreatorAsHostInTheFirstSlot()
        {
            CreateRoom();

            LobbyRoomSnapshot room = _service.Current;
            Assert.That(room.Code, Is.Not.Empty);
            Assert.That(room.Slots.Count, Is.EqualTo(4));
            Assert.That(room.Slots[0].DisplayName, Is.EqualTo("Budi"));
            Assert.That(room.Slots[0].IsHost, Is.True);
            Assert.That(room.Slots[1].IsEmpty, Is.True);
            Assert.That(room.AmHost, Is.True);
            Assert.That(room.Phase, Is.EqualTo(LobbyPhase.Waiting));
            Assert.That(_snapshots, Has.Count.EqualTo(1), "creating raises exactly one update");
        }

        [Test]
        public void CreateRoom_WithAnUnusableName_FailsAndCreatesNothing()
        {
            _service.CreateRoom("egrang", " ", 4).GetAwaiter().GetResult();

            Assert.That(_errors, Is.EqualTo(new[] { LobbyError.NameInvalid }));
            Assert.That(_service.Current, Is.Null);
            Assert.That(_snapshots, Is.Empty);
        }

        [Test]
        public void AddSimulatedPlayer_FillsTheLowestEmptySlotAndRaisesAnUpdate()
        {
            CreateRoom();

            _service.AddSimulatedPlayer("Sari");

            Assert.That(_service.Current.Slots[1].DisplayName, Is.EqualTo("Sari"));
            Assert.That(_service.Current.Slots[1].IsHost, Is.False);
            Assert.That(_service.Current.OccupiedCount, Is.EqualTo(2));
            Assert.That(_snapshots, Has.Count.EqualTo(2));
        }

        [Test]
        public void AddSimulatedPlayer_BeyondCapacity_FailsWithRoomFull()
        {
            CreateRoom();
            _service.AddSimulatedPlayer("Sari");
            _service.AddSimulatedPlayer("Andi");
            _service.AddSimulatedPlayer("Rina");

            _service.AddSimulatedPlayer("Fifth");

            Assert.That(_service.Current.IsFull, Is.True);
            Assert.That(_errors, Is.EqualTo(new[] { LobbyError.RoomFull }));
        }

        [Test]
        public void RemoveSimulatedPlayer_FreesTheSlot()
        {
            CreateRoom();
            string sessionId = _service.AddSimulatedPlayer("Sari");

            _service.RemoveSimulatedPlayer(sessionId);

            Assert.That(_service.Current.Slots[1].IsEmpty, Is.True);
            Assert.That(_service.Current.OccupiedCount, Is.EqualTo(1));
        }

        [Test]
        public void RemovingTheHost_MigratesHostToTheLowestRemainingSeat()
        {
            CreateRoom();
            _service.AddSimulatedPlayer("Sari");
            string hostSessionId = _service.Current.HostSessionId;

            _service.RemoveSimulatedPlayer(hostSessionId);

            Assert.That(_service.Current.Slots[0].IsEmpty, Is.True);
            Assert.That(_service.Current.Slots[1].IsHost, Is.True);
            Assert.That(_service.Current.HostSessionId, Is.EqualTo(_service.Current.Slots[1].SessionId));
        }

        [Test]
        public void JoinRoom_WithAnUnknownCode_FailsWithRoomNotFound()
        {
            _service.JoinRoom("ZZZZZ", "Budi").GetAwaiter().GetResult();

            Assert.That(_errors, Is.EqualTo(new[] { LobbyError.RoomNotFound }));
            Assert.That(_service.Current, Is.Null);
        }

        [Test]
        public void JoinRoom_WithTheCodeOfTheRoomJustCreated_SeatsTheJoinerAsNonHost()
        {
            CreateRoom();
            string code = _service.Current.Code;
            _service.Leave().GetAwaiter().GetResult();

            _service.JoinRoom(code, "Sari").GetAwaiter().GetResult();

            Assert.That(_errors, Is.Empty);
            Assert.That(_service.Current.Code, Is.EqualTo(code));
            Assert.That(_service.Current.AmHost, Is.False, "a joiner is never the host");
            Assert.That(_service.Current.OccupiedCount, Is.EqualTo(2), "the simulated host is still seated");
        }

        [Test]
        public void StartGame_BelowTheMinimum_FailsAndLeavesThePhaseAlone()
        {
            CreateRoom();

            _service.StartGame().GetAwaiter().GetResult();

            Assert.That(_errors, Is.EqualTo(new[] { LobbyError.NotEnoughPlayers }));
            Assert.That(_service.Current.Phase, Is.EqualTo(LobbyPhase.Waiting));
        }

        [Test]
        public void StartGame_AsHostWithTwoPlayers_MovesThePhaseToInProgress()
        {
            CreateRoom();
            _service.AddSimulatedPlayer("Sari");

            _service.StartGame().GetAwaiter().GetResult();

            Assert.That(_errors, Is.Empty);
            Assert.That(_service.Current.Phase, Is.EqualTo(LobbyPhase.InProgress));
        }

        [Test]
        public void Leave_ClearsTheCurrentRoom()
        {
            CreateRoom();

            _service.Leave().GetAwaiter().GetResult();

            Assert.That(_service.Current, Is.Null);
        }

        [Test]
        public void ForceNextFailure_MakesTheNextCallFailWithThatCode()
        {
            _service.ForceNextFailure(LobbyError.ConnectionFailed);

            CreateRoom();

            Assert.That(_errors, Is.EqualTo(new[] { LobbyError.ConnectionFailed }));
            Assert.That(_service.Current, Is.Null);
            Assert.That(_service.Current, Is.Null, "a forced failure creates nothing");
        }
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_filter: "Museum.Lobby.Tests.FakeLobbyServiceTests"`.
Expected: compile error — `FakeLobbyService` does not exist.

- [ ] **Step 3: Write `LobbyRequest`**

Create `Assets/Scripts/Lobby/LobbyRequest.cs`:

```csharp
namespace Museum.Lobby
{
    /// <summary>
    /// What the lobby is being opened *for*: which server room to talk to, how many seats, and
    /// which scene to load once the game starts. The launcher (a MainMenu button, later a museum
    /// launch point) fills this in and loads the Lobby scene; the lobby reads it on start.
    ///
    /// This is the only place a room-name string appears, so confirming `"egrang"` against the
    /// server's protocol.md later is a one-line change (Assets/Docs/networking.md "Open items").
    ///
    /// Held in a static rather than passed through the scene load because Unity's scene API has
    /// no parameter channel; <see cref="Pending"/> is written immediately before
    /// SceneLoader.LoadScene and read in the lobby's Start.
    /// </summary>
    public sealed class LobbyRequest
    {
        /// <summary>Set by the launcher just before loading the Lobby scene.</summary>
        public static LobbyRequest Pending { get; set; }

        public string RoomName { get; }
        public int MaxPlayers { get; }
        public string GameScene { get; }

        public LobbyRequest(string roomName, int maxPlayers, string gameScene)
        {
            RoomName = roomName;
            MaxPlayers = maxPlayers;
            GameScene = gameScene;
        }
    }
}
```

- [ ] **Step 4: Write `FakeLobbyService`**

Create `Assets/Scripts/Lobby/FakeLobbyService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Museum.Lobby
{
    /// <summary>
    /// An in-memory room. It exists because the Node server has no `egrang` room yet
    /// (Assets/Docs/games/egrang.md is a stub) and the lobby screen should not wait on it: with
    /// this, all four slots, host migration, the full-room rejection and every error toast are
    /// playable and unit-testable with nothing running.
    ///
    /// It is also the contract. Whatever the server room ends up doing, it has to agree with
    /// FakeLobbyServiceTests — that is the point of writing the rules down here rather than
    /// inside a MonoBehaviour.
    ///
    /// Not thread-safe and not meant to be: everything happens on Unity's main thread. The
    /// methods are async only to match <see cref="ILobbyService"/>, whose real implementation
    /// awaits the network.
    /// </summary>
    public sealed class FakeLobbyService : ILobbyService
    {
        // The one room this fake knows about. A single fake client can only be in one room, and
        // simulated peers exist only inside it.
        private List<LobbySlot> _slots;
        private string _code = string.Empty;
        private string _mySessionId = string.Empty;
        private string _hostSessionId = string.Empty;
        private LobbyPhase _phase = LobbyPhase.Waiting;
        private bool _inRoom;

        private readonly Random _random;
        private int _nextSessionNumber;
        private string _forcedFailure;

        public FakeLobbyService(int seed = 0)
        {
            _random = seed == 0 ? new Random() : new Random(seed);
        }

        public LobbyRoomSnapshot Current { get; private set; }

        public event Action<LobbyRoomSnapshot> RoomUpdated;
        public event Action<string, string> Failed;

        public Task CreateRoom(string roomName, string displayName, int maxPlayers)
        {
            if (ConsumeForcedFailure() || !SeatIsNamed(displayName))
            {
                return Task.CompletedTask;
            }

            _slots = new List<LobbySlot>(maxPlayers);
            for (int i = 0; i < maxPlayers; i++)
            {
                _slots.Add(LobbySlot.Empty);
            }

            _code = RoomCode.Generate(_random);
            _phase = LobbyPhase.Waiting;
            _inRoom = true;

            _mySessionId = NextSessionId();
            _hostSessionId = _mySessionId;
            _slots[0] = LobbySlot.Occupied(_mySessionId, Museum.Core.PlayerNameRules.Sanitize(displayName), true);

            Publish();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Joining a code the fake did not mint cannot work — there is no directory of rooms to
        /// look it up in. Only the code from a room this instance created is accepted, which is
        /// enough to walk the join screen; two real clients need the server.
        /// </summary>
        public Task JoinRoom(string code, string displayName)
        {
            if (ConsumeForcedFailure() || !SeatIsNamed(displayName))
            {
                return Task.CompletedTask;
            }

            string sanitized = RoomCode.Sanitize(code);
            if (_slots == null || sanitized.Length == 0 || sanitized != _code)
            {
                Fail(LobbyError.RoomNotFound);
                return Task.CompletedTask;
            }

            int seat = FirstEmptySeat();
            if (seat < 0)
            {
                Fail(LobbyError.RoomFull);
                return Task.CompletedTask;
            }

            _inRoom = true;
            _mySessionId = NextSessionId();
            _slots[seat] = LobbySlot.Occupied(_mySessionId, Museum.Core.PlayerNameRules.Sanitize(displayName), false);

            Publish();
            return Task.CompletedTask;
        }

        public Task StartGame()
        {
            if (ConsumeForcedFailure() || Current == null)
            {
                return Task.CompletedTask;
            }

            if (!Current.AmHost)
            {
                Fail(LobbyError.NotHost);
                return Task.CompletedTask;
            }

            if (Current.OccupiedCount < LobbyRoomSnapshot.MinPlayersToStart)
            {
                Fail(LobbyError.NotEnoughPlayers);
                return Task.CompletedTask;
            }

            _phase = LobbyPhase.InProgress;
            Publish();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Vacates this client's seat but keeps the room and its simulated peers alive, so the
        /// create-then-join walkthrough works: leave the room you made, join it back by code.
        /// </summary>
        public Task Leave()
        {
            if (_inRoom && _slots != null)
            {
                Vacate(_mySessionId);
            }

            _inRoom = false;
            _mySessionId = string.Empty;
            Current = null;
            return Task.CompletedTask;
        }

        // ---- dev / test helpers: stand in for other people joining ----

        /// <summary>Seat a simulated peer. Returns their session id, or empty if the room was full.</summary>
        public string AddSimulatedPlayer(string displayName)
        {
            if (_slots == null)
            {
                Fail(LobbyError.RoomNotFound);
                return string.Empty;
            }

            int seat = FirstEmptySeat();
            if (seat < 0)
            {
                Fail(LobbyError.RoomFull);
                return string.Empty;
            }

            string sessionId = NextSessionId();
            _slots[seat] = LobbySlot.Occupied(sessionId, Museum.Core.PlayerNameRules.Sanitize(displayName),
                isHost: _hostSessionId.Length == 0);

            if (_hostSessionId.Length == 0)
            {
                _hostSessionId = sessionId;
            }

            Publish();
            return sessionId;
        }

        /// <summary>Remove a seated player by session id, migrating the host if they were it.</summary>
        public void RemoveSimulatedPlayer(string sessionId)
        {
            if (_slots == null)
            {
                return;
            }

            Vacate(sessionId);
            Publish();
        }

        /// <summary>Make the next Create/Join/Start fail with this code, to exercise the error toasts.</summary>
        public void ForceNextFailure(string errorCode)
        {
            _forcedFailure = errorCode;
        }

        // ---- internals ----

        private bool SeatIsNamed(string displayName)
        {
            if (Museum.Core.PlayerNameRules.IsValid(displayName))
            {
                return true;
            }

            Fail(LobbyError.NameInvalid);
            return false;
        }

        private bool ConsumeForcedFailure()
        {
            if (_forcedFailure == null)
            {
                return false;
            }

            string code = _forcedFailure;
            _forcedFailure = null;
            Fail(code);
            return true;
        }

        private int FirstEmptySeat()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }

        private void Vacate(string sessionId)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].SessionId == sessionId)
                {
                    _slots[i] = LobbySlot.Empty;
                    break;
                }
            }

            if (_hostSessionId != sessionId)
            {
                return;
            }

            // Host left: the lowest remaining seat inherits it, so the room never becomes
            // unstartable just because the creator closed their tab.
            _hostSessionId = string.Empty;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsEmpty)
                {
                    _hostSessionId = _slots[i].SessionId;
                    _slots[i] = LobbySlot.Occupied(_slots[i].SessionId, _slots[i].DisplayName, true);
                    break;
                }
            }
        }

        private string NextSessionId() => $"sim{++_nextSessionNumber}";

        private void Publish()
        {
            Current = new LobbyRoomSnapshot(_code, new List<LobbySlot>(_slots), _phase, _mySessionId,
                _hostSessionId);
            RoomUpdated?.Invoke(Current);
        }

        private void Fail(string code)
        {
            Failed?.Invoke(code, LobbyError.MessageFor(code));
        }
    }
}
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_filter: "Museum.Lobby.Tests"`.
Expected: all 27 tests across the four fixtures PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Lobby
git commit -m "feat: add in-memory FakeLobbyService and LobbyRequest"
```

---

### Task 5: `ColyseusLobbyService` seam

A named place for the real implementation so the swap point is visible in the codebase rather than implied by a design doc. It must not pretend to work.

**Files:**
- Create: `Assets/Scripts/Lobby/ColyseusLobbyService.cs`

**Interfaces:**
- Consumes: `ILobbyService`, `LobbyError` (Task 3).
- Produces: `Museum.Lobby.ColyseusLobbyService` implementing `ILobbyService`; every method raises `Failed(LobbyError.ConnectionFailed, …)`.

- [ ] **Step 1: Write the stub**

Create `Assets/Scripts/Lobby/ColyseusLobbyService.cs`:

```csharp
using System;
using System.Threading.Tasks;

namespace Museum.Lobby
{
    /// <summary>
    /// The real transport — **not implemented yet, deliberately**. The server has no `egrang`
    /// room and its protocol.md has no lobby state, and Assets/Docs/networking.md is explicit
    /// that a message not in the server contract must not be invented client-side. So this seat
    /// stays empty until the server side lands.
    ///
    /// When it does, this class wires ColyseusNetManager.CreateRoom / JoinRoomById, attaches the
    /// schema callbacks, and maps the synced state into <see cref="LobbyRoomSnapshot"/>. Nothing
    /// above this line changes: LobbyController already renders whole snapshots and never mutates
    /// one.
    ///
    /// It fails loudly rather than silently doing nothing — a lobby that quietly sits at
    /// "Waiting…" would look like a network problem.
    /// </summary>
    public sealed class ColyseusLobbyService : ILobbyService
    {
        private const string NotReady =
            "Online play isn't available yet — the server room hasn't been built.";

        public LobbyRoomSnapshot Current => null;

        public event Action<LobbyRoomSnapshot> RoomUpdated;
        public event Action<string, string> Failed;

        public Task CreateRoom(string roomName, string displayName, int maxPlayers) => NotImplementedYet();

        public Task JoinRoom(string code, string displayName) => NotImplementedYet();

        public Task StartGame() => NotImplementedYet();

        public Task Leave() => Task.CompletedTask;

        private Task NotImplementedYet()
        {
            // Referenced so the compiler doesn't warn about an event that is never raised; the
            // real implementation raises it on every state patch.
            _ = RoomUpdated;
            Failed?.Invoke(LobbyError.ConnectionFailed, NotReady);
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Confirm the project still compiles**

Run `mcp__UnityMCP__read_console` with `types: ["error"]` after Unity recompiles.
Expected: no compile errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Lobby/ColyseusLobbyService.cs*
git commit -m "feat: add ColyseusLobbyService seam pending server room"
```

---

### Task 6: `LobbySlotView` and `LobbyController`

The behaviour of the screen. `LobbySlotView` is presentational (`Assets/Docs/ui-style.md` §9); `LobbyController` owns the panel switching and binds service events.

**Files:**
- Create: `Assets/Scripts/Lobby/LobbySlotView.cs`
- Create: `Assets/Scripts/Lobby/LobbyController.cs`

**Interfaces:**
- Consumes: `ILobbyService`, `FakeLobbyService`, `ColyseusLobbyService`, `LobbyRequest`, `LobbyRoomSnapshot`, `LobbySlot`, `LobbyPhase`, `LobbyError`, `RoomCode` (Tasks 2–5); `Museum.Core.SessionData`, `PlayerNameRules`, `SceneLoader`, `SceneReference` (Task 1).
- Produces: `Museum.Lobby.LobbySlotView` with `void Render(LobbySlot slot, int seatNumber, bool isMe)`. `Museum.Lobby.LobbyController` — a `MonoBehaviour` with serialized fields wired by the builder in Task 8; public methods `ConfirmName()`, `CreateRoom()`, `JoinRoom()`, `StartGame()`, `LeaveRoom()`, `CopyCode()`, `BackToMainMenu()`.

- [ ] **Step 1: Write `LobbySlotView`**

Create `Assets/Scripts/Lobby/LobbySlotView.cs`:

```csharp
using TMPro;
using UnityEngine;

namespace Museum.Lobby
{
    /// <summary>
    /// One of the four seats. Purely presentational: it renders the slot it is handed and owns
    /// nothing (Assets/Docs/ui-style.md §9) — same shape as EgrangStickCard.
    ///
    /// An empty seat is drawn, not hidden. Four visible chairs tell a player how many more people
    /// the race is waiting for; collapsing the list would not.
    /// </summary>
    public sealed class LobbySlotView : MonoBehaviour
    {
        [Header("Text")]
        [Tooltip("Player's nickname, or the waiting placeholder when the seat is empty.")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("Seat number, e.g. \"Pemain 2\".")]
        [SerializeField] private TMP_Text seatText;

        [Header("Badges")]
        [Tooltip("Shown on the host's seat only. Optional.")]
        [SerializeField] private GameObject hostBadge;
        [Tooltip("Shown on this client's own seat only. Optional.")]
        [SerializeField] private GameObject youBadge;

        [Header("Copy")]
        [SerializeField] private string emptyLabel = "Menunggu…";

        public void Render(LobbySlot slot, int seatNumber, bool isMe)
        {
            if (seatText != null)
            {
                seatText.text = $"Pemain {seatNumber}";
            }

            if (nameText != null)
            {
                nameText.text = slot.IsEmpty ? emptyLabel : slot.DisplayName;
            }

            if (hostBadge != null)
            {
                hostBadge.SetActive(!slot.IsEmpty && slot.IsHost);
            }

            if (youBadge != null)
            {
                youBadge.SetActive(!slot.IsEmpty && isMe);
            }
        }
    }
}
```

- [ ] **Step 2: Write `LobbyController`**

Create `Assets/Scripts/Lobby/LobbyController.cs`:

```csharp
using System.Collections.Generic;
using Museum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Museum.Lobby
{
    /// <summary>
    /// Drives the lobby screen: which panel is showing, and turning service events into rendered
    /// slots. It never touches Colyseus and never mutates a slot — every refresh re-renders a
    /// whole <see cref="LobbyRoomSnapshot"/> handed to it by <see cref="ILobbyService"/>, which
    /// is what lets the fake service be swapped for the real one without editing this file.
    ///
    /// The name panel is a fallback, not the main way in: the nickname is normally typed on
    /// MainMenu. It exists because a museum launch point can drop a visitor straight here with
    /// no name set, and a create/join screen with an empty name is a dead end.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        [Header("Service")]
        [Tooltip("Use the in-memory fake. Turn off only once the server has the room (see ColyseusLobbyService).")]
        [SerializeField] private bool useFakeService = true;

        [Header("Fallback request")]
        [Tooltip("Used when the scene is opened directly in the Editor with no LobbyRequest set.")]
        [SerializeField] private string fallbackRoomName = "egrang";
        [SerializeField] private int fallbackMaxPlayers = 4;
        [SerializeField] private string fallbackGameScene = SceneReference.Egrang;

        [Header("Panels")]
        [SerializeField] private GameObject namePanel;
        [SerializeField] private GameObject entryPanel;
        [SerializeField] private GameObject roomPanel;

        [Header("Name panel")]
        [SerializeField] private TMP_InputField nameInput;

        [Header("Entry panel")]
        [SerializeField] private TMP_InputField codeInput;

        [Header("Room panel")]
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private LobbySlotView slotPrefab;
        [SerializeField] private Button startButton;

        [Header("Toast")]
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private float toastSeconds = 2.5f;

        private ILobbyService _service;
        private LobbyRequest _request;
        private readonly List<LobbySlotView> _slotViews = new List<LobbySlotView>();
        private float _toastHideAt;
        private bool _loadingGame;

        private void Start()
        {
            _request = LobbyRequest.Pending ??
                new LobbyRequest(fallbackRoomName, fallbackMaxPlayers, fallbackGameScene);

            _service = useFakeService ? (ILobbyService)new FakeLobbyService() : new ColyseusLobbyService();
            _service.RoomUpdated += OnRoomUpdated;
            _service.Failed += OnFailed;

            BuildSlotViews(_request.MaxPlayers);

            if (toastRoot != null)
            {
                toastRoot.SetActive(false);
            }

            if (codeInput != null)
            {
                codeInput.text = SessionData.Instance != null ? SessionData.Instance.LastRoomCode : string.Empty;
            }

            bool named = SessionData.Instance != null && SessionData.Instance.HasPlayerName;
            ShowPanel(named ? entryPanel : namePanel);
        }

        private void OnDestroy()
        {
            if (_service == null)
            {
                return;
            }

            _service.RoomUpdated -= OnRoomUpdated;
            _service.Failed -= OnFailed;
        }

        private void Update()
        {
            if (toastRoot != null && toastRoot.activeSelf && Time.unscaledTime >= _toastHideAt)
            {
                toastRoot.SetActive(false);
            }
        }

        // ---- button handlers ----

        /// <summary>Name panel → Entry panel. Wired to the confirm button.</summary>
        public void ConfirmName()
        {
            string typed = nameInput != null ? nameInput.text : string.Empty;

            if (!PlayerNameRules.IsValid(typed))
            {
                ShowToast(LobbyError.MessageFor(LobbyError.NameInvalid));
                return;
            }

            if (SessionData.Instance != null)
            {
                SessionData.Instance.PlayerName = typed;
            }

            ShowPanel(entryPanel);
        }

        public void CreateRoom()
        {
            _ = _service.CreateRoom(_request.RoomName, PlayerName(), _request.MaxPlayers);
        }

        public void JoinRoom()
        {
            string code = RoomCode.Sanitize(codeInput != null ? codeInput.text : string.Empty);

            if (!RoomCode.IsPlausible(code))
            {
                ShowToast(LobbyError.MessageFor(LobbyError.RoomNotFound));
                return;
            }

            if (SessionData.Instance != null)
            {
                SessionData.Instance.LastRoomCode = code;
            }

            _ = _service.JoinRoom(code, PlayerName());
        }

        public void StartGame()
        {
            _ = _service.StartGame();
        }

        public void LeaveRoom()
        {
            _ = _service.Leave();
            ShowPanel(entryPanel);
        }

        /// <summary>Copy the room code so it can be pasted into a chat message.</summary>
        public void CopyCode()
        {
            if (_service.Current == null)
            {
                return;
            }

            GUIUtility.systemCopyBuffer = _service.Current.Code;
            ShowToast("Kode disalin");
        }

        public void BackToMainMenu()
        {
            _ = _service.Leave();
            SceneLoader.Instance.LoadScene(SceneReference.MainMenu);
        }

        // ---- service events ----

        private void OnRoomUpdated(LobbyRoomSnapshot room)
        {
            ShowPanel(roomPanel);

            if (codeText != null)
            {
                codeText.text = room.Code;
            }

            if (SessionData.Instance != null)
            {
                SessionData.Instance.LastRoomCode = room.Code;
            }

            for (int i = 0; i < _slotViews.Count; i++)
            {
                LobbySlot slot = i < room.Slots.Count ? room.Slots[i] : LobbySlot.Empty;
                _slotViews[i].Render(slot, i + 1, !slot.IsEmpty && slot.SessionId == room.MySessionId);
            }

            if (startButton != null)
            {
                startButton.gameObject.SetActive(room.AmHost);
                startButton.interactable = room.CanStart;
            }

            if (room.Phase == LobbyPhase.InProgress && !_loadingGame)
            {
                _loadingGame = true;
                SceneLoader.Instance.LoadScene(_request.GameScene);
            }
        }

        private void OnFailed(string code, string message)
        {
            ShowToast(message);

            // A failed create/join leaves us with no room, so fall back to the screen where the
            // player can try again — with the code they typed still in the field.
            if (_service.Current == null)
            {
                ShowPanel(entryPanel);
            }
        }

        // ---- helpers ----

        private string PlayerName() =>
            SessionData.Instance != null ? SessionData.Instance.PlayerName : string.Empty;

        private void BuildSlotViews(int maxPlayers)
        {
            if (slotContainer == null || slotPrefab == null)
            {
                Debug.LogError("LobbyController: slotContainer or slotPrefab is not assigned.", this);
                return;
            }

            for (int i = 0; i < maxPlayers; i++)
            {
                LobbySlotView view = Instantiate(slotPrefab, slotContainer);
                view.name = $"Slot {i + 1}";
                view.Render(LobbySlot.Empty, i + 1, false);
                _slotViews.Add(view);
            }
        }

        private void ShowPanel(GameObject panel)
        {
            if (namePanel != null) namePanel.SetActive(panel == namePanel);
            if (entryPanel != null) entryPanel.SetActive(panel == entryPanel);
            if (roomPanel != null) roomPanel.SetActive(panel == roomPanel);
        }

        private void ShowToast(string message)
        {
            if (toastRoot == null || toastText == null)
            {
                Debug.LogWarning($"Lobby toast (no UI assigned): {message}", this);
                return;
            }

            toastText.text = message;
            toastRoot.SetActive(false);
            toastRoot.SetActive(true); // re-enable so the tween replays from the start
            _toastHideAt = Time.unscaledTime + toastSeconds;
        }
    }
}
```

- [ ] **Step 3: Confirm the project compiles**

Run `mcp__UnityMCP__read_console` with `types: ["error"]`.
Expected: no compile errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Lobby
git commit -m "feat: add lobby slot view and lobby controller"
```

---

### Task 7: Promote `EgrangUIStyle` to a shared `MuseumUIStyle`

The lobby generator needs the same palette, font paths and nine-slice ladder the Egrang builders use. `Assets/Docs/ui-style.md` §9 already states the rule: *"Two generators with their own copy of the palette are two screens free to drift apart."* Copying it would break that rule, and a `Museum.Lobby.Editor` assembly referencing `Museum.Games.Egrang.Editor` would make the lobby depend on a minigame. So the style source moves up to Core.

**Files:**
- Create: `Assets/Scripts/Core/Editor/Museum.Core.Editor.asmdef`
- Move: `Assets/Scripts/Games/Egrang/Editor/EgrangUIStyle.cs` → `Assets/Scripts/Core/Editor/MuseumUIStyle.cs`
- Modify: `Assets/Scripts/Games/Egrang/Editor/Museum.Games.Egrang.Editor.asmdef`
- Modify: `Assets/Scripts/Games/Egrang/Editor/EgrangStickSelectionUIBuilder.cs`, `EgrangRaceProgressUIBuilder.cs`, `EgrangRunHudStyler.cs`, `EgrangRunChainWirer.cs` (only the `using static` line, and only in the files that have one)
- Modify: `Assets/Docs/ui-style.md`

**Interfaces:**
- Consumes: nothing.
- Produces: `Museum.Core.EditorTools.MuseumUIStyle` — same public API as `EgrangUIStyle` had (`ReferenceResolution`, `Gold`, `Cream`, `Tan`, `Scrim`, `ContainerFrame`, `InnerFrame`, `CreateUI`, `CreateButton`, `AddLabel`, `AddColumnLabel`, `StyleText`, `SetFrame`, `ApplyStockTint`, `AddTween`, `FrameSprite`, `CornerFrameSprite`, `LoadSprite`, `FillArray`, `Anchor`, `Stretch`, and every font-path constant it declares).

- [ ] **Step 1: Create the Core editor assembly definition**

Create `Assets/Scripts/Core/Editor/Museum.Core.Editor.asmdef`:

```json
{
    "name": "Museum.Core.Editor",
    "rootNamespace": "Museum.Core.EditorTools",
    "references": [
        "Museum.Core",
        "UnityEngine.UI",
        "Unity.TextMeshPro",
        "Unity.InputSystem"
    ],
    "includePlatforms": [
        "Editor"
    ],
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

- [ ] **Step 2: Move the file, keeping its meta**

```bash
git mv "Assets/Scripts/Games/Egrang/Editor/EgrangUIStyle.cs" "Assets/Scripts/Core/Editor/MuseumUIStyle.cs"
git mv "Assets/Scripts/Games/Egrang/Editor/EgrangUIStyle.cs.meta" "Assets/Scripts/Core/Editor/MuseumUIStyle.cs.meta"
```

Moving the `.meta` alongside keeps the asset GUID, so nothing that references it breaks.

- [ ] **Step 3: Rename the class and namespace**

In `Assets/Scripts/Core/Editor/MuseumUIStyle.cs`:
- `namespace Museum.Games.Egrang.EditorTools` → `namespace Museum.Core.EditorTools`
- `public static class EgrangUIStyle` → `public static class MuseumUIStyle`
- Update the class XML doc's first line to: `/// Shared style source for every editor UI generator in the project — palette, font paths, the nine-slice ladder and the Create/Style helpers. Assets/Docs/ui-style.md is the prose; this is the executable copy.`

- [ ] **Step 4: Point the Egrang editor assembly at it**

In `Assets/Scripts/Games/Egrang/Editor/Museum.Games.Egrang.Editor.asmdef`, add `"Museum.Core.Editor"` to the `references` array.

- [ ] **Step 5: Update the consumers**

Find every reference and update it:

```bash
grep -rn "EgrangUIStyle" Assets/Scripts
```

For each hit, replace `using static Museum.Games.Egrang.EditorTools.EgrangUIStyle;` with `using static Museum.Core.EditorTools.MuseumUIStyle;`, and any qualified `EgrangUIStyle.` usage with `MuseumUIStyle.`.

- [ ] **Step 6: Verify nothing still references the old name and the project compiles**

```bash
grep -rn "EgrangUIStyle" Assets/Scripts
```
Expected: no output.

Then run `mcp__UnityMCP__read_console` with `types: ["error"]`.
Expected: no compile errors.

- [ ] **Step 7: Record the move in the style doc**

In `Assets/Docs/ui-style.md` §9, replace the sentence naming `Assets/Scripts/Games/Egrang/Editor/EgrangUIStyle.cs` as the shared style source with `Assets/Scripts/Core/Editor/MuseumUIStyle.cs`, keeping the rest of that bullet as-is.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Core/Editor Assets/Scripts/Games/Egrang/Editor Assets/Docs/ui-style.md
git commit -m "refactor: promote EgrangUIStyle to shared MuseumUIStyle in Core"
```

---

### Task 8: `LobbyUIBuilder` — generate the Lobby scene

A big static layout, so it is editor-generated like `EgrangStickSelectionUIBuilder` (`Assets/Docs/ui-style.md` §9). The generator must produce a scene that passes the §10 checklist.

**Files:**
- Create: `Assets/Scripts/Lobby/Editor/Museum.Lobby.Editor.asmdef`
- Create: `Assets/Scripts/Lobby/Editor/LobbyUIBuilder.cs`
- Create (generated): `Assets/Scenes/Lobby.unity`, `Assets/Prefabs/Lobby Slot.prefab`

**Interfaces:**
- Consumes: `MuseumUIStyle` (Task 7), `LobbyController`, `LobbySlotView` (Task 6).
- Produces: menu item `Museum/Build Lobby Scene` that writes `Assets/Scenes/Lobby.unity` with a fully wired `LobbyController`.

- [ ] **Step 1: Create the editor assembly definition**

Create `Assets/Scripts/Lobby/Editor/Museum.Lobby.Editor.asmdef`:

```json
{
    "name": "Museum.Lobby.Editor",
    "rootNamespace": "Museum.Lobby.EditorTools",
    "references": [
        "Museum.Lobby",
        "Museum.Core",
        "Museum.Core.Editor",
        "UnityEngine.UI",
        "Unity.TextMeshPro",
        "Unity.InputSystem"
    ],
    "includePlatforms": [
        "Editor"
    ],
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

- [ ] **Step 2: Read the reference generator before writing this one**

Read `Assets/Scripts/Games/Egrang/Editor/EgrangStickSelectionUIBuilder.cs` in full and `Assets/Scripts/Core/Editor/MuseumUIStyle.cs` in full. Match their structure and helper usage exactly — the exact signatures of `CreateUI`, `CreateButton`, `AddLabel`, `StyleText`, `SetFrame`, `ApplyStockTint`, `AddTween`, `Anchor` and `Stretch`, and the font-path constants, come from that file and must be used as they are declared there rather than guessed.

- [ ] **Step 3: Write the builder**

Create `Assets/Scripts/Lobby/Editor/LobbyUIBuilder.cs`, a static class with `[MenuItem("Museum/Build Lobby Scene")]`, that builds this hierarchy and saves it to `Assets/Scenes/Lobby.unity`:

```
Lobby (scene)
├── Main Camera                 orthographic-irrelevant; Solid Color background, referenced by the Canvas
├── EventSystem                 EventSystem + InputSystemUIInputModule
├── Bootstrap                   SessionData + SceneLoader + ColyseusNetManager  (see step 4)
└── Canvas                      Screen Space – Camera, 800×600, Match 0.5, PPU 100, GraphicRaycaster
    ├── BG                      Image, stretch, frame sprite tinted #FFFFFF
    ├── Name Panel              CanvasGroup + PopupTween          (LobbyController.namePanel)
    │   ├── Text (TMP)          "Siapa nama kamu?"  Roboto-Bold SDF 18 #FFFFFF
    │   ├── Name Input          TMP_InputField, characterLimit 16 (LobbyController.nameInput)
    │   └── Confirm             composed button "Lanjut"          → LobbyController.ConfirmName
    ├── Entry Panel             CanvasGroup + PopupTween          (LobbyController.entryPanel)
    │   ├── Text (TMP)          "Egrang — 4 Pemain"  Roboto-Bold SDF 18
    │   ├── Create Room         composed button "Buat Ruangan"    → LobbyController.CreateRoom
    │   ├── Code Input          TMP_InputField, characterLimit 8, contentType Alphanumeric
    │   │                                                         (LobbyController.codeInput)
    │   ├── Join Room           composed button "Gabung"          → LobbyController.JoinRoom
    │   └── Back                composed button "Kembali"         → LobbyController.BackToMainMenu
    ├── Room Panel              CanvasGroup + PopupTween          (LobbyController.roomPanel)
    │   ├── Code Text (TMP)     CG-Regular SDF Bold 56 #F4EAD5    (LobbyController.codeText)
    │   ├── Copy                composed button "Salin Kode"      → LobbyController.CopyCode
    │   ├── Slots               HorizontalLayoutGroup, spacing 12 (LobbyController.slotContainer)
    │   ├── Start               composed button "Mulai"           → LobbyController.StartGame
    │   │                                                         (LobbyController.startButton)
    │   └── Leave               composed button "Keluar"          → LobbyController.LeaveRoom
    └── Toast                   Image frame_with_corner + CanvasGroup + FadeTween, starts inactive
        └── Text (TMP)          Roboto-Regular SDF 14 #D8C0A1     (LobbyController.toastText)
```

Style requirements the generator must satisfy (all from `Assets/Docs/ui-style.md`, all available as `MuseumUIStyle` helpers):
- Canvas per §1; `InputSystemUIInputModule`, never the legacy module.
- Every TMP component gets its font asset assigned explicitly, auto-size off; sizes and colors from §4/§5.
- Frames sliced, `frame_default` for buttons and containers, `frame_with_corner` for the toast and panels; PPU multiplier from the §6 ladder (3.13 buttons, 5.05 toast chip, 1.0 panels).
- Buttons via `MuseumUIStyle.CreateButton` + `ApplyStockTint`.
- Each panel gets a `CanvasGroup` + `PopupTween` via `MuseumUIStyle.AddTween`; the toast gets `CanvasGroup` + `FadeTween`. Tweens are added by name through `AddTween` because they live in the default assembly.
- Button `onClick` wiring uses `UnityEventTools.AddPersistentListener` so it is saved into the scene.
- The builder must be safe to re-run: if `Assets/Scenes/Lobby.unity` exists, it is overwritten wholesale, and the slot prefab is written to `Assets/Prefabs/Lobby Slot.prefab` with `PrefabUtility.SaveAsPrefabAsset`.

The slot prefab is one card: `Image` (frame_default, PPU multiplier 1.97) + `LobbySlotView`, containing `Seat Text (TMP)` (Roboto-Medium SDF 8 #FFFFFF), `Name Text (TMP)` (Roboto-Bold SDF 13.5 #FFFFFF), `Host Badge` (Text "Host", Gold `#C19F61`, inactive by default) and `You Badge` (Text "Kamu", Tan `#D8C0A1`, inactive by default), with the four `LobbySlotView` fields assigned.

- [ ] **Step 4: Wire the Bootstrap object**

The Lobby scene needs `SessionData`, `SceneLoader` and `ColyseusNetManager` present, because the lobby can be opened directly in the Editor without passing through MainMenu. All three are `DontDestroyOnLoad` singletons that destroy duplicates in `Awake`, so having one in every scene is safe. In the builder, create a `Bootstrap` GameObject and `AddComponent` all three. Assign `SceneLoader.loadingScreenPrefab` from `Assets/Prefabs/` using the same prefab the MainMenu scene references — find it with:

```bash
grep -rn "loadingScreenPrefab" Assets/Scenes/MainMenu.unity
```

and resolve that GUID to a path with `AssetDatabase.GUIDToAssetPath`.

- [ ] **Step 5: Run the generator**

In Unity: **Museum → Build Lobby Scene**. Then run `mcp__UnityMCP__read_console` with `types: ["error", "warning"]`.
Expected: no errors.

- [ ] **Step 6: Verify the generated scene against the §10 checklist**

Open `Assets/Scenes/Lobby.unity` and confirm each item of `Assets/Docs/ui-style.md` §10 that applies: 800×600 canvas at Match 0.5 PPU 100; `InputSystemUIInputModule`; every TMP has an explicit font and auto-size off; no `UnityEngine.UI.Text` anywhere; frames sliced with a ladder PPU multiplier; buttons on stock ColorTint; every animated element has a `CanvasGroup` plus exactly one tween; anchors match roles and pivots are 0.5,0.5.

- [ ] **Step 7: Add the scene to Build Settings**

Add `Assets/Scenes/Lobby.unity` and `Assets/Scenes/Egrang.unity` to `File → Build Settings → Scenes In Build` (both enabled). `SceneLoader.LoadScene(string)` cannot load a scene that is not in the list.

- [ ] **Step 8: Play-test the fake flow**

Enter Play mode on `Lobby.unity` and confirm, in order:
1. Name panel appears (no name set), typing `A` and confirming shows the "Enter a name" toast; typing `Budi` moves to the entry panel.
2. **Buat Ruangan** shows the room panel with a 5-character code, seat 1 named Budi with the Host badge, seats 2–4 reading "Menunggu…", **Mulai** visible but disabled.
3. **Salin Kode** shows the "Kode disalin" toast and the code is in the system clipboard.
4. **Keluar** returns to the entry panel; typing the same code and pressing **Gabung** re-enters the room in seat 1 as a non-host with the previous host still seated, and **Mulai** hidden.
5. Typing a nonsense code and pressing **Gabung** shows "Room not found" and stays on the entry panel with the code still in the field.

Peers can be added from a temporary Editor script or the Inspector while paused; if that is awkward, add a debug button in the builder guarded by `#if UNITY_EDITOR` that calls `FakeLobbyService.AddSimulatedPlayer`. Report anything that does not match.

- [ ] **Step 9: Commit**

```bash
git add Assets/Scripts/Lobby/Editor Assets/Scenes/Lobby.unity* "Assets/Prefabs/Lobby Slot.prefab"* ProjectSettings/EditorBuildSettings.asset
git commit -m "feat: generate Lobby scene with 4-slot room UI"
```

---

### Task 9: MainMenu entry point

Where the player types their name and picks Egrang. Also removes the duplicate nickname now that `SessionData` owns it.

**Files:**
- Modify: `Assets/Scripts/Core/MainMenu.cs`
- Modify: `Assets/Scripts/Core/ColyseusNetManager.cs:23-24`
- Modify: `Assets/Scenes/MainMenu.unity` (via the Editor)
- Modify: `Assets/Scripts/Core/Museum.Core.asmdef`

**Interfaces:**
- Consumes: `SessionData`, `PlayerNameRules`, `SceneReference` (Task 1); `LobbyRequest` (Task 4).
- Produces: `Museum.Core.MainMenu.PlayEgrang()` and `MainMenu.OnNameChanged(string)`, both wired to scene UI.

- [ ] **Step 1: Let Core see the lobby types**

`MainMenu` needs to build a `LobbyRequest`, so add `"Museum.Lobby"` to the `references` array in `Assets/Scripts/Core/Museum.Core.asmdef`.

`Museum.Lobby` already references `Museum.Core` (Task 2), and Unity forbids circular assembly references — so this step will fail to compile. Resolve it by moving `LobbyRequest.cs` from `Assets/Scripts/Lobby/` to `Assets/Scripts/Core/` and changing its namespace to `Museum.Core`:

```bash
git mv Assets/Scripts/Lobby/LobbyRequest.cs Assets/Scripts/Core/LobbyRequest.cs
git mv Assets/Scripts/Lobby/LobbyRequest.cs.meta Assets/Scripts/Core/LobbyRequest.cs.meta
```

Then in `LobbyRequest.cs` change `namespace Museum.Lobby` to `namespace Museum.Core`, and in `LobbyController.cs` (which already has `using Museum.Core;`) nothing changes. Do **not** add `"Museum.Lobby"` to the Core asmdef.

- [ ] **Step 2: Rewrite `MainMenu`**

Replace `Assets/Scripts/Core/MainMenu.cs` with:

```csharp
using TMPro;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// The front screen. Two jobs: capture the nickname before anything networked happens, and
    /// route into either the museum hub (single player, no room) or the lobby for a chosen
    /// minigame.
    ///
    /// The name is captured here rather than in the lobby because it is needed for every route
    /// out of this screen; the lobby keeps a fallback prompt only for entries that skip MainMenu.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Nickname")]
        [Tooltip("Nickname field. Its onValueChanged should call OnNameChanged.")]
        [SerializeField] private TMP_InputField nameInput;
        [Tooltip("Disabled until the typed name is valid. Optional.")]
        [SerializeField] private UnityEngine.UI.Button playEgrangButton;

        private void Start()
        {
            if (nameInput != null && SessionData.Instance != null)
            {
                nameInput.text = SessionData.Instance.PlayerName;
            }

            RefreshPlayButton();
        }

        /// <summary>Wired to the nickname field's onValueChanged.</summary>
        public void OnNameChanged(string _)
        {
            RefreshPlayButton();
        }

        public void GoToMuseum()
        {
            SceneLoader.Instance.LoadScene(SceneReference.Museum);
        }

        /// <summary>Open the lobby for a 4-player Egrang race.</summary>
        public void PlayEgrang()
        {
            if (!StoreName())
            {
                return;
            }

            LobbyRequest.Pending = new LobbyRequest("egrang", 4, SceneReference.Egrang);
            SceneLoader.Instance.LoadScene(SceneReference.Lobby);
        }

        private bool StoreName()
        {
            string typed = nameInput != null ? nameInput.text : string.Empty;

            if (!PlayerNameRules.IsValid(typed))
            {
                return false;
            }

            if (SessionData.Instance != null)
            {
                SessionData.Instance.PlayerName = typed;
            }

            return true;
        }

        private void RefreshPlayButton()
        {
            if (playEgrangButton == null)
            {
                return;
            }

            playEgrangButton.interactable =
                PlayerNameRules.IsValid(nameInput != null ? nameInput.text : string.Empty);
        }
    }
}
```

- [ ] **Step 3: Make `ColyseusNetManager.PlayerName` a passthrough**

In `Assets/Scripts/Core/ColyseusNetManager.cs`, replace the `PlayerName` auto-property (lines 23–24) with:

```csharp
        /// <summary>
        /// Player nickname, sent as `displayName` on every create/join. Stored on
        /// <see cref="SessionData"/> — a second copy here would be a second thing to keep in sync.
        /// Falls back to "Player" only if no name was ever entered.
        /// </summary>
        public string PlayerName
        {
            get
            {
                if (SessionData.Instance != null && SessionData.Instance.HasPlayerName)
                {
                    return SessionData.Instance.PlayerName;
                }

                return "Player";
            }
            set
            {
                if (SessionData.Instance != null)
                {
                    SessionData.Instance.PlayerName = value;
                }
            }
        }
```

- [ ] **Step 4: Wire the MainMenu scene**

Open `Assets/Scenes/MainMenu.unity` and, following `Assets/Docs/ui-style.md`:
1. Add a `SessionData` component to the same persistent object that already carries `SceneLoader` / `ColyseusNetManager` (if there is no such object in this scene, create `Bootstrap` and add all three).
2. Add a `TMP_InputField` named `Name Input` (frame `frame_default` sliced, PPU multiplier 3.13; placeholder "Nama kamu" Roboto-Regular SDF 14 `#D8C0A1`; text Roboto-Bold SDF 13.5 `#FFFFFF`; characterLimit 16). Assign it to `MainMenu.nameInput` and set its `onValueChanged` to `MainMenu.OnNameChanged`.
3. Add a composed button `Main Egrang` matching the existing menu buttons, `onClick` → `MainMenu.PlayEgrang`. Assign it to `MainMenu.playEgrangButton`.

- [ ] **Step 5: Verify the compile and the run**

Run `mcp__UnityMCP__read_console` with `types: ["error"]` — expected: no errors.

Then enter Play mode on `MainMenu.unity`: the Egrang button is disabled until at least 2 characters are typed; pressing it loads the Lobby scene straight to the Entry panel (the name panel is skipped because the name is already set); **Buat Ruangan** shows a room with your typed name in seat 1.

- [ ] **Step 6: Run the full test suite**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"` and no filter.
Expected: every fixture passes, including the pre-existing Dakon and Egrang tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Core Assets/Scripts/Lobby Assets/Scenes/MainMenu.unity
git commit -m "feat: add nickname entry and Egrang lobby entry point to MainMenu"
```

---

### Task 10: Documentation

The docs in `Assets/Docs/` describe the client as it is, not as it was planned. Three of them are now out of date.

**Files:**
- Modify: `Assets/Docs/ui-flow.md`
- Modify: `Assets/Docs/dev-plan.md`
- Modify: `Assets/Docs/architecture.md`
- Modify: `Assets/Docs/games/egrang.md`

**Interfaces:**
- Consumes: everything built in Tasks 1–9.
- Produces: nothing code-facing.

- [ ] **Step 1: Update `ui-flow.md`**

In the flow graph and Screens section, record what was actually built:
- MainMenu now holds nickname entry plus an **Main Egrang** button that opens the Lobby scene.
- A new **Lobby scene** section: three panels (name fallback / entry / room), the 4-slot room view, host-only Start enabled at 2+ players, copy-code button, Leave.
- Note that the lobby currently runs on `FakeLobbyService`, so create/join works in-Editor but two browsers cannot yet meet — that needs `ColyseusLobbyService`.
- Keep the deep-link row; mark it as not yet implemented.

- [ ] **Step 2: Update `dev-plan.md`**

Under **P4 — Lobby & room UI/UX**, mark the lobby scene, nickname, create/join, 4-slot room, host start and failure UX as done against the fake, and list what P4 still owes: the real `ColyseusLobbyService`, the `?room=CODE` deep-link, and reconnect. Update the "Current state" bullet listing scenes to include `Lobby`.

- [ ] **Step 3: Update `architecture.md`**

Add `SessionData` to whatever the persistent-bootstrap section lists, and add a short subsection on the lobby: `ILobbyService` boundary, snapshot-render discipline, and that the pure model lives outside `MonoBehaviour` so it can be checked against the server's rules later.

- [ ] **Step 4: Update `games/egrang.md`**

Add a line stating that matchmaking is no longer blocked — Egrang enters through the generic Lobby with `LobbyRequest("egrang", 4, SceneReference.Egrang)` — while the race rules themselves remain blocked on the server's `docs/games/egrang.md`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Docs
git commit -m "docs: record the lobby scene in flow, plan and architecture docs"
```

---

## What this plan deliberately does not build

Called out so a reviewer does not read them as omissions — each is listed as a follow-up in the spec:

1. **`ColyseusLobbyService`** — blocked on the server's `egrang` room and its `protocol.md` lobby state. The seam exists (Task 5) and fails loudly.
2. **`?room=CODE` deep-link auto-join** — belongs with the real transport.
3. **Reconnect** — `ColyseusNetManager` already caches the token; wiring it into the lobby waits for the real service.
4. **Egrang race gameplay and networking** — blocked on server rules; inventing them client-side is explicitly forbidden by `Assets/Docs/games/egrang.md`.
