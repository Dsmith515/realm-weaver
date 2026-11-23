import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { fakeApi } from '../test/fakeApi'
import { renderApp } from '../test/render'

const routes = {
  'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
  'GET /api/players': [],
}

describe('Theme picker', () => {
  it('starts on Ink & Ember and applies it to the page', async () => {
    fakeApi(routes)
    renderApp('/about')

    expect(await screen.findByRole('radio', { name: 'Ink & Ember', hidden: true })).toBeChecked()
    expect(document.documentElement.dataset.theme).toBe('ember')
  })

  it('switches the palette and remembers the choice', async () => {
    fakeApi(routes)
    renderApp('/about')
    const user = userEvent.setup()

    await user.click(screen.getByRole('radio', { name: 'Gilded Arcana', hidden: true }))

    expect(document.documentElement.dataset.theme).toBe('arcana')
    expect(localStorage.getItem('realm-weaver.theme')).toBe('arcana')
  })

  it('restores a saved palette and ignores an unknown one', () => {
    fakeApi(routes)
    localStorage.setItem('realm-weaver.theme', 'parchment')
    const { unmount } = renderApp('/about')
    expect(screen.getByRole('radio', { name: 'Parchment (light)', hidden: true })).toBeChecked()
    unmount()

    localStorage.setItem('realm-weaver.theme', 'not-a-theme')
    renderApp('/about')
    expect(screen.getByRole('radio', { name: 'Ink & Ember', hidden: true })).toBeChecked()
  })
})
