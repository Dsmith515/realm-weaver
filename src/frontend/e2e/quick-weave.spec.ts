import { expect, test } from '@playwright/test'
import { expectMapDrawn } from './helpers.ts'

test('a GM rolls a Quick Weave within limits and weaves the realm that was shown', async ({ page }) => {
  await page.goto('/weave/quick')

  const biomes = page.getByRole('group', { name: 'Biome' })
  await biomes.getByRole('button', { name: 'Select all' }).click()
  await expect(biomes.getByRole('button', { name: 'Unselect all' })).toBeVisible()

  await page.getByRole('button', { name: 'Roll' }).click()
  const rolled = page.getByRole('region', { name: 'Rolled components' })
  await expect(rolled).toContainText('Needle:')
  await expect(page.getByRole('main').getByRole('button', { name: 'Weave' })).toBeEnabled()

  await page.getByRole('main').getByRole('button', { name: 'Weave' }).click()
  await expect(page).toHaveURL(/\/realms\/[0-9a-f-]+$/)
  await expectMapDrawn(page)
})
