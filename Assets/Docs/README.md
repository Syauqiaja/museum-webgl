# Museum Minigames — Unity WebGL Client Docs

Everything this project is, how it behaves, and the rules of the games in it. Read the
relevant doc before changing anything; update it in the same change when the change alters
what the doc describes (a new scene, a changed flow, a resolved TBD, a new constraint).
Stale docs are worse than none — this set was rewritten from the code on 2026-08-19 after
the project was destroyed and rebuilt, and every number in it was read out of a file, not
remembered.

## What this is

A browser-playable (Unity WebGL) exhibit of Indonesian traditional games for a museum.
A visitor types a nickname, walks around a 3D museum, watches reference footage on the
exhibit screens, and walks up to a game doorway to play it against other people over the
network. Two games are built: **Dakon** (congklak/mancala, 2 players) and **Egrang**
(stilt race, 3 players).

## Two repos, one product

| | Where | Owns |
|---|---|---|
| **Client** (this repo) | `Documents/Works/unity/Museum Minigames` | Scenes, rendering, input, offline modes, UI |
| **Server** | `Documents/Works/nodejs/museum-minigames` | Rules, authority, matchmaking, persistence |

The server is authoritative for everything that decides a match. The client sends input
events and renders what it is told. Where a fact belongs to both, the **server repo's docs
are the contract**, and these docs restate it only where the client has to act on it:

- `docs/protocol.md` — message/state contract per room (**contract source of truth**)
- `docs/games/*.md` — per-game rules ground truth
- `docs/unity-integration.md` — the server side's "how to wire Unity" companion
- `docs/room-system.md` — matchmaking, seats, host, reconnect, idle cleanup

Never invent a message type or a rule client-side. If it is not in the server's
`protocol.md`, flag it so both repos change together.

## The docs

| Doc | Read it when |
|---|---|
| [overview.md](overview.md) | You want the shape of the product: scenes, games, audience, current status |
| [architecture.md](architecture.md) | You are touching bootstrap, scene flow, assemblies, config, or the model/view split |
| [scene-setup.md](scene-setup.md) | You are wiring a scene, or a scene stopped working |
| [networking.md](networking.md) | You are touching Colyseus: connect, create/join, state, messages, reconnect, the lobby→game handoff |
| [ui-flow.md](ui-flow.md) | You are changing what screen follows what, or a failure case's UX |
| [ui-style.md](ui-style.md) | You are building or generating any UI |
| [input-and-platform.md](input-and-platform.md) | You are touching control schemes, the touch overlay, `FPSController`'s input surface, or the WebGL template |
| [lessons.md](lessons.md) | You are touching the educational plaques in the museum, or adding a game's exhibit copy |
| [lighting.md](lighting.md) | You are touching how the Museum looks: lights, ambient, fog, bloom, colour space |
| [museum-decor.md](museum-decor.md) | You are touching the generated dressing: screen bezels, signboards, per-game bay props, the ground-floor gallery and its stations, or a Blender-built prop |
| [scenario-compliance.md](scenario-compliance.md) | You want to know how the built games compare to the curriculum sheet's scenarios, and the min-2-players rule |
| [games/dakon.md](games/dakon.md) | Anything Dakon — full v6 ruleset plus how the scene renders it |
| [games/egrang.md](games/egrang.md) | Anything Egrang — full race ruleset, stilt tuning, prediction model |
| [games/engklak.md](games/engklak.md) | Engklak — not built, and what it would take |
| [testing.md](testing.md) | You are adding tests, or a suite failed |
| [build-and-deploy.md](build-and-deploy.md) | You are producing a WebGL build or putting one on the VPS |
| [asset-budget.md](asset-budget.md) | You are adding a texture, model, terrain or package, or the payload grew |
| [boundaries.md](boundaries.md) | You are about to add a dependency, a service, or client-side authority |
| [dev-plan.md](dev-plan.md) | You want the phase plan and what is genuinely left |

Design specs and plans for individual features live outside this folder, in
`docs/superpowers/specs/` and `docs/superpowers/plans/` at the repo root. They are
historical records of a decision, not living documentation — when they disagree with this
set, this set wins.

## House rules for these docs

- **Numbers come from code.** A rule number in here (60 seeds, 50 strides, 15 s countdown)
  must match the file it names. If you change one, change both.
- **Link, don't duplicate, across repos.** A rule stated twice drifts.
- **Say what is built and what is not.** A doc that describes an intention as if it exists
  is how a rebuild loses a week.
