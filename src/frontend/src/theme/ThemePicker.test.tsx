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
  it('starts on Gilded Arcana and applies it to the page', async () => {
    fakeApi(routes)
    renderApp('/about')

    expect(await screen.findByRole('combobox', { name: 'Theme' })).toHaveValue('arcana')
    expect(document.documentElement.dataset.theme).toBe('arcana')
  })

  it('switches the palette and remembers the choice', async () => {
    fakeApi(routes)
    renderApp('/about')
    const user = userEvent.setup()

    await user.selectOptions(screen.getByRole('combobox', { name: 'Theme' }), screen.getByRole('option', { name: 'Ink & Ember' }))

    expect(document.documentElement.dataset.theme).toBe('ember')
    expect(localStorage.getItem('realm-weaver.theme')).toBe('ember')
  })

  it('restores a saved palette and ignores an unknown one', () => {
    fakeApi(routes)
    localStorage.setItem('realm-weaver.theme', 'parchment')
    const { unmount } = renderApp('/about')
    expect(screen.getByRole('combobox', { name: 'Theme' })).toHaveValue('parchment')
    unmount()

    localStorage.setItem('realm-weaver.theme', 'not-a-theme')
    renderApp('/about')
    expect(screen.getByRole('combobox', { name: 'Theme' })).toHaveValue('arcana')
  })
})
