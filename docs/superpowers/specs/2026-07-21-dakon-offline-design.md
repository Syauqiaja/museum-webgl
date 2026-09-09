# Dakon — P2 Offline Model + Hotseat View (Design)

> **Superseded design record — do not read as current rules.** The board shipped as 20 holes
> (10 per side, 5 dicot each), a 60-seed pool and 15-seed hands; this spec's 16 holes / 8 per
> side / 4:4 was never built. The ruleset is now owned jointly by
> [`Assets/Docs/games/dakon.md`](../../../Assets/Docs/games/dakon.md) and the server's
> `docs/games/dakon.md`. Kept for the reasoning behind the design, not its numbers.

**Date:** 2026-07-21
**Scope:** dev-plan **P2** only — pure-C# game model + local hotseat view/input. No
networking (that is P3). Colyseus swap is designed *for* but not built here.
**Rules source:** iterated live with the product owner in this session. Supersedes the
older client summary in `Assets/Docs/games/dakon.md` where they differ (that doc predates
these decisions). ⚑-flagged items are assumptions to confirm against the (not-in-repo)
server `docs/games/dakon.md` before P3.

---

## 1. Game rules (v6 — the settled ruleset)

Two players. A center **pool** holds a bunch of mixed seeds, each seed of category
**Monocot** or **Dicot**. The board is a ring of **16 holes** — 8 per player.

### Board
- Holes `0–7` = Player 0's side, `8–15` = Player 1's side.
- Each side has exactly **4 Dicot + 4 Monocot** holes. Their arrangement is a **random
  4:4 permutation generated once at game start** and fixed for the whole game (NOT
  re-rolled per turn). Seeded RNG → reproducible.
  - ⚑ Each side's permutation is rolled **independently** (not mirrored/symmetric).
- Each player has **2 storehouses**: a Monocot store and a Dicot store (4 total, 2/side).
  A scored seed lands in the winner's store **matching the seed's own category** (real
  physical sorting — drives rendering). The store split is cosmetic/organizational.
  - Because physical sorting drives rendering (species art shown in the bins), each store
    **retains the scored `Seed`s** (per-category `List<Seed>`), not a bare count — species
    would be lost by an `int` counter. `Store(player, category)` exposes the count; a
    `StoreSeeds(player, category)` read-only view backs species rendering.
- **Score = total** per player = `store[Monocot] + store[Dicot]`. **Winner = max total;
  equal totals = tie.**
- Holes hold **no persistent state** — a hole is empty except the instant between a drop
  and its immediate sweep.

### Turn
1. Active player **grabs up to 15 random seeds** from the pool into a **hand**
   (`min(15, poolRemaining)` — the last turns may grab fewer).
2. The player **sows** starting at their own **far-left hole**, moving left→right,
   **wrapping around the full 16-hole ring** (own side → opponent side → around, `mod 16`),
   **one seed per hole, mandatory — no skipping a hole.**
3. At each hole the only decision is **which held seed to place**. The drop is **swept
   immediately**:
   - seed category **==** hole type → **+1 to the ACTIVE player's** store of that category.
   - seed category **!=** hole type (**mismatch**) → **+1 to the OPPONENT's** store of that
     category. This is a **forced loss** when the hand has no matching seed left for the
     hole — the core tension of the game.
4. Sowing continues until the **hand is empty** (15 seeds → 15 placements → just under one
   full lap; wraps `mod 16` for Player 1 whose start is index 8). Then the **turn passes**.
5. **Game over** when the **pool is empty** after the final turn's sweeps resolve. Compare
   totals: higher wins, equal is a tie.

**Strategy** = choosing the placement order of held seeds to land matches on their holes
and dump unavoidable mismatches where they hurt least.

### ⚑ Defaults to confirm vs server
- Hole-type parity/layout: **random 4:4 per side, independent rolls, seeded**.
- Pool total seed count + overall Monocot/Dicot split: **configurable; sensible default**
  (proposed: even split, total large enough for several turns — final number set in the
  plan, e.g. 60 seeds = 30 M + 30 D). Must drain to a clean endgame.
- Seed **species**: category is what scores; species is a cosmetic label for art. A small
  configurable species set per category (default: 1–2 species each), flagged.
- Grab size **15**, board **16 holes / 4:4:4:4** — all per this session's decisions.

---

## 2. Architecture — two isolated units

Per CLAUDE.md ("keep game logic engine-independent"), logic lives outside `MonoBehaviour`
so it can be tested standalone and later moved server-side for P3 authority.

```
Assets/Scripts/Games/Dakon/
  Museum.Games.Dakon.asmdef          # isolates game logic; no dep on Core
  DakonBoard.cs                      # pure C# model — owns ALL state + rules
  Seed.cs / SeedCategory.cs          # value types
  DropResult.cs / DakonError.cs      # method result + error enum
  DakonConfig.cs                     # tunables (pool size/split, grab size, species)
  DakonView.cs                       # MonoBehaviour — renders model, hotseat input, anim
  Tests/
    Museum.Games.Dakon.Tests.asmdef  # EditMode; refs model + test-framework
    DakonBoardTests.cs
```

Existing `Assets/Scripts/Core/*` stays in the default assembly untouched — asmdefs are
introduced only for the Dakon folder (isolation without restructuring existing code).

