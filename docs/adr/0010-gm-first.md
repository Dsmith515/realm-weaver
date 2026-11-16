# ADR 0010: GM first: open recipes and GM-run completion

- **Status:** Accepted; completion made repeatable by ADR 0011
- **Date:** 2026-11-15
- **Supersedes:** the Cartographer gate in ADR 0008 and the *Map Required* default in ADR 0009

## Context
After trying Milestone 8, the user saw that it mixed GM and player concepts in ways the app can't support yet:
- **Players completed objectives themselves,** gated by Cartographer status. Without logins, parties or sessions, nothing stops the wrong person from clicking Complete. "The player completes the run" is also a session feature, not a map-generation one.
- **Recipes didn't stick.** Weaving the same Needle, Thread and Coating without holding the map wove a brand-new realm. Rule 2 only looked at maps the weaver owned. The user expects ingredients to lead to *a place*: the map distributes the recipe, or grants entry when the GM says entry needs it (a guild hall, a special area).

The main goal is a **GM tool**: quick realms for a session, plus a repository of components the GM creates and hands to players. The player side stays as groundwork for a later session client.

## Decision
- **Same ingredients, same realm.** Rule 2 now checks the catalog as well as the weaver's inventory.
  - When no map is chosen, the resolver uses a map woven with the same Thread, Coating and needle base type: an owned one first, otherwise the oldest in the catalog (`ICatalogModule.FindMapsByRecipeAsync`, `SlotSource.KnownRecipe`).
  - The ritual reopens it, with the same seed, and records no new map. The player still doesn't get the map.
  - Advanced size options skip the lookup and weave a custom realm.
- **Maps are open by default.** `MapFlags.MapRequired` defaults to `false`. A GM marks a map *Map required* to seal it.
  - When the ingredients point at a sealed map the player doesn't hold, the preview shows `map-required`, and perform returns 409, the same as `not-owned`.
  - A sealed realm is reachable only by its holders. Weaving the same ingredients doesn't quietly make a new one.
- **The GM completes the run.** `POST /api/realms/{id}/completion` takes `{ objectiveKey?, recipientPlayerIds[] }`.
  - The objective is optional.
  - It works for Quick Weave realms too.
  - The completion and every grant run in one transaction (ADR 0005).
  - Players who can't take the map are skipped with a reason: `already-held`, `unknown-player`, `unique-held` or `map-missing`. A Unique map takes one recipient.
  - The realm stores who received the map (`MapGivenTo`).
- **Cartographer is parked.**
  - The skill endpoint and the DTO fields are gone.
  - The `trained_skills` column stays, unused.
  - `NeedleBoon.Cartographer` stays a valid value with no effect.
  - Attunement stays, and the realm sheet still records the weaver's attuned needle for the GM.
- **The navigation leads with the GM:** Quick Weave (the landing page), Realms and Components. Ritual and Player sit under *Player preview*.
- **Dev data was wiped** rather than migrated, because the project is pre-release. The migrations still apply cleanly to a non-empty database: `map_given_to` defaults to `{}`.

## Consequences
- **The first weave of a combination becomes its canonical realm.** There are only so many Thread × Coating × base-type combinations, so a GM who wants variety uses Advanced options, mirrors (a chosen map plus other components) or Quick Weave. If that proves too tight, a later option is a per-weave "fresh realm" toggle, or folding the advanced options into the recipe key.
- **Two simultaneous first weaves of the same new combination can both create maps.** The oldest one wins from then on. This is acceptable for a GM tool; the Phase 2 version can take a recipe-level lock.
- **Player-run completion, party hand-outs and trading move to a separate session/VTT client** that consumes this API with real identities. That client is also the natural first external consumer for the Milestone 9 service split. Its needs (auth, presence, a shared ritual view) shouldn't shape the generator's core.
