# ADR 0003: Dungeons as a room graph with host-driven material zones

- **Status:** Accepted
- **Date:** 2026-11-11

## Context
Dungeons need to make sense. A dungeon under a stone castle should be mostly stone, but because it is underground it can reach past the castle's outline and turn into caves or other natural passages. Every room must be reachable, layouts shouldn't be linear, and encounters, settlements, traps, chests and secret rooms all have to fit without overlapping.

## Decision
- **Host structure.** A field-with-dungeon realm places a host structure (castle, ruined keep, mine, cave mouth, tower, temple) on the field first. The host is a normal event template with a `HostMaterial`. A dungeon-only realm picks a host the same way but never draws it.
- **Room graph.** Each floor places room boxes, with floor 1's core clustered under the host footprint. A minimum spanning tree connects them, plus about 15% extra edges, so layouts have loops. Corridors are found with BFS and avoid other rooms' boxes.
- **Material zones.** Every room starts as the primary material. Past a core depth, whole subtrees may switch to a biome-specific branch material. The root of a switched subtree is a **transition room**. Extra edges may only join rooms of the same material, or a transition room. The primary share is at least 75% on floor 1, 55% on floor 2 and 40% on floor 3.
- **Shape follows material.** Masonry gives rectangles and L-shapes with doors. Mine gives shafts with side galleries. Cave gives cellular-automata blobs.
- **Rooms hold events.** Each room holds at most one event. Settlements can spread over connected rooms of the same material. The farthest room on the deepest floor is the boss chamber; on other floors it holds the stairs down, and the next floor starts at the same coordinates.
- **Stable layout.** Room layout, materials and the field host come from layout streams that don't depend on the encounter variant. Events, secret rooms, chests and traps come from the variant stream.

## Consequences
- The tests check that every room is reachable, materials only meet through a transition, floor 1 is mostly the host material, events never share a room, traps and chests stay out of events, stairs line up between floors, and the boss is on the deepest floor.
- Graph-level rules are easy to check and to explain. One known gap: a corridor can run past the side of another room's box. That's allowed for reachability, but it's a cosmetic opening that isn't recorded as a connection.
