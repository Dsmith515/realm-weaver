import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ComponentInput } from '../api/types'
import { fakeApi, json, problem } from '../test/fakeApi'
import { moss, needle, otherPlayer, owned, player, wax } from '../test/fixtures'
import { renderApp } from '../test/render'

function routes(overrides: Record<string, unknown> = {}) {
  return {
    'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
    'GET /api/players': [player, otherPlayer],
    'GET /api/settings': { playersCanSelfAssign: true },
    'GET /api/components': { items: [needle, moss, wax], page: 1, pageSize: 25, totalCount: 3 },
    ...overrides,
  }
}

describe('Components page', () => {
  it('lists components and sends filters to the API', async () => {
    const api = fakeApi(routes())
    const user = userEvent.setup()
    renderApp('/components')

    expect(await screen.findByRole('cell', { name: 'Mossgreen Thread' })).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: 'Forest · Earth' })).toBeInTheDocument()

    await user.selectOptions(screen.getByRole('combobox', { name: 'Type' }), 'thread')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Biome' }), 'forest')

    await waitFor(() => {
      const query = api.callsTo('GET', '/api/components').at(-1)!.query
      expect(query.get('type')).toBe('thread')
      expect(query.get('biome')).toBe('forest')
      expect(query.get('page')).toBe('1')
    })
  })

  it('shows the server validation errors next to the fields', async () => {
    const api = fakeApi(
      routes({
        'POST /api/components': problem(400, 'One or more fields are invalid.', {
          name: ['A name is required.'],
          'needle.difficultyTier': ['Difficulty tier must be between 1 and 10.'],
        }),
      }),
    )
    const user = userEvent.setup()
    renderApp('/components')

    await user.click(await screen.findByRole('button', { name: 'New component' }))
    const form = screen.getByRole('form', { name: 'New component' })
    const tier = within(form).getByRole('spinbutton', { name: 'Difficulty tier' })
    await user.clear(tier)
    await user.type(tier, '11')
    await user.type(within(form).getByRole('textbox', { name: 'Abilities (comma separated)' }), 'Spark, Hum')
    await user.click(within(form).getByRole('button', { name: 'Create' }))

    expect(await within(form).findByText('A name is required.')).toBeInTheDocument()
    expect(within(form).getByText('Difficulty tier must be between 1 and 10.')).toBeInTheDocument()
    const sent = api.callsTo('POST', '/api/components')[0].body as ComponentInput
    expect(sent.needle).toEqual({ difficultyTier: 11, category: '', abilities: ['Spark', 'Hum'] })
    expect(sent.thread).toBeNull()
  })

  it('switching the type swaps the details section', async () => {
    const api = fakeApi(routes({ 'POST /api/components': () => json(wax, 201) }))
    const user = userEvent.setup()
    renderApp('/components')

    await user.click(await screen.findByRole('button', { name: 'New component' }))
    const form = screen.getByRole('form', { name: 'New component' })
    await user.selectOptions(within(form).getByRole('combobox', { name: 'Type' }), 'coating')
    await user.type(within(form).getByRole('textbox', { name: 'Name' }), 'Test Wax')
    await user.type(within(form).getByRole('textbox', { name: 'Mob types (comma separated)' }), 'beast, undead')
    await user.selectOptions(within(form).getByRole('combobox', { name: 'Realm kind' }), 'dungeon')
    await user.click(within(form).getByRole('button', { name: 'Create' }))

    await waitFor(() => expect(screen.queryByRole('form', { name: 'New component' })).not.toBeInTheDocument())
    expect(api.callsTo('POST', '/api/components')[0].body).toMatchObject({
      type: 'coating',
      name: 'Test Wax',
      needle: null,
      coating: { mobTypes: ['beast', 'undead'], realmKind: 'dungeon', floorCount: 1 },
    })
  })

  it('assigns a component to a chosen player', async () => {
    const api = fakeApi(routes({ 'POST /api/components/:id/assign': owned(moss, 4) }))
    const user = userEvent.setup()
    renderApp('/components')

    await user.click(await screen.findByRole('button', { name: 'Assign Mossgreen Thread' }))
    const form = screen.getByRole('form', { name: 'Assign Mossgreen Thread' })
    await user.selectOptions(within(form).getByRole('combobox', { name: 'Player' }), otherPlayer.id)
    await user.clear(within(form).getByRole('spinbutton', { name: 'Quantity' }))
    await user.type(within(form).getByRole('spinbutton', { name: 'Quantity' }), '4')
    await user.click(within(form).getByRole('button', { name: 'Assign' }))

    await waitFor(() => expect(api.callsTo('POST', `/api/components/${moss.id}/assign`)[0]?.body).toEqual({ playerId: otherPlayer.id, quantity: 4 }))
  })

  it('archives after confirming, and toggles self-assign', async () => {
    const api = fakeApi(routes({ 'DELETE /api/components/:id': () => json(null, 204), 'PUT /api/settings': { playersCanSelfAssign: false } }))
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const user = userEvent.setup()
    renderApp('/components')

    await user.click(await screen.findByRole('button', { name: 'Archive Beastward Wax' }))
    await user.click(screen.getByRole('checkbox', { name: /Players can add assignable components/ }))

    await waitFor(() => expect(api.callsTo('DELETE', `/api/components/${wax.id}`)).toHaveLength(1))
    await waitFor(() => expect(api.callsTo('PUT', '/api/settings')[0]?.body).toEqual({ playersCanSelfAssign: false }))
    expect(screen.getByRole('checkbox', { name: /Players can add assignable components/ })).not.toBeChecked()
  })
})
