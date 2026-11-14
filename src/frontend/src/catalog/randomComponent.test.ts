import { describe, expect, it } from 'vitest'
import { randomComponent, type Rng } from './randomComponent'

/** A small seeded generator so each run sees the same "random" sequence. */
function seeded(seed: number): Rng {
  let state = seed
  return () => {
    state = (state * 1664525 + 1013904223) % 4294967296
    return state / 4294967296
  }
}

const suffixes = { needle: ['Needle'], thread: ['Thread', 'Cord', 'Twine'], coating: ['Coating', 'Wax', 'Pitch'] }

describe('randomComponent', () => {
  it('names every type with an allowed last word and only that type’s details', () => {
    for (let seed = 1; seed <= 50; seed++) {
      for (const type of ['needle', 'thread', 'coating'] as const) {
        const c = randomComponent(type, suffixes, seeded(seed))
        expect(suffixes[type]).toContain(c.name.split(' ').at(-1))
        expect(c.type).toBe(type)
        expect([c.needle, c.thread, c.coating].filter(Boolean)).toHaveLength(1)
      }
    }
  })

  it('keeps every value inside the server’s limits', () => {
    for (let seed = 1; seed <= 200; seed++) {
      const needle = randomComponent('needle', suffixes, seeded(seed))
      expect(needle.rank).toBeGreaterThanOrEqual(1)
      expect(needle.rank).toBeLessThanOrEqual(10)
      expect(needle.needle!.difficultyTier).toBeGreaterThanOrEqual(1)
      expect(needle.needle!.difficultyTier).toBeLessThanOrEqual(10)

      const coating = randomComponent('coating', suffixes, seeded(seed)).coating!
      expect(new Set(coating.mobTypes).size).toBe(coating.mobTypes.length)
      if (coating.realmKind === 'field') {
        expect(coating.floorCount).toBe(0)
      } else {
        expect(coating.floorCount).toBeGreaterThanOrEqual(1)
        expect(coating.floorCount).toBeLessThanOrEqual(3)
      }
    }
  })

  it('is repeatable for the same random sequence', () => {
    expect(randomComponent('coating', suffixes, seeded(7))).toEqual(randomComponent('coating', suffixes, seeded(7)))
  })
})
