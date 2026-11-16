import { expect, test } from '@playwright/test'
import { expectMapDrawn, nav, newPlayer } from './helpers.ts'

test('a new player performs a random ritual, completes an objective as a Cartographer, records the map and downloads exports', async ({ page }) => {
  await newPlayer(page, 'E2E Ritual')

  await nav(page, 'Ritual')
  await page.getByRole('button', { name: 'Random' }).click()
  const perform = page.getByRole('button', { name: 'Perform ritual' })
  await expect(perform).toBeEnabled()
  await perform.click()
  const opened = page.getByRole('status').filter({ hasText: 'The portal opens.' })
  await expect(opened).toContainText('Complete one of its objectives as a Cartographer to record the map.')

  // The ritual doesn't grant the map any more. The GM trains the player as a Cartographer.
  await nav(page, 'Player')
  const maps = page.getByRole('region', { name: 'Maps' })
  await expect(maps.getByRole('heading')).toContainText('(0)')
  const attunement = page.getByRole('region', { name: 'Attunement' })
  await expect(attunement.getByRole('status')).toContainText('Not a Cartographer')
  await attunement.getByRole('checkbox', { name: 'Trained Cartographer (GM)' }).check()
  await expect(attunement.getByRole('status')).toContainText('a trained skill')

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
  await page.getByRole('dialog').getByRole('button', { name: 'Close' }).click()

  // The run has one to three objectives; completing any one completes it and records the map.
  const objectives = page.getByRole('region', { name: 'Objectives' })
  await expect(objectives).toContainText('Complete any one to complete the run.')
  await objectives.getByRole('button', { name: /^Complete:/ }).first().click()
  await expect(objectives.getByRole('status')).toContainText('A Cartographer recorded the map')

  await nav(page, 'Player')
  await expect(page.getByRole('region', { name: 'Maps' }).getByRole('heading')).toContainText('(1)')
})
