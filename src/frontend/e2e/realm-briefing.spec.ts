import { expect, test } from '@playwright/test'
import { expectMapDrawn, newPlayer, nav, viewAs } from './helpers.ts'

test('a player who doesn’t hold the map follows the ritual result into a briefing with no map, locations or GM controls', async ({ page }) => {
  await newPlayer(page, 'E2E Briefing')

  await nav(page, 'Ritual')
  await page.getByRole('button', { name: 'Random' }).click()
  const perform = page.getByRole('button', { name: 'Perform ritual' })
  await expect(perform).toBeEnabled()
  await perform.click()
  // Rituals never grant the map (ADR 0010), so the weaver doesn't hold it.
  const opened = page.getByRole('status').filter({ hasText: 'The portal opens.' })
  await opened.getByRole('link', { name: 'View the realm' }).click()

  const briefing = page.getByRole('region', { name: 'Realm briefing' })
  await expect(briefing.getByRole('heading', { level: 1 })).toBeVisible()
  await expect(briefing.getByRole('region', { name: 'Briefing objectives' })).toBeVisible()
  await expect(briefing).toContainText('You don’t hold this realm’s map')

  // No map.
  await expect(page.locator('.svg-viewer')).toHaveCount(0)
  await expect(page.getByRole('link', { name: 'Download SVG' })).toHaveCount(0)
  // No locations: no placements list, nothing listed by where it is, and objectives give hints only.
  await expect(page.getByRole('complementary', { name: 'Placements' })).toHaveCount(0)
  for (const list of ['Creatures', 'Mobs', 'Potential loot', 'Resource nodes', 'Settlements']) {
    await expect(page.getByRole('list', { name: list })).toHaveCount(0)
  }
  // The GM page writes locations as tile ranges such as D2-F5, and realm-wide objectives as "Whole realm".
  await expect(briefing).not.toContainText(/\b[A-Z]{1,2}\d{1,3}-[A-Z]{1,2}\d{1,3}\b|Whole realm/)
  // No GM controls.
  for (const control of ['Map details', 'Image prompt', 'Complete objective', 'Pin an objective (GM)…']) {
    await expect(page.getByRole('button', { name: control })).toHaveCount(0)
  }
  await expect(page.getByRole('region', { name: 'Completion' })).toHaveCount(0)

  // The same link in GM view is the full realm page.
  await viewAs(page, 'GM')
  await expectMapDrawn(page)
  await expect(page.getByRole('button', { name: 'Map details' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'Realm briefing' })).toHaveCount(0)
})
