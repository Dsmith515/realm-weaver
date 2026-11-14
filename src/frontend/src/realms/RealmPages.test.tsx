import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { Realm, RealmArea, RealmSummary } from '../api/types'
import { fakeApi, type ApiCall } from '../test/fakeApi'
import { player } from '../test/fixtures'
import { renderApp } from '../test/render'

const summary: RealmSummary = {
  id: 'realm-1',
  playerId: player.id,
  mapComponentId: 'map-1',
  kind: 'fieldWithDungeon',
  biome: 'forest',
  width: 24,
  height: 20,
  floorCount: 1,
  threat: 2,
  dangerModifier: 1,
  encounterVariant: 7,
  seed: '18446744073709551000',
  generatorVersion: '0.3.0',
  createdAt: '2026-11-12T12:00:00Z',
  areas: [
    { id: 'field', kind: 'field', name: 'Forest field', floorNumber: 0 },
    { id: 'floor-1', kind: 'dungeonFloor', name: 'Floor 1', floorNumber: 1 },
  ],
}

function area(id: string, eventId: string, eventName: string): RealmArea {
  return {
    id,
    kind: id === 'field' ? 'field' : 'dungeonFloor',
    name: id,
    floorNumber: id === 'field' ? 0 : 1,
    width: 24,
    height: 20,
    terrainRows: [],
    spawn: null,
    events: [
      {
        id: eventId,
        templateId: 'beast-lair',
        name: eventName,
        category: 'encounter',
        footprint: { x: 1, y: 1, width: 2, height: 2 },
        location: 'B2:C3',
        mobs: [{ archetypeId: 'grey-wolf', name: 'Grey wolf', role: 'minion', count: 3 }],
      },
    ],
    features: [{ id: 'T1', kind: 'trap', position: { x: 5, y: 5 }, description: 'Pit trap', location: 'F6' }],
    rooms: [],
  }
}

const realm = {
  summary,
  document: { generatorVersion: '0.3.0', seed: summary.seed, threat: 2, request: {}, theme: null, areas: [area('field', 'E1', 'Wolf den'), area('floor-1', 'E2', 'Guard post')] },
} as unknown as Realm

const svgFor = (call: ApiCall) =>
  new Response(
    `<svg xmlns="http://www.w3.org/2000/svg"><g data-event-id="${call.query.get('area') === 'floor-1' ? 'E2' : 'E1'}"><rect/></g><g data-feature-id="T1"><rect/></g></svg>`,
    { headers: { 'content-type': 'image/svg+xml' } },
  )

const mirror: RealmSummary = {
  ...summary,
  id: 'realm-2',
  components: {
    mapName: "Beginner's Mossgreen Beastward Wax Forest Field and Dungeon Map",
    needle: { id: 'n', name: "Beginner's Needle" },
    thread: { id: 't', name: 'Wheatgold Thread' },
    coating: { id: 'c', name: 'Beastward Wax' },
  },
  isMirror: true,
}

function routes() {
  return {
    'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
    'GET /api/players': [player],
    'GET /api/realms': [summary, mirror],
    'GET /api/realms/:id': realm,
    'GET /api/realms/:id/export': svgFor,
  }
}

