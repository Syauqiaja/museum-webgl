# Dakon — Scene Setup Guide (P2 hotseat)

> **Superseded — kept for the board-layout walkthrough only.** The scene is now wired by
> `Museum/Rebuild UI/Dakon` (`Assets/Scripts/Games/Dakon/Editor/DakonUIBuilder.cs`), which is
> the authority on every field and rect below; run it instead of following the wiring steps by
> hand. The numbers here are also pre-v6: this guide says 16 holes and inspector-editable
> `poolSeeds` / `grabSize`, and the shipped game is 20 holes, 60 seeds, 15 per hand, with pool
> and grab size deliberately not editable on the view. Current docs:
> [`Assets/Docs/games/dakon.md`](../Assets/Docs/games/dakon.md) and
> [`Assets/Docs/scene-setup.md`](../Assets/Docs/scene-setup.md).

How to wire `Assets/Scenes/Dakon.unity` so `DakonView` renders a playable local game. The
model is done and tested; this is the in-Editor wiring the code can't do headless. All
`SerializeField` names below match `DakonView.cs` / `DakonCard.cs` exactly.

> Open the project in Unity `6000.3.19f1`, open `Dakon.unity`. After adding the new scripts,
> let Unity finish compiling (watch the Console — zero errors expected). Then wire the scene.

---

## 0. Prereqs (once)

- **EventSystem with the new Input System module.** GameObject → UI → Event System. If it
  was created with the old `StandaloneInputModule`, click **Replace with InputSystemUIInputModule**
  (button appears on the component). uGUI Button clicks won't fire without this.
- **A Canvas** for the hand + HUD: GameObject → UI → Canvas (Screen Space – Overlay is
  simplest). The EventSystem is auto-created with the first UI object.
- Run the tests first: Window → General → Test Runner → EditMode → **Run All** → 14 green.

---

## 1. Board holes (20 anchors)

The model owns hole *types*; the scene only supplies *positions* for the highlight + the
3D seed drop, ring order `0..19` (`0..9` = Player 1 side, `10..19` = Player 2 side; index 0 =
P1 far-left, index 10 = P2 far-left).

1. Create an empty `Holes` GameObject.
2. Under it, 20 empties `Hole00 … Hole19` placed in the ring layout you want (world or UI
   space — anchors are read by `.position`).
3. **Optional labels:** if you want each hole to show `Monocot`/`Dicot`, add a `Text` on/near
   each hole and collect them into `holeLabels` (same index order). Leave empty to skip.

## 2. Highlight marker (optional but recommended)

One object (quad, sprite, ring) = `highlightMarker`. `DakonView` moves it onto the current
`NextHoleIndex` anchor every step and hides it on game over. Purely a destination indicator —
there is **no** hole click; picking a card plays it at the forced next hole.

## 3. Hand card layout

1. Under the Canvas: empty `HandContainer` (a `RectTransform`). Add a **Horizontal Layout
   Group** (or Grid) so cards row/fan and reflow automatically → this is `handContainer`.
2. **Card prefab:**
   - Create a UI element (Image) as the card body.
   - Add a **Button** component (or let `DakonCard` auto-`GetComponent<Button>()`).
   - Add a child **Image** for the seed sprite and a child **Text** for the name.
   - Add the **`DakonCard`** component. Wire `artwork` → the child Image, `label` → the child
     Text, `button` → the Button (or leave null to auto-find).
   - Drag to `Assets/` to make it a **prefab**, delete from scene.
   - Assign the prefab to `DakonView.cardPrefab`.

`DakonView` spawns one card per `board.Hand` seed, resolves its `SeedType` (step 3.5) and
calls `DakonCard.Init(seed, type, …)`; the card shows that type's sprite + name. On click it
plays the seed. Cards are destroyed/redealt each turn — never hand-place cards.

## 3.5 Seed types (ScriptableObject catalog)

Each seed species is an authored asset. The model only deals in `TypeId` strings; these SOs
supply the art + which category each id belongs to.

1. For each species: **Assets → Create → Dakon → Seed Type**. Set:
   - `typeId` — stable id (blank = asset name).
   - `displayName` — shown on the card.
   - `cardSprite` — the card art.
   - `seedPrefab` — the 3D mesh dropped into the hole when this species is played (e.g. a
     model under `Assets/Models/Dakon/`). Leave null to fall back to the flat card-shrink sweep.
   - `category` — Monocot or Dicot.
2. Make at least one Monocot and one Dicot type (more per category = variety).
3. Assign them all to `DakonView.seedTypes`. `DakonView` derives the pool's per-category id
   lists from these and builds the `TypeId → SeedType` map for rendering.

