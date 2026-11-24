# Realm Weaver: notes for AI coding agents

The roadmap is in `docs/roadmap.md`, and the decisions behind it are in `docs/adr/`. Work one milestone at a time, keeping each change PR-sized.

This file is the single source of agent instructions for every tool (Codex reads it directly; `CLAUDE.md` only imports it). Edit rules here, not in tool-specific files.

## Commands
- Backend build/test: `dotnet build RealmWeaver.slnx` and `dotnet test RealmWeaver.slnx`
- Run API: `dotnet run --project src/backend/RealmWeaver.Api` (http://localhost:5080; API reference at /scalar in Development)
- Frontend (in `src/frontend`): `npm run dev`, `npm run lint`, `npm test`, `npm run build`
- E2E (in `src/frontend`): start the full stack, then `npm run e2e` (Playwright, specs in `e2e/`). Tests create their own player instead of relying on seeded data. CI runs them in `e2e.yml`.
- API contract: after changing an endpoint or DTO, run `UPDATE_OPENAPI=1 dotnet test --filter OpenApiContractTests`, then `npm run api:types` in `src/frontend`, and commit both generated files. CI checks both.
- Full stack in containers: `docker compose --profile full up -d --build` (web at http://localhost:8080; the API runs in Production, so no /scalar). Images: `src/backend/RealmWeaver.Api/Dockerfile` (context: repo root) and `src/frontend/Dockerfile`.
- Release: `release.yml` pushes both images to GHCR on main (`edge`, `sha-*`) and on `v*.*.*` tags (`1.2.3`, `1.2`, `latest`). On PRs that touch a Dockerfile or the workflow, it only builds.
- Database: `docker compose up -d` (Postgres 17, user/pass/db `realmweaver`, local only). The API migrates and seeds on startup.
- New migration (per module): `dotnet tool restore`, then `dotnet ef migrations add <Name> --context <Module>DbContext --project src/backend/RealmWeaver.Modules.<Module> --startup-project src/backend/RealmWeaver.Api -o Persistence/Migrations`
- API integration tests use Testcontainers, so Docker must be running for `dotnet test`.
- Sample realms: `dotnet run --project tools/RealmWeaver.Cli -- --seed 42 --biome forest --size 40x30` (output in `samples/`, which is gitignored)

## Architecture rules
- **Modular monolith.** Modules (`RealmWeaver.Modules.*`) talk to each other only through their public contracts (interfaces and DTOs). They never touch another module's entities or tables. This keeps the Milestone 9 microservice split mechanical.
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

## Project direction
- **GM tool first** (ADR 0010). Realm Weaver is for a GM: quick-weave maps, a component catalog, completing realms and handing out maps. The Ritual and Player pages are a *player preview*. Player-session features (logins, parties, trading, player-run completion) belong to the separate Campaign Manager client planned with Milestone 9. Ask before adding them here.
- **Local dev data is disposable** while the project is pre-release. When a change would need a backfill or legacy-shape handling, prefer a clean migration plus a DB reset (`docker compose down -v`) and say so in the summary. Still design schemas and contracts so later features don't need big rewrites.

## Working with the maintainer
The maintainer is using this project to sharpen their skills, so agents leave room for them to build:
- **The UI's look is theirs.** Don't restyle the frontend (colours, layout, visual design of components) unless asked. Functional changes are fine; keep the hooks, the API client and the tests working.
- **Hand them a piece.** Each milestone plan names one small, self-contained task for the maintainer to write (for example a test, an endpoint or a component), with a short brief and acceptance criteria. Agents don't implement that task.
- **Explain the why.** PR descriptions include a short *Concept practised* note (for example idempotency, transactional outbox, RBAC) saying what the change teaches and why it's built that way.
- **Review, don't rewrite.** When the maintainer has written code, review it like a senior engineer: point out issues and explain the reasoning, and let them make the change unless they ask otherwise.

## Review guidelines
These apply to every reviewer (Codex on GitHub, Claude's `/code-review`, a local audit). CI already checks the architecture tests, analyzer warnings and OpenAPI drift, so don't repeat those.
- **Treat unprotected concurrency as P1.** Flag any check-then-act on shared state (a holder check before a grant, a quantity check before a spend) that isn't guarded by a lock, a constraint or `xmin`. `UnitOfWork` runs at Read Committed, so a transaction alone doesn't make a check safe.
- **Treat drift from an ADR as P1.** Flag a change that contradicts an ADR in `docs/adr/`, or that relies on an assumption an ADR states which no longer holds. Say which ADR and suggest whether to change the code or amend the ADR.
- **Treat generator output changes without a version bump as P1.** Any change to `RealmWeaver.Generation` that changes the output for the same seed and inputs must bump `GeneratorLimits.Version` (ADR 0002).

## Feedback that outlives a PR
Findings that aren't fixed in the PR where they're raised go to GitHub Issues with the `review-finding` label, so every agent sees them across branches and milestones.
- **Where things go.** Feedback on a diff stays a PR comment. A finding deferred past its PR becomes an issue. An accepted decision becomes an ADR.
- **What an issue says.** What was observed (with file references), why it matters, and when to revisit it (a milestone or a condition), plus who raised it. Phrase open design questions as questions rather than as a solution.
- **Before planning a milestone**, check the open issues (`gh issue list --label review-finding`) and say which ones the plan covers.
- **Closing.** A PR that resolves an issue says `Closes #<n>`. The maintainer decides which issues are closed as won't-fix or moved into the roadmap.
