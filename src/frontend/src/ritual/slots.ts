import type {
  Component,
  ComponentType,
  InventoryItem,
  RitualPreview,
  RitualPreviewRequest,
  RitualSelection,
  RitualSlot,
  SlotPreview,
  SlotSource,
} from '../api/types'
import { componentTypes } from '../api/types'

/** What the player has put in each slot. Empty slots may still be filled by the server (map, matching map). */
export interface SlotChoice {
  needleId?: string
  threadId?: string
  coatingId?: string
  mapId?: string
}

/** Manual realm settings. Only used when the ritual creates a new map. */
export interface AdvancedOptions {
  width?: number
  height?: number
  eventCount?: number
}

const fieldFor: Record<RitualSlot, keyof SlotChoice> = {
  needle: 'needleId',
  thread: 'threadId',
  coating: 'coatingId',
  map: 'mapId',
}

/** Each component type has exactly one slot of the same name. */
export const slotFor = (type: ComponentType): RitualSlot => type

export const chosenId = (choice: SlotChoice, slot: RitualSlot) => choice[fieldFor[slot]]

/** An item can go in its slot if the player has one. Consumables are spent, so they need quantity 1+. */
export function canUse(item: InventoryItem): boolean {
  return item.quantity >= 1
}

/**
 * Puts a component in its slot, replacing whatever was there. Choosing the component that is already
 * in the slot takes it out again, so a click toggles.
 */
export function toggleInSlot(choice: SlotChoice, component: Component): SlotChoice {
  const field = fieldFor[slotFor(component.type)]
  return { ...choice, [field]: choice[field] === component.id ? undefined : component.id }
}

/** Puts a component in its slot (used by drag and drop, which never toggles). */
export function placeInSlot(choice: SlotChoice, component: Component): SlotChoice {
  return { ...choice, [fieldFor[slotFor(component.type)]]: component.id }
}

export function clearSlot(choice: SlotChoice, slot: RitualSlot): SlotChoice {
  return { ...choice, [fieldFor[slot]]: undefined }
}

/**
 * After a Random preview, keep what the server picked so Perform runs exactly that.
 * A map only stays if the player chose it; a matching map is found again by the server.
 */
export function choiceFromRandom(current: SlotChoice, preview: RitualPreview): SlotChoice {
  return {
    needleId: preview.needle?.component.id ?? current.needleId,
    threadId: preview.thread?.component.id ?? current.threadId,
    coatingId: preview.coating?.component.id ?? current.coatingId,
    mapId: current.mapId,
  }
}

export function toSelection(playerId: string, choice: SlotChoice, advanced: AdvancedOptions): RitualSelection {
  return {
    playerId,
    needleId: choice.needleId ?? null,
    threadId: choice.threadId ?? null,
    coatingId: choice.coatingId ?? null,
    mapId: choice.mapId ?? null,
    width: advanced.width ?? null,
    height: advanced.height ?? null,
    eventCount: advanced.eventCount ?? null,
  }
}

export function toPreviewRequest(playerId: string, choice: SlotChoice, advanced: AdvancedOptions): RitualPreviewRequest {
  return { ...toSelection(playerId, choice, advanced), randomize: false }
}

export function slotPreview(preview: RitualPreview | undefined, slot: RitualSlot): SlotPreview | null | undefined {
  return preview?.[slot]
}

const sourceLabels: Record<SlotSource, string | undefined> = {
  selected: undefined,
  fromMap: 'From map',
  matchingMap: 'Matching map',
  random: 'Random',
}

export const sourceLabel = (source: SlotSource) => sourceLabels[source]

export interface InventoryFilter {
  type?: ComponentType
  text?: string
}

/** The inventory panel: only items the player still has, optionally by type or name. */
export function filterInventory(items: readonly InventoryItem[], filter: InventoryFilter): InventoryItem[] {
  const text = filter.text?.trim().toLowerCase()
  return items.filter(
    (item) =>
      canUse(item) &&
      (!filter.type || item.component.type === filter.type) &&
      (!text || item.component.name.toLowerCase().includes(text)),
  )
}

/** Groups by component type in slot order, with names sorted inside each group. Empty groups are kept. */
export function groupByType(items: readonly InventoryItem[]): { type: ComponentType; items: InventoryItem[] }[] {
  return componentTypes.map((type) => ({
    type,
    items: items.filter((i) => i.component.type === type).sort((a, b) => a.component.name.localeCompare(b.component.name)),
  }))
}
