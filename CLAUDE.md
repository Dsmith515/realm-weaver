# Realm Weaver: notes for AI coding agents

The roadmap is in `docs/roadmap.md`, and the decisions behind it are in `docs/adr/`. Work one milestone at a time, keeping each change PR-sized.

## Commands
- Backend build/test: `dotnet build RealmWeaver.slnx` and `dotnet test RealmWeaver.slnx`
- Run API: `dotnet run --project src/backend/RealmWeaver.Api` (http://localhost:5080)
- Frontend (in `src/frontend`): `npm run dev`, `npm run lint`, `npm test`, `npm run build`
- Database: `docker compose up -d` (Postgres 17, user/pass/db `realmweaver`, local only)

## Architecture rules
- **Modular monolith.** Modules (`RealmWeaver.Modules.*`) talk to each other only through their public contracts (interfaces and DTOs). They never touch another module's entities or tables. This keeps the Milestone 8 microservice split mechanical.
- **`RealmWeaver.Generation` is pure.** It has no EF Core, no ASP.NET, no I/O, no `System.Random` and no `DateTime.Now`. Randomness comes only from the project's seeded RNG, so the same seed and inputs always give the same realm. `ArchitectureTests` checks this.
- The generator outputs template ids and references (event templates, mob archetypes), not just display text, so content can move into DB tables later.
- Rituals run in a single DB transaction in Phase 1, with idempotency keys and optimistic concurrency (Postgres `xmin`).

## Conventions
- `TreatWarningsAsErrors` is on, along with the .NET recommended analyzers. Fix warnings rather than suppressing them.
- Test names use `Method_or_behavior_reads_like_a_sentence`. Underscores are allowed in `tests/` only.
- Frontend uses function components, TanStack Query for server state, and no global state library unless one is needed.
- Don't copy rules text or stat blocks from published TTRPGs (DC20 etc.). Content is original or user-authored.
