import { expect, test } from '@playwright/test'
import { expectMapDrawn, nav, newPlayer } from './helpers.ts'

test('a new player performs a random ritual, gets the map, views the realm and downloads exports', async ({ page }) => {
  await newPlayer(page, 'E2E Ritual')

  await nav(page, 'Ritual')
  await page.getByRole('button', { name: 'Random' }).click()
  const perform = page.getByRole('button', { name: 'Perform ritual' })
  await expect(perform).toBeEnabled()
  await perform.click()
  const opened = page.getByRole('status').filter({ hasText: 'The portal opens.' })
  await expect(opened).toContainText('A new map was added to your inventory.')

  // The map is in the inventory.
  await nav(page, 'Player')
  const maps = page.getByRole('region', { name: 'Maps' })
  await expect(maps.getByRole('heading')).toContainText('(1)')

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
})
