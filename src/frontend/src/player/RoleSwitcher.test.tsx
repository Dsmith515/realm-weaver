import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { fakeApi } from '../test/fakeApi'
import { inventory, otherPlayer, player } from '../test/fixtures'
import { renderApp } from '../test/render'

const routes = {
  'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
  'GET /api/players': [player, otherPlayer],
  'GET /api/players/:id/inventory': inventory,
  'GET /api/components': { items: [], page: 1, pageSize: 50, totalCount: 0 },
}

const viewingAs = () => screen.getByRole('group', { name: 'Viewing as' })
const mainNav = () => screen.getByRole('navigation', { name: 'Main' })

describe('Viewing as GM or Player', () => {
  it('starts as GM, with the GM pages in the menu and no player picker', async () => {
    fakeApi(routes)
    renderApp('/about')
    const user = userEvent.setup()

    expect(within(viewingAs()).getByRole('button', { name: 'GM' })).toHaveAttribute('aria-pressed', 'true')
    await user.click(within(mainNav()).getByRole('button', { name: 'Weave' }))
    expect(within(mainNav()).getByRole('link', { name: /Quick Weave/ })).toBeInTheDocument()
    await user.click(within(mainNav()).getByRole('button', { name: 'Codex' }))
    expect(within(mainNav()).getByRole('link', { name: /Components/ })).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Player' })).not.toBeInTheDocument()
  })

  it('switching to Player drops the GM-only pages, shows the player picker and is remembered', async () => {
    fakeApi(routes)
    renderApp('/about')
    const user = userEvent.setup()

    await user.click(within(viewingAs()).getByRole('button', { name: 'Player' }))

    expect(within(viewingAs()).getByRole('button', { name: 'Player' })).toHaveAttribute('aria-pressed', 'true')
    // Codex only holds Components so far, so the whole group goes; Weave keeps Ritual.
    expect(within(mainNav()).queryByRole('button', { name: 'Codex' })).not.toBeInTheDocument()
    await user.click(within(mainNav()).getByRole('button', { name: 'Weave' }))
    expect(within(mainNav()).queryByRole('link', { name: /Quick Weave/ })).not.toBeInTheDocument()
    expect(within(mainNav()).getByRole('link', { name: /Ritual/ })).toBeInTheDocument()
    expect(within(mainNav()).getByRole('link', { name: 'Realms' })).toBeInTheDocument()
    expect(await screen.findByRole('combobox', { name: 'Player' })).toBeInTheDocument()
    expect(localStorage.getItem('realm-weaver.role')).toBe('player')
  })

  it('a GM-only page explains itself in player view and switches back to GM', async () => {
    fakeApi(routes)
    localStorage.setItem('realm-weaver.role', 'player')
    renderApp('/weave/quick')
    const user = userEvent.setup()

    expect(screen.getByText("This page is for the GM. Players don't see it.")).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Roll' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Switch to GM view' }))

    expect(screen.getByRole('button', { name: 'Roll' })).toBeInTheDocument()
    expect(localStorage.getItem('realm-weaver.role')).toBe('gm')
  })

  it('ignores an unknown saved role', () => {
    fakeApi(routes)
    localStorage.setItem('realm-weaver.role', 'dragon')
    renderApp('/about')

    expect(within(viewingAs()).getByRole('button', { name: 'GM' })).toHaveAttribute('aria-pressed', 'true')
  })
})
