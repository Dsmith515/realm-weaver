import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { fakeApi, problem } from '../test/fakeApi'
import { inventory, moss, owned, player, wax } from '../test/fixtures'
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

describe('Player page', () => {
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
