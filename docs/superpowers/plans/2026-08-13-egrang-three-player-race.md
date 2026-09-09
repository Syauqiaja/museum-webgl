# Egrang Three-Player Race Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three players race each other in one Egrang room — three lanes on the client, progress and placement owned by the Colyseus server.

**Architecture:** The client grades its own skill-check press and animates it immediately; the server bounds how often a press may arrive, banks the distance in strides, assigns places, and broadcasts every accepted step so the other two lanes replay the same stride animation. Lanes are seat-absolute: seat 0 is `Player 1 Point` on every screen, and the camera moves to whichever lane is yours.

**Tech Stack:** Unity 6000.3.19f1 (URP, WebGL, Input System), C#, Unity Test Framework (NUnit). Node 20 + Colyseus 0.17, `@colyseus/schema` 4.0.27, TypeScript, mocha + `@colyseus/testing`.

**Spec:** `docs/superpowers/specs/2026-08-13-egrang-three-player-race-design.md` (in the Unity repo).

## Global Constraints

- **Two repos.** Client `/Users/mac/Documents/Works/unity/Museum Minigames`; server `/Users/mac/Documents/Works/nodejs/museum-minigames`. Every task says which. Commit in the repo the task touches.
- **Units:** one unit = one stride = `stepLength` = 0.5 m. `StepsFor`: Full = 2, Half = 1, Fail = 0. Lane length 25 m, so `finishUnits = 50`.
- **Rate limit:** a `step` closer than **500 ms** to the previous accepted one is rejected.
- **Countdown:** `startsAtMs = Date.now() + 3000` at game start.
- **Straggler timeout:** the race ends 15 s after the first finisher if others are still running.
- **Stick shapes:** `0 = Persegi, 1 = Lingkaran, 2 = Segitiga` (matches `EgrangStickShape`).
- **Server tests:** `npm test` (mocha, `DB_DISABLED=1`, only globs `test/*.test.ts`). There is **no lint script** — do not invent one.
- **Server schema style:** legacy `@type(...)` property decorators, no explicit field indices, `.js` extensions on relative imports.
- **Client assembly rule:** `Museum.Net` references game assemblies, never the reverse. `Museum.Games.Egrang` may reference only `Museum.Core`, `UnityEngine.UI`, `Unity.TextMeshPro`, `Unity.InputSystem`.
- **Never re-type `BaseGameState.players`** — it is shared with Dakon and moving field order breaks it.
- Do not hand-edit `Library/`, `Temp/`, `Logs/`, or `Unity.*.csproj`.

---

## File Structure

**Server** (`/Users/mac/Documents/Works/nodejs/museum-minigames`)

- `docs/games/egrang.md` — modify: the rules, currently an all-TODO stub that declares itself ground truth.
- `docs/protocol.md` — modify: replace the TODO `## Egrang` section (lines 49–57).
- `src/rooms/schema/EgrangState.ts` — modify: add `EgrangRacer`, `racers`, `finishUnits`, `startsAtMs`.
- `src/games/egrang/EgrangRace.ts` — create: pure TS race bookkeeping (units, places), no Colyseus imports, unit-testable like `DakonBoard`.
- `src/rooms/EgrangRoom.ts` — modify: messages, `onGameStart`, finish handling, `scoreOf`.
- `test/EgrangRace.test.ts` — create: pure engine tests.
- `test/EgrangRoom.test.ts` — create: room/message tests over `@colyseus/testing`.

**Client** (`/Users/mac/Documents/Works/unity/Museum Minigames`)

- `Assets/Scripts/Games/Egrang/EgrangStepMover.cs` — modify: expose `StepLength`, add `SnapTo`.
- `Assets/Scripts/Games/Egrang/EgrangRacer.cs` — create: one lane's racer.
- `Assets/Scripts/Games/Egrang/EgrangSeating.cs` — create: pure seat→lane mapping.
- `Assets/Scripts/Games/Egrang/IEgrangSession.cs` — create: what `EgrangRace` talks to.
- `Assets/Scripts/Games/Egrang/EgrangRace.cs` — create: scene-level conductor.
- `Assets/Scripts/Games/Egrang/EgrangProgressView.cs` — modify: accept an explicit track.
- `Assets/Scripts/Net/NetEgrangSession.cs` — create: `IEgrangSession` over a Colyseus room.
- `Assets/Scripts/Net/EgrangNetBootstrap.cs` — create: reconnect + bind.
- `Assets/Scripts/Net/Museum.Net.asmdef` — modify: reference `Museum.Games.Egrang`.
- `Assets/Scripts/Games/Egrang/Tests/EgrangSeatingTests.cs`, `.../EgrangRacerTests.cs` — create.
- `Assets/Docs/games/egrang.md` — modify: client-side rules note.
- `Assets/Scenes/Egrang.unity` — modify: lanes 2 and 3, component wiring.

Server first: the contract is the thing both sides implement against.

---

### Task 1: Server — write the race rules docs

The server repo's own instructions say room logic must not be written against invented rules, and that `docs/games/egrang.md` is ground truth once filled in. Fill it in first.

**Files:**
- Modify: `docs/games/egrang.md` (whole file — it is a 27-line all-TODO stub)
- Modify: `docs/protocol.md:49-57` (the `## Egrang` section)

- [ ] **Step 1: Rewrite `docs/games/egrang.md`**

```markdown
# Egrang — stilt race

Three players race on stilts down parallel 25 m lanes. Rules below are the ground
truth for `EgrangRoom`; the Unity client implements the same numbers.

## Objective

First racer to cover the lane wins. All three are ranked: places 1, 2 and 3.

## Track setup

Three lanes, one per seat, identical in length. Distance is counted in **strides**,
not meters: one stride is 0.5 m on the client, and the race is `finishUnits = 50`
strides (25 m). The server never sees world coordinates.

## Real-time rules

A racer walks by timing presses against a sweeping skill-check bar. Each press
scores one of three outcomes, worth a fixed number of strides:

| Outcome | Value | Strides |
|---------|-------|---------|
| Fail    | 0     | 0 — stumbles in place |
| Half    | 1     | 1 |
| Full    | 2     | 2 |

Each player picks one of three stilts before the start, which changes only how hard
the timing is (green-zone width and sweep speed): `0` persegi, `1` lingkaran,
`2` segitiga. The stilt does not change what a step is worth.

The match starts on a 3-second countdown: `startsAtMs` is stamped at game start and
no step counts before it.

## Player actions / inputs

- `choose_stick { shape }` while waiting.
- `step { result }` during the race, one per press.

**The client grades its own press.** The bar's cursor sweeps a lap in 0.85–2.0 s, so
grading on arrival would turn a green press into a yellow one on any real
connection. The server instead bounds the *rate*: a press within 500 ms of the
previous accepted one is rejected. The bar's own lockout is 600 ms and a full step's
animation runs 1.12 s, so no honest press is ever refused. Every number that decides
the race — banked strides, places, winner, persisted score — is the server's.

## Win condition

Reaching 50 strides takes the next free place (1, then 2, then 3). The match ends
when all racers are placed, or 15 s after the first finisher, whichever comes first.
Unplaced racers keep `place = 0` and their banked strides, which is their score.

## Edge cases

- A step before `startsAtMs`, from an unseated client, with an out-of-range result,
  or from a racer already placed: rejected with `invalid_move`, no state change.
- Banked strides clamp at `finishUnits`; a Full step across the line is not overshot.
- A racer who leaves mid-race stops sending steps. The base class's forfeit
  bookkeeping applies and the straggler timeout ends the race for the rest.
- Reconnect: `stepUnits` is synced state, so a returning client places its racer
  from it rather than replaying steps.
```

- [ ] **Step 2: Replace the `## Egrang` section of `docs/protocol.md`**

```markdown
## Egrang

Room name: `egrang` (implemented — `EgrangRoom` + `src/games/egrang/EgrangRace.ts`). Rules: [games/egrang.md](games/egrang.md). 3 seats, host may start at 2.

Distance is counted in **strides** (0.5 m each on the client), never in world coordinates. The race is 50 strides.

- **State schema** (`EgrangState`, on top of the shared `phase` / `hostSessionId` / `players`):
  - `racers: { [sessionId]: { stick, stepUnits, place, ready } }` — `stick` is `0` persegi / `1` lingkaran / `2` segitiga; `stepUnits` is banked strides; `place` is `0` until the racer finishes, then `1..3`; `ready` is set once a stilt is chosen.
  - `finishUnits: number` — race length in strides. 50.
  - `startsAtMs: number` — wall-clock start, stamped 3 s ahead at game start. No step counts before it.
- **Client → server:**
  - `choose_stick` — `{ shape: 0 | 1 | 2 }` — only while `phase === "waiting"`.
  - `step` — `{ result: 0 | 1 | 2 }` — Fail / Half / Full, worth 0 / 1 / 2 strides. The client grades its own press (the cursor sweeps in under a second; grading on arrival would misgrade it). The server bounds the rate instead: a press within 500 ms of the previous accepted one is rejected.
  - `start_game` — shared, see above.
- **Server → client:**
  - `step_taken` — `{ sessionId, result, stepUnits }` — one per accepted press, to everyone including the sender. Remote clients replay the stride animation from it; the sender uses it to confirm its prediction.
  - `error` — `{ code, message }`: `invalid_move` for a malformed payload, a step before the countdown, a step from an unseated or already-placed client, or one inside the rate limit.
  - `game_over` — `{ places: { [sessionId]: number }, winner: string | null }` — `0` means never finished. Emitted when all racers are placed or 15 s after the first finisher.

Nothing about the skill-check bar is simulated server-side: the room stores outcomes, not cursor positions.
```

