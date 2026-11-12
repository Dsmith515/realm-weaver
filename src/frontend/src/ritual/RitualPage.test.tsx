import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { RitualPreviewRequest } from '../api/types'
import { fakeApi, json, problem, type ApiCall } from '../test/fakeApi'
import { forestMap, inventory, moss, needle, player, preview, slot, wax, wheat } from '../test/fixtures'
import { renderApp } from '../test/render'

const byId = Object.fromEntries(inventory.map((i) => [i.component.id, i.component]))

/** A fake preview that fills exactly the slots that were sent. */
function echoPreview(call: ApiCall) {
  const request = call.body as RitualPreviewRequest
  const pick = (id: string | null | undefined) => (id ? slot(byId[id]) : null)
  const filled = { needle: pick(request.needleId), thread: pick(request.threadId), coating: pick(request.coatingId), map: pick(request.mapId) }
  return preview({ ...filled, canPerform: !!(filled.needle && filled.thread && filled.coating), createsNewMap: !filled.map })
}

function routes(overrides: Record<string, unknown> = {}) {
  return {
    'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
    'GET /api/players': [player],
    'GET /api/players/:id/inventory': inventory,
    'POST /api/rituals/preview': echoPreview,
    ...overrides,
  }
}

const slotRegion = (name: string) => screen.findByRole('region', { name: `${name} slot` })
const lastPreview = (api: ReturnType<typeof fakeApi>) => api.callsTo('POST', '/api/rituals/preview').at(-1)?.body as RitualPreviewRequest

async function pick(user: ReturnType<typeof userEvent.setup>, name: string) {
  const panel = await screen.findByRole('complementary', { name: 'Inventory' })
  await user.click(await within(panel).findByRole('button', { name: new RegExp(name) }))
}

describe('Ritual page', () => {
  it('fills slots from clicked inventory items and previews them', async () => {
    const api = fakeApi(routes())
    const user = userEvent.setup()
    renderApp('/ritual')

    await pick(user, needle.name)
    await pick(user, moss.name)
    await pick(user, wax.name)

    await waitFor(() => expect(lastPreview(api)).toMatchObject({ playerId: player.id, needleId: needle.id, threadId: moss.id, coatingId: wax.id, mapId: null }))
    expect(await within(await slotRegion('Thread')).findByText(moss.name)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Perform ritual' })).toBeEnabled()
    expect(screen.getByText('Creates a new map.')).toBeInTheDocument()
  })

  it('hides used-up consumables from the inventory panel', async () => {
    fakeApi(routes())
    renderApp('/ritual')

    const panel = await screen.findByRole('complementary', { name: 'Inventory' })
    await within(panel).findByRole('button', { name: new RegExp(moss.name) })
    expect(within(panel).queryByRole('button', { name: new RegExp(wheat.name) })).not.toBeInTheDocument()
  })

  it('shows slots filled from a map, and warns about a mismatch', async () => {
    fakeApi(
      routes({
        'POST /api/rituals/preview': preview({
          needle: slot(needle, 'fromMap'),
          thread: slot(wheat, 'selected', 2),
          coating: slot(wax, 'fromMap', 2),
          map: slot(forestMap, 'selected'),
          isMismatch: true,
          mismatchedSlots: ['thread'],
          dangerModifier: 1,
          createsNewMap: false,
        }),
      }),
    )
    renderApp('/ritual')

    expect(await within(await slotRegion('Needle')).findByText('From map')).toBeInTheDocument()
    expect(within(await slotRegion('Thread')).getByText('Mismatch')).toBeInTheDocument()
    expect(screen.getByText(/Mismatched map\./).parentElement).toHaveTextContent('The thread differs from the map\'s. Same layout, rerolled encounters, danger +1.')
    expect(screen.getByText('Reuses a map you own.')).toBeInTheDocument()
  })

  it('lists what stops the ritual and disables Perform', async () => {
    fakeApi(
      routes({
        'POST /api/rituals/preview': preview({
          canPerform: false,
          thread: slot(moss, 'selected', 0),
          issues: [
            { slot: 'thread', field: 'threadId', code: 'not-owned', message: "You don't have any Mossgreen Thread." },
            { slot: 'coating', field: 'coatingId', code: 'missing', message: 'Choose a Coating.' },
          ],
        }),
      }),
    )
    renderApp('/ritual')

    expect(await within(await slotRegion('Thread')).findByText("You don't have any Mossgreen Thread.")).toBeInTheDocument()
    expect(within(await slotRegion('Coating')).queryByText('Choose a Coating.')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Perform ritual' })).toBeDisabled()
  })

  it('Random keeps the components the server picked', async () => {
    const api = fakeApi(
      routes({
        'POST /api/rituals/preview': (call: ApiCall) =>
          (call.body as RitualPreviewRequest).randomize
            ? preview({ needle: slot(needle, 'random'), thread: slot(moss, 'random'), coating: slot(wax, 'random') })
            : echoPreview(call),
      }),
    )
    const user = userEvent.setup()
    renderApp('/ritual')

    await user.click(await screen.findByRole('button', { name: 'Random' }))

    await waitFor(() => expect(lastPreview(api)).toMatchObject({ needleId: needle.id, threadId: moss.id, coatingId: wax.id, randomize: false }))
  })

  it('performs with an idempotency key, reuses it on retry, and links to the realm', async () => {
    let attempts = 0
    const api = fakeApi(
      routes({
        'POST /api/rituals': () =>
          ++attempts === 1
            ? problem(500, 'Something broke')
            : json(
                {
                  ritualId: 'r1', playerId: player.id, realmId: 'realm-1', mapId: 'map-new', mapCreated: true,
                  needleId: needle.id, threadId: moss.id, coatingId: wax.id, isMismatch: false, dangerModifier: 0, encounterVariant: 0, createdAt: '2026-11-12T12:00:00Z',
                },
                201,
              ),
      }),
    )
    const user = userEvent.setup()
    renderApp('/ritual')
    await pick(user, needle.name)
    await pick(user, moss.name)
    await pick(user, wax.name)
    const perform = screen.getByRole('button', { name: 'Perform ritual' })
    await waitFor(() => expect(perform).toBeEnabled())

    await user.click(perform)
    expect(await screen.findByRole('alert')).toHaveTextContent('Something broke')
    await user.click(screen.getByRole('button', { name: 'Perform ritual' }))

    expect(await screen.findByRole('link', { name: 'View the realm' })).toHaveAttribute('href', '/realms/realm-1')
    const [first, retry] = api.callsTo('POST', '/api/rituals')
    const key = first.headers.get('Idempotency-Key')
    expect(key).toBeTruthy()
    expect(retry.headers.get('Idempotency-Key')).toBe(key)
    expect(first.body).toMatchObject({ needleId: needle.id, threadId: moss.id, coatingId: wax.id })
  })

  it('explains a lost race and reloads the inventory', async () => {
    const api = fakeApi(routes({ 'POST /api/rituals': problem(409, 'The ritual lost a race for your components.') }))
    const user = userEvent.setup()
    renderApp('/ritual')
    await pick(user, needle.name)
    await pick(user, moss.name)
    await pick(user, wax.name)
    const perform = screen.getByRole('button', { name: 'Perform ritual' })
    await waitFor(() => expect(perform).toBeEnabled())
    const inventoryLoads = api.callsTo('GET', `/api/players/${player.id}/inventory`).length

    await user.click(perform)

    expect(await screen.findByRole('alert')).toHaveTextContent('lost a race')
    await waitFor(() => expect(api.callsTo('GET', `/api/players/${player.id}/inventory`).length).toBeGreaterThan(inventoryLoads))
  })
})
