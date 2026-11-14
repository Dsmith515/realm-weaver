import type { ComponentInput, ComponentType, NameSuffixes, Rarity } from '../api/types'
import { biomes, elements, rarities, realmKinds } from '../api/types'

/** Returns a number in [0, 1). Injected so tests can be deterministic. */
export type Rng = () => number

/** The types an admin can create by hand (and randomize). Maps come only from rituals. */
export const creatableTypes = ['needle', 'thread', 'coating'] as const satisfies readonly ComponentType[]

export type RandomizableType = (typeof creatableTypes)[number]

/** Used until the server's list has loaded. It matches the server's first (literal) word per type. */
export const fallbackSuffixes: NameSuffixes = { needle: ['Needle'], thread: ['Thread'], coating: ['Coating', 'Wax'] }

const prefixes: Record<RandomizableType, readonly string[]> = {
  needle: ['Thornwood', 'Ashen', 'Hollow', 'Gilded', 'Driftwood', 'Ember', 'Whisper', 'Ironbark', 'Moonsilver', 'Wayfarer'],
  thread: ['Duskspun', 'Mistwoven', 'Cinderline', 'Rootbraid', 'Tidewhisper', 'Frostweft', 'Sunloom', 'Briarstrand', 'Gloamthread'],
  coating: ['Gloomward', 'Hexbrine', 'Marrowmilk', 'Sporeslick', 'Rustbloom', 'Ashveil', 'Graveglow', 'Wolfsbane', 'Bogmire'],
}
const categories = ['Iron', 'Bone', 'Silver', 'Glass', 'Copper', 'Obsidian', 'Driftwood', 'Amber']
const abilities = ['Steady Point', 'Marrow Sense', 'Ward Stitch', 'Clear Sight', 'True North', 'Quiet Step', 'Far Glance', 'Knit Wound', 'Read the Land']
const mobTypes = ['beast', 'undead', 'bandit', 'elemental', 'plant', 'goblinoid', 'humanoid', 'construct', 'fey']
const descriptions = ['Found in a peddler’s tray.', 'Older than it looks.', 'Hums faintly when held.', 'Smells of rain and iron.', 'Still warm from the forge.']

/** Needle tiers by rarity: common 1–2 … legendary 9–10. */
const tierBands: Record<Rarity, [number, number]> = { common: [1, 2], uncommon: [3, 4], rare: [5, 6], epic: [7, 8], legendary: [9, 10] }

const maxFloors = 3

/**
 * A plausible random component of the given type, for the "Randomize" button. Its name follows the naming rule
 * (it ends with a word allowed for the type) and every field stays within the server's limits.
 */
export function randomComponent(type: RandomizableType, suffixes: NameSuffixes = fallbackSuffixes, rng: Rng = Math.random): ComponentInput {
  const pick = <T,>(list: readonly T[]): T => list[Math.floor(rng() * list.length)]
  const between = (min: number, max: number) => min + Math.floor(rng() * (max - min + 1))
  const some = <T,>(list: readonly T[], count: number): T[] => {
    const pool = [...list]
    const chosen: T[] = []
    while (chosen.length < count && pool.length > 0) chosen.push(pool.splice(Math.floor(rng() * pool.length), 1)[0])
    return chosen
  }

  const rarity = pick(rarities)
  const rank = rarities.indexOf(rarity) * 2 + between(1, 2)
  const base = {
    type,
    name: `${pick(prefixes[type])} ${pick(suffixes[type])}`,
    rarity,
    rank,
    description: pick(descriptions),
    isPlayerAssignable: rarity === 'common',
    needle: null,
    thread: null,
    coating: null,
    map: null,
  } satisfies ComponentInput

  switch (type) {
    case 'needle': {
      const [low, high] = tierBands[rarity]
      return { ...base, needle: { difficultyTier: between(low, high), category: pick(categories), abilities: some(abilities, between(1, 3)) } }
    }
    case 'thread':
      return { ...base, thread: { biome: pick(biomes), element: pick(elements) } }
    case 'coating': {
      const realmKind = pick(realmKinds)
      return { ...base, coating: { mobTypes: some(mobTypes, between(1, 2)), realmKind, floorCount: realmKind === 'field' ? 0 : between(1, maxFloors) } }
    }
  }
}