- [ ] **Step 3: Commit**

```bash
cd /Users/mac/Documents/Works/nodejs/museum-minigames
git add docs/games/egrang.md docs/protocol.md
git commit -m "docs(egrang): write the race rules and wire protocol"
```

---

### Task 2: Server — the race engine

Pure TypeScript, no Colyseus imports, the way `src/games/dakon/DakonBoard.ts` is. This is where every rule number lives so the room stays translation-only.

**Files:**
- Create: `src/games/egrang/EgrangRace.ts`
- Test: `test/EgrangRace.test.ts`

**Interfaces:**
- Consumes: nothing.
- Produces: `EgrangRace` class with `constructor(sessionIds: string[], finishUnits?: number)`, `readonly finishUnits: number`, `step(sessionId: string, result: number, nowMs: number): StepOutcome`, `unitsOf(sessionId): number`, `placeOf(sessionId): number`, `get allPlaced(): boolean`, `get firstFinishMs(): number | null`, `get winner(): string | null`, `places(): Record<string, number>`, `arm(startsAtMs: number)`. `type StepOutcome = { ok: true; stepUnits: number; place: number } | { ok: false; reason: "not_seated" | "not_started" | "bad_result" | "too_soon" | "already_placed" }`.

- [ ] **Step 1: Write the failing test**

`test/EgrangRace.test.ts`:

```ts
import assert from "assert";
import { EgrangRace } from "../src/games/egrang/EgrangRace.js";

const START = 1_000_000;

function armed(sessions = ["a", "b", "c"]) {
  const race = new EgrangRace(sessions);
  race.arm(START);
  return race;
}

describe("EgrangRace", () => {
  it("banks 2 strides for a full step, 1 for half, 0 for fail", () => {
    const race = armed();

    assert.deepStrictEqual(race.step("a", 2, START), { ok: true, stepUnits: 2, place: 0 });
    assert.deepStrictEqual(race.step("a", 1, START + 600), { ok: true, stepUnits: 3, place: 0 });
    assert.deepStrictEqual(race.step("a", 0, START + 1200), { ok: true, stepUnits: 3, place: 0 });
  });

  it("rejects a step inside the 500 ms rate limit and banks nothing", () => {
    const race = armed();
    race.step("a", 2, START);

    assert.deepStrictEqual(race.step("a", 2, START + 499), { ok: false, reason: "too_soon" });
    assert.strictEqual(race.unitsOf("a"), 2);
  });

  it("rejects steps before the countdown, from strangers, and out of range", () => {
    const race = armed();

    assert.deepStrictEqual(race.step("a", 2, START - 1), { ok: false, reason: "not_started" });
    assert.deepStrictEqual(race.step("zz", 2, START), { ok: false, reason: "not_seated" });
    assert.deepStrictEqual(race.step("a", 7, START), { ok: false, reason: "bad_result" });
  });

  it("clamps at the finish and hands out places in finishing order", () => {
    const race = new EgrangRace(["a", "b", "c"], 4);
    race.arm(START);

    race.step("a", 2, START);
    assert.deepStrictEqual(race.step("a", 2, START + 600), { ok: true, stepUnits: 4, place: 1 });

    race.step("b", 2, START);
    race.step("b", 2, START + 600);
    assert.strictEqual(race.placeOf("b"), 2);

    assert.deepStrictEqual(race.step("a", 2, START + 1200), { ok: false, reason: "already_placed" });
    assert.strictEqual(race.unitsOf("a"), 4);
    assert.strictEqual(race.allPlaced, false);
    assert.strictEqual(race.winner, "a");
    assert.strictEqual(race.firstFinishMs, START + 600);
  });

  it("reports every racer's place, zero for the unfinished", () => {
    const race = new EgrangRace(["a", "b"], 2);
    race.arm(START);
    race.step("a", 2, START);

    assert.deepStrictEqual(race.places(), { a: 1, b: 0 });
    assert.strictEqual(race.allPlaced, false);
  });
});
```

- [ ] **Step 2: Run it and watch it fail**

```bash
cd /Users/mac/Documents/Works/nodejs/museum-minigames
npm test -- --grep "EgrangRace"
```

Expected: failure resolving `../src/games/egrang/EgrangRace.js`.

- [ ] **Step 3: Write the engine**

`src/games/egrang/EgrangRace.ts`:

```ts
/** How many strides an outcome is worth: fail 0, half 1, full 2. */
export function stepsFor(result: number): number {
  return result === 2 ? 2 : result === 1 ? 1 : 0;
}

export type StepReject =
  | "not_seated"
  | "not_started"
  | "bad_result"
  | "too_soon"
  | "already_placed";

export type StepOutcome =
  | { ok: true; stepUnits: number; place: number }
  | { ok: false; reason: StepReject };

/** Shortest gap between two accepted presses. The bar's own lockout is 600 ms. */
export const MIN_STEP_INTERVAL_MS = 500;

/** Race length in strides: 25 m of lane at 0.5 m a stride. */
export const DEFAULT_FINISH_UNITS = 50;

interface Racer {
  units: number;
  place: number;
  lastStepMs: number;
}

/**
 * Egrang race bookkeeping — see docs/games/egrang.md.
 *
 * Deliberately knows nothing about Colyseus or about world coordinates: it counts
 * strides and hands out places, so the whole ruleset can be tested without a server.
 * The client grades its own press; this class bounds how often one may arrive.
 */
export class EgrangRace {
  private readonly racers = new Map<string, Racer>();
  private startsAtMs = Number.POSITIVE_INFINITY;
  private nextPlace = 1;
  private firstFinish: number | null = null;

  constructor(sessionIds: readonly string[], readonly finishUnits: number = DEFAULT_FINISH_UNITS) {
    for (const sessionId of sessionIds) {
      this.racers.set(sessionId, { units: 0, place: 0, lastStepMs: Number.NEGATIVE_INFINITY });
    }
  }

  /** Opens the race at a wall-clock instant. Steps before it do not count. */
  arm(startsAtMs: number): void {
    this.startsAtMs = startsAtMs;
  }

  step(sessionId: string, result: number, nowMs: number): StepOutcome {
    const racer = this.racers.get(sessionId);
    if (!racer) return { ok: false, reason: "not_seated" };
    if (nowMs < this.startsAtMs) return { ok: false, reason: "not_started" };
    if (!Number.isInteger(result) || result < 0 || result > 2) return { ok: false, reason: "bad_result" };
    if (racer.place > 0) return { ok: false, reason: "already_placed" };
    if (nowMs - racer.lastStepMs < MIN_STEP_INTERVAL_MS) return { ok: false, reason: "too_soon" };

    racer.lastStepMs = nowMs;
    racer.units = Math.min(racer.units + stepsFor(result), this.finishUnits);

    if (racer.units >= this.finishUnits) {
      racer.place = this.nextPlace++;
      this.firstFinish ??= nowMs;
    }

    return { ok: true, stepUnits: racer.units, place: racer.place };
  }

  unitsOf(sessionId: string): number {
    return this.racers.get(sessionId)?.units ?? 0;
  }

  placeOf(sessionId: string): number {
    return this.racers.get(sessionId)?.place ?? 0;
  }

  get allPlaced(): boolean {
    for (const racer of this.racers.values()) if (racer.place === 0) return false;
    return this.racers.size > 0;
  }

  /** When the first racer crossed, or null while nobody has. Drives the straggler timeout. */
  get firstFinishMs(): number | null {
    return this.firstFinish;
  }

  get winner(): string | null {
    for (const [sessionId, racer] of this.racers) if (racer.place === 1) return sessionId;
    return null;
  }

  places(): Record<string, number> {
    const out: Record<string, number> = {};
    for (const [sessionId, racer] of this.racers) out[sessionId] = racer.place;
    return out;
  }
}
```

- [ ] **Step 4: Run the tests and watch them pass**

```bash
npm test -- --grep "EgrangRace"
```

Expected: 5 passing.

- [ ] **Step 5: Commit**

