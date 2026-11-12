# ADR 0005: The ritual as one local transaction, with idempotency and deterministic mismatches

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
A ritual touches all four modules. It spends a Thread and a Coating (Inventory), may create a Map (Catalog), grants that map (Inventory) and stores the realm (Realms). Phase 1 runs this as one ACID transaction (ADR 0001). Each module has its own DbContext and schema (ADR 0004), so the transaction has to span several contexts without any module reaching into another.

Clients retry, double-click and race each other. A retry must never spend twice. Two rituals spending the last thread must not both succeed.

The roadmap leaves open what a mismatched map changes and by how much.

## Decision
**One connection, one transaction.** Within a DI scope, every module DbContext is built on the scope's single `NpgsqlConnection`. `UnitOfWork.ExecuteAsync` begins a transaction on that connection and enlists every context in the scope, including contexts created during the work. It commits when the work returns and rolls back and clears change trackers when it throws. Modules expose plain contract methods (`SpendOneEachAsync`, `CreateMapAsync`, `GrantAsync`, `SaveAsync`) that don't know about the transaction.

**Order of work.** Reads and the (pure) generation happen first, outside the transaction, which keeps row locks short. Inside the transaction:
1. Insert the `Ritual` row, which claims the unique idempotency key.
2. Spend one Thread and one Coating with `UPDATE … WHERE xmin = <loaded>`.
3. Create the Map, if it is new. Its id is chosen up front by the orchestrator.
4. Grant the map. If the player already owns it, nothing changes.
5. Store the realm snapshot.

All ids (ritual, map, realm) are allocated before the transaction. The Phase 2 saga needs this too, so its steps can be retried safely.

**Idempotency.** `POST /api/rituals` requires an `Idempotency-Key` header. The ritual row stores the key and a SHA-256 of the request.
- If the key already exists with the same hash, the stored result is replayed: `200` with `Idempotent-Replayed: true`.
- If it exists with a different hash, the request is rejected with `422`.
- Because the key is claimed first, a concurrent request with the same key blocks on the unique index until the first commits, then fails with `23505` and replays. If the first rolls back, the second simply proceeds.
- Failed rituals leave no row, so their key can be retried.

**Status codes.**
- `400` for an invalid selection: a missing slot, an unknown id, the wrong type, or a bad Advanced option.
- `409` when a component isn't owned, or when a concurrent ritual spent it first. The second case is an `xmin` conflict, or the quantity hit zero. The client should reload and retry.
- `422` when a key is reused for a different request, or when the generator can't build the realm.

**Mismatched maps.**
- *Layout* (seed, size, realm kind, floors and biome) always comes from the map and its *recorded* Thread and Coating.
- *Encounters* come from what is in the slots: element and mob types from the slotted Thread and Coating, and difficulty from the slotted Needle.
- `DangerModifier` is +1 for one mismatched slot and +2 for two or more. That's deterministic, so the preview shows the real number.
- `EncounterVariant` is 0 for a matching ritual, which rebuilds the map's realm exactly. A mismatch uses `1 + StableHash(needle/thread/coating ids) mod 10⁶`, so the same mismatch always rerolls to the same encounters.
- Advanced size and event-count options apply only when the ritual creates a new map. Event count sets the field's events, so it is rejected for dungeon-only coatings. Leaving it unset lets a dungeon-only map rebuild exactly.

## Consequences
- The tests prove the transaction's properties against a real Postgres:
  - A failure injected in the last step rolls back every earlier step, and the key stays retryable.
  - Two rituals racing for the last thread give exactly one `201` and one `409`, through the `xmin` path.
  - Five concurrent requests with one key perform the ritual once.
  - Reusing a map rebuilds an identical realm.
- Terrain under a mismatched realm's settlements can differ from the original, because settlements clear terrain inside their own footprint (ADR 0002). Everything outside event footprints is identical.
- Phase 2 replaces `UnitOfWork` with a saga. The ordering above becomes its steps, and the up-front ids and the idempotency key become its correlation and deduplication keys.
