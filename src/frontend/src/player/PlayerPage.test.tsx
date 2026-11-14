import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { fakeApi, problem } from '../test/fakeApi'
import type { Component } from '../api/types'
import { forestMap, inventory, moss, needle, owned, player, wax } from '../test/fixtures'
import { renderApp } from '../test/render'

function routes(selfAssign: boolean, overrides: Record<string, unknown> = {}) {
  return {
    'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
    'GET /api/players': [player],
    'GET /api/players/:id/inventory': inventory,
    'GET /api/settings': { playersCanSelfAssign: selfAssign },
    'GET /api/components': { items: [moss, wax], page: 1, pageSize: 200, totalCount: 2 },
    ...overrides,
  }
}

const recordedMap = {
  ...forestMap,
  map: { ...forestMap.map!, recipe: { kind: 'fieldWithDungeon', floorCount: 2, biome: 'forest', element: 'earth', mobTags: ['beast'], difficulty: 3 }, threat: 3 },
} as Component

const mapDetails = {
  map: recordedMap,
  needle,
  thread: moss,
  coating: wax,
  originalRealm: {
    realmId: 'realm-1',
    threat: 3,
    digest: {
      mobs: [{ archetypeId: 'grey-wolf', name: 'Grey wolf', count: 5 }],
      loot: [{ areaName: 'Floor 1', id: 'C1', name: 'Hidden chest (rare)', location: 'D4' }],
      resourceNodes: [{ areaName: 'Forest field', id: 'E3', name: 'Resource node', location: 'B2-B3' }],
    },
  },
}

describe('Player page', () => {
  it('shows a map’s realm kind and threat, and its details when opened', async () => {
    const withMap = inventory.map((i) => (i.component.id === forestMap.id ? owned(recordedMap) : i))
    const api = fakeApi(
      routes(true, {
        'GET /api/players/:id/inventory': withMap,
        'GET /api/maps/:id/details': mapDetails,
        'PUT /api/players/:id/inventory/:componentId/notes': { ...owned(recordedMap), notes: 'Wolves by the river.' },
      }),
    )
    const user = userEvent.setup()
    renderApp('/player')

    const maps = await screen.findByRole('region', { name: 'Maps' })
    expect(within(maps).getByText('Field + Dungeon · Threat 3')).toBeInTheDocument()
    expect(within(maps).queryByText(/seed/i)).not.toBeInTheDocument()

    await user.click(within(maps).getByRole('button', { name: `Open ${forestMap.name}` }))
    const dialog = screen.getByRole('dialog', { name: forestMap.name })
    expect(await within(dialog).findByText('18446744073709551000')).toBeInTheDocument()
    expect(within(dialog).getByText('Field + Dungeon (2 floors)')).toBeInTheDocument()
    expect(within(dialog).getByText('5× Grey wolf')).toBeInTheDocument()
    expect(within(dialog).getByText('Hidden chest (rare)')).toBeInTheDocument()
    expect(within(dialog).getByText('Resource node')).toBeInTheDocument()
    expect(within(dialog).getByRole('link', { name: 'Open the original realm' })).toHaveAttribute('href', '/realms/realm-1')

    await user.type(within(dialog).getByRole('textbox', { name: 'Your notes' }), 'Wolves by the river.')
    await user.click(within(dialog).getByRole('button', { name: 'Save notes' }))
    await waitFor(() =>
      expect(api.callsTo('PUT', `/api/players/${player.id}/inventory/${forestMap.id}/notes`)[0]?.body).toEqual({ notes: 'Wolves by the river.' }),
    )
  })

  it('groups the inventory by type with total quantities', async () => {
    fakeApi(routes(true))
    renderApp('/player')

    const threads = await screen.findByRole('region', { name: 'Threads' })
    expect(within(threads).getByText('Mossgreen Thread')).toBeInTheDocument()
    expect(within(threads).getByLabelText('Quantity 3')).toBeInTheDocument()
    expect(within(threads).getByText('(3)')).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Maps' })).getByText('Forest field map 1A2B3C')).toBeInTheDocument()
  })

  it('adds an assignable component when self-assign is on', async () => {
    const api = fakeApi(routes(true, { 'POST /api/players/:id/inventory': owned(moss, 5) }))
    const user = userEvent.setup()
    renderApp('/player')

    const quantity = await screen.findByRole('spinbutton', { name: 'Quantity of Mossgreen Thread' })
    await user.clear(quantity)
    await user.type(quantity, '2')
    await user.click(screen.getByRole('button', { name: 'Add Mossgreen Thread' }))

    await waitFor(() => expect(api.callsTo('POST', `/api/players/${player.id}/inventory`)[0]?.body).toEqual({ componentId: moss.id, quantity: 2 }))
  })

  it('disables adding when an admin has turned self-assign off', async () => {
    fakeApi(routes(false))
    renderApp('/player')

    expect(await screen.findByText('An admin has turned off adding components yourself.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Add Beastward Wax' })).toBeDisabled()
  })

  it('shows the reason when the server refuses', async () => {
    fakeApi(routes(true, { 'POST /api/players/:id/inventory': problem(403, "This component can't be self-assigned.") }))
    const user = userEvent.setup()
    renderApp('/player')

    await user.click(await screen.findByRole('button', { name: 'Add Beastward Wax' }))

    expect(await screen.findByRole('alert')).toHaveTextContent("This component can't be self-assigned.")
  })
})
