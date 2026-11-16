import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { Realm, RealmArea, RealmSummary } from '../api/types'
import { fakeApi, type ApiCall } from '../test/fakeApi'
import { component, player } from '../test/fixtures'
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
  completionCount: 0,
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
  document: {
    generatorVersion: '0.3.0',
    seed: summary.seed,
    threat: 2,
    request: {},
    theme: null,
    areas: [area('field', 'E1', 'Wolf den'), area('floor-1', 'E2', 'Guard post')],
    objectives: [
      { id: 'O1', key: 'floor-1/E2/take-the-post', templateId: 'take-the-post', title: 'Take the post', description: 'Hold it.', scope: 'event', areaId: 'floor-1', eventId: 'E2', eventName: 'Guard post', location: 'B2:C3' },
      { id: 'O2', key: 'realm/clear-the-realm', templateId: 'clear-the-realm', title: 'Clear the realm', description: 'Every hostile group.', scope: 'realm' },
    ],
  },
} as unknown as Realm

const candidates = [
  ...(realm.document.objectives ?? []),
  { id: 'P', key: 'field/E1/slay-the-beast', templateId: 'slay-the-beast', title: 'Slay or drive off the beast', description: 'For good.', scope: 'event', areaId: 'field', eventId: 'E1', eventName: 'Wolf den', location: 'B2:C3' },
]

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

const mapDetails = {
  map: component('map', 'Forest field + dungeon', { id: 'map-1', map: { seed: '42', needleId: 'n', threadId: 't', coatingId: 'c', width: 24, height: 20, eventCount: 6, generatorVersion: '0.5.0', createdByPlayerId: null, objectives: [], flags: { mapRequired: true, componentsMustMatch: false, unique: false } } }),
  needle: null,
  thread: null,
  coating: null,
  originalRealm: {
    realmId: 'realm-1',
    threat: 2,
    digest: {
      mobs: [{ archetypeId: 'wolf', name: 'Wolf', count: 4 }],
      loot: [{ areaName: 'Field', id: 'F1', name: 'Chest', location: 'C4' }],
      resourceNodes: [],
      settlements: [
        { areaName: 'Field', id: 'E2', name: 'Village', location: 'D2-F5', population: 'bustling' as const, wealth: 'prosperous' as const },
        { areaName: 'Field', id: 'E4', name: 'Hamlet', location: 'J8-K9', population: 'sparse' as const, wealth: 'poor' as const },
      ],
    },
  },
}