describe('Realm pages', () => {
  it('lists the player’s realms with a link to each', async () => {
    const api = fakeApi(routes())
    renderApp('/realms')

    expect(await screen.findByRole('link', { name: 'Forest field + dungeon' })).toHaveAttribute('href', '/realms/realm-1')
    expect(screen.getAllByText(/danger \+1/)).toHaveLength(2)
    expect(api.callsTo('GET', '/api/realms')[0].query.get('playerId')).toBe(player.id)
  })

  it('lists GM Quick Weaves on their own tab', async () => {
    const api = fakeApi(routes())
    const user = userEvent.setup()
    renderApp('/realms')

    await user.click(await screen.findByRole('tab', { name: 'GM Quick Weaves' }))

    await waitFor(() => expect(api.callsTo('GET', '/api/realms').some((c) => c.query.get('gm') === 'true')).toBe(true))
    expect(screen.getByRole('tab', { name: 'GM Quick Weaves' })).toHaveAttribute('aria-selected', 'true')
  })

  it('names a realm after its map, lists its components and flags mirrors', async () => {
    fakeApi(routes())
    renderApp('/realms')

    const link = await screen.findByRole('link', { name: "Beginner's Mossgreen Beastward Wax Forest Field and Dungeon Map" })
    const item = link.closest('li')!
    expect(within(item).getByText("Beginner's Needle · Wheatgold Thread · Beastward Wax")).toBeInTheDocument()
    expect(within(item).getByText('Mirror')).toBeInTheDocument()
    expect(within(item).getByText(/Field \+ Dungeon · threat 2/)).toBeInTheDocument()
    expect(within(screen.getByRole('link', { name: 'Forest field + dungeon' }).closest('li')!).queryByText('Mirror')).not.toBeInTheDocument()
  })

  it('lists the spawn point first and highlights it on the map', async () => {
    const withSpawn = {
      ...realm,
      document: { ...realm.document, areas: [{ ...area('field', 'E1', 'Wolf den'), spawn: { x: 2, y: 11 } }, area('floor-1', 'E2', 'Guard post')] },
    } as unknown as Realm
    fakeApi({
      ...routes(),
      'GET /api/realms/:id': withSpawn,
      'GET /api/realms/:id/export': () => new Response('<svg xmlns="http://www.w3.org/2000/svg"><g id="spawn" data-feature-id="S"><circle/></g></svg>'),
    })
    const user = userEvent.setup()
    renderApp('/realms/realm-1')

    const placements = await screen.findByRole('complementary', { name: 'Placements' })
    const buttons = await within(placements).findAllByRole('button')
    expect(buttons[0]).toHaveAccessibleName(/S Spawn point C12/)

    const svg = await screen.findByTestId('realm-svg')
    await waitFor(() => expect(svg.querySelector('[data-feature-id="S"]')).not.toBeNull())
    await user.click(buttons[0])
    expect(svg.querySelector('[data-feature-id="S"]')).toHaveClass('rw-highlight')
  })

  it('highlights a placement on the map when it is clicked', async () => {
    fakeApi(routes())
    const user = userEvent.setup()
    renderApp('/realms/realm-1')

    const svg = await screen.findByTestId('realm-svg')
    await waitFor(() => expect(svg.querySelector('[data-event-id="E1"]')).not.toBeNull())
    const placements = screen.getByRole('complementary', { name: 'Placements' })
    await user.click(within(placements).getByRole('button', { name: /E1 Wolf den/ }))

    expect(svg.querySelector('[data-event-id="E1"]')).toHaveClass('rw-highlight')
    expect(within(placements).getByText('3× Grey wolf')).toBeInTheDocument()

    await user.click(within(placements).getByRole('button', { name: /T1 Trap/ }))
    expect(svg.querySelector('[data-feature-id="T1"]')).toHaveClass('rw-highlight')
    expect(svg.querySelector('[data-event-id="E1"]')).not.toHaveClass('rw-highlight')
  })

  it('switches floors and offers exports for the shown area', async () => {
    const api = fakeApi(routes())
    const user = userEvent.setup()
    renderApp('/realms/realm-1')

    await user.click(await screen.findByRole('tab', { name: 'Floor 1' }))

    expect(screen.getByRole('tab', { name: 'Floor 1' })).toHaveAttribute('aria-selected', 'true')
    expect(await screen.findByRole('button', { name: /E2 Guard post/ })).toBeInTheDocument()
    await waitFor(() => expect(api.callsTo('GET', '/api/realms/realm-1/export').at(-1)!.query.get('area')).toBe('floor-1'))
    expect(screen.getByRole('link', { name: 'Download SVG' })).toHaveAttribute('href', '/api/realms/realm-1/export?format=svg&area=floor-1')
    expect(screen.getByRole('link', { name: 'JSON' })).toHaveAttribute('href', '/api/realms/realm-1/export?format=json')
  })
})
