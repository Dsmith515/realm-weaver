import { describe, expect, it } from 'vitest'
import { forestMap, inventory, moss, needle, owned, preview, slot, wax, wheat } from '../test/fixtures'
import { IdempotencyKeys } from './idempotency'
import {
  canUse,
  choiceFromRandom,
  clearSlot,
  filterInventory,
  groupByType,
  placeInSlot,
  sourceLabel,
  toggleInSlot,
  toPreviewRequest,
  toSelection,
} from './slots'

describe('slot choice', () => {
  it('puts each component in the slot for its type', () => {
    let choice = toggleInSlot({}, needle)
    choice = toggleInSlot(choice, moss)
    choice = toggleInSlot(choice, wax)
    choice = toggleInSlot(choice, forestMap)

    expect(choice).toEqual({ needleId: needle.id, threadId: moss.id, coatingId: wax.id, mapId: forestMap.id })
  })

  it('replaces the component in a slot, and clicking the same one again takes it out', () => {
    const withMoss = toggleInSlot({}, moss)

    expect(toggleInSlot(withMoss, wheat).threadId).toBe(wheat.id)
    expect(toggleInSlot(withMoss, moss).threadId).toBeUndefined()
  })

  it('drag and drop always places, never toggles', () => {
    const withMoss = placeInSlot({}, moss)

    expect(placeInSlot(withMoss, moss).threadId).toBe(moss.id)
  })

  it('clears one slot and leaves the others', () => {
    expect(clearSlot({ needleId: 'n', threadId: 't' }, 'thread')).toEqual({ needleId: 'n', threadId: undefined })
  })

  it('sends empty slots as null so the server can fill them from a map', () => {
    const selection = toSelection('p1', { mapId: forestMap.id }, { width: 30 })

    expect(selection).toEqual({ playerId: 'p1', needleId: null, threadId: null, coatingId: null, mapId: forestMap.id, width: 30, height: null, eventCount: null })
    expect(toPreviewRequest('p1', {}, {}).randomize).toBe(false)
  })
})

describe('random fill', () => {
  it('keeps the components the server picked, and only a map the player chose', () => {
    const random = preview({
      needle: slot(needle, 'random'),
      thread: slot(moss, 'random'),
      coating: slot(wax, 'selected'),
      map: slot(forestMap, 'matchingMap'),
    })

    expect(choiceFromRandom({ coatingId: wax.id }, random)).toEqual({ needleId: needle.id, threadId: moss.id, coatingId: wax.id, mapId: undefined })
    expect(choiceFromRandom({ mapId: forestMap.id }, random).mapId).toBe(forestMap.id)
  })

  it('leaves a slot as it was when the player has nothing for it', () => {
    expect(choiceFromRandom({ threadId: 'mine' }, preview()).threadId).toBe('mine')
  })

  it('labels where a slot was filled from', () => {
    expect(sourceLabel('fromMap')).toBe('From map')
    expect(sourceLabel('matchingMap')).toBe('Matching map')
    expect(sourceLabel('random')).toBe('Random')
    expect(sourceLabel('selected')).toBeUndefined()
  })
})

describe('inventory filtering', () => {
  it('hides used-up consumables', () => {
    expect(canUse(owned(wheat, 0))).toBe(false)
    expect(filterInventory(inventory, {}).map((i) => i.component.id)).not.toContain(wheat.id)
  })

  it('filters by type and by name, ignoring case', () => {
    expect(filterInventory(inventory, { type: 'coating' }).map((i) => i.component.name)).toEqual(['Beastward Wax'])
    expect(filterInventory(inventory, { text: '  NEEDLE ' }).map((i) => i.component.name)).toEqual(["Beginner's Needle"])
  })

  it('groups by type in slot order with names sorted', () => {
    const groups = groupByType([owned(wax), owned(wheat), owned(moss), owned(needle)])

    expect(groups.map((g) => g.type)).toEqual(['needle', 'thread', 'coating', 'map'])
    expect(groups[1].items.map((i) => i.component.name)).toEqual(['Mossgreen Thread', 'Wheatgold Thread'])
    expect(groups[3].items).toEqual([])
  })
})

describe('idempotency keys', () => {
  it('reuses the key while the request is unchanged and makes a new one after a change or reset', () => {
    let n = 0
    const keys = new IdempotencyKeys(() => `key-${++n}`)

    expect(keys.keyFor({ a: 1 })).toBe('key-1')
    expect(keys.keyFor({ a: 1 })).toBe('key-1')
    expect(keys.keyFor({ a: 2 })).toBe('key-2')
    keys.reset()
    expect(keys.keyFor({ a: 2 })).toBe('key-3')
  })
})
