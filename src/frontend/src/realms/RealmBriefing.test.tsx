import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { RealmBriefing } from '../api/types'
import { fakeApi } from '../test/fakeApi'
import { player } from '../test/fixtures'
import { renderApp } from '../test/render'

/** What the API sends a player who doesn't hold the realm's map: no areas, no finds, objectives without locations. */
const briefing: RealmBriefing = {
  realmId: 'realm-1',
  mapName: 'Mossgreen Forest Field and Dungeon Map',
  kind: 'fieldWithDungeon',
  biome: 'forest',
  element: 'earth',
  threat: 2,
  dangerModifier: 1,
  floorCount: 1,
  isMirror: false,
  wovenFrom: ["Beginner's Needle", 'Mossgreen Thread', 'Beastward Wax'],
  completionCount: 0,
  objectives: [
    { id: 'P', title: 'Slay or drive off the beast', hint: 'Deal with the lair’s occupant for good.', isPinned: true },
    { id: 'O1', title: 'Take the outpost', hint: 'Drive out the garrison and hold the walls.', isPinned: false },
  ],
  holdsMap: false,
  finds: null,
  areas: null,
}

const summary = {
  id: 'realm-1',
  playerId: player.id,
  mapComponentId: 'map-1',
  kind: 'field',
  biome: 'forest',
  width: 24,
  height: 20,
  floorCount: 0,
  threat: 2,
  dangerModifier: 0,
  encounterVariant: 0,
  seed: '42',
  generatorVersion: '0.9.0',
  createdAt: '2026-11-12T12:00:00Z',
  completionCount: 0,
  areas: [{ id: 'field', kind: 'field', name: 'Forest field', floorNumber: 0 }],
}

function routes(overrides: Record<string, unknown> = {}) {
  return {
    'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
    'GET /api/players': [player],
    'GET /api/realms': { items: [summary], page: 1, pageSize: 25, totalCount: 1 },
    'GET /api/realms/:id/briefing': briefing,
    'GET /api/realms/:id/completions': { completions: [], players: [] },
    ...overrides,
  }
}

const viewAsPlayer = () => localStorage.setItem('realm-weaver.role', 'player')

describe('Player realm briefing', () => {
  it('shows what the realm is and what to aim for, with no map, locations or GM controls', async () => {
    viewAsPlayer()
    const api = fakeApi(routes())
    renderApp('/realms/realm-1')

    const page = await screen.findByRole('region', { name: 'Realm briefing' })
    expect(within(page).getByRole('heading', { level: 1 })).toHaveTextContent('Mossgreen Forest Field and Dungeon Map')
    expect(within(page).getByText("Beginner's Needle · Mossgreen Thread · Beastward Wax")).toBeInTheDocument()
    expect(within(page).getByText('Field + Dungeon (1 floor)')).toBeInTheDocument()
    expect(within(page).getByText('2 (danger +1)')).toBeInTheDocument()
    const objectives = within(page).getByRole('region', { name: 'Briefing objectives' })
    expect(within(objectives).getAllByRole('listitem')).toHaveLength(2)
    expect(within(objectives).getByText('GM pick')).toBeInTheDocument()
    expect(within(page).getByText(/You don’t hold this realm’s map/)).toBeInTheDocument()

    // No map, no placements, no lists of what's where, and nothing the GM uses.
    expect(document.querySelector('.svg-viewer')).toBeNull()
    expect(screen.queryByRole('complementary', { name: 'Placements' })).not.toBeInTheDocument()
    for (const list of ['Creatures', 'Mobs', 'Potential loot', 'Settlements']) {
      expect(screen.queryByRole('list', { name: list })).not.toBeInTheDocument()
    }
    for (const control of ['Map details', 'Image prompt', 'Complete objective', /Pin (a|an) .*objective/]) {
      expect(within(page).queryByRole('button', { name: control })).not.toBeInTheDocument()
    }
    expect(within(page).queryByRole('link', { name: 'Download SVG' })).not.toBeInTheDocument()

    // The page never fetches the GM's document or the map image, so there's nothing to leak.
    expect(api.callsTo('GET', '/api/realms/realm-1')).toHaveLength(0)
    expect(api.callsTo('GET', '/api/realms/realm-1/export')).toHaveLength(0)
    await waitFor(() => expect(api.callsTo('GET', '/api/realms/realm-1/briefing').at(-1)?.query.get('playerId')).toBe(player.id))
  })

  // Maintainer's piece (Milestone 8i step 4): see the guide in the PR description.
  it.todo('a player who holds the map sees the map and the kinds of creatures')

  it('keeps the GM realm page for the GM', async () => {
    const api = fakeApi(
      routes({
        'GET /api/realms/:id': { summary, document: { generatorVersion: '0.9.0', seed: '42', threat: 2, request: {}, theme: null, areas: [], objectives: [] } },
      }),
    )
    renderApp('/realms/realm-1')

    expect(await screen.findByRole('button', { name: 'Map details' })).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Realm briefing' })).not.toBeInTheDocument()
    expect(api.callsTo('GET', '/api/realms/realm-1/briefing')).toHaveLength(0)
  })

  it('lists only the player’s own realms in player view, with no GM tab', async () => {
    viewAsPlayer()
    const api = fakeApi(routes())
    renderApp('/realms')

    expect(await screen.findByRole('link', { name: 'Forest field' })).toHaveAttribute('href', '/realms/realm-1')
    expect(screen.queryByRole('tab', { name: 'GM Quick Weaves' })).not.toBeInTheDocument()
    expect(api.callsTo('GET', '/api/realms')[0].query.get('playerId')).toBe(player.id)
  })
})