function routes() {
  return {
    'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
    'GET /api/players': [player],
    'GET /api/realms': { items: [summary, mirror], page: 1, pageSize: 25, totalCount: 2 },
    'GET /api/realms/:id': realm,
    'GET /api/realms/:id/completions': { completions: [], players: [] },
    'GET /api/maps/:id/details': mapDetails,
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

  it('shows a settlement’s population and the structures inside it', async () => {
    const village = area('field', 'E1', 'Village')
    village.events[0] = {
      ...village.events[0],
      templateId: 'village',
      category: 'settlement',
      population: 'bustling',
      structures: [
        { id: 'B1', templateId: 'hut-cluster', name: 'Hut cluster', footprint: { x: 1, y: 1, width: 2, height: 2 }, location: 'B2:C3' },
        { id: 'B2', templateId: 'training-pit', name: 'Training pit', footprint: { x: 4, y: 1, width: 3, height: 3 }, location: 'E2:G4', ruined: true },
      ],
    }
    fakeApi({ ...routes(), 'GET /api/realms/:id': { ...realm, document: { ...realm.document, areas: [village, realm.document.areas[1]] } } })
    renderApp('/realms/realm-1')

    expect(await screen.findByText('Bustling · 3× Grey wolf')).toBeInTheDocument()
    const structures = screen.getByRole('list', { name: 'Structures in E1' })
    expect(within(structures).getByText('B1 Hut cluster B2:C3')).toBeInTheDocument()
    expect(within(structures).getByText('B2 Training pit (ruined) E2:G4')).toBeInTheDocument()
  })

  it('switches floors and offers exports for the shown area', async () => {
    const api = fakeApi(routes())
    const user = userEvent.setup()
    renderApp('/realms/realm-1')

    await user.click(await screen.findByRole('tab', { name: 'Floor 1' }))

    expect(screen.getByRole('tab', { name: 'Floor 1' })).toHaveAttribute('aria-selected', 'true')
    const placements = screen.getByRole('complementary', { name: 'Placements' })
    expect(await within(placements).findByRole('button', { name: /E2 Guard post/ })).toBeInTheDocument()
    await waitFor(() => expect(api.callsTo('GET', '/api/realms/realm-1/export').at(-1)!.query.get('area')).toBe('floor-1'))
    expect(screen.getByRole('link', { name: 'Download SVG' })).toHaveAttribute('href', '/api/realms/realm-1/export?format=svg&area=floor-1')
    expect(screen.getByRole('link', { name: 'JSON' })).toHaveAttribute('href', '/api/realms/realm-1/export?format=json')
  })

  it('shows the image prompt in a window that copies and downloads it', async () => {
    const api = fakeApi({
      ...routes(),
      'GET /api/realms/:id/export': (call: ApiCall) =>
        call.query.get('format') === 'prompt'
          ? new Response(`Top-down battle map of ${call.query.get('area')}.`, { headers: { 'content-type': 'text/plain' } })
          : svgFor(call),
    })
    const user = userEvent.setup() // installs its own clipboard stub, so ours goes on afterwards
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })
    renderApp('/realms/realm-1')

    await user.click(await screen.findByRole('button', { name: 'Image prompt' }))

    const dialog = await screen.findByRole('dialog', { name: 'Image prompt' })
    expect(await within(dialog).findByText('Top-down battle map of field.')).toBeInTheDocument()
    expect(api.callsTo('GET', '/api/realms/realm-1/export').some((c) => c.query.get('format') === 'prompt')).toBe(true)
    expect(within(dialog).getByRole('link', { name: 'Download prompt' })).toHaveAttribute(
      'href',
      '/api/realms/realm-1/export?format=prompt&area=field',
    )

    await user.click(within(dialog).getByRole('button', { name: 'Copy to clipboard' }))
    expect(writeText).toHaveBeenCalledWith('Top-down battle map of field.')
    expect(within(dialog).getByRole('status')).toHaveTextContent('Copied to the clipboard.')

    await user.click(within(dialog).getByRole('button', { name: 'Close' }))
    expect(screen.queryByRole('dialog', { name: 'Image prompt' })).not.toBeInTheDocument()
  })

  it('says so when the prompt cannot be copied', async () => {
    fakeApi({
      ...routes(),
      'GET /api/realms/:id/export': (call: ApiCall) =>
        call.query.get('format') === 'prompt' ? new Response('A prompt.') : svgFor(call),
    })
    const user = userEvent.setup()
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: vi.fn().mockRejectedValue(new Error('denied')) },
      configurable: true,
    })
    renderApp('/realms/realm-1')

    await user.click(await screen.findByRole('button', { name: 'Image prompt' }))
    const dialog = await screen.findByRole('dialog', { name: 'Image prompt' })
    await within(dialog).findByText('A prompt.')
    await user.click(within(dialog).getByRole('button', { name: 'Copy to clipboard' }))

    expect(within(dialog).getByRole('status')).toHaveTextContent("Couldn't copy")
  })

  it('lists the objectives and shows an objective’s event on its floor', async () => {
    const api = fakeApi(routes())
    const user = userEvent.setup()
    renderApp('/realms/realm-1')

    const objectives = await screen.findByRole('region', { name: 'Objectives' })
    expect(within(objectives).getByText('Any of these can be completed, by any party, as often as the realm is run.')).toBeInTheDocument()
    expect(within(objectives).getByRole('button', { name: /Clear the realm Whole realm/ })).toBeInTheDocument()

    await user.click(within(objectives).getByRole('button', { name: /Take the post E2 Guard post · B2:C3/ }))

    expect(screen.getByRole('tab', { name: 'Floor 1' })).toHaveAttribute('aria-selected', 'true')
    await waitFor(() => expect(api.callsTo('GET', '/api/realms/realm-1/export').at(-1)!.query.get('area')).toBe('floor-1'))
    const svg = await screen.findByTestId('realm-svg')
    await waitFor(() => expect(svg.querySelector('[data-event-id="E2"]')).toHaveClass('rw-highlight'))
    expect(within(objectives).getByRole('button', { name: /^Take the post/ })).toHaveAttribute('aria-pressed', 'true')
  })

  it('lets a GM pin a candidate objective and unpin it', async () => {
    const pinned = { ...summary, pinnedObjective: candidates[2] }
    const api = fakeApi({
      ...routes(),
      'GET /api/realms/:id/objectives/candidates': candidates,
      'PUT /api/realms/:id/pinned-objective': pinned,
      'DELETE /api/realms/:id/pinned-objective': summary,
    })
    const user = userEvent.setup()
    renderApp('/realms/realm-1')

    const objectives = await screen.findByRole('region', { name: 'Objectives' })
    await user.click(within(objectives).getByRole('button', { name: 'Pin an objective (GM)…' }))
    const chooser = await within(objectives).findByRole('form', { name: 'Pin an objective' })
    await user.selectOptions(within(chooser).getByLabelText('Objective to pin'), 'field/E1/slay-the-beast')
    await user.click(within(chooser).getByRole('button', { name: 'Pin' }))

    expect(api.callsTo('PUT', '/api/realms/realm-1/pinned-objective')[0].body).toEqual({ key: 'field/E1/slay-the-beast' })
    expect(await within(objectives).findByText('GM pin')).toBeInTheDocument()
    expect(within(objectives).getByRole('button', { name: /^Slay or drive off the beast/ })).toBeInTheDocument()

    await user.click(within(objectives).getByRole('button', { name: 'Unpin' }))
    await waitFor(() => expect(within(objectives).queryByText('GM pin')).not.toBeInTheDocument())
    expect(api.callsTo('DELETE', '/api/realms/realm-1/pinned-objective')).toHaveLength(1)
  })

  it('notes that a mirror’s objectives may differ and explains realms without objectives', async () => {
    fakeApi({ ...routes(), 'GET /api/realms/:id': { summary: mirror, document: { ...realm.document, objectives: [] } } })
    renderApp('/realms/realm-2')

    const objectives = await screen.findByRole('region', { name: 'Objectives' })
    expect(within(objectives).getByText(/This is a mirror/)).toBeInTheDocument()
    expect(within(objectives).getByText(/woven before objectives existed/)).toBeInTheDocument()
  })

  it('lets the GM complete an objective for a party, again and again, and shows who completed it', async () => {
    const objective = realm.document.objectives![1]
    const friend = { ...player, id: 'p2', name: 'Friend' }
    const history = {
      completions: [
        {
          id: 'c1',
          realmId: summary.id,
          objective,
          completedAt: '2026-11-15T12:00:00Z',
          players: [
            { id: player.id, name: player.name, mapGiven: false },
            { id: friend.id, name: 'Friend', mapGiven: true },
          ],
          isFirst: true,
        },
      ],
      players: [
        { id: player.id, name: player.name, completions: 1, firstCompletedAt: '2026-11-15T12:00:00Z', inFirstCompletion: true },
        { id: friend.id, name: 'Friend', completions: 1, firstCompletedAt: '2026-11-15T12:00:00Z', inFirstCompletion: true },
      ],
    }
    let completed = false
    const api = fakeApi({
      ...routes(),
      'GET /api/players': [player, friend],
      'GET /api/realms/:id/completions': () => (completed ? history : { completions: [], players: [] }),
      'POST /api/realms/:id/completion': () => {
        completed = true
        return {
          completionId: 'c1',
          realmId: summary.id,
          objective,
          completedAt: '2026-11-15T12:00:00Z',
          isFirst: true,
          mapId: 'map-1',
          playerIds: [player.id, friend.id],
          mapGivenTo: [friend.id],
          skipped: [],
        }
      },
    })
    const user = userEvent.setup()
    renderApp('/realms/realm-1')

    const card = await screen.findByRole('region', { name: 'Completion' })
    expect(await within(card).findByText('No one has completed an objective here yet.')).toBeInTheDocument()
    // The weaver is ticked by default; the GM finds a friend by searching and adds them.
    expect(await within(card).findByRole('checkbox', { name: /Wanderer/ })).toBeChecked()
    await user.type(within(card).getByRole('searchbox', { name: 'Search players for players who completed it' }), 'Fri')
    await waitFor(() => expect(api.callsTo('GET', '/api/players').some((c) => c.query.get('search') === 'Fri')).toBe(true))
    await user.click(within(card).getByRole('checkbox', { name: 'Friend' }))
    await user.selectOptions(within(card).getByLabelText('Objective completed'), 'realm/clear-the-realm')
    await user.click(within(card).getByRole('button', { name: 'Complete objective' }))

    expect(api.callsTo('POST', '/api/realms/realm-1/completion')[0].body).toEqual({
      objectiveKey: 'realm/clear-the-realm',
      playerIds: [player.id, friend.id],
      mapRecipientIds: null,
    })
    const status = await within(card).findByRole('status')
    expect(status).toHaveTextContent('Clear the realm completed, and they’re the first to complete this realm.')
    expect(status).toHaveTextContent('Map handed to Friend.')

    // The form stays: another party can complete the realm later. The player list is collapsed under a toggle.
    expect(within(card).getByRole('button', { name: 'Complete objective' })).toBeInTheDocument()
    await user.click(await within(card).findByText(/Player completion \(2 players, 1 completion\)/))
    const completers = within(card).getByRole('list', { name: 'Players who completed an objective' })
    expect(within(completers).getAllByText('First to complete')).toHaveLength(2)
    expect(within(card).getByRole('list', { name: 'Completion history' })).toHaveTextContent('Friend (got the map)')
  })

  it('can complete without handing out the map', async () => {
    const api = fakeApi({
      ...routes(),
      'POST /api/realms/:id/completion': {
        completionId: 'c1',
        realmId: summary.id,
        objective: realm.document.objectives![0],
        completedAt: '2026-11-15T12:00:00Z',
        isFirst: false,
        mapId: 'map-1',
        playerIds: [player.id],
        mapGivenTo: [],
        skipped: [],
      },
    })
    const user = userEvent.setup()
    renderApp('/realms/realm-1')

    const card = await screen.findByRole('region', { name: 'Completion' })
    await within(card).findByRole('checkbox', { name: /Wanderer/ })
    await user.click(within(card).getByRole('checkbox', { name: 'Hand the map to players who don’t have it yet' }))
    await user.click(within(card).getByRole('button', { name: 'Complete objective' }))

    expect(api.callsTo('POST', '/api/realms/realm-1/completion')[0].body).toMatchObject({ playerIds: [player.id], mapRecipientIds: [] })
    expect(await within(card).findByRole('status')).toHaveTextContent('No one new received the map.')
  })

  it('opens the map details for the GM', async () => {
    fakeApi(routes())
    const user = userEvent.setup()
    renderApp('/realms/realm-1')

    await user.click(await screen.findByRole('button', { name: 'Map details' }))
    const dialog = await screen.findByRole('dialog', { name: 'Forest field + dungeon details' })
    expect(await within(dialog).findByText('4× Wolf')).toBeInTheDocument()
    expect(within(dialog).getByText('Map required')).toHaveAttribute('data-tip', expect.stringMatching(/only players holding this map/))
    expect(within(dialog).getByText('Chest')).toBeInTheDocument()
    expect(within(dialog).getByText('Field, C4')).toBeInTheDocument()

    // Each settlement with its population and wealth; nothing about how settlements were set up.
    const settlements = within(dialog).getByRole('list', { name: 'Settlements' })
    expect(within(dialog).getByRole('heading', { name: 'Settlements (2)' })).toBeInTheDocument()
    expect(within(settlements).getByText('Bustling · Prosperous')).toBeInTheDocument()
    expect(within(settlements).getByText('Sparse · Poor')).toBeInTheDocument()
    expect(within(dialog).queryByText(/Random population|structures/)).not.toBeInTheDocument()

    // This is the original realm, so there's no link back to itself.
    expect(within(dialog).queryByRole('link', { name: /Open the/ })).not.toBeInTheDocument()

    // The × closes it, like the Close button.
    await user.click(within(dialog).getByRole('button', { name: 'Close dialog' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('links a mirror’s map details to the realm it mirrors', async () => {
    fakeApi({ ...routes(), 'GET /api/realms/:id': { summary: mirror, document: realm.document } })
    const user = userEvent.setup()
    renderApp(`/realms/${mirror.id}`)

    await user.click(await screen.findByRole('button', { name: 'Map details' }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByRole('link', { name: 'Open the realm this mirrors' })).toHaveAttribute('href', '/realms/realm-1')
  })
})
