# ADR 0009: Per-map flags are rules on the Map, enforced where they bite

- **Status:** Accepted; partly superseded by [ADR 0010](0010-gm-first.md). The Unique race was closed on 2026-11-23 (see below).
- **Date:** 2026-11-15

## Context
The design notes add three per-map options:
- **Map Required:** you can't get in without the map.
- **Components Must Match:** no mirrors.
- **Unique:** the map can't be shared.

Each one is a rule about the map, but they bite in different modules. Rituals decides who may weave. Inventory decides who may hold.

## Decision
- `MapDetails.Flags` is a `MapFlags(MapRequired = true, ComponentsMustMatch = false, Unique = false)` stored in the map's jsonb.
  - The defaults keep the old behaviour, and maps from before flags read as required.
  - Flags aren't recipe fields, so they're never locked.
  - An update that leaves them out keeps the recorded flags, the same way as objectives (ADR 0007).
- **Map Required** is enforced in `RitualResolver`. When it's off, the Map slot's `not-owned` check is skipped, so anyone holding the components can weave the recorded seed. The map is *recorded* only through completion (ADR 0008).
- **Components Must Match** is also enforced in `RitualResolver`. A mismatch on such a map adds a `mirror-not-allowed` issue: the preview shows it, and perform rejects it with 400. GM Quick Weave is exempt. The GM sets the flags and can mirror their own maps for testing.
- **Unique** is enforced in `InventoryService.GrantAsync`, so every way of granting obeys it: admin assign returns 409, and completion records nothing, with the reason `unique-held`.
  - The check is "does any other player hold it" followed by the insert. At first two grants racing to two different players could both pass, which was accepted while only an admin assigned and only one weaver completed a given realm.
  - **That assumption stopped holding** once completions became repeatable (ADR 0011) and players could self-assign. So the per-map lock planned for Phase 2 came forward:
    - `GrantAsync` takes `pg_advisory_xact_lock` keyed by the map before the check, so grants of one Unique map run one at a time. The check runs after the lock, and under Read Committed it sees any grant that committed while it waited.
    - An advisory transaction lock lasts only as long as its transaction. A caller already inside a unit of work (completion) holds it until commit; assign and self-assign get a transaction of their own.
  - A partial unique index can't express "one holder per *unique* map", because the flag lives in another module's table (ADR 0004).
  - **Turning Unique on** is refused (409) while more than one player holds the map, because the GM can't take a map back. Catalog asks through `IMapHolderCount`, a Catalog contract that Inventory implements, so neither module reads the other's tables. An edit racing a grant can still leave two holders; that window is accepted in Phase 1.

## Consequences
- Mirrors of a map flagged Components Must Match can't be made, so the realm sheet's "objectives may differ" note never appears for them.
- Map Required off plus Unique on gives a "public, but only one keeper" map: anyone can visit, and only its holder keeps it.
