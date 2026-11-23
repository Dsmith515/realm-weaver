import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { fakeApi } from '../test/fakeApi'
import { player } from '../test/fixtures'
import { renderApp } from '../test/render'

const routes = {
  'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
  'GET /api/players': [player],
}

describe('Landing page', () => {
  it('greets the GM with Quick Weave, the realms and how it works', () => {
    fakeApi(routes)
    renderApp('/')

    const page = within(screen.getByRole('main'))
    expect(page.getByRole('heading', { level: 1, name: 'Run a one-shot tonight.' })).toBeInTheDocument()
    expect(page.getByRole('img', { name: 'Realm Weaver' })).toBeInTheDocument()
    expect(page.getByRole('link', { name: 'Quick Weave' })).toHaveAttribute('href', '/weave/quick')
    expect(page.getByRole('link', { name: 'Browse realms' })).toHaveAttribute('href', '/realms')
    expect(page.getByRole('link', { name: 'How it works' })).toHaveAttribute('href', '#how-it-works')
  })

  it('offers a player the ritual instead of Quick Weave', async () => {
    fakeApi(routes)
    const user = userEvent.setup()
    renderApp('/')

    await user.click(within(screen.getByRole('group', { name: 'Viewing as' })).getByRole('button', { name: 'Player' }))

    const page = within(screen.getByRole('main'))
    expect(page.getByRole('link', { name: 'Weave a ritual' })).toHaveAttribute('href', '/weave/ritual')
    expect(page.queryByRole('link', { name: 'Quick Weave' })).not.toBeInTheDocument()
  })
})