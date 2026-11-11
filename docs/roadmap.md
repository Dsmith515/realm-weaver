# Realm Weaver roadmap

## Context
Realm Weaver is a portfolio project for practising modern backend and DevOps skills: a modular monolith that later splits into microservices (saga, outbox), CI/CD with GitHub Actions, and testing with xUnit, Vitest and Playwright. The theme draws on grid tactics and TTRPGs, with .Hack-style "keyword" portals. Needle, Thread, Coating and Map components are combined in a ritual that opens a portal. That generates a seeded, grid-based field or dungeon with placed encounters, which can be exported as SVG or as an image-generation prompt.

Decisions made:
- **Monolith first, then split.** Phase 1 is a modular monolith. Phase 2 extracts services with a broker, saga and outbox.
- **Rituals fail only on invalid input** (missing or unowned components, concurrent spends). No random failure roll.
- **Mismatched map** keeps the same seed and layout, raises the danger tier and rerolls the encounters.
- **Free deployment.** GitHub Actions runs CI and pushes images to GHCR. The app runs locally with docker compose.
- **v1 defaults** (easy to change later): no authentication. A player switcher sits in the header. The admin page has no access control (it's marked as a TODO for when auth arrives). Thimbles are modelled as a fifth, optional component type in Phase 3.

---

## Repo layout
```
/RealmWeaver.sln
/src/backend/
  RealmWeaver.Api/            ASP.NET Core minimal APIs, OpenAPI, composition root
  RealmWeaver.Modules.Catalog/    component definitions, assignable toggle
  RealmWeaver.Modules.Inventory/  players, inventory items, starter kit
  RealmWeaver.Modules.Rituals/    ritual orchestration + transaction
  RealmWeaver.Modules.Realms/     realm persistence, export endpoints
  RealmWeaver.Generation/         PURE library: seeded RNG, field/dungeon/event generation, SVG + prompt export (no EF, no ASP.NET)
  RealmWeaver.Infrastructure/     EF Core DbContext, migrations, seeding
/tests/
  RealmWeaver.Generation.Tests/   xUnit invariant tests (no DB)
  RealmWeaver.Api.IntegrationTests/  xUnit + Testcontainers Postgres + WebApplicationFactory
/src/frontend/                  Vite + React + TS
  e2e/                          Playwright
/docker-compose.yml             postgres (+ api + web in a "full" profile)
/.github/workflows/             ci.yml, e2e.yml, release.yml
/docs/adr/                      short architecture decision records (good portfolio signal)
CLAUDE.md                       project conventions for Claude Code / Codex
```
Each module exposes only a public `I…Module` interface plus DTOs. The project-reference rules keep them separate, which makes the Phase 2 split mechanical.

## Domain model (EF Core + PostgreSQL)
- `ComponentDefinition`: Id, Type (Needle | Thread | Coating | Map | Thimble), Name, Rarity, Rank, Description, `IsPlayerAssignable`, `IsConsumable` (Thread and Coating are true), CreatedAt. Type-specific data is stored in jsonb columns. Each type also has a typed owned record:
  - Needle: DifficultyTier (1–10), Category, Abilities[]
  - Thread: Biome (forest, desert, tundra, swamp, volcanic, plains…), Element
  - Coating: MobTypes[] (beast, undead, bandit, elemental…), RealmKind (Field | Dungeon | FieldWithDungeon), FloorCount (1–MaxFloors)
  - Map: Seed (ulong), NeedleId, ThreadId, CoatingId, Width, Height, EventCount, GeneratorVersion, CreatedByPlayerId
- `Player`: Id, Name. `InventoryItem`: PlayerId, ComponentDefinitionId, Quantity. Postgres `xmin` acts as the concurrency token.
- `Ritual`: Id, PlayerId, component ids, IdempotencyKey (unique), Status, ResultRealmId, CreatedAt.
- `Realm`: Id, MapComponentId, DangerModifier, the generated `RealmDocument` stored as jsonb, plus GeneratorVersion.
- `AppSettings`: a global `PlayersCanSelfAssign` toggle. It works together with the per-component `IsPlayerAssignable` flag.
- Seed data: the beginner needle (always the same one), 3–4 threads, 3–4 coatings and a catalog of higher-rarity items. A new player gets the beginner needle, 2 different threads ×3 and 2 coatings ×3.

## Ritual rules (Rituals module)
1. **Choosing a map** fills in its Needle, Thread and Coating. Any the player doesn't own are flagged. If the player picks different non-map components, the map is marked as a **mismatch**: same seed, `DangerModifier += 1–2`, encounters rerolled with the new thread and coating.
2. **Choosing a Needle, Thread and Coating** checks whether the player owns a Map with exactly those three. If so, the Map slot fills in automatically.
3. **Random** fills the non-map slots from the player's own items (consumables need a quantity of at least 1), then runs rule 2.
4. **Perform ritual** runs `POST /api/rituals` with an `Idempotency-Key`. It runs in **one DB transaction**:
   - Check ownership.
   - Decrement Thread and Coating. An `xmin` conflict returns 409 and the call is retried.
   - Reuse the map seed, or create a new `Map` ComponentDefinition with a fresh seed. A new map appears in the admin catalog automatically.
   - Add the map to inventory (skip if already owned).
   - Generate the realm (a pure call) and save the `Realm` and `Ritual`.
   - Commit.

   Any failure rolls back the whole thing. This is the "local ACID" baseline that Phase 2 replaces with a saga.

## Generation library (the core, written test-first)
- **Deterministic RNG**: a custom SplitMix64/xoshiro256** (not `System.Random`, whose sequence can change between .NET versions). Sub-streams are derived from the seed per purpose (terrain, rooms, events, per floor), so changing the event count doesn't move the terrain.
- **Size**: random from 15×15 to 100×100 unless set manually. Width and height can differ.
- **Field**: value or simplex noise plus biome thresholds give the terrain tiles (grass, water, rock, trees…). Impassable tiles are kept walkable-connected with a flood fill, carving paths where needed.
- **Dungeon**: each floor puts irregular rooms (rect unions, circles, cellular-automata caves) on a grid. A minimum spanning tree plus about 15% extra edges builds the corridors, so layouts aren't linear. Stairs go down/up, at most `MaxFloors` floors (config, default 3).
  - Every room is reachable (verified by BFS). A secret door counts as an entrance and is listed.
  - Features: traps, chests and secret rooms, scaled by the needle difficulty.
- **Field with dungeon**: the dungeon entrance is tied to a **host structure** on the field (castle, ruins, cave mouth, mine, tower, temple). That structure is a field event with a footprint, and floor 1 links to it.
- **Coherent dungeon materials and themes**: a `DungeonTheme` is built from the host structure, or from the coating and thread when the realm is dungeon-only. It has a **primary material** (castle → dressed stone, mine → timber-braced rock, cave mouth → natural cave, ruins → crumbling brick) and a short list of **branch materials** that fit the biome (natural cave, flooded grotto, crystal vein, root-choked earth, lava tube).
  - Rooms are tagged by graph distance from the entrance. The **core** next to the host uses the primary material, and floor 1's core sits under the host footprint. **Branches** can extend past the host's outline because they're underground, and they can switch to a branch material. A **transition** room or corridor is placed between material zones, for example "masonry gives way to rough cave".
  - Room shape follows material: masonry gives rectilinear and composite rooms with doors, cave gives cellular-automata blobs and irregular passages, mine gives long shafts and side galleries.
  - Deeper floors get more variance: floor 1 is about 75% primary material, and later floors drift further from it.
  - Invariants (tested): the primary material is the majority on floor 1. Two different materials only meet through a transition. Every room stays reachable.
- **Dungeon events**: the same event system runs inside dungeons, using dungeon-allowed event types:
  - lair or den (room), patrol (along a corridor loop), wandering mob, an underground settlement or outpost (for example a kobold warren or a smuggler den, spread over several rooms), shrine, prison, treasure vault, and a **boss or guardian room** on the deepest floor.
  - Events use whole rooms or room groups as footprints. Nothing overlaps with each other or with traps or chests.
  - Event types are filtered by material and zone. A cave branch favors beasts and lairs; the masonry core favors guards and patrols.
  - Each floor has its own event budget, scaled by walkable area and kept within the same 5–60 rule. A field-with-dungeon realm budgets the field and each floor separately.
- **Content catalog seam (v1)**: event types, mob archetypes, trap types and chest tiers come from an `IContentCatalog` interface. In v1 a static in-code catalog backs it. The generator outputs `templateId` and mob references, not just text, so moving the catalog to DB tables later doesn't change the generator.
- **Events and encounters** (wandering monster, patrol, village, settlement, camp, lair, shrine, ruins…) are chosen by weight from biome, mob types, difficulty and later the thimble archetype. Each has a footprint covering several tiles.
  - An occupancy grid guarantees no overlaps.
  - A village or settlement covers at most half of the map area.
  - Not every event type has to appear.
- **Event count**: area-scaled bounds in one `EventBudget` class: `min = clamp(5 + area/1500, 5, 15)` and `max = clamp(5 + area/180, 5, 50)`. The user can override up to **60**, always at least 5. If footprints can't all fit, smaller footprints are tried, then the generator returns a clear error.
- **Spawn point (field)**: at least **5 tiles** (Chebyshev distance) from every event footprint.
- **Labels**: columns A…Z, AA…CV; rows 1…100. A tile reads like `C12`. Each placed item is listed as `{type, name, anchor tile, footprint tiles, notes}`. That covers events, traps, chests and secret entrances, per floor.
- **Exports**:
  - **SVG**: the grid, legend and labels, made server-side for consistency. The frontend can display it or download it.
  - **Image prompt**: a structured text block for a top-down battle map (biome, size, landmark positions by label, mood).
  - **JSON** of the full `RealmDocument`.
  - A **Markdown list** of the placements.

## API (minimal APIs, OpenAPI in .NET 10)
`/api/components` (CRUD + filter by type, rarity, biome, text) · `/api/components/{id}/assign` · `/api/settings` · `/api/players` · `/api/players/{id}/inventory` (GET, POST to self-add when allowed) · `/api/rituals/preview` (resolve slots, map auto-fill, mismatch flag) · `POST /api/rituals` · `/api/realms/{id}` · `/api/realms/{id}/export?format=svg|prompt|json|md`.
Validation uses ProblemDetails. The TS types are generated from OpenAPI with `openapi-typescript`, so the frontend and backend contract stays checked.

## Frontend (React + TS + Vite)
React Router, TanStack Query and **dnd-kit** for drag and drop.
- **Ritual page**: 4 slots plus an inventory panel. Items can be dragged or clicked to select. Buttons: Random and Perform. Map auto-fill and the mismatch warning come from `/rituals/preview`. Manual size and event-count controls sit under an Advanced section.
- **Components (admin) page**: a filterable table, a create/edit form, assign-to-player, and the self-assign toggle.
- **Player page**: the inventory grid, grouped by type with quantities, plus self-add from assignable components.
- **Realm page**: the SVG map with zoom and pan, a floor tabs, a placements list (click to highlight tiles) and export buttons.

## Testing
- **xUnit (Generation)**: invariant tests run over about 200 seeds × sizes × realm kinds. They check that results are deterministic, that rooms are reachable, that nothing overlaps, that the event count stays in range (5–60), that spawn distance is at least 5, that a village is at most half the area, and that there are at most MaxFloors floors. For dungeons they also check: event count per floor stays in budget, dungeon events don't overlap traps or chests, the primary material is the majority on floor 1, material zones meet only through transitions, and the boss room is on the deepest floor. Also golden snapshot tests on 2–3 seeds.
- **xUnit integration**: Testcontainers Postgres covers the ritual transaction (consumables decremented, map added, rollback on a failure injected mid-transaction), two concurrent rituals racing for the last thread (expect one success and one 409), and idempotency-key replay.
- **Vitest + React Testing Library**: slot auto-fill logic, random selection and inventory filtering.
- **Playwright**: on docker compose: pick a player, run Random, Perform, check the map is in inventory, the realm page renders the SVG, and an export downloads.

## CI/CD (GitHub Actions, free)
- `ci.yml` (PRs and main): `dotnet build/test` (Testcontainers works on ubuntu runners), plus `npm ci && lint && vitest && build`, with NuGet and npm caching.
- `e2e.yml`: `docker compose --profile full up` then Playwright. Reports are uploaded as artifacts.
- `release.yml` (on main/tags): build multi-stage Docker images and push them to **GHCR**.
- Dependabot, branch protection on main, and a README badge.

## Milestones (each one is a PR-sized portfolio slice)
1. **Scaffold**: git init, solution and projects, the Vite app, docker-compose Postgres, `ci.yml`, CLAUDE.md, README, first ADR.
2. **Generation library**: RNG, field generation, event placement, labels, SVG and prompt export, invariant tests. A tiny CLI or test dumps sample SVGs.
3. **Dungeons**: irregular rooms, connectivity, floors, the host structure and `DungeonTheme` material zones with transitions, dungeon events (including a boss room on the deepest floor), traps, chests, secrets, field-with-dungeon linking.
4. **Persistence and APIs**: EF models, migrations, seeding, Catalog and Inventory endpoints, integration tests.
5. **Ritual transaction**: preview, perform, idempotency, concurrency, map creation, mismatch danger.
6. **Frontend pages**: Ritual (dnd-kit), Components, Player, Realm, with Vitest.
7. **E2E and release**: Playwright flow, Dockerfiles, GHCR pipeline.
8. **Phase 2, microservices**: split the system into `catalog`, `inventory`, `ritual` and `realm` services. Each service has its own Postgres database (a schema or DB per service in a single container, to keep it light).
   - A **YARP** gateway sits in front, with **RabbitMQ** in compose.
   - **Wolverine** (MIT-licensed; MassTransit v9 is now commercial) provides the **transactional outbox and inbox**.
   - An orchestrated **saga** in the ritual service runs: Reserve consumables, then Generate realm, then Create map, then Grant map, then Commit reservation. Compensation steps release the reservation and delete the orphan map.
   - **OpenTelemetry** sends traces to the free Aspire Dashboard container.
   - Tests for failure injection in the saga, with ADRs comparing it to the Phase 1 ACID version.
9. **Content tables** (a later phase, which could become its own `content` service): this moves `IContentCatalog` to Postgres, with admin UI pages that work like the Components page.
   - `MobTemplate`: name, tags (biome, element, mob type, environment field/dungeon/material), tier/CR, role (minion, brute, skirmisher, caster, boss) and a **stat block reference**. Stat blocks are your own JSON documents, entered by you. No copied DC20 or other licensed text.
   - `EventTemplate`: type, allowed environments and materials, footprint rules (min/max size, room count), mob slots (role × count range), and loot/trap hooks.
   - `DifficultyTemplate`: modifiers such as Elite, Champion, Swarm or Enraged (HP/damage/count multipliers, extra abilities). They're applied from the needle tier and the mismatch `DangerModifier`.
   - `TrapTemplate`, `LootTable` / `ChestTier`.
   - The realm stores resolved references (`eventTemplateId`, `mobTemplateIds`, applied difficulty templates). The Realm page can then show which stat blocks to pull and which templates to apply.
   - The generator version goes up. Older realms keep their stored snapshot.
10. **Phase 3 (optional)**: thimble archetypes (Merchant, Hunter, Explorer) skew the event weights. Also a configurable max floors, auth (ASP.NET Identity or Keycloak in Docker) and an optional free cloud host.

## Related portfolio ideas that reuse this core (brief)
- **Tower-defense prototype**: reuse the field generator for lanes and paths. A wave-simulation service teaches background workers and deterministic simulation tests.
- **Deckbuilder rules service**: cards and runs stored with **event sourcing** (Marten on Postgres, free). Turns can be replayed from the event log.
- **Grid-tactics encounter builder**: places your own creature statblocks onto a realm map. It keeps rules generic and doesn't copy DC20 or other licensed text.

## Verification
- `dotnet test` passes, including the generation invariants and the Testcontainers ritual tests.
- `npm run test` and `npm run build` pass in `src/frontend`.
- `docker compose --profile full up`, then open the web app. Follow the flow: starter inventory shown → Random → Perform → new map in inventory and in the admin catalog → realm SVG renders with labels and placements → SVG and prompt exports download. Reselect the same N/T/C and the map auto-fills. Swap a thread and the mismatch warning appears with a higher danger level.
- `npx playwright test` runs green locally. All three GitHub workflows pass on the first PR once the repo is pushed.