> If `seedTypes` is left empty the game still runs — the model falls back to its default
> string ids (`"monocot"`/`"dicot"`) and cards show category text with no sprite.

## 3.6 3D seeds + storehouses

When a card is played its 3D `seedPrefab` flies from the card into the highlighted hole and
**stays there**, piling up over the game. When the pool empties (game over), every piled
seed sweeps to the correct **storehouse bin**.

1. **`boardCamera`** — the camera that renders the board. Used to project a played card's
   screen position into world space (the seed's launch point). Leave null → `Camera.main`.
2. **`storeAnchors` (4 empties)** — one bin per player per category. Wire in this exact index
   order:

   | Index | Bin          |
   |-------|--------------|
   | 0     | P1 Monocot   |
   | 1     | P1 Dicot     |
   | 2     | P2 Monocot   |
   | 3     | P2 Dicot     |

   Index = `player * 2 + category` (`Monocot=0, Dicot=1`). A seed routes by *which player
   scored it* — a mismatched drop scores to the **opponent**, so it flies to that player's bin,
   not the one who dropped it. Leave any bin null to keep its seeds in their holes.
3. **`pileRadius`** (~0.15) — scatter radius of a pile inside a hole/bin. **`dropArcHeight`**
   (~0.5) — arc height while a seed flies. **`storeAnimSeconds`** (~0.6) — game-over sweep
   duration (all seeds move in parallel).

> If a species has no `seedPrefab`, that play uses the old flat card-shrink instead — the game
> still works, just without the 3D seed for that species.

## 4. HUD (all optional — null-safe)

Add `Text` objects on the Canvas and wire:

| Field           | Shows                                   |
|-----------------|-----------------------------------------|
| `turnLabel`     | `Player N's turn`                       |
| `poolLabel`     | `Pool: <count>`                         |
| `scoreP0Label`  | `P1: <total>`                           |
| `scoreP1Label`  | `P2: <total>`                           |
| `toastLabel`    | rejection reason (invalid drop)         |

## 5. Game-over panel

1. A `GameObject` panel (Image full-screen + a `Text`), **disabled by default** — `DakonView`
   toggles it. → `gameOverPanel`.
2. The `Text` inside → `gameOverLabel` (renders `Player N wins!` / `Tie!` + totals).

## 6. The DakonView object

1. Empty GameObject `DakonGame`, add **`DakonView`**.
2. Config (Inspector): `poolSeeds` 120, `grabSize` 15. `randomizeSeedOnStart` = on for a
   fresh game each play; turn **off** and set `rngSeed` to reproduce an exact game.
3. `dropAnimSeconds` ~0.15; `boardCamera`, `storeAnchors`, `pileRadius`, `dropArcHeight`,
   `storeAnimSeconds` per step 3.6.
4. Drag every ref from steps 1–5 into the matching slots.

---

## 7. Play

Press **Play**. `DakonView.Start()` → `NewGame()`: rolls the board, deals P1's 15 cards,
highlights hole 0. Click a card → it plays at the highlighted hole, scores, card reflows.
Hand empties → turn passes, new hand dealt for the other side. Pool empties → game-over panel.

**Verify (P2 exit): a full game plays to a win/tie with correct scores.**

### Quick checks if nothing happens
- Cards don't respond → EventSystem missing the **InputSystemUIInputModule** (step 0), or the
  card prefab has no `Button`, or a full-screen Image is blocking raycasts (disable its
  *Raycast Target*).
- No cards appear → `handContainer` or `cardPrefab` unassigned.
- Rejection toast on every click → holes wired out of ring order (index 0 must be P1's
  far-left; the model advances `mod 20`).
- Seed drops as a flat card, not a 3D mesh → that species has no `seedPrefab`, or the hole
  anchor is null.
- Seeds don't move at game over → `storeAnchors` unwired (all 4 needed), or wired in the wrong
  index order (see 3.6 table).

---

## Notes / scope

- Plays spawn a 3D `seedPrefab` that flies into the hole and piles there; at game over every
  seed sweeps to its player/category store bin (`storceAnchors`, step 3.6). Species with no
  `seedPrefab` fall back to the flat card-shrink sweep (`AnimateCardOut`). Score totals
  (`scoreP0Label`/`scoreP1Label`) still show alongside; they match the bin counts.
- Pile scatter is `DakonPile.OffsetFor` (pure/deterministic, unit-tested) — used for both hole
  and store piles.
- `DakonView` never mutates state — it reads `DakonBoard` and calls `DropSeed`. Keep it that
  way: P3 swaps the local board for synced server state behind the same read pattern.
