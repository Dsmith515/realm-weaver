# ADR 0006: Cross-module references are ids plus a snapshot of what must stay stable

- **Status:** Accepted
- **Date:** 2026-11-13

## Context
Several features need data that belongs to another module:
- A Map (Catalog) names the Needle, Thread and Coating it was woven from.
- A Realm (Realms) shows the map's name and the components used.
- The Player page shows a map's threat, mobs, loot and resource nodes, which live in the realm document.

ADR 0004 forbids joins and foreign keys across modules, and Milestone 9 will put each module in its own service.

M6 exposed a bug. A map stored only the component *ids*, and the ritual read realm kind, floors and biome from the *live* components. Editing a coating therefore re-shaped every map that used it, which broke "same map, same realm" (ADR 0002).

## Decision
**1. Store the id, plus a snapshot of anything that must not change underneath you.**
- A reference to another module is its id (a `Guid`), with no foreign key.
- If your behaviour depends on the referenced values staying the same, copy those values in when you create the record.
  - A map's `Recipe` (kind, floors, biome, element, mob tags, difficulty) is copied when the map is created, and the ritual rebuilds the layout from it.
  - A realm copies the map name and the component names so its list can be shown without calling Catalog. Realms are immutable, so a stale name is the correct, historical name.
- Display data that is fine to show *current* (a component's present name in an inventory) is looked up live through the owning module's contract instead.

**2. Protect snapshots at the source when that's cheaper than reconciling.** Catalog refuses to change a component's recipe fields once any map references it (409). The admin clones the component instead. Name, rarity, description and flags stay editable.

**3. Reads that need several modules are composed outside the modules.** An Api endpoint or a contract method calls each module's public interface and stitches the DTOs together. For example, map details:
1. `ICatalogModule` gives the map: seed, recipe, threat and component ids.
2. `ICatalogModule` resolves the component names.
3. `IRealmsModule` returns a digest of the map's original, non-mirror realm.

No module reads another module's tables or entities.

**How to add a new cross-module field:**
1. Decide whether the value must be *historical* (snapshot it) or *current* (look it up).
2. For a snapshot, add it to the owning record's creation DTO and fill it in from the orchestrator (usually Rituals).
3. For a lookup, add a contract method (or reuse one) on the module that owns the data, returning a DTO, never an entity.
4. Compose the result in the Api layer or in the orchestrating module, and expose it through a DTO so the OpenAPI contract and generated TS types cover it.

## Consequences
- Maps and realms reproduce exactly even after admin edits. Old maps were backfilled from their components' values at migration time.
- Some data is duplicated (names, recipe fields). That's intentional, and each copy has a single writer.
- The Milestone 9 split stays mechanical: composed reads become calls between services, and snapshots are already local to their owner.
