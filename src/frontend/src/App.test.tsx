import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { App } from './App'

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('App shell', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('redirects the root to the ritual page and shows navigation', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ name: 'Realm Weaver', version: '1.0.0' }))),
    )
    renderAt('/')

    expect(screen.getByRole('heading', { name: 'Ritual' })).toBeInTheDocument()
    for (const label of ['Ritual', 'Player', 'Realms', 'Components']) {
      expect(screen.getByRole('link', { name: label })).toBeInTheDocument()
    }
    expect(await screen.findByText('API: 1.0.0')).toBeInTheDocument()
  })

  it('shows the API as offline when the request fails', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network down')))
    renderAt('/components')

    expect(screen.getByRole('heading', { name: 'Components' })).toBeInTheDocument()
    expect(await screen.findByText('API: offline')).toBeInTheDocument()
  })
})