### `DakonBoard` (pure C#, no MonoBehaviour) — sole owner of state
State:
- `HoleType[16] holes` — set once in `StartGame()` from RNG (per-game layout).
- `List<Seed> pool`
- `int[2,2] store` — `store[player, category]`
- `int activePlayer`
- `List<Seed> hand` — active player's current grab
- `int nextHoleIndex` — absolute ring index the next drop must target
- `Phase phase` — `Waiting | InProgress | Finished`

API (shaped to mirror the P3 wire protocol so networking is a drop-in swap):
- `DakonBoard(DakonConfig config, int rngSeed)` / injectable RNG for deterministic tests.
- `StartGame()` → rolls hole layout, fills pool, sets `activePlayer = 0`, grabs first
  hand, `phase = InProgress`.
- `DropSeed(string seedId, int holeIndex)` → `DropResult`. Validates:
  `holeIndex == nextHoleIndex`; seed is an unsown seed in `hand`. Applies sweep, advances
  `nextHoleIndex` (`mod 16`), and — if the hand is now empty — ends the turn (swap active,
  grab next hand) or, if the pool is empty, finishes the game.
- Read-only accessors: `ActivePlayer`, `Hand` (readonly view), `NextHoleIndex`,
  `HoleTypeAt(i)`, `Store(player, category)`, `Total(player)`, `PoolCount`, `Phase`,
  `Winner` (`int?`, null = tie/none).

`DropResult`:
```csharp
struct DropResult {
    bool Ok;
    DakonError? Error;        // set when !Ok
    int ScoringPlayer;        // who got the point
    SeedCategory ScoringCategory; // which store it landed in
    bool WasMatch;            // active-match vs opponent-mismatch
    bool TurnEnded;           // hand emptied this drop
    bool GameOver;            // pool emptied after this turn
}
```

`DakonError` (mirrors server codes; no `pass` action exists, so no new server message):
`NotYourTurn`, `InvalidHole` (wrong index / not `nextHoleIndex`), `SeedNotInHand`.

### `DakonView` (MonoBehaviour) — render + hotseat, never authoritative
- Holds a `DakonBoard`; renders from it, **never mutates game state directly**
  (same read-only discipline P3's schema rendering will enforce).
- Self-contained input (own pointer raycast; does **not** reuse `Core/Hoverable` — that is
  a museum-prop material-swap component, wrong model for game pieces).
- **Hand = card hand (card-game style).** The grabbed seeds render as **cards held in the
  active player's hand** — a fanned/row card layout. Each card shows the seed's category
  (and species art). The player **selects a card**, then clicks the highlighted
  `nextHoleIndex` hole to place it; the played card leaves the hand and the remaining cards
  reflow. A card is one `Seed` from `board.Hand` (`seedId` identity → maps clicks to
  `DropSeed`). Purely presentational — card state derives from `board.Hand`, never a
  separate source of truth.
- `SerializeField` scene refs (wired in `Dakon.unity` in-Editor): 16 hole transforms, 4
  storehouse transforms, **hand card-layout container + card prefab**, per-turn/score/pool
  UI, game-over panel.
- Colors/labels holes from `HoleTypeAt(i)` after `StartGame()`.

---

## 3. Data flow (hotseat)

```
Active player: select a hand CARD → click highlighted nextHoleIndex hole
  → DakonView calls board.DropSeed(card.seedId, holeIndex)
  → board returns DropResult
  → view animates: card → hole → the ScoringPlayer's ScoringCategory storehouse
  → removes played card, remaining hand cards reflow
  → updates score / pool UI, advances hole highlight (mod 16)
  → on TurnEnded: swap active-player indicator, deal new hand of cards
  → on GameOver: show game-over panel (totals + winner, null = tie)
```

Both players share the screen (hotseat); input is enabled for whichever side is active.
Invalid drop → `DropResult.Ok == false` → non-blocking toast, no state change.

---

## 4. Testing (TDD, EditMode)

Pure model → fast EditMode tests, seeded RNG = deterministic. Cases:
- Match placement scores **active** in the seed's category store.
- Mismatch placement scores **opponent** in the seed's category store.
- Sequential enforcement: `holeIndex != nextHoleIndex` → `InvalidHole`, no state change.
- Off-turn drop → `NotYourTurn`.
- `seedId` not in hand → `SeedNotInHand`.
- Ring wrap: Player 1 sow starting at index 8 wraps through `mod 16` correctly.
- Turn end: hand empties → active swaps, new hand grabbed (`min(15, pool)`).
- Endgame: pool empties → `phase == Finished`, `GameOver` on the finishing drop.
- Win detection by **total**; equal totals → `Winner == null` (tie).
- Hole layout: each side has exactly 4 Dicot + 4 Monocot; stable for the game; a fixed
  seed reproduces the same layout.

Covers dev-plan **P2 exit**: a full Dakon game playable locally to a win/tie with correct
scoring.

---

## 5. Out of scope (explicit)

- **Networking / Colyseus (P3)** — no schema, no `room.Send`, no reconnect. The model API
  and `DropResult` are *shaped* for the swap; that is all.
- Lobby / room UI (P4), WebGL build tuning (P5), other minigames.
- Final art/animation polish — view does functional rendering + basic sweep animation.

## 6. P3 forward-compat notes (not built now)

- `DakonBoard` is engine-independent → the same class can move server-side for authority.
- No client-invented messages: only `drop_seed` (matches server `protocol.md`); the earlier
  `pass` idea was dropped, so nothing new to add server-side.
- Per-game hole layout must become synced schema state in P3 (server rolls it, client
  renders) — noted so it is not baked as client-only.
