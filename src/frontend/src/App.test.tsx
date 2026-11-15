import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { fakeApi, json } from './test/fakeApi'
import { inventory, otherPlayer, player, preview } from './test/fixtures'
import { renderApp } from './test/render'

const shellRoutes = {
  'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
  'GET /api/players': [player, otherPlayer],
  'GET /api/players/:id/inventory': inventory,
  'POST /api/rituals/preview': preview({ canPerform: false }),
  'GET /api/components': { items: [], page: 1, pageSize: 50, totalCount: 0 },
}

describe('App shell', () => {
  it('opens on the GM Quick Weave page, with GM links first and the player pages as a preview', async () => {
    fakeApi(shellRoutes)
    renderApp('/')

    expect(screen.getByRole('heading', { name: 'Quick Weave' })).toBeInTheDocument()
    const gm = screen.getByRole('group', { name: 'GM' })
    for (const label of ['Quick Weave', 'Realms', 'Components']) {
      expect(within(gm).getByRole('link', { name: label })).toBeInTheDocument()
    }
    const players = screen.getByRole('group', { name: 'Player preview' })
    for (const label of ['Ritual', 'Player']) {
      expect(within(players).getByRole('link', { name: label })).toBeInTheDocument()
    }
    expect(await screen.findByText('API: 1.0.0')).toBeInTheDocument()
  })

  it('shows the API as offline when requests fail', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network down')))
    renderApp('/components')

    expect(screen.getByRole('heading', { name: 'Components' })).toBeInTheDocument()
    expect(await screen.findByText('API: offline')).toBeInTheDocument()
  })

  it('selects the first player, remembers a switch, and creates players', async () => {
    const api = fakeApi({
      ...shellRoutes,
      'POST /api/players': () => json({ id: 'p3', name: 'Newcomer', createdAt: '2026-11-12T12:00:00Z' }, 201),
    })
    renderApp('/player')
    const user = userEvent.setup()

    const select = await screen.findByRole('combobox', { name: 'Player' })
    await screen.findByRole('option', { name: 'Tester' })
    expect(select).toHaveValue('p1')

    await user.selectOptions(select, 'p2')
    expect(localStorage.getItem('realm-weaver.player')).toBe('p2')

    await user.click(screen.getByRole('button', { name: 'New player' }))
    await user.type(screen.getByRole('textbox', { name: 'New player name' }), 'Newcomer')
    await user.click(screen.getByRole('button', { name: 'Create' }))

    expect(api.callsTo('POST', '/api/players')[0].body).toEqual({ name: 'Newcomer' })
    expect(localStorage.getItem('realm-weaver.player')).toBe('p3')
    expect(within(screen.getByRole('banner')).queryByRole('textbox')).not.toBeInTheDocument()
  })
})
