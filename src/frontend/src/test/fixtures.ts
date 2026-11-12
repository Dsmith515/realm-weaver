import type { Component, ComponentType, InventoryItem, Player, RitualPreview, SlotPreview, SlotSource } from '../api/types'

export const player: Player = { id: 'p1', name: 'Wanderer', createdAt: '2026-11-12T10:00:00Z' }
export const otherPlayer: Player = { id: 'p2', name: 'Tester', createdAt: '2026-11-12T11:00:00Z' }

let next = 0

export function component(type: ComponentType, name: string, overrides: Partial<Component> = {}): Component {
  next++
  return {
    id: overrides.id ?? `${type}-${next}`,
    type,
    name,
    rarity: 'common',
    rank: 1,
    description: '',
    isPlayerAssignable: true,
    isConsumable: type === 'thread' || type === 'coating',
    isArchived: false,
    createdAt: '2026-11-12T10:00:00Z',
    needle: type === 'needle' ? { difficultyTier: 1, category: 'Iron', abilities: [] } : null,
    thread: type === 'thread' ? { biome: 'forest', element: 'earth' } : null,
    coating: type === 'coating' ? { mobTypes: ['beast'], realmKind: 'field', floorCount: 0 } : null,
    map:
      type === 'map'
        ? { seed: '18446744073709551000', needleId: 'n', threadId: 't', coatingId: 'c', width: 30, height: 20, eventCount: 8, generatorVersion: '0.3.0', createdByPlayerId: null }
        : null,
    ...overrides,
  }
}

export const owned = (c: Component, quantity = 1, acquiredAt = '2026-11-12T10:00:00Z'): InventoryItem => ({ component: c, quantity, acquiredAt })

export const needle = component('needle', "Beginner's Needle", { id: 'needle-1' })
export const moss = component('thread', 'Mossgreen Thread', { id: 'thread-moss' })
export const wheat = component('thread', 'Wheatgold Thread', { id: 'thread-wheat', thread: { biome: 'plains', element: 'light' } })
export const wax = component('coating', 'Beastward Wax', { id: 'coating-wax' })
export const forestMap = component('map', 'Forest field map 1A2B3C', { id: 'map-forest', isPlayerAssignable: false })

export const inventory: InventoryItem[] = [owned(needle), owned(moss, 3), owned(wheat, 0), owned(wax, 2), owned(forestMap)]

export const slot = (c: Component, source: SlotSource = 'selected', ownedQuantity = 1): SlotPreview => ({ component: c, source, ownedQuantity })

export function preview(overrides: Partial<RitualPreview> = {}): RitualPreview {
  return {
    playerId: player.id,
    needle: null,
    thread: null,
    coating: null,
    map: null,
    isMismatch: false,
    mismatchedSlots: [],
    dangerModifier: 0,
    createsNewMap: true,
    advancedOptionsIgnored: false,
    issues: [],
    canPerform: true,
    ...overrides,
  }
}
