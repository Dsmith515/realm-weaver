import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { fakeApi } from '../test/fakeApi'
import { inventory, player } from '../test/fixtures'
import { renderApp } from '../test/render'

const routes = {
  'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
  'GET /api/players': [player],
  'GET /api/players/:id/inventory': inventory,
  'GET /api/components': { items: [], page: 1, pageSize: 50, totalCount: 0 },
}

const mainNav = () => screen.getByRole('navigation', { name: 'Main' })

describe('Main navigation', () => {
  it('opens a group as a flyout of described pages and closes it after navigating', async () => {
    fakeApi(routes)
    renderApp('/about')
    const user = userEvent.setup()

    const weave = within(mainNav()).getByRole('button', { name: 'Weave' })
    expect(weave).toHaveAttribute('aria-expanded', 'false')
    await user.click(weave)

    expect(weave).toHaveAttribute('aria-expanded', 'true')
    const flyout = within(mainNav()).getByRole('group', { name: 'Weave' })
    expect(within(flyout).getByText('Use a needle and spend a thread and coating to weave a realm.')).toBeInTheDocument()

    await user.click(within(flyout).getByRole('link', { name: /Ritual/ }))
    expect(screen.getByRole('heading', { level: 1, name: 'Ritual' })).toBeInTheDocument()
    expect(within(mainNav()).queryByRole('group', { name: 'Weave' })).not.toBeInTheDocument()
  })

  it('closes a flyout with Escape', async () => {
    fakeApi(routes)
    renderApp('/about')
    const user = userEvent.setup()

    await user.click(within(mainNav()).getByRole('button', { name: 'Codex' }))
    expect(within(mainNav()).getByRole('group', { name: 'Codex' })).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(within(mainNav()).queryByRole('group', { name: 'Codex' })).not.toBeInTheDocument()
  })

  it('pins the rail open with the current group expanded, and remembers it', async () => {
    fakeApi(routes)
    renderApp('/weave/ritual')
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Pin navigation open' }))

    expect(screen.getByRole('button', { name: 'Collapse navigation' })).toHaveAttribute('aria-pressed', 'true')
    expect(within(mainNav()).getByRole('button', { name: 'Weave' })).toHaveAttribute('aria-expanded', 'true')
    expect(within(mainNav()).getByRole('link', { name: 'Ritual' })).toHaveClass('active')
    expect(localStorage.getItem('realm-weaver.rail')).toBe('pinned')
  })
})