```bash
git add src/games/egrang/EgrangRace.ts test/EgrangRace.test.ts
git commit -m "feat(egrang): count strides and places in a testable race engine"
```

---

### Task 3: Server — schema and race start

**Files:**
- Modify: `src/rooms/schema/EgrangState.ts`
- Modify: `src/rooms/EgrangRoom.ts`
- Test: `test/EgrangRoom.test.ts`

**Interfaces:**
- Consumes: `EgrangRace`, `DEFAULT_FINISH_UNITS` from Task 2.
- Produces: `EgrangRacer` schema class (`stick`, `stepUnits`, `place`, `ready`); `EgrangState.racers: MapSchema<EgrangRacer>`, `.finishUnits: number`, `.startsAtMs: number`; on `EgrangRoom`, `protected countdownMs = 3000` and `protected stragglerMs = 15000` (tests shorten them).

- [ ] **Step 1: Write the failing test**

`test/EgrangRoom.test.ts`:

```ts
import assert from "assert";
import { type ColyseusTestServer } from "@colyseus/testing";
import { testServer } from "./support/server.js";
import { EgrangState } from "../src/rooms/schema/EgrangState.js";

describe("EgrangRoom", () => {
  let colyseus: ColyseusTestServer;

  before(async () => (colyseus = await testServer()));
  beforeEach(async () => await colyseus.cleanup());

  async function startedRace() {
    const host = await colyseus.sdk.create<EgrangState>("egrang", { private: true, displayName: "A" });
    const two = await colyseus.sdk.joinById<EgrangState>(host.roomId, { displayName: "B" });
    const three = await colyseus.sdk.joinById<EgrangState>(host.roomId, { displayName: "C" });
    const room = colyseus.getRoomById<EgrangState>(host.roomId);
    await room.waitForNextPatch();
    return { host, two, three, room };
  }

  it("seats three racers and arms a countdown when the room fills", async () => {
    const { host, two, three, room } = await startedRace();

    assert.strictEqual(room.state.phase, "in_progress");
    assert.strictEqual(room.state.racers.size, 3);
    assert.strictEqual(room.state.finishUnits, 50);
    assert.ok(room.state.startsAtMs > Date.now());

    for (const client of [host, two, three]) {
      const racer = room.state.racers.get(client.sessionId);
      assert.ok(racer, "every seated client gets a racer");
      assert.strictEqual(racer.stepUnits, 0);
      assert.strictEqual(racer.place, 0);
    }
  });

  it("records a stilt choice before the race starts", async () => {
    const host = await colyseus.sdk.create<EgrangState>("egrang", { private: true, displayName: "A" });
    const room = colyseus.getRoomById<EgrangState>(host.roomId);
    await room.waitForNextPatch();

    host.send("choose_stick", { shape: 2 });
    await room.waitForNextPatch();

    const racer = room.state.racers.get(host.sessionId);
    assert.strictEqual(racer?.stick, 2);
    assert.strictEqual(racer?.ready, true);
  });
});
```

Note: `choose_stick` arrives while waiting, so a racer must exist from `onJoin`, not only from `onGameStart`.

- [ ] **Step 2: Run it and watch it fail**

```bash
npm test -- --grep "EgrangRoom"
```

Expected: failure — `room.state.racers` is undefined.

- [ ] **Step 3: Extend the schema**

