# Dakon — Rules and Client Mapping

**Dakon Edukasi: Digital Edition (Monokotil vs. Dikotil)** — the **v7 ruleset**.

> **v7 (2026-09-11)** replaced v6's forced sowing order. The player now chooses the hole as
> well as the seed, restricted to their own side, one seed per hole per turn; the hand shrank
> from 15 to 10 so a turn is exactly "fill your ten holes". The `nextHoleIndex` state field
> became `sownMask`, and there is a new error, `hole_already_sown`. Both repos changed together.

Two implementations, one ruleset: `Assets/Scripts/Games/Dakon/DakonBoard.cs` (client,
offline hotseat) and `src/games/dakon/DakonBoard.ts` (server, authoritative). The server's
is a port of the client's. **Change one, change the other** — the server is authoritative,
so a divergence shows up as a client rendering a board the server does not have.

Ground truth for the rules: this doc and the server's `docs/games/dakon.md` — the two now
agree, and every number below was read out of `DakonConfig.cs` / `DakonConfig.ts`. Wire
contract: the server's `docs/protocol.md#dakon`.

---

## The game

Two players. A centre pool holds seeds of two botanical categories — **Monokotil** and
**Dikotil**. Every hole on the board is also typed as one of those two. On your turn you are
dealt ten seeds and place them into your own ten holes, one seed per hole, in whatever order
you like. A seed whose category matches the hole scores for you; a mismatch scores for your
opponent. Most seeds banked at the end wins.

It is an educational game: the interesting decision is recognising which species is a
monocot and which is a dicot, and — since a hand is drawn at random and rarely splits 5/5 the
way the holes do — deciding which unavoidable mismatches to give away.

## Setup

| Thing | Value | Source |
|---|---|---|
| Centre pool | **60 seeds** — 30 monocot, 30 dicot (an odd pool would give the extra to monocot) | `DakonConfig.PoolSeeds` |
| Holes | **20**, ten per side. Ring indices 0–9 = seat 0's side, 10–19 = seat 1's | `HolesPerSide = 10` |
| Hole types | A **fixed layout of 20**, 5 dicot + 5 monocot per side. The same board every match, pinned to the icons painted on the board texture | `DakonConfig.HoleTypes` |
| Storehouses | 2, one per player, each split monocot/dicot for display | `DakonBoard.Store` |
| Hand size | **10 seeds** drawn per turn — one per own hole — or everything left if fewer | `GrabSize = 10` (`= HolesPerSide`) |
| Match length | 60 ÷ 10 = **6 hands — 3 turns each** | — |

Holes are typed **by category, never by species**. A monocot hole accepts any monocot
species. Species are cosmetic: they decide the card art and the 3D seed prefab, nothing else.

### The hole layout is the artwork

Each of the twenty holes has a botanical icon painted above it in
`Assets/Texture2D/dakon_surface.png` — a corn kernel, a taproot, a five-petal flower — and
that is how a player is meant to know what a hole accepts. It is the exhibit's whole teaching
device.

The types used to be **shuffled per side** at `StartGame`. Since no code has ever read or
written that texture, the icons were decorative: a hole under a taproot was a dicot hole half
the time, and a player following the board was wrong as often as right — while the vignette
told them so without ever showing what the right answer had been. Nothing displays a hole's
type: `holeLabels` is unwired in `Dakon.unity` (all twenty entries null) and every per-hole
`SpriteRenderer` carries the highlight sprite, disabled.

Paint cannot move, so the ruleset was pinned to it. `DakonConfig.HoleTypes` is the layout read
off the texture, entry by entry, each one commented with the icon it came from. **It is not
scene-editable and not rolled** — same board every match, same board on both sides of the wire.

Ring order follows the anchors, which follow the art: **0–9 is seat 0's near row, left to
right**; **10–19 is seat 1's far row, and that row's `p0` is the rightmost hole**, so those ten
read *right to left*. Getting that backwards mirrors the far side against its own icons, which
is why it is spelled out both here and in the array's comment.

Changing the texture means changing `HoleTypes` **and** the server's `DAKON_HOLE_TYPES`, or an
online board stops matching the board the player is looking at.

RNG (pool order, draws) is **the server's** in an online match — seeded from the room code, so
a match is reproducible from its result row and no client can choose it. Offline, `DakonView`
rolls a random seed at `Start` unless `randomizeSeedOnStart` is off. The hole layout is no
longer among the things the seed decides.

