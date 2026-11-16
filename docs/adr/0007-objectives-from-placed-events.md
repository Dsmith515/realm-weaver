# ADR 0007: Objectives come from placed events, are recorded on the map, and a GM's pin lives on the realm

- **Status:** Accepted
- **Date:** 2026-11-15

## Context
Milestone 8 gives each run 1–3 objectives, and completing any one completes the run. Three questions needed answers:
1. Where do objectives come from, so they're always achievable and can be shown on the map?
2. What keeps a player's map stable when content or the generator changes later?
3. Where does a GM's manual choice go?

## Decision
**1. Objectives are derived from what was placed, not rolled on their own.**
- Each `EventTemplate` carries `ObjectiveHooks`, for example `prison` → *Free the captive*. The catalog adds realm-wide objectives: *Clear the realm*, and *Chart every floor* when the realm has dungeon floors.
- After layout and events, `ObjectivePicker` builds the pool from the placed events' hooks plus the realm-wide ones. It picks 1–3 by weight without replacement: at most one objective per event, and at most one realm-wide.
- Hooks marked `NeedsResidents` are skipped for abandoned settlements, because there's nobody left to settle a dispute with.
- The picker uses its own `Fork("objectives")` stream (ADR 0002), so adding objectives didn't move any terrain, room or event. Generator version 0.5.0.
- Every event objective stores its area id, event id, name and location, so the realm page can switch to that floor and highlight it.
- Mirrors reroll their events, so their objectives fall out of the new events, and the realm page says so.

**2. Objectives are snapshotted on the Map (ADR 0006).** When a ritual or Quick Weave creates a map, it copies the realm's objectives into `MapDetails.Objectives`. Later content edits (Milestone 10's event tables) or generator bumps then leave maps that players already hold unchanged. An admin edit that leaves `objectives` out keeps the recorded ones.

**3. A GM's pin is stored on the realm, not the map.**
- `PUT /api/realms/{id}/pinned-objective { key }` accepts any candidate: a rolled objective, another hook of a placed event, or a realm-wide objective.
- The pin is copied into a nullable jsonb column, so later content changes don't rewrite it.
- The realm document itself never changes. The pin is the only mutable field on a realm, and it's last-write-wins, because only a GM sets it and nothing else depends on it.

## Consequences
- Every objective has a place on the map and can always be completed, since the event it needs exists.
- Phase 3 Thimbles shape objectives *through* the events they boost, with no second weighting system.
- Realms from before 0.5.0 have no objectives. Their documents still load (`Objectives` defaults to empty), and a GM can still pin one from the candidates, which are computed from the stored events.
- The pipeline is: layout, then events with mobs, then objectives. Mobs are still resolved while each event is placed, on the event's own stream. Splitting mobs into a separate step after objectives would be a refactor with no behaviour change, so it waits until an objective needs to influence mobs.
