# Scenario Compliance — the curriculum sheet vs. what is built

The client's exhibit copy comes from `AMS-NAM.xlsx` (see [lessons.md](lessons.md) for the
lesson columns, `GameSignageContent.cs` for the signboard columns). The same sheet's
**"Ide game virtual"** column also sketches, per game, a scenario for a playable version.
This doc records, as of **2026-09-10**, how the three games that have any code compare to
those scenarios, and what the decision about the gap was.

**Decision (owner, 2026-09-10): the gameplay of Dakon, Engklek and Egrang is not to be
changed.** The one requirement carried over from the review is that every game needs **at
least two players** to start. Everything below is a description, not a to-do list.

## The minimum-players rule

| Where | Value |
|---|---|
| `LobbyRoomSnapshot.MinPlayersToStart` (`Assets/Scripts/Lobby/LobbyRoomSnapshot.cs`) | **2** |
| `LobbyRoomSnapshot.CanStart` | host **and** phase `Waiting` **and** `OccupiedCount >= MinPlayersToStart` |
| `LobbyController.StartHintFor` | `Minimal 2 pemain untuk mulai — tunggu N pemain lagi` under the seat list until the count is reached |
| `FakeLobbyService.StartGame` | refuses below the same constant, so the offline lobby cannot cheat it |
| Tests | `LobbyRoomSnapshotTests` (EditMode) cover the threshold |

It is a lobby rule, not a game rule, so it applies to both games from one constant. The
server keeps its own gate (`docs/room-system.md` in the server repo); the client's is there so
the Start button is honest before the round trip. Room size is still the doorway's
`SceneTriggerPrompt.maxPlayers` (2 for Dakon, 3 for Egrang) — the minimum is the same for
both.

## Dakon (sheet row 1)

**Sheet scenario:** *"Simulasi pengelolaan lahan dan panen: pemain memilih strategi
distribusi biji/hasil panen agar 'ekosistem ladang' tetap seimbang."* Concept: agricultural
ecology, use of biological resources, the flow of the harvest through an agro-ecosystem.

**Built** ([games/dakon.md](games/dakon.md)): two players, a 60-seed pool of monocot and
dicot species, 20 typed holes, a 15-seed hand sown into a forced hole order; the choice is
*which seed goes into the next hole*, and a category match scores for you, a mismatch for
your opponent.

| Sheet | Built | Match |
|---|---|---|
| Two players | 2 seats, lobby needs both | ✅ |
| "Distribution of seeds/harvest" as the core decision | Sowing order is forced; the decision is seed *classification* per hole | ⚠️ partial — the sheet's strategy loop is resource balance, the build's is monocot/dicot recognition |
| "Ekosistem ladang tetap seimbang" (a balance meter) | No ecosystem or balance state; score is banked seeds | ❌ not built |
| Concept: harvest → storehouse (*lumbung*) | Two storehouses, split by category, are the score | ✅ the lumbung idea is literal |

Unchanged by decision. The signboard under the screen shows the sheet's scenario text
verbatim, so a visitor reads the intended idea beside the game that exists.

## Engklek (sheet row 2)

**Sheet scenario:** *"Avatar harus menjaga keseimbangan saat berpindah petak; skor naik bila
postur tubuh stabil dan tidak menyentuh garis."*

**Built:** **no game** — see [games/engklak.md](games/engklak.md). No rules were ever
supplied, no server room, no scene. What exists is the exhibit: the footage, the lesson
plaque, the signboard, a chalked court prop in the bay, and — new on the ground floor — the
walkable **hopscotch course** (`HopscotchCourse`, [museum-decor.md](museum-decor.md)), which
is an exhibit interaction, not a scored game: tiles light in order, a wrong row resets, the
gunung plays a fanfare. It has no players, no seats and no server.

The Museum's Engklek trigger (`Vid LT1/Vid Engklek/Cube`) is therefore a `ComingSoonNotice`,
not a doorway: it shows "Permainan Engklek segera hadir" while the visitor stands there and
offers no key to press ([scene-setup.md](scene-setup.md)).

## Egrang (sheet row 4)

**Sheet scenario:** *"Simulasi berjalan di atas egrang dengan pengaturan titik tumpu; pemain
mengatur langkah agar tidak jatuh."* Concept: locomotor system, muscle–nerve coordination,
balance, joint function, breathing, muscle fatigue.

**Built** ([games/egrang.md](games/egrang.md)): three lanes, 50 strides; each player picks
one of three stilts by cross-section (square / circle / triangle), which sets the width of the
timing band and the sweep speed; a Step press is graded full / half / miss; first across
wins.

| Sheet | Built | Match |
|---|---|---|
| "Pengaturan titik tumpu" (choose the footing) | Stilt choice by bearing area — the steadier pole gives the easier timing | ✅ same idea, expressed as a pre-race pick |
| "Mengatur langkah agar tidak jatuh" | Timed Step presses; a miss is a stumble in place, never a fall | ⚠️ no fall state; a bad rhythm costs distance, not the race |
| Muscle fatigue / stamina | None — the bar does not slow or narrow over time | ❌ not built |
| Multiplayer | 3 seats, lobby needs at least 2 | ✅ |

Unchanged by decision.

## The other fifteen games

The sheet gives every one of the eighteen a scenario; none of the other fifteen was ever in
scope as a playable game and none has code. Their scenario text is shown on their signboard
(`Signage (Generated)`, [museum-decor.md](museum-decor.md)), and the ground-floor **Ragam**
station names all eighteen. If one is ever commissioned, the rule chain is the one in
`CLAUDE.md`: rules in the server repo first, pure C# mirror second, scene last.