`src/rooms/schema/EgrangState.ts` (replace the empty class body, keep the file's header comment style):

```ts
import { MapSchema, Schema, type } from "@colyseus/schema";
import { BaseGameState } from "./BaseGameState.js";

/** One seat's race standing. Distance is in strides, never metres — see docs/games/egrang.md. */
export class EgrangRacer extends Schema {
  /** Stilt: 0 persegi, 1 lingkaran, 2 segitiga. Difficulty only; it never changes what a step is worth. */
  @type("uint8") stick: number = 0;

  /** Strides banked so far, clamped to `finishUnits`. */
  @type("uint16") stepUnits: number = 0;

  /** 0 until the racer crosses, then 1..3 in finishing order. */
  @type("uint8") place: number = 0;

  /** Set once a stilt has been chosen. */
  @type("boolean") ready: boolean = false;
}

/**
 * Egrang — the three-lane stilt race. See docs/games/egrang.md and docs/protocol.md#egrang.
 *
 * `racers` is a second map rather than a re-typed `players`: the inherited map is
 * shared with Dakon, and re-declaring it would move field order in a base two games
 * depend on.
 */
export class EgrangState extends BaseGameState {
  /** Race standings, keyed by sessionId. */
  @type({ map: EgrangRacer }) racers = new MapSchema<EgrangRacer>();

  /** Race length in strides. */
  @type("uint16") finishUnits: number = 0;

  /** Wall-clock start. Steps before it do not count. */
  @type("number") startsAtMs: number = 0;
}
```

- [ ] **Step 4: Seat racers and arm the race**

`src/rooms/EgrangRoom.ts` — replace the class body's stubs, keeping the file's doc comment updated to say the race now exists:

```ts
import { Client } from "colyseus";
import { BaseGameRoom } from "./BaseGameRoom.js";
import { EgrangRacer, EgrangState } from "./schema/EgrangState.js";
import { DEFAULT_FINISH_UNITS, EgrangRace } from "../games/egrang/EgrangRace.js";

export class EgrangRoom extends BaseGameRoom<EgrangState> {
  maxClients = 3;
  protected minPlayers = 2;

  /** Countdown before steps count. Shortened by tests. */
  protected countdownMs = 3000;

  /** How long the race waits for stragglers after the first finisher. Shortened by tests. */
  protected stragglerMs = 15000;

  private race?: EgrangRace;

  messages = {
    choose_stick: function (this: EgrangRoom, client: Client, message: any) {
      this.handleChooseStick(client, message);
    },
  };

  protected createInitialState(): EgrangState {
    return new EgrangState();
  }

  protected onRoomCreated(_options: any): void {
    this.state.finishUnits = DEFAULT_FINISH_UNITS;
  }

  protected onGameStart(): void {
    // A racer already exists per seat from the lobby, so a stilt chosen while
    // waiting survives the start.
    for (const sessionId of this.state.players.keys()) this.racerFor(sessionId);

    this.race = new EgrangRace([...this.state.racers.keys()], this.state.finishUnits);
    this.state.startsAtMs = Date.now() + this.countdownMs;
    this.race.arm(this.state.startsAtMs);
  }

  protected onOpponentLeft(_client: Client): void {
    // The base has already written the forfeit. A departed racer simply stops
    // stepping; the straggler timeout ends it for the rest.
  }

  protected scoreOf(sessionId: string): number {
    return this.race?.unitsOf(sessionId) ?? 0;
  }

  /** The racer row for a seat, created on first use so lobby-time choices have somewhere to land. */
  private racerFor(sessionId: string): EgrangRacer {
    let racer = this.state.racers.get(sessionId);
    if (!racer) {
      racer = new EgrangRacer();
      this.state.racers.set(sessionId, racer);
    }
    return racer;
  }

  private handleChooseStick(client: Client, message: any) {
    if (this.state.phase !== "waiting") {
      this.sendError(client, "invalid_move", "The stilt cannot change once the race has started.");
      return;
    }

    const shape = Number(message?.shape);
    if (!Number.isInteger(shape) || shape < 0 || shape > 2) {
      this.sendError(client, "invalid_move", "choose_stick needs { shape: 0 | 1 | 2 }.");
      return;
    }

    const racer = this.racerFor(client.sessionId);
    racer.stick = shape;
    racer.ready = true;
  }
}
```

- [ ] **Step 5: Run the tests and watch them pass**

```bash
npm test -- --grep "EgrangRoom"
```

Expected: 2 passing. Then run the whole suite — `npm test` — and confirm Dakon and StartGame still pass, since the schema base was touched.

- [ ] **Step 6: Commit**

```bash
git add src/rooms/schema/EgrangState.ts src/rooms/EgrangRoom.ts test/EgrangRoom.test.ts
git commit -m "feat(egrang): seat racers, hold stilt choices, arm the countdown"
```

---

### Task 4: Server — accept steps, broadcast them, end the race

**Files:**
- Modify: `src/rooms/EgrangRoom.ts`
- Test: `test/EgrangRoom.test.ts` (add cases)

**Interfaces:**
- Consumes: `EgrangRace.step/places/winner/allPlaced/firstFinishMs`, `EgrangRoom.countdownMs`, `.stragglerMs` from Task 3.
- Produces: message `step {result}`; broadcasts `step_taken {sessionId, result, stepUnits}` and `game_over {places, winner}`.

- [ ] **Step 1: Write the failing tests**

Append inside the `describe("EgrangRoom")` block in `test/EgrangRoom.test.ts`:

```ts
  /** Resolves with the first message of `type`, or null after 250 ms. */
  function nextMessage<T = any>(client: any, type: string): Promise<T | null> {
    return new Promise((resolve) => {
      const timer = setTimeout(() => resolve(null), 250);
      client.onMessage(type, (payload: T) => {
        clearTimeout(timer);
        resolve(payload);
      });
    });
  }

  it("banks an accepted step and tells everyone about it", async () => {
    const { host, two, room } = await startedRace();
    room.state.startsAtMs = Date.now() - 1;

    const seen = nextMessage(two, "step_taken");
    host.send("step", { result: 2 });

    const payload = await seen;
    assert.deepStrictEqual(payload, { sessionId: host.sessionId, result: 2, stepUnits: 2 });
    assert.strictEqual(room.state.racers.get(host.sessionId)?.stepUnits, 2);
  });

  it("rejects a second step inside the rate limit", async () => {
    const { host, room } = await startedRace();
    room.state.startsAtMs = Date.now() - 1;

    host.send("step", { result: 2 });
    await room.waitForNextPatch();

    const error = nextMessage(host, "error");
    host.send("step", { result: 2 });

    assert.strictEqual((await error)?.code, "invalid_move");
    assert.strictEqual(room.state.racers.get(host.sessionId)?.stepUnits, 2);
  });

  it("rejects a step taken before the countdown ends", async () => {
    const { host, room } = await startedRace();

    const error = nextMessage(host, "error");
    host.send("step", { result: 2 });

    assert.strictEqual((await error)?.code, "invalid_move");
    assert.strictEqual(room.state.racers.get(host.sessionId)?.stepUnits, 0);
  });

  it("places a finisher and ends the race once the stragglers time out", async () => {
    const { host, room } = await startedRace();
    room.state.startsAtMs = Date.now() - 1;
    (room as any).stragglerMs = 50;
    (room as any).race.finishUnits = 2;
    room.state.finishUnits = 2;

    const over = nextMessage(host, "game_over");
    host.send("step", { result: 2 });

    const payload = await over;
    assert.strictEqual(payload?.winner, host.sessionId);
    assert.strictEqual(payload?.places[host.sessionId], 1);
    assert.strictEqual(room.state.racers.get(host.sessionId)?.place, 1);
    assert.strictEqual(room.state.phase, "finished");
  });
```

`finishUnits` is `readonly` on the engine, so the last test reaches through `as any` deliberately — the alternative is a production setter that exists only for tests.

- [ ] **Step 2: Run them and watch them fail**

```bash
npm test -- --grep "EgrangRoom"
```

Expected: the four new cases fail; `step` is not a registered message so nothing is banked and no broadcast arrives.

- [ ] **Step 3: Add the `step` handler and the ending**

In `src/rooms/EgrangRoom.ts`, add to `messages`:

```ts
    step: function (this: EgrangRoom, client: Client, message: any) {
      this.handleStep(client, message);
    },
```

and add these members:

```ts
  private handleStep(client: Client, message: any) {
    const race = this.race;

    if (!race || this.state.phase !== "in_progress") {
      this.sendError(client, "invalid_move", "No race is running.");
      return;
    }

    const result = Number(message?.result);
    const outcome = race.step(client.sessionId, result, Date.now());

    if (!outcome.ok) {
      this.sendError(client, "invalid_move", this.rejectMessage(outcome.reason));
      return;
    }

    const racer = this.state.racers.get(client.sessionId)!;
    racer.stepUnits = outcome.stepUnits;
    racer.place = outcome.place;

    this.broadcast("step_taken", {
      sessionId: client.sessionId,
      result,
      stepUnits: outcome.stepUnits,
    });

    if (race.allPlaced) {
      this.finish();
      return;
    }

    // First one home starts the clock on everyone else. One timer, armed once.
    if (outcome.place === 1) {
      this.clock.setTimeout(() => this.finish(), this.stragglerMs);
    }
  }

  /** Closes the race exactly once: the straggler timer and the last finisher both land here. */
  private finish(): void {
    if (!this.race || this.state.phase === "finished") return;

    this.state.phase = "finished";

    const winner = this.race.winner;
    void this.persistMatchEnd("completed", this.collectResults(), winner);
    this.broadcast("game_over", { places: this.race.places(), winner });
  }

  private rejectMessage(reason: string): string {
    switch (reason) {
      case "not_seated": return "You are not racing in this match.";
      case "not_started": return "The race has not started yet.";
      case "bad_result": return "step needs { result: 0 | 1 | 2 }.";
      case "too_soon": return "That step came too soon after the last one.";
      default: return "You have already finished.";
    }
  }
```

- [ ] **Step 4: Run the tests and watch them pass**

```bash
npm test
```

Expected: the whole suite green, including Dakon and StartGame.

- [ ] **Step 5: Commit**

```bash
git add src/rooms/EgrangRoom.ts test/EgrangRoom.test.ts
git commit -m "feat(egrang): validate steps, broadcast them, and place finishers"
```

---

### Task 5: Client — mover exposes stride length and a snap

Small, but everything downstream needs it: the racer converts server strides into world metres, and reconnect/desync repair has to place a racer without animating.

**Files:**
- Modify: `Assets/Scripts/Games/Egrang/EgrangStepMover.cs`
- Test: `Assets/Scripts/Games/Egrang/Tests/PlayMode/EgrangStepMoverSnapTests.cs` (create — PlayMode, because it needs a live GameObject)

**Interfaces:**
- Produces: `EgrangStepMover.StepLength { get; }` (float, metres per stride) and `EgrangStepMover.SnapTo(Vector3 position)`.

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Museum.Games.Egrang.Tests.PlayMode
{
    public class EgrangStepMoverSnapTests
    {
        [Test]
        public void SnapTo_PlacesTheMoverExactly()
        {
            var go = new GameObject("mover");
            var mover = go.AddComponent<EgrangStepMover>();

            mover.SnapTo(new Vector3(1f, 2f, 3f));

            Assert.That(go.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void StepLength_IsTheAuthoredStride()
        {
            var go = new GameObject("mover");
            var mover = go.AddComponent<EgrangStepMover>();

            Assert.That(mover.StepLength, Is.EqualTo(0.5f).Within(0.0001f));
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Unity → Window → General → Test Runner → PlayMode → run `EgrangStepMoverSnapTests`.
Expected: compile error, `StepLength`/`SnapTo` not defined.

- [ ] **Step 3: Add the two members**

In `EgrangStepMover.cs`, next to `FullStepSeconds`:

```csharp
        /// <summary>Metres covered by one stride. One server-side unit of race progress is exactly this.</summary>
        public float StepLength => stepLength;

        /// <summary>
        /// Puts the racer somewhere without animating, abandoning any step in flight. This is how a
        /// racer arrives at its position on reconnect, and how a mispredicted local step is repaired:
        /// the server's distance is the truth, and sliding to it would read as a step that never
        /// happened.
        /// </summary>
        public void SnapTo(Vector3 position)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            transform.position = position;
        }
```

- [ ] **Step 4: Run the tests and watch them pass**

Test Runner → PlayMode → 2 passing.

- [ ] **Step 5: Commit**

```bash
cd "/Users/mac/Documents/Works/unity/Museum Minigames"
git add Assets/Scripts/Games/Egrang/EgrangStepMover.cs Assets/Scripts/Games/Egrang/Tests/PlayMode/EgrangStepMoverSnapTests.cs
git commit -m "feat(egrang): let the mover report its stride and snap to a position"
```

---

### Task 6: Client — seat-to-lane mapping

A plain C# class, so the rule that decides "which lane am I" is testable without a scene.

**Files:**
- Create: `Assets/Scripts/Games/Egrang/EgrangSeating.cs`
- Test: `Assets/Scripts/Games/Egrang/Tests/EgrangSeatingTests.cs`

**Interfaces:**
- Produces: `EgrangSeating` with `void Assign(string sessionId, int seat)`, `int LaneOf(string sessionId)` (−1 when unknown), `string SessionAt(int lane)` (null when empty), `int LocalLane { get; }`, `void SetLocal(string sessionId)`, `int Count { get; }`.

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;

namespace Museum.Games.Egrang.Tests
{
    public class EgrangSeatingTests
    {
        [Test]
        public void LaneFollowsTheServerSeat_NotJoinOrder()
        {
            var seating = new EgrangSeating();

            seating.Assign("c", 2);
            seating.Assign("a", 0);
            seating.Assign("b", 1);

            Assert.That(seating.LaneOf("a"), Is.EqualTo(0));
            Assert.That(seating.LaneOf("b"), Is.EqualTo(1));
            Assert.That(seating.LaneOf("c"), Is.EqualTo(2));
            Assert.That(seating.SessionAt(2), Is.EqualTo("c"));
            Assert.That(seating.Count, Is.EqualTo(3));
        }

        [Test]
        public void LocalLaneIsWhicheverSeatTheLocalSessionGot()
        {
            var seating = new EgrangSeating();
            seating.Assign("a", 0);
            seating.Assign("me", 2);

            seating.SetLocal("me");

            Assert.That(seating.LocalLane, Is.EqualTo(2));
        }

        [Test]
        public void UnknownSessionsAndEmptyLanesReadAsAbsent()
        {
            var seating = new EgrangSeating();
            seating.Assign("a", 0);

            Assert.That(seating.LaneOf("nobody"), Is.EqualTo(-1));
            Assert.That(seating.SessionAt(1), Is.Null);
            Assert.That(seating.LocalLane, Is.EqualTo(-1));
        }

        [Test]
        public void ReassigningASessionMovesItRatherThanDuplicating()
        {
            var seating = new EgrangSeating();
            seating.Assign("a", 0);

            seating.Assign("a", 1);

            Assert.That(seating.LaneOf("a"), Is.EqualTo(1));
            Assert.That(seating.SessionAt(0), Is.Null);
            Assert.That(seating.Count, Is.EqualTo(1));
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Test Runner → EditMode → run `EgrangSeatingTests`. Expected: compile error, `EgrangSeating` not defined.

- [ ] **Step 3: Write the class**

```csharp
using System.Collections.Generic;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// Which lane each player races in. Lanes are the server's seat numbers, identically on every
    /// screen: the racer in lane 2 is the same person for all three players, which is what lets one
    /// client talk about another's position at all — and what a spectator view would need.
    ///
    /// Plain C# rather than a MonoBehaviour so the mapping can be tested without a scene.
    /// </summary>
    public sealed class EgrangSeating
    {
        readonly Dictionary<string, int> _laneBySession = new Dictionary<string, int>();
        readonly Dictionary<int, string> _sessionByLane = new Dictionary<int, string>();
        string _local = string.Empty;

        /// <summary>How many seats are filled.</summary>
        public int Count => _laneBySession.Count;

        /// <summary>The local player's lane, or -1 before the local session is known or seated.</summary>
        public int LocalLane => LaneOf(_local);

        /// <summary>Seats a session. Re-seating an existing session moves it rather than duplicating it.</summary>
        public void Assign(string sessionId, int seat)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            if (_laneBySession.TryGetValue(sessionId, out int previous)) _sessionByLane.Remove(previous);

            _laneBySession[sessionId] = seat;
            _sessionByLane[seat] = sessionId;
        }

        /// <summary>Marks which session is this client. Safe to call before the seat is known.</summary>
        public void SetLocal(string sessionId) => _local = sessionId ?? string.Empty;

        /// <summary>The lane a session races in, or -1 if it is not seated.</summary>
        public int LaneOf(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return -1;
            return _laneBySession.TryGetValue(sessionId, out int lane) ? lane : -1;
        }

        /// <summary>Who races in a lane, or null if it is empty.</summary>
        public string SessionAt(int lane) => _sessionByLane.TryGetValue(lane, out string sessionId) ? sessionId : null;
    }
}
```

- [ ] **Step 4: Run the tests and watch them pass**

Test Runner → EditMode → 4 passing.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Games/Egrang/EgrangSeating.cs Assets/Scripts/Games/Egrang/Tests/EgrangSeatingTests.cs
git commit -m "feat(egrang): map sessions to lanes by server seat"
```

---

### Task 7: Client — one lane's racer

**Files:**
- Create: `Assets/Scripts/Games/Egrang/EgrangRacer.cs`
- Test: `Assets/Scripts/Games/Egrang/Tests/PlayMode/EgrangRacerTests.cs`

**Interfaces:**
- Consumes: `EgrangStepMover.StepLength`, `.SnapTo`, `.OnStepResult` (Task 5); `EgrangRaceTrack.StartPosition`, `.FinishPosition`, `.Progress`.
- Produces: `EgrangRacer` with `int Lane { get; }`, `EgrangRaceTrack Track { get; }`, `void ApplyStep(EgrangStepResult result)`, `void SnapToUnits(int units)`, `int BankedUnits { get; }`, `EgrangTrackProgress Progress { get; }`.

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Museum.Games.Egrang.Tests.PlayMode
{
    public class EgrangRacerTests
    {
        GameObject _root;

        EgrangRacer Build(out EgrangStepMover mover)
        {
            _root = new GameObject("lane");

            var start = new GameObject("start").transform;
            var finish = new GameObject("finish").transform;
            start.position = new Vector3(0f, 0f, 0f);
            finish.position = new Vector3(0f, 0f, 25f);
            start.SetParent(_root.transform);
            finish.SetParent(_root.transform);

            var racerObject = new GameObject("racer");
            racerObject.transform.SetParent(_root.transform);
            mover = racerObject.AddComponent<EgrangStepMover>();

            var track = _root.AddComponent<EgrangRaceTrack>();
            var racer = _root.AddComponent<EgrangRacer>();
            racer.Bind(lane: 1, track: track, mover: mover, start: start, finish: finish);
            return racer;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void SnapToUnits_PlacesTheRacerAStridePerUnitFromTheStart()
        {
            EgrangRacer racer = Build(out EgrangStepMover mover);

            racer.SnapToUnits(4);

            // 4 strides at 0.5 m, along start -> finish.
            Assert.That(mover.transform.position.z, Is.EqualTo(2f).Within(0.001f));
            Assert.That(racer.BankedUnits, Is.EqualTo(4));
        }

        [Test]
        public void SnapToUnits_ClampsToTheLaneEnds()
        {
            EgrangRacer racer = Build(out EgrangStepMover mover);

            racer.SnapToUnits(-5);
            Assert.That(mover.transform.position.z, Is.EqualTo(0f).Within(0.001f));

            racer.SnapToUnits(999);
            Assert.That(mover.transform.position.z, Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void ApplyStep_BanksTheStepsWithoutWaitingForTheAnimation()
        {
            EgrangRacer racer = Build(out EgrangStepMover _);

            racer.ApplyStep(EgrangStepResult.Full);
            racer.ApplyStep(EgrangStepResult.Half);
            racer.ApplyStep(EgrangStepResult.Fail);

            Assert.That(racer.BankedUnits, Is.EqualTo(3));
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Test Runner → PlayMode. Expected: compile error, `EgrangRacer` not defined.

- [ ] **Step 3: Write the component**

```csharp
using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// One lane's racer: the pairing of a mover, the track it runs on, and the seat that owns it.
    /// Put this on the lane root (`Player N Point`).
    ///
    /// It counts strides as well as animating them, because the server counts strides too — holding
    /// the same number locally is what makes a server echo either confirm the local prediction or
    /// correct it, without having to measure the transform back into units.
    /// </summary>
    public sealed class EgrangRacer : MonoBehaviour
    {
        [Tooltip("Seat this lane belongs to: 0 for Player 1 Point, 1 for Player 2, 2 for Player 3.")]
        [SerializeField] private int lane;
        [Tooltip("This lane's track. Leave empty to take one from this object or its children.")]
        [SerializeField] private EgrangRaceTrack track;
        [Tooltip("The racer that walks this lane. Leave empty to find one in the children.")]
        [SerializeField] private EgrangStepMover mover;
        [Tooltip("Lane start. Leave empty to use the track's own start marker.")]
        [SerializeField] private Transform start;
        [Tooltip("Lane finish. Leave empty to use the track's own finish marker.")]
        [SerializeField] private Transform finish;

        int _units;

        /// <summary>Seat that races here.</summary>
        public int Lane => lane;

        /// <summary>This lane's track, for the HUD to read.</summary>
        public EgrangRaceTrack Track => track;

        /// <summary>Strides banked locally. Compared against the server's count on every echo.</summary>
        public int BankedUnits => _units;

        /// <summary>How far along the lane this racer is.</summary>
        public EgrangTrackProgress Progress => track != null
            ? track.Progress
            : new EgrangTrackProgress(0f, 0f, 0f, 0f);

        void Awake()
        {
            if (track == null) track = GetComponentInChildren<EgrangRaceTrack>();
            if (mover == null) mover = GetComponentInChildren<EgrangStepMover>();

            if (mover == null)
            {
                Debug.LogWarning($"{nameof(EgrangRacer)} on '{name}' has no mover, so this lane cannot race.", this);
            }
        }

        /// <summary>Wiring from code — used by the tests and by any runtime lane building.</summary>
        public void Bind(int lane, EgrangRaceTrack track, EgrangStepMover mover, Transform start, Transform finish)
        {
            this.lane = lane;
            this.track = track;
            this.mover = mover;
            this.start = start;
            this.finish = finish;
        }

        /// <summary>Plays one step and banks it. The same call serves the local press and a remote echo.</summary>
        public void ApplyStep(EgrangStepResult result)
        {
            _units += EgrangStep.StepsFor(result);

            if (mover != null) mover.OnStepResult(result);
        }

        /// <summary>
        /// Places the racer at a stride count without animating: reconnect, and repair after the
        /// server disagreed with a local prediction.
        /// </summary>
        public void SnapToUnits(int units)
        {
            if (mover == null) return;

            Vector3 from = StartPoint();
            Vector3 to = FinishPoint();
            float laneLength = Vector3.Distance(from, to);
            float metres = Mathf.Clamp(units * mover.StepLength, 0f, laneLength);

            _units = Mathf.Max(0, units);
            mover.SnapTo(laneLength <= 0f ? from : from + (to - from).normalized * metres);
        }

        Vector3 StartPoint() => start != null ? start.position
            : track != null ? track.StartPosition
            : transform.position;

        Vector3 FinishPoint() => finish != null ? finish.position
            : track != null ? track.FinishPosition
            : transform.position;
    }
}
```

- [ ] **Step 4: Run the tests and watch them pass**

Test Runner → PlayMode → 3 passing. `SnapToUnits(999)` should land exactly on the finish, not past it.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Games/Egrang/EgrangRacer.cs Assets/Scripts/Games/Egrang/Tests/PlayMode/EgrangRacerTests.cs
git commit -m "feat(egrang): give each lane a racer that banks and snaps strides"
```

---

### Task 8: Client — the session interface and the race conductor

**Files:**
- Create: `Assets/Scripts/Games/Egrang/IEgrangSession.cs`
- Create: `Assets/Scripts/Games/Egrang/EgrangRace.cs`
- Modify: `Assets/Scripts/Games/Egrang/EgrangProgressView.cs:44` (accept an explicit track)
- Test: `Assets/Scripts/Games/Egrang/Tests/PlayMode/EgrangRaceTests.cs`

**Interfaces:**
- Consumes: `EgrangRacer` (Task 7), `EgrangSeating` (Task 6), `FollowCamera.Target`/`SnapToTarget`, `SkillCheckBar.OnStepResult`, `EgrangStickSelector.OnStickSelected`, `EgrangStickProfile.Shape`.
- Produces: `IEgrangSession` (implemented by `NetEgrangSession` in Task 9) and `EgrangRace` with `void Bind(IEgrangSession session)`, `EgrangRacer LocalRacer { get; }`, `EgrangProgressView.SetTrack(EgrangRaceTrack)`.

- [ ] **Step 1: Write the interface**

```csharp
using System;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The race as seen from the scene: who is seated where, what the other racers just did, and a
    /// way to report what this player did.
    ///
    /// Declared here rather than in the networking assembly because `Museum.Net` references the game
    /// assemblies and not the other way round — the same shape Dakon uses. The offline scene runs
    /// with no session at all.
    /// </summary>
    public interface IEgrangSession
    {
        /// <summary>This client's session id.</summary>
        string LocalSessionId { get; }

        /// <summary>Seats known so far, as (sessionId, seat) pairs.</summary>
        System.Collections.Generic.IEnumerable<(string sessionId, int seat)> Seats { get; }

        /// <summary>Someone took a step: (sessionId, result, stepUnits banked after it).</summary>
        event Action<string, EgrangStepResult, int> StepTaken;

        /// <summary>Seating changed — a player joined, left, or the state first arrived.</summary>
        event Action SeatsChanged;

        /// <summary>The race ended. Places by session id, 0 for anyone who never finished.</summary>
        event Action<System.Collections.Generic.IReadOnlyDictionary<string, int>> RaceOver;

        /// <summary>Report a graded press.</summary>
        void SendStep(EgrangStepResult result);

        /// <summary>Report the chosen stilt.</summary>
        void SendStick(EgrangStickShape shape);
    }
}
```

- [ ] **Step 2: Write the failing test**

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Museum.Games.Egrang.Tests.PlayMode
{
    public class EgrangRaceTests
    {
        sealed class FakeSession : IEgrangSession
        {
            public string LocalSessionId { get; set; } = "me";
            public List<(string sessionId, int seat)> SeatList = new List<(string, int)>();
            public IEnumerable<(string sessionId, int seat)> Seats => SeatList;
            public List<EgrangStepResult> Sent = new List<EgrangStepResult>();
            public List<EgrangStickShape> SentSticks = new List<EgrangStickShape>();

            public event Action<string, EgrangStepResult, int> StepTaken;
            public event Action SeatsChanged;
            public event Action<IReadOnlyDictionary<string, int>> RaceOver;

            public void SendStep(EgrangStepResult result) => Sent.Add(result);
            public void SendStick(EgrangStickShape shape) => SentSticks.Add(shape);

            public void RaiseSeats() => SeatsChanged?.Invoke();
            public void RaiseStep(string sessionId, EgrangStepResult result, int units) => StepTaken?.Invoke(sessionId, result, units);
            public void RaiseOver(IReadOnlyDictionary<string, int> places) => RaceOver?.Invoke(places);
        }

        GameObject _root;
        EgrangRace _race;
        EgrangRacer[] _racers;
        FollowCamera _camera;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("race");
            _racers = new EgrangRacer[3];

            for (int lane = 0; lane < 3; lane++)
            {
                var laneRoot = new GameObject($"lane{lane}");
                laneRoot.transform.SetParent(_root.transform);

                var start = new GameObject("start").transform;
                var finish = new GameObject("finish").transform;
                start.SetParent(laneRoot.transform);
                finish.SetParent(laneRoot.transform);
                start.position = new Vector3(lane * 3f, 0f, 0f);
                finish.position = new Vector3(lane * 3f, 0f, 25f);

                var moverObject = new GameObject("racer");
                moverObject.transform.SetParent(laneRoot.transform);
                var mover = moverObject.AddComponent<EgrangStepMover>();

                var track = laneRoot.AddComponent<EgrangRaceTrack>();
                var racer = laneRoot.AddComponent<EgrangRacer>();
                racer.Bind(lane, track, mover, start, finish);
                _racers[lane] = racer;
            }

            _camera = new GameObject("camera").AddComponent<FollowCamera>();
            _race = _root.AddComponent<EgrangRace>();
            _race.Configure(_racers, _camera, progressView: null, bar: null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_camera != null) Object.DestroyImmediate(_camera.gameObject);
        }

        [Test]
        public void TheCameraFollowsWhicheverLaneIsLocal()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("them", 0));
            session.SeatList.Add(("me", 2));

            _race.Bind(session);
            session.RaiseSeats();

            Assert.That(_race.LocalRacer, Is.SameAs(_racers[2]));
            Assert.That(_camera.Target, Is.SameAs(_racers[2].transform));
        }

        [Test]
        public void ARemoteStepAnimatesThatLaneOnly()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            session.SeatList.Add(("them", 1));
            _race.Bind(session);
            session.RaiseSeats();

            session.RaiseStep("them", EgrangStepResult.Full, 2);

            Assert.That(_racers[1].BankedUnits, Is.EqualTo(2));
            Assert.That(_racers[0].BankedUnits, Is.EqualTo(0));
        }

        [Test]
        public void AnEchoOfTheLocalStepIsIgnoredWhenItAgrees()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            _race.ReportLocalStep(EgrangStepResult.Full);   // predicted: 2
            session.RaiseStep("me", EgrangStepResult.Full, 2);

            Assert.That(session.Sent, Is.EqualTo(new[] { EgrangStepResult.Full }));
            Assert.That(_racers[0].BankedUnits, Is.EqualTo(2));
        }

        [Test]
        public void AnEchoThatDisagreesSnapsTheLocalRacerBack()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            _race.ReportLocalStep(EgrangStepResult.Full);   // predicted 2, server rejected it
            session.RaiseStep("me", EgrangStepResult.Fail, 0);

            Assert.That(_racers[0].BankedUnits, Is.EqualTo(0));
            Assert.That(_racers[0].transform.position, Is.Not.Null);
        }

        [Test]
        public void WithNoSessionTheLocalLaneIsLaneOne()
        {
            _race.Bind(null);

            Assert.That(_race.LocalRacer, Is.SameAs(_racers[0]));
            Assert.That(_camera.Target, Is.SameAs(_racers[0].transform));
        }
    }
}
```

- [ ] **Step 3: Run it and watch it fail**

Test Runner → PlayMode. Expected: compile error, `EgrangRace` not defined.

- [ ] **Step 4: Write the conductor**

```csharp
using System.Collections.Generic;
using Museum.Core;
using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The scene's race: three lanes, one of which is yours.
    ///
    /// This is the only object that knows which lane is local. It points the camera and the HUD at
    /// that lane, sends your presses to the session, and replays everyone else's. With no session it
    /// runs the scene offline in lane 1, which is how the race is iterated on in the editor.
    ///
    /// Local presses animate before the server has seen them, so the race feels like the
    /// single-player one; the echo either agrees, and is dropped, or disagrees, and the racer is
    /// snapped to the server's count. Remote lanes have no bar and no input.
    /// </summary>
    public sealed class EgrangRace : MonoBehaviour
    {
        [Tooltip("Lane racers, in seat order: Player 1 Point, Player 2 Point, Player 3 Point.")]
        [SerializeField] private EgrangRacer[] racers = new EgrangRacer[3];
        [Tooltip("Camera that follows the local racer.")]
        [SerializeField] private FollowCamera followCamera;
        [Tooltip("HUD strip. It is pointed at the local lane's track.")]
        [SerializeField] private EgrangProgressView progressView;
        [Tooltip("The local player's skill-check bar. Remote lanes have none.")]
        [SerializeField] private SkillCheckBar bar;
        [Tooltip("Stick selection panel, so the chosen stilt reaches the server.")]
        [SerializeField] private EgrangStickSelector stickSelector;

        readonly EgrangSeating _seating = new EgrangSeating();
        IEgrangSession _session;

        /// <summary>The racer this client drives. Lane 1 while offline.</summary>
        public EgrangRacer LocalRacer { get; private set; }

        void Awake()
        {
            if (bar != null) bar.OnStepResult.AddListener(ReportLocalStep);
            if (stickSelector != null) stickSelector.OnStickSelected.AddListener(OnStickSelected);
        }

        void OnDestroy()
        {
            if (bar != null) bar.OnStepResult.RemoveListener(ReportLocalStep);
            if (stickSelector != null) stickSelector.OnStickSelected.RemoveListener(OnStickSelected);

            Unsubscribe();
        }

        /// <summary>Wiring from code — used by the tests.</summary>
        public void Configure(EgrangRacer[] racers, FollowCamera followCamera, EgrangProgressView progressView, SkillCheckBar bar)
        {
            this.racers = racers;
            this.followCamera = followCamera;
            this.progressView = progressView;
            this.bar = bar;
        }

        /// <summary>
        /// Attaches the race to a session, or to nothing. A null session is the offline scene: lane 1
        /// is yours and the other two stand still.
        /// </summary>
        public void Bind(IEgrangSession session)
        {
            Unsubscribe();
            _session = session;

            if (_session == null)
            {
                TakeLane(0);
                return;
            }

            _session.SeatsChanged += OnSeatsChanged;
            _session.StepTaken += OnStepTaken;
            _session.RaceOver += OnRaceOver;

            _seating.SetLocal(_session.LocalSessionId);
            OnSeatsChanged();
        }

        /// <summary>A graded local press: animate it now, tell the server, let the echo confirm it.</summary>
        public void ReportLocalStep(EgrangStepResult result)
        {
            if (LocalRacer != null) LocalRacer.ApplyStep(result);

            _session?.SendStep(result);
        }

        void OnStickSelected(EgrangStickProfile profile)
        {
            if (profile != null) _session?.SendStick(profile.Shape);
        }

        void OnSeatsChanged()
        {
            if (_session == null) return;

            foreach ((string sessionId, int seat) in _session.Seats) _seating.Assign(sessionId, seat);

            int lane = _seating.LocalLane;
            TakeLane(lane < 0 ? 0 : lane);
        }

        void OnStepTaken(string sessionId, EgrangStepResult result, int stepUnits)
        {
            int lane = _seating.LaneOf(sessionId);
            EgrangRacer racer = RacerAt(lane);
            if (racer == null) return;

            // Your own step already played when you pressed. Take the server's count as the truth and
            // only intervene when it disagrees — replaying it would double the stride.
            if (racer == LocalRacer)
            {
                if (racer.BankedUnits != stepUnits) racer.SnapToUnits(stepUnits);
                return;
            }

            racer.ApplyStep(result);

            if (racer.BankedUnits != stepUnits) racer.SnapToUnits(stepUnits);
        }

        void OnRaceOver(IReadOnlyDictionary<string, int> places)
        {
            foreach (KeyValuePair<string, int> entry in places)
            {
                Debug.Log($"Egrang: lane {_seating.LaneOf(entry.Key) + 1} finished {entry.Value}");
            }
        }

        void TakeLane(int lane)
        {
            EgrangRacer racer = RacerAt(lane);
            if (racer == null) return;

            LocalRacer = racer;

            if (followCamera != null)
            {
                followCamera.Target = racer.transform;
                followCamera.SnapToTarget();
            }

            if (progressView != null) progressView.SetTrack(racer.Track);
        }

        EgrangRacer RacerAt(int lane) =>
            racers != null && lane >= 0 && lane < racers.Length ? racers[lane] : null;

        void Unsubscribe()
        {
            if (_session == null) return;

            _session.SeatsChanged -= OnSeatsChanged;
            _session.StepTaken -= OnStepTaken;
            _session.RaceOver -= OnRaceOver;
            _session = null;
        }
    }
}
```

- [ ] **Step 5: Let the HUD be pointed at a lane**

In `EgrangProgressView.cs`, replace the `Awake` fallback (`:44`) and add a setter:

```csharp
        /// <summary>
        /// Points the strip at a lane's track. `EgrangRace` calls this with the local lane — with
        /// three tracks in the scene, finding one by type would pick an arbitrary racer's progress.
        /// </summary>
        public void SetTrack(EgrangRaceTrack value) => track = value;
```

and in `Awake`:

```csharp
            // No search fallback: three lanes exist, so an unassigned strip has to stay blank rather
            // than quietly show someone else's race. EgrangRace assigns it.
            if (track == null)
            {
                Debug.LogWarning($"{nameof(EgrangProgressView)} on '{name}' has no track yet; " +
                                 "EgrangRace assigns the local lane at runtime.", this);
            }
```

Confirm `Update` already guards `track == null` before calling `Draw`; if it does not, add `if (track == null) return;`.

- [ ] **Step 6: Run the tests and watch them pass**

Test Runner → PlayMode → 5 `EgrangRaceTests` passing, plus Tasks 5–7's tests still green.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Games/Egrang/IEgrangSession.cs Assets/Scripts/Games/Egrang/EgrangRace.cs Assets/Scripts/Games/Egrang/EgrangProgressView.cs Assets/Scripts/Games/Egrang/Tests/PlayMode/EgrangRaceTests.cs
git commit -m "feat(egrang): drive three lanes from one seat-aware race"
```

---

### Task 9: Client — the networked session

**Files:**
- Create: `Assets/Scripts/Net/NetEgrangSession.cs`
- Create: `Assets/Scripts/Net/EgrangNetBootstrap.cs`
- Modify: `Assets/Scripts/Net/Museum.Net.asmdef`
- Modify: `Assets/Scripts/Net/Schema/EgrangState.cs`

**Interfaces:**
- Consumes: `IEgrangSession` (Task 8), `EgrangRace.Bind`, `ColyseusNetManager.Instance.Reconnect<T>()`, `SessionData.Instance.CanReconnect` / `.RoomName`.
- Produces: `NetEgrangSession(Room<EgrangState> room)`, `EgrangNetBootstrap` MonoBehaviour.

There is no unit test here: it is SDK glue, and the repo does not test `NetDakonSession` either. Task 10's play test is what exercises it.

- [ ] **Step 1: Mirror the server schema**

`Assets/Scripts/Net/Schema/EgrangState.cs` — fill the empty partial, matching the generated style of `BasePlayer.cs` (field indices continue after the base's 0–2):

```csharp
using Colyseus.Schema;
#if UNITY_5_3_OR_NEWER
using UnityEngine.Scripting;
#endif

namespace Museum.Net.State
{
    public partial class EgrangRacer : Schema
    {
        [Preserve] public EgrangRacer() { }

        [Type(0, "uint8")] public byte stick = default(byte);
        [Type(1, "uint16")] public ushort stepUnits = default(ushort);
        [Type(2, "uint8")] public byte place = default(byte);
        [Type(3, "boolean")] public bool ready = default(bool);
    }

    public partial class EgrangState : BaseGameState
    {
        [Preserve] public EgrangState() { }

        [Type(3, "map", typeof(MapSchema<EgrangRacer>))] public MapSchema<EgrangRacer> racers = null;
        [Type(4, "uint16")] public ushort finishUnits = default(ushort);
        [Type(5, "number")] public float startsAtMs = default(float);
    }
}
```

- [ ] **Step 2: Let the net assembly see the Egrang game assembly**

`Assets/Scripts/Net/Museum.Net.asmdef` — add `"Museum.Games.Egrang"` to `references`, beside `"Museum.Games.Dakon"`. The direction matters: the game assembly must never reference `Museum.Net`.

- [ ] **Step 3: Write the session**

```csharp
using System;
using System.Collections.Generic;
using Colyseus;
using Museum.Games.Egrang;
using Museum.Net.State;

namespace Museum.Net
{
    /// <summary>
    /// `IEgrangSession` over a live Colyseus room. Follows the same idiom as
    /// <see cref="NetDakonSession"/>: whole-state `OnStateChange` plus typed messages, no callback
    /// proxy, since the state is small enough that diffing it wholesale costs nothing.
    /// </summary>
    public sealed class NetEgrangSession : IEgrangSession
    {
        readonly Room<EgrangState> _room;
        readonly List<(string sessionId, int seat)> _seats = new List<(string, int)>();

        public NetEgrangSession(Room<EgrangState> room)
        {
            _room = room;

            _room.OnStateChange += (_, __) => ReadSeats();
            _room.OnMessage<StepTakenPayload>("step_taken", OnStepTaken);
            _room.OnMessage<GameOverPlacesPayload>("game_over", OnGameOver);

            ReadSeats();
        }

        public string LocalSessionId => _room.SessionId;

        public IEnumerable<(string sessionId, int seat)> Seats => _seats;

        public event Action<string, EgrangStepResult, int> StepTaken;
        public event Action SeatsChanged;
        public event Action<IReadOnlyDictionary<string, int>> RaceOver;

        public void SendStep(EgrangStepResult result) => _room.Send("step", new { result = (int)result });

        public void SendStick(EgrangStickShape shape) => _room.Send("choose_stick", new { shape = (int)shape });

        void ReadSeats()
        {
            EgrangState state = _room.State;
            if (state?.players == null) return;

            _seats.Clear();
            state.players.ForEach((sessionId, player) => _seats.Add((sessionId, player.seat)));

            SeatsChanged?.Invoke();
        }

        void OnStepTaken(StepTakenPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.sessionId)) return;

            StepTaken?.Invoke(payload.sessionId, (EgrangStepResult)payload.result, payload.stepUnits);
        }

        void OnGameOver(GameOverPlacesPayload payload)
        {
            RaceOver?.Invoke(payload?.places ?? new Dictionary<string, int>());
        }
    }

    [Serializable]
    public class StepTakenPayload
    {
        public string sessionId;
        public int result;
        public int stepUnits;
    }

    [Serializable]
    public class GameOverPlacesPayload
    {
        public Dictionary<string, int> places;
        public string winner;
    }
}
```

- [ ] **Step 4: Write the bootstrap**

```csharp
using System;
using Colyseus;
using Museum.Core;
using Museum.Games.Egrang;
using Museum.Net.State;
using UnityEngine;

namespace Museum.Net
{
    /// <summary>
    /// Reconnects the Egrang scene to its room and hands the race a session.
    ///
    /// The live room does not survive the scene load, so — exactly as
    /// <see cref="DakonNetBootstrap"/> does — the room is re-obtained from the reconnection token
    /// the lobby stored. Any failure leaves the race unbound, which is the offline scene: lane 1 is
    /// yours and the other two stand still.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class EgrangNetBootstrap : MonoBehaviour
    {
        [SerializeField] private EgrangRace race;

        private async void Awake()
        {
            EgrangRace target = race != null
                ? race
                : FindFirstObjectByType<EgrangRace>(FindObjectsInactive.Include);

            if (target == null)
            {
                Debug.LogWarning($"{nameof(EgrangNetBootstrap)} found no {nameof(EgrangRace)} to bind.", this);
                return;
            }

            if (SessionData.Instance == null || !SessionData.Instance.CanReconnect)
            {
                target.Bind(null);
                return;
            }

            if (SessionData.Instance.RoomName != "egrang")
            {
                Debug.LogWarning($"Session is in room '{SessionData.Instance.RoomName}', not egrang — racing offline.", this);
                target.Bind(null);
                return;
            }

            try
            {
                Room<EgrangState> room = await ColyseusNetManager.Instance.Reconnect<EgrangState>();

                if (room == null)
                {
                    target.Bind(null);
                    return;
                }

                target.Bind(new NetEgrangSession(room));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Egrang reconnect failed — racing offline: {e.Message}", this);
                target.Bind(null);
            }
        }
    }
}
```

- [ ] **Step 5: Confirm it compiles**

Switch to the Unity Editor and let it reload. Expected: no console errors, and the EditMode + PlayMode suites still pass.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Net/NetEgrangSession.cs Assets/Scripts/Net/EgrangNetBootstrap.cs Assets/Scripts/Net/Museum.Net.asmdef Assets/Scripts/Net/Schema/EgrangState.cs
git commit -m "feat(egrang): bind the race to its room over Colyseus"
```

---

### Task 10: Client — build the three lanes in the scene

Editor work. Do it in the Unity Editor (MCP tools are fine); there is no script that can be tested for it, so the check is playing the scene.

**Files:**
- Modify: `Assets/Scenes/Egrang.unity`

- [ ] **Step 1: Give lanes 2 and 3 a racer**

The scene already has `Player 1 Point`, `Player 2 Point`, `Player 3 Point`, each with its own `Race Track` child; lane 1 also holds `Egrang Player`. Copy `Egrang Player` under `Player 2 Point` and `Player 3 Point`, each at local position `(0, 0, -3)` and the same scale/rotation as lane 1's.

- [ ] **Step 2: Remove the duplicate mover**

`Egrang Player` carries an `EgrangStepMover` inside the prefab *and* a second one added in the scene. Delete the scene-added component so each racer has exactly one; then re-point anything referencing the deleted one at the prefab's.

- [ ] **Step 3: Add the lane components**

On each `Player N Point`, add `EgrangRacer` and set: `lane` = 0/1/2 respectively, `track` = that object's `Race Track`, `mover` = that lane's `Egrang Player` mover, `start`/`finish` = the track's `Start Line` / `Finish Line`.

- [ ] **Step 4: Add the race root**

Create an empty scene root named `Egrang Race`. Add `EgrangRace` and `EgrangNetBootstrap`. On `EgrangRace` set: `racers` = the three `EgrangRacer`s in lane order, `followCamera` = `Main Camera`'s `FollowCamera`, `progressView` = `Race Progress`'s `EgrangProgressView`, `bar` = the HUD's `SkillCheckBar`, `stickSelector` = the selection panel's `EgrangStickSelector`.

- [ ] **Step 5: Unhook the old single-player wiring**

- On `SkillCheckBar`, remove the `OnStepResult` entry that calls lane 1's `EgrangStepMover.OnStepResult` — `EgrangRace` now listens and routes it, and leaving both would double every step.
- Clear `FollowCamera.target` in the inspector; `EgrangRace` assigns it.
- Clear `EgrangProgressView.track`; `EgrangRace` assigns it.
- Leave each `EgrangRaceTrack.racer` pointing at its own lane's player.

- [ ] **Step 6: Play the scene offline**

Press Play with no lobby session. Expected: you race in lane 1 exactly as before — bar responds, camera follows, HUD counts down the metres — and lanes 2 and 3 stand still. No console errors.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scenes/Egrang.unity
git commit -m "feat(egrang): put a racer in every lane and wire the race root"
```

---

### Task 11: Both repos — play a real three-player race

The first time the two halves meet. No new code unless something is broken; if it is, fix it here and note what.

- [ ] **Step 1: Start the server**

```bash
cd /Users/mac/Documents/Works/nodejs/museum-minigames
npm start
```

- [ ] **Step 2: Get three clients into one room**

Build the WebGL client or run three editor/browser instances, create a room, join it with the other two, choose a different stilt in each, and start.

- [ ] **Step 3: Check the race**

Expected:
- All three players are in the room and each sees themselves in a different lane, with the camera on their own racer.
- Nobody moves until the 3 s countdown elapses.
- Each player's steps appear in the same lane on all three screens, with the same stride animation.
- The first over the line is place 1 on every screen; `game_over` arrives with the same places for everyone.
- The server console shows no `invalid_move` spam during normal play — if it does, the rate limit is too tight for the authored lockout and the constant needs revisiting in `EgrangRace.ts`, not in the client.

- [ ] **Step 4: Check reconnect**

Reload one client mid-race. Expected: it comes back into its own lane at the distance it had banked, not at the start line.

- [ ] **Step 5: Update the client docs**

In `Assets/Docs/games/egrang.md`, describe the three-lane setup and point at the server's `docs/games/egrang.md` as the rules' ground truth. Keep it to what a client reader needs: lanes are seat-absolute, distance is strides, the local press animates before the server confirms.

- [ ] **Step 6: Commit**

```bash
cd "/Users/mac/Documents/Works/unity/Museum Minigames"
git add Assets/Docs/games/egrang.md
git commit -m "docs(egrang): describe the three-lane race on the client"
```

---

## Notes for the executor

- **Task 5–8 tests are PlayMode**, not EditMode: they build GameObjects and add MonoBehaviours. The PlayMode asmdef already exists at `Assets/Scripts/Games/Egrang/Tests/PlayMode/`. Task 6's test is pure C# and belongs in the EditMode assembly.
- **Do not add a `Museum.Net` reference to `Museum.Games.Egrang.asmdef`.** The dependency runs the other way; `IEgrangSession` exists precisely so it can.
- If a server test needs the race open immediately, set `room.state.startsAtMs = Date.now() - 1` on the server-side room object rather than sleeping through the countdown.
