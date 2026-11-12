# Realm Weaver: notes for AI coding agents

The roadmap is in `docs/roadmap.md`, and the decisions behind it are in `docs/adr/`. Work one milestone at a time, keeping each change PR-sized.

## Commands
- Backend build/test: `dotnet build RealmWeaver.slnx` and `dotnet test RealmWeaver.slnx`
- Run API: `dotnet run --project src/backend/RealmWeaver.Api` (http://localhost:5080)
- Frontend (in `src/frontend`): `npm run dev`, `npm run lint`, `npm test`, `npm run build`
- API contract: after changing an endpoint or DTO, run `UPDATE_OPENAPI=1 dotnet test --filter OpenApiContractTests`, then `npm run api:types` in `src/frontend`, and commit both generated files. CI checks both.
- Database: `docker compose up -d` (Postgres 17, user/pass/db `realmweaver`, local only). The API migrates and seeds on startup.
- New migration (per module): `dotnet tool restore`, then `dotnet ef migrations add <Name> --context <Module>DbContext --project src/backend/RealmWeaver.Modules.<Module> --startup-project src/backend/RealmWeaver.Api -o Persistence/Migrations`
- API integration tests use Testcontainers, so Docker must be running for `dotnet test`.
- Sample realms: `dotnet run --project tools/RealmWeaver.Cli -- --seed 42 --biome forest --size 40x30` (output in `samples/`, which is gitignored)

## Architecture rules
- **Modular monolith.** Modules (`RealmWeaver.Modules.*`) talk to each other only through their public contracts (interfaces and DTOs). They never touch another module's entities or tables. This keeps the Milestone 8 microservice split mechanical.
- **Each module owns its persistence** (ADR 0004): its own internal DbContext, Postgres schema and migrations, and no foreign keys into other modules. `RealmWeaver.Infrastructure` holds shared plumbing only and never references a module.
- **`RealmWeaver.Generation` is pure.** It has no EF Core, no ASP.NET, no I/O, no `System.Random` and no `DateTime.Now`. Randomness comes only from the project's seeded RNG, so the same seed and inputs always give the same realm. `ArchitectureTests` checks this.
- Each generation step draws from its own `SeededRandom.Fork("purpose")` stream (see ADR 0002). Changing the algorithm means bumping `GeneratorLimits.Version`.
- The generator outputs template ids and references (event templates, mob archetypes), not just display text, so content can move into DB tables later.
- Rituals run in a single DB transaction in Phase 1, with idempotency keys and optimistic concurrency (Postgres `xmin`). Cross-module writes that must be atomic go through `UnitOfWork` (ADR 0005); module contract methods stay transaction-agnostic.

## Conventions
- `TreatWarningsAsErrors` is on, along with the .NET recommended analyzers. Fix warnings rather than suppressing them.
- Test names use `Method_or_behavior_reads_like_a_sentence`. Underscores are allowed in `tests/` only.
- Frontend uses function components, TanStack Query for server state, and no global state library unless one is needed. Call the API through the typed `api` client (`src/api/client.ts`) and the hooks in `src/api/queries.ts`; page tests stub `fetch` with `src/test/fakeApi.ts`.
- Don't copy rules text or stat blocks from published TTRPGs (DC20 etc.). Content is original or user-authored.