### Known: three species are botanically miscategorised

`alpukat` (avocado) and `zaitun` (olive) are authored **Monocot** and are dicots; `jagung`
(corn) is authored **Dicot** and is a monocot. The server mirrors the client's grouping and
flags the same thing in `DakonConfig.ts`. Correcting them splits the eight species 3/5, which
breaks the even round-robin — it needs two more monocot species, so it is a content decision,
not a code one. **The hole layout above is unaffected**: holes are typed by category, and the
painted icons are correct.

## A turn

1. **Draw (automatic).** At the start of a turn the server draws `min(10, pool)` random
   seeds into the active player's hand.
2. **Sow.** The player picks a held seed **and** one of **their own** holes — seat 0 → holes
   0–9, seat 1 → holes 10–19 — in any order. A hole takes **one seed per turn**; the set of
   holes already filled this turn is `sownMask` (bit *i* = hole *i*), cleared when the turn
   ends. Nothing ever lands on the opponent's side.
3. **Both halves are the choice.** A full hand is one side's worth of holes, so a turn always
   ends with every own hole filled; the decision is which seed goes where.
4. Sowing is sequential: drop → the server validates and sweeps → the next drop.
5. When the hand empties, the turn passes and the next player draws. When the hand empties
   *and* the pool is empty, the match is over.

## The Sweep

Each hole is scored and cleared **immediately** after a seed lands in it:

| Case | Points | Seed ends up in |
|---|---|---|
| **Match** — seed category equals hole type | +1 active player | active player's storehouse |
| **Mismatch** — categories differ | +1 opponent | opponent's storehouse |

Holes hold no state between drops. Every seed ends in someone's storehouse, so the two
storehouses always sum to 60 at the end.

## Win condition

The match ends when the pool is exhausted and the final hand is sown. The larger storehouse
total wins. **A tie is possible and valid** (`winner: null` → "Seri!").

## Edge cases

| Case | Behaviour |
|---|---|
| Short final draw | Fewer than 10 left → the hand is whatever remains, and the game ends once it is sown (with 60 seeds and hands of 10 this never happens; the rule is kept for a retuned pool) |
| Opponent's hole, or off the board | `invalid_hole` → rejected, nothing changes |
| Hole already filled this turn | `hole_already_sown` → rejected, nothing changes; the hole is not marked by a refused drop |
| Seed not held | `seedId` not in the hand → rejected |
| Off-turn drop | Rejected with `not_your_turn` |
| Disconnect | Hand and sow position are server state and survive a reconnect inside the 30 s window |
| Walkout | A player leaving mid-match ends it; the remaining player wins by forfeit and both clients get `game_over` |

---

## Client mapping

Scene `Assets/Scenes/Dakon.unity`, room `dakon`, 2 seats, starts when both seats fill.

Entered from the museum's Dakon doorway, which opens the Lobby with
`new LobbyRequest("dakon", 2, SceneReference.Dakon, "Dakon")`.

### The two modes

`DakonView` renders an `IDakonSession` and owns no rules:

- **`LocalDakonSession`** — hotseat. A local `DakonBoard` answers synchronously; both
  players share one screen; `IsMyTurn` is always true, so the waiting panel never appears
  and seats are labelled `Pemain 1` / `Pemain 2`.
- **`NetDakonSession`** — online. A drop is a *request*. Nothing is drawn optimistically, so
  the board on screen is always one the server agrees with.

`DakonNetBootstrap` (`[DefaultExecutionOrder(-100)]`) reconnects in `Awake` when
`SessionData` holds a `dakon` seat and binds the net session; `DakonView.Start` starts a
local game only if nothing bound.

### State → screen

| `DakonState` field | Rendered as |
|---|---|
| `centerPoolCount` | `Pool: n` |
| `holes[20]` (`"monocot"` / `"dicot"`) | Per-hole type label / colour, ring order 0–19 |
| `activePlayer` (sessionId) | Turn label; input live only when it is this client's |
| `sownMask` (uint32, bit *i* = hole *i*) | Which own holes are closed for the rest of the turn; a tap on one is refused client-side before it is sent |
| `hand[]` (`id`, `category`, `typeId`) | The card row; `typeId` resolves to a `SeedType` asset for art and 3D prefab |
| `storehouses[sessionId]` (`monocot`, `dicot`, `total`) | Both score labels |
| `phase` | `waiting` / `in_progress` / `finished` — drives the results panel |

