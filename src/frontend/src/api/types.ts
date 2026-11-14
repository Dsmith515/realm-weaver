import type { Schemas } from './client'

export type Component = Schemas['ComponentDto']
export type ComponentInput = Schemas['ComponentInput']
export type ComponentType = Schemas['ComponentType']
export type Rarity = Schemas['Rarity']
export type Biome = Schemas['Biome']
export type Element = Schemas['Element']
export type RealmKind = Schemas['RealmKind']
export type Player = Schemas['PlayerDto']
export type InventoryItem = Schemas['InventoryItemDto']
export type Settings = Schemas['CatalogSettingsDto']
export type RitualSelection = Schemas['RitualSelection']
export type RitualPreviewRequest = Schemas['RitualPreviewRequest']
export type RitualPreview = Schemas['RitualPreviewDto']
export type RitualResult = Schemas['RitualResultDto']
export type RitualSlot = Schemas['RitualSlot']
export type SlotPreview = Schemas['SlotPreview']
export type SlotSource = Schemas['SlotSource']
export type RealmSummary = Schemas['RealmSummaryDto']
export type Realm = Schemas['RealmDto']
export type RealmArea = Schemas['RealmArea']

// Runtime lists of the API's enums. `satisfies` plus the exhaustiveness checks below make the compiler
// fail if the backend adds a value the frontend doesn't list.
export const componentTypes = ['needle', 'thread', 'coating', 'map'] as const satisfies readonly ComponentType[]
export const rarities = ['common', 'uncommon', 'rare', 'epic', 'legendary'] as const satisfies readonly Rarity[]
export const biomes = ['plains', 'forest', 'desert', 'tundra', 'swamp', 'volcanic', 'mountains', 'coast'] as const satisfies readonly Biome[]
export const elements = ['none', 'fire', 'water', 'earth', 'air', 'frost', 'lightning', 'light', 'shadow'] as const satisfies readonly Element[]
export const realmKinds = ['field', 'dungeon', 'fieldWithDungeon'] as const satisfies readonly RealmKind[]
export const ritualSlots = ['needle', 'thread', 'coating', 'map'] as const satisfies readonly RitualSlot[]

type Exhaustive<All, Listed> = [All] extends [Listed] ? true : never
export const enumListsAreComplete: [
  Exhaustive<ComponentType, (typeof componentTypes)[number]>,
  Exhaustive<Rarity, (typeof rarities)[number]>,
  Exhaustive<Biome, (typeof biomes)[number]>,
  Exhaustive<Element, (typeof elements)[number]>,
  Exhaustive<RealmKind, (typeof realmKinds)[number]>,
  Exhaustive<RitualSlot, (typeof ritualSlots)[number]>,
] = [true, true, true, true, true, true]

export type NameSuffixes = Schemas['NameSuffixesDto']
export type MapDetails = Schemas['MapDetailsDto']
export type QuickWeaveRequest = Schemas['QuickWeaveRequest']
export type QuickWeaveLimits = Schemas['QuickWeaveLimits']
export type QuickWeavePreview = Schemas['QuickWeavePreviewDto']
export type DigestEntry = Schemas['DigestEntry']

const labels: Record<string, string> = {
  fieldWithDungeon: 'Field + Dungeon',
}

/** A tile's label as the exports print it: column letters then a 1-based row, so (2, 11) → "C12". */
export function tileLabel(x: number, y: number): string {
  let column = ''
  for (let n = x + 1; n > 0; n = Math.floor((n - 1) / 26)) {
    column = String.fromCharCode(65 + ((n - 1) % 26)) + column
  }
  return `${column}${y + 1}`
}

/** "fieldWithDungeon" → "Field + Dungeon", "forest" → "Forest". */
export function label(value: string): string {
  return labels[value] ?? value.charAt(0).toUpperCase() + value.slice(1).replace(/([A-Z])/g, ' $1').toLowerCase()
}
