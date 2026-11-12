# ADR 0004: Each module owns its DbContext, schema and migrations

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
ADR 0001 says modules talk only through public contracts, so that the Phase 2 split into services is mechanical. Persistence is where that rule usually breaks. One shared `DbContext` that maps every module's entities makes it easy to join across modules or load another module's rows, and then the split means untangling queries.

Phase 1 still needs one ACID transaction across modules for a ritual (spend consumables, create the map, grant it, store the realm).

## Decision
- **One DbContext per module, in the module's project.** `CatalogDbContext` maps the `catalog` schema and `InventoryDbContext` maps `inventory`. Rituals and Realms get theirs in Milestone 5. Each context keeps its own migration history table (`<schema>.__ef_migrations_history`) and its own `Persistence/Migrations` folder.
- **Entities and contexts are `internal`.** Other modules can only see the `Contracts` namespace: an `I…Module` interface plus DTOs. The compiler enforces this, and `PersistenceTests` checks it.
- **No cross-module foreign keys.** `inventory_items.component_definition_id` is a plain uuid. Inventory gets component data by calling `ICatalogModule`, and Catalog archives components instead of deleting them, so ids that players own never dangle.
- **Endpoints live with their module.** Granting a component (`POST /api/components/{id}/assign`) is in Inventory, because it is an inventory write, and Catalog must not depend on Inventory.
- **`RealmWeaver.Infrastructure` is shared plumbing only.** It holds `AddModuleDbContext<T>` (Npgsql, snake_case names, per-schema history), the startup initializer that migrates and seeds every module in order, and HTTP error mapping. It references no module, so every module can depend on it.
- **Postgres `xmin` is the concurrency token** on mutable rows (`IsRowVersion()` on a `uint`). A lost race raises `DbUpdateConcurrencyException`, which the API maps to 409.
- **Inventory grants are a single `INSERT … ON CONFLICT DO UPDATE`.** Concurrent grants of the same stack can't lose an update or fail on the primary key.
- **Migrate and seed on startup** (`Database:InitializeOnStartup`, on by default) because the app runs locally. Seed rows have fixed ids and are only inserted when missing, so admin edits survive restarts. The release pipeline (Milestone 7) can switch this off and run an EF migration bundle instead.

## Consequences
- In Phase 2, each module's schema moves to its own database with no query changes. The calls through `ICatalogModule` become HTTP or message calls.
- In Milestone 5 the ritual transaction spans several contexts. They share one `NpgsqlConnection` and `DbTransaction` (`Database.UseTransaction`), which works because every schema is in the same database. That shared transaction is exactly what the Phase 2 saga has to replace.
- Joins across modules happen in memory through contracts. That is fine at this scale, and it keeps the cost of crossing a module boundary visible.
- Adding a migration means naming the context: `dotnet ef migrations add <Name> --context CatalogDbContext --project src/backend/RealmWeaver.Modules.Catalog --startup-project src/backend/RealmWeaver.Api -o Persistence/Migrations`.
