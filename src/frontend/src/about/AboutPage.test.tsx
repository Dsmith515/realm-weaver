import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { fakeApi } from '../test/fakeApi'
import { player } from '../test/fixtures'
import { renderApp } from '../test/render'

describe('About page', () => {
  it('is linked from the main navigation and explains the project', async () => {
    fakeApi({ 'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' }, 'GET /api/players': [player] })
    const user = userEvent.setup()
    renderApp('/ritual')

    await user.click(screen.getByRole('link', { name: 'About' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'About Realm Weaver' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'How it works' })).toBeInTheDocument()
  })
})
