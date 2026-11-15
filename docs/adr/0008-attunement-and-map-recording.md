# ADR 0008: Needles match by base type, players attune one needle, and Cartographers record maps on completion

- **Status:** Accepted; partly superseded by [ADR 0010](0010-gm-first.md)
- **Date:** 2026-11-15
- **Amends:** ADR 0005, whose step 4 (grant the map) moves out of the ritual.

## Context
Up to Milestone 7, a ritual granted its map straight away. A map also matched only the exact Needle component it was woven with. The design notes (Notion: "Solo/GM tool") ask for three changes:
- Two Beginner's Needles with different abilities should count as the same recipe ingredient.
- The needle a player *weaves* with should be separate from the needle they *adventure* with. The adventure needle supplies abilities and a boon.
- A map should be recorded only when someone on the run qualifies as a **Cartographer** and an objective is completed. That makes a map a reward, not a receipt.

## Decision
**Base type.**
- `NeedleDetails.BaseType` defaults to the needle's name, so cloning a needle keeps the base type.
- A new map's `MapRecipe.NeedleBaseType` snapshots it (ADR 0006).
- The resolver's rule 1 (mismatch) and rule 2 (auto-fill an owned map) compare needles by base type. Threads and Coatings still compare by id.
- Maps from before the snapshot fall back to their recorded needle's current base type.
- Base type joins the recipe fields that lock once a map uses the component.

**Attunement and Cartographer status.** Both live in Inventory, which owns players.
- `Player.AttunedNeedleId` must be a needle the player owns. New players start attuned to the beginner needle, and a migration backfills existing players the same way.
- `Player.TrainedSkills` is a text array. Only `cartographer` exists so far, and a GM sets it.
- `PlayerDto.Cartographer` is `trainedSkill`, `attunedNeedle` (the needle's boon is `cartographer`) or null.
  - It's computed when read: Inventory asks Catalog for the attuned needle's boon through `ICatalogModule`.
  - A trained skill wins, and a boon never stacks with it.
- At weaving time the realm snapshots the attuned needle's name, abilities and boon, so the realm sheet shows what the party brought.

**Completion records the map.**
- The ritual transaction no longer grants the map. It still creates the Map component, because the realm references its seed and a mirror needs it.
- `POST /api/realms/{id}/completion { objectiveKey }` is orchestrated by Rituals, since it spans Realms and Inventory. Its reads happen first: is the realm already complete, is there a weaver, are they a Cartographer, do they hold the map already.
- Then, in one `UnitOfWork`:
  1. `IRealmsModule.CompleteAsync` sets the completion with a conditional `UPDATE … WHERE completed_at IS NULL`, so of two racing completions exactly one wins and the other gets 409.
  2. When the weaver qualifies, `IInventoryModule.GrantAsync` adds the map.
- The objective must be one of the realm's rolled objectives or its GM pin.
- The response says whether the map was recorded and, if not, why: `not-cartographer`, `gm-realm`, `already-held` or `map-missing`.

## Consequences
- Rule 2 (auto-fill) and reweaving a map only work after a run is completed by a Cartographer. A non-Cartographer can still complete runs, but keeps no map. That's the intended trade-off.
- The two writes are atomic in Phase 1. In Phase 2 the completion becomes a small saga: complete in Realms, then grant in Inventory, with compensation that reopens the realm. The claimed-once update makes it safe to retry.
- GM Quick Weaves have no weaver, so completing one records nothing. The GM assigns the map from the Components page as before.
- Party completion (every Cartographer in the party gets a map) waits for multiplayer. The single weaver stands in for the party.
