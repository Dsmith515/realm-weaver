import { expect, test } from '@playwright/test'
import { expectMapDrawn, nav, newPlayer } from './helpers.ts'

test('a new player performs a random ritual, the GM completes an objective and hands out the map, and exports download', async ({ page }) => {
  await newPlayer(page, 'E2E Ritual')

  await nav(page, 'Ritual')
  await page.getByRole('button', { name: 'Random' }).click()
  const perform = page.getByRole('button', { name: 'Perform ritual' })
  await expect(perform).toBeEnabled()
  await perform.click()
  const opened = page.getByRole('status').filter({ hasText: 'The portal opens.' })
  // A random pick may reopen a realm woven before with the same ingredients; either way the ritual grants no map.
  await expect(opened).toContainText(/A new realm was woven|You reopened a realm woven before/)

  await nav(page, 'Player')
  const maps = page.getByRole('region', { name: 'Maps' })
  await expect(maps.getByRole('heading')).toContainText('(0)')

  // The realm renders, and exports download.
  // A new player has exactly the one realm they just opened.
  await nav(page, 'Realms')
  const realms = page.locator('.realm-list').getByRole('link')
  await expect(realms).toHaveCount(1)
  await realms.click()
  await expectMapDrawn(page)

  for (const [link, extension] of [['Download SVG', '.svg'], ['Placement sheet (Markdown)', '.md'], ['JSON', '.json']] as const) {
    const download = page.waitForEvent('download')
    await page.getByRole('link', { name: link }).click()
    expect((await download).suggestedFilename().endsWith(extension), `${link} downloads a ${extension} file`).toBe(true)
  }

  await page.getByRole('button', { name: 'Image prompt' }).click()
  await expect(page.getByRole('dialog')).toContainText('battle map')
  await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click()

  // The GM completes an objective (the first is chosen) and hands the map to the weaver, who is ticked by default.
  await expect(page.getByRole('region', { name: 'Objectives' })).toContainText('as often as the realm is run')
  const completion = page.getByRole('region', { name: 'Completion' })
  await expect(completion.getByRole('checkbox', { name: /\(weaver\)/ })).toBeChecked()
  await completion.getByRole('button', { name: 'Complete objective' }).click()
  await expect(completion.getByRole('status')).toContainText('Map handed to E2E Ritual')

  // The realm stays open for the next party; the weaver is now on its completion list.
  await completion.getByText(/^Player completion/).click()
  await expect(completion.getByRole('list', { name: 'Players who completed an objective' })).toContainText('E2E Ritual')

  await nav(page, 'Player')
  await expect(page.getByRole('region', { name: 'Maps' }).getByRole('heading')).toContainText('(1)')
})