The hand is synced **publicly** and is always the *active* player's, so `DakonView` hides it
from the waiting player and shows "Mohon tunggu giliran {name}" instead. Leaving it on
screen would show someone else's cards and invite a click on them.

### Input → messages

```csharp
room.Send("drop_seed", new { seedId, holeIndex });  // holeIndex: one of my own, not yet sown this turn
```

**Input is two-step.** Tapping a card selects it (it scales up in the row; tapping it again
puts it back). Tapping a hole while a card is selected sends the drop. The holes are hit by a
`Pointer.current` ray against `DakonHoleTarget` trigger spheres that `DakonView` builds over
the anchors at runtime — the anchors themselves are 0.05-scaled placement transforms and
carry no collider. Taps that land on UI (the card row overlaps the near edge of the board)
belong to the UI; a tap that misses every hole leaves the card selected. While a card is
selected the highlight marker follows the hole under the pointer — a preview, not a rule.

The view refuses the two obvious cases itself so the answer is instant: a hole on the
opponent's side (`InvalidHole`) and a hole already sown this turn (`HoleAlreadySown`).
`NetDakonSession` makes the same two checks and counts a hole with a drop **in flight** as
sown, so a burst cannot queue two seeds into one hole. The server still decides.

**Drops are pipelined.** The player can place a whole hand without waiting a round trip each
time. Only the sent card locks — dimmed, not destroyed, so a fast tap still looks like it
landed. A refusal clears the whole in-flight set and the hand is re-dealt from the next
patch, which is server truth.

### Animation

`drop_applied` drives it: the card leaves the hand and a 3D seed is **thrown** at its hole.
The seed is a real rigidbody from then on — it arcs, tumbles, hits the board and settles in
the bowl among the seeds already there. Nothing steers it after the launch, which is the
point: an interpolated seed already knows where it will stop, so it can never bounce off a
rim or come to rest leaning on its neighbour. `DakonSeedBody` owns that life;
`DakonBallistics` solves the launch velocity and is unit-tested without a scene.

**The HUD and the highlight update when the drop is applied, ahead of the flight**, never
when it lands: a score that only caught up when the last seed landed reads as a bug.

Six things about the throw are load-bearing. The first four were measured rather than guessed;
the last two replaced the retry and still want a match's worth of observation:

- **Aimed by apex, not by duration.** Fixing the flight time lets a long throw go flat, and a
  seed arriving nearly horizontally skips off the far rim of a 6cm cup. Fixing the apex fixes
  the descent angle, so a throw from across the board falls in as steeply as a short one. The
  apex is **proportional to the distance** (`ApexPerDistance`, clamped): a fixed one is wrong
  at both ends, and it made a seed being nudged five centimetres lob half a metre into the
  air, which reads as a hiccup rather than a throw.
- **Aimed at the bowl floor.** A hole anchor sits at board level. Aim there and the seed
  arrives at rim height still carrying its horizontal speed.
- **Lifted before the sweep.** A settled seed lies at the bottom of the cup; thrown from
  there it drives straight into the near wall and never leaves. It is raised clear of the rim
  first — which is what scooping looks like anyway.
- **Spaced.** Drops that resolve on the same frame queue up (`throwSpacingSeconds`) and seeds
  in flight pass through each other. Spawned together they appear a centimetre apart and
  spend their first contact shoving each other off the table.
- **Steered, gently.** A launch is solved exactly, so an undisturbed throw is never touched.
  After a graze — the rim, a seed already in the bowl — the horizontal velocity is bent each
  physics step toward whatever closes the gap by the time the seed falls past the bowl's mouth
  (`SteerResponse`), capped at `MaxSteerPerSecond` so the correction stays inside the tumble
  rather than reading as a seed changing its mind.
- **Caught by a wall the model does not have.** Below `CaptureHeight` above the rim, and within
  `CaptureMargin` bowl radii of the axis, the seed is confined to a cylinder the width of the
  bowl: outward motion past the wall is reflected at `CaptureBounce`. The rattle survives; the
  escape does not.

