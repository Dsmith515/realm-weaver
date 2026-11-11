# ADR 0002: Deterministic generation with a custom seeded RNG and forked streams

- **Status:** Accepted
- **Date:** 2026-11-11

## Context
A Map component *is* a seed: using it again must rebuild the same realm, possibly years later and on a newer .NET runtime. A mismatched Map should keep the same layout but reroll its encounters. Changing the event count should not reshuffle the terrain.

`System.Random` doesn't promise a fixed sequence across .NET versions, and `string.GetHashCode()` is randomized per process.

## Decision
- `SeededRandom` implements **xoshiro256\*\*** seeded through SplitMix64. A unit test pins its output.
- `StableHash` (FNV-1a + SplitMix64 mixing) derives seeds and noise values.
- Each generation step uses its own **forked stream**: `Fork("size")`, `Fork("field/terrain")`, `Fork("field/events/{variant}")`, and so on. A fork depends only on the parent seed and a purpose string, never on how many numbers were drawn before it.
- Noise lattice values are hashed from `(seed, x, y)`, so there are no stored random tables.
- `RealmGenerator` is pure. Content comes in through `IContentCatalog`, and the output is a serializable `RealmDocument` that records `GeneratorVersion`.

## Consequences
- The same request always gives the same document. The tests check this byte for byte through JSON.
- Encounter variants and difficulty only change the event streams, so terrain stays stable. The exception is tiles a settlement clears inside its own footprint.
- Any change to the algorithm changes old realms. Bump `GeneratorLimits.Version`. Stored realms keep their snapshot, and maps can be regenerated with the version they were created under if that becomes necessary.
