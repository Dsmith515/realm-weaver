# ADR 0011: Repeatable completions, player and GM map views

- **Status:** Accepted
- **Date:** 2026-11-15
- **Supersedes:** the "completes once" rule in ADR 0010

## Context
ADR 0010 let the GM complete a run once. After trying it, the user pointed out that realms can always be visited again: the same ingredients reopen the same place. When a new party runs the realm, the GM has to be able to complete it again for them, and hand the map to the players who don't hold it yet. The GM also wants to see everyone who has completed something on a realm, and to know which party was first.

The same round of feedback separated what a player sees on a map from what the GM sees, and asked for GM-friendly defaults: no structures unless asked for, sortable and scrolling tables, and player search wherever players are picked.

## Decision
- **A completion is an event, not a status.**
  - `realms.completions` stores one row per completion: realm, map, objective (a snapshot), the players with their names copied (ADR 0006), whether each received the map, and when.
  - The realm columns `completed_at`, `completed_objective` and `map_given_to` are dropped. Dev data was wiped, as before.
- **Completions belong to the map.** Every run woven from the same map shares one history (`GET /api/realms/{id}/completions`).
  - The earliest completion is the *first to complete*. Ties go to the lower (time-ordered) id.
  - Realm summaries carry a `completionCount` for the list badge.
- **The GM completes for a party.** `POST /api/realms/{id}/completion` takes `{ objectiveKey, playerIds, mapRecipientIds? }`.
  - An objective and at least one known player are required.
  - With no `mapRecipientIds`, every player in the party who doesn't hold the map receives it. An empty list hands it to no one.
  - Recipients must be in the party. A Unique map takes one recipient, and the GM picks who keeps it.
  - The completion and the grants still run in one transaction (ADR 0005). Two completions racing both count, and a map is only ever granted once (the grant is idempotent).
- **Player and GM views of a map.**
  - The player's view (inventory) shows kinds of creatures and loot without counts or locations, and objective titles without locations. Its rules show as tags (*Map required*, *Unique*, *No mirrors*) with an explanation on hover or focus. An open map shows no rules.
  - The GM's view (realm page, Components catalog) adds counts, locations, settlement settings and a link to the original realm.
  - The GM's view lists each settlement with its population and wealth. How settlements were set up (a chosen or random population, structure options) isn't shown: it doesn't help at the table.
  - From a realm page, the details link to another realm only for a mirror, and then to the realm it mirrors.
  - A GM who wants to remember where something is can note it on the component for now.
  - The views are filtered in the client. When sessions and logins arrive, the session client should get a player projection from the API instead (ADR 0010).
- **Structures are off by default in Quick Weave.** Image generators add buildings without being asked, and a GM may prefer to place them by hand.
  - A new `SuggestStructures` option names structure ideas for each field settlement without placing them. It's recorded in the map recipe so a reweave rebuilds the same ideas.
  - Suggestions appear on the realm page and the placement sheet, never on the SVG or in the image prompt.
  - Rituals keep placing structures.
- **Settlements have a wealth** (Destitute, Poor, Comfortable, Prosperous, Opulent), rolled from its own `wealth` stream so it never changes the mobs or structures. It leans richer as threat rises, and an abandoned settlement is at most Poor. The generator moves to 0.6.0. Wealth is flavour for now; loot could lean on it later.
- **Lists that grow are server-paged and searchable.**
  - The catalog sorts by column (`sort`, `desc`), with type and rarity in their natural order.
  - The realm list is paged.
  - Players can be searched (`search`, `limit`).
  - Search text treats `%` and `_` literally (Npgsql's two-argument `ILike` disables the escape character, so the escape is passed explicitly).

## Consequences
- A party that repeats an objective is recorded again. The history shows every completion, and the player list counts them.
- Names on completions are as they were at the time. A renamed player shows their newest recorded name in the player list.
- The player view hides information only by not showing it. Real hiding waits for authentication.