**One throw, one arc.** Seeds used to be picked up and thrown a second time when they missed,
and that second hop was the most artificial thing on the board — nothing in the game reaches
into a hole and re-drops a seed. Steering and containment replaced it by fixing the miss while
the seed is still moving, where the correction is invisible. (The retry counts quoted here
previously — 28 clean of 45, 12 on a second throw, 5 on a third, 3 placed — are what the old
scheme measured; the replacement has not been re-measured over a full match yet.)

The by-hand placement in `Settle` is what is left of the old repair, kept because the pile is
the visible record of the score and may never disagree with the totals. It is a backstop for a
seed that never reached its bowl at all, not a stage of the throw — `WasCorrected` says it
fired, and it is not expected to.

Settled seeds are **pinned kinematic**. Left dynamic they creep through the board's thin
non-convex shell and drop through the table — about half of them over a match, measured.

At game over the results panel is raised **first** and the sweep runs second: every piled
seed is thrown to its scoring player's category bin and settles there, staggered so sixty
seeds read as a pour rather than one block. Gating the scores behind it would leave players
staring at a finished board with no idea who won.

Physics is cosmetic and client-side. Two clients settle their seeds differently, and neither
picture is the score — the server's `storehouses` are.

### Names

Seats are labelled with the nickname registered on MainMenu, read from
`BasePlayer.displayName` via `IDakonSession.DisplayNameOf(seat)`. `NetDakonSession` caches
names per seat for the same reason it caches seating — the results panel names both players
at exactly the moment an opponent is most likely to have closed their tab. A blank name
falls back to `Pemain N` (the server accepts an empty `displayName`). Your own turn reads
"Giliran kamu"; everyone else is named.

### Errors

Shared: `invalid_move`, `not_your_turn`, `room_full`, `room_not_found`. Dakon-specific:
`invalid_hole` (not on the active seat's side, or off the board), `hole_already_sown`,
`seed_not_in_hand`. All arrive as an `error` message, surface as a toast, and re-sync from
the next patch.

The toast reads its wording from `DakonErrorText.MessageFor`, in Indonesian like the rest of
the screen, and clears itself after `toastSeconds`. Both halves matter: the enum name is not a
sentence a player can act on, and a line that never cleared would still be accusing them of a
refusal several drops after the board moved on. Offline the same map is used — a hotseat
rejection is the same rejection.

### Game over

```csharp
room.OnMessage<GameOverPayload>("game_over", msg => { /* scores{sessionId:int}, winner:string|null */ });
```

`winner == null` is a tie. `NetDakonSession` keeps the final scores because the room's state
can go away while the results panel is still on screen.

The panel has its own way out — `Game Over/Exit`, "Kembali ke Museum" — because the alternative
was opening the pause menu, which reads as "the match is still going" at exactly the moment it
is not. It routes through `DakonView.BackToMainMenu` (a frozen name; see
[scene-setup.md](../scene-setup.md)) to `SceneReference.Museum`, clearing the room session on
the way so a stale reconnection token cannot send the next visit reconnecting into a room the
server has already disposed.

**There is no rematch, and it cannot be built client-side.** The server locks the room at
`startGame()` and never unlocks it, `onAuth` rejects any join once the phase leaves `waiting`,
and `persistMatchEnd` closes the match row for good. Playing again means a new room, through
the lobby. `DakonView.NewGame()` restarts an offline board only.

## Seed species

Eight `SeedType` assets in `Assets/Resources/seeds/`. The server hands out the same ids
round-robin (`DAKON_DEFAULTS`):

| Category | Species ids |
|---|---|
| Monocot | `beras`, `gabah`, `alpukat`, `zaitun` |
| Dicot | `jagung`, `kacang_tanah`, `kakao`, `mangga` |

A `SeedType`'s `TypeId` falls back to the asset name when the field is blank, so the asset
field and the server's id list must agree in **lowercase, underscored** form.

> Content note carried over from the server: some of these categories look botanically off
> (Jagung/corn grouped with Kacang Tanah). The server mirrors the client's grouping rather
> than second-guessing it — fix them together or not at all.

## Tests

`Assets/Scripts/Games/Dakon/Tests/` — `DakonBoardTests` (setup counts, hand = one side,
per-side type split, free hole order, own-side and one-seed-per-hole refusals, `SownMask`,
sweep scoring, turn/hand boundaries, three turns each, endgame and tie) and `DakonPileTests`
(pile layout stays inside its radius and stacks upward). Deterministic: the board takes an
explicit RNG seed.
