import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { QuickWeavePreview, QuickWeaveRequest } from '../api/types'
import { fakeApi, json, type ApiCall } from '../test/fakeApi'
import { moss, needle, player, slot, wax, wheat } from '../test/fixtures'
import { renderApp } from '../test/render'

const rolled = (threadPick = moss): QuickWeavePreview => ({
  needle: slot(needle, 'selected', 0),
  thread: slot(threadPick, 'random', 0),
  coating: slot(wax, 'random', 0),
  map: null,
  isMismatch: false,
  mismatchedSlots: [],
  dangerModifier: 0,
  createsNewMap: true,
  issues: [],
  canWeave: true,
})

function routes(previews: QuickWeavePreview[]) {
  let call = 0
  return {
    'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
    'GET /api/players': [player],
    'GET /api/components': (call: ApiCall) => {
      const items = [needle, moss, wheat, wax].filter((c) => c.type === call.query.get('type'))
      return { items, page: 1, pageSize: 200, totalCount: items.length }
    },
    'POST /api/gm/weave/preview': () => previews[Math.min(call++, previews.length - 1)],
    'POST /api/gm/weave': () => json({ realmId: 'realm-9', mapId: 'map-9', mapCreated: true, needleId: needle.id, threadId: moss.id, coatingId: wax.id, isMismatch: false, dangerModifier: 0 }, 201),
    'GET /api/realms/:id': () => json({ status: 404, title: 'Not found' }, 404),
  }
}

describe('Quick Weave page', () => {
  it('rolls within the limits, re-rolls one slot and weaves exactly what was shown', async () => {
    const api = fakeApi(routes([rolled(moss), rolled(wheat)]))
    const user = userEvent.setup()
    renderApp('/gm/weave')

    await screen.findByRole('option', { name: needle.name })
    await user.selectOptions(screen.getByRole('combobox', { name: 'Needle' }), needle.id)
    await user.click(within(screen.getByRole('group', { name: 'Rarity' })).getByRole('button', { name: 'Common' }))
    await user.click(within(screen.getByRole('group', { name: 'Biome' })).getByRole('button', { name: 'Forest' }))
    await user.type(screen.getByRole('spinbutton', { name: 'Highest tier' }), '3')
    await user.click(screen.getByRole('button', { name: 'Roll' }))

    const result = await screen.findByRole('region', { name: 'Rolled components' })
    expect(within(result).getByText('Mossgreen Thread')).toBeInTheDocument()
    const first = api.callsTo('POST', '/api/gm/weave/preview')[0].body as QuickWeaveRequest
    expect(first).toMatchObject({ needleId: needle.id, threadId: null, limits: { rarities: ['common'], biomes: ['forest'], maxTier: 3 } })
    expect(within(result).queryByRole('button', { name: 'Re-roll needle' })).not.toBeInTheDocument()

    await user.click(within(result).getByRole('button', { name: 'Re-roll thread' }))
    expect(await within(result).findByText('Wheatgold Thread')).toBeInTheDocument()
    const second = api.callsTo('POST', '/api/gm/weave/preview')[1].body as QuickWeaveRequest
    expect(second).toMatchObject({ needleId: needle.id, threadId: null, coatingId: wax.id })

    await user.click(screen.getByRole('button', { name: 'Weave' }))
    await waitFor(() => expect(api.callsTo('POST', '/api/gm/weave')[0]?.body).toMatchObject({ needleId: needle.id, threadId: wheat.id, coatingId: wax.id, mapId: null }))
  })

  it('shows why nothing can be woven', async () => {
    fakeApi(routes([{ ...rolled(), coating: null, canWeave: false, issues: [{ slot: 'coating', field: 'coatingId', code: 'no-candidates', message: 'No Coating in the catalog fits the limits.' }] }]))
    const user = userEvent.setup()
    renderApp('/gm/weave')

    await user.click(await screen.findByRole('button', { name: 'Roll' }))

    expect(await screen.findByText('No Coating in the catalog fits the limits.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Weave' })).toBeDisabled()
  })
})
