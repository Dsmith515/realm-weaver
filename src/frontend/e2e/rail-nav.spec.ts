import { expect, test } from '@playwright/test'
import { nav } from './helpers.ts'

// The flyout opens over the Ritual page's slots. Playwright refuses to click an element that something else covers,
// so this fails if the page content paints over the rail's flyout.
test('the rail flyout stays above page content it overlaps', async ({ page }) => {
  await page.goto('/weave/ritual')
  await expect(page.locator('.slot').first()).toBeVisible()

  await nav(page, 'Components')
  await expect(page.getByRole('heading', { level: 1, name: 'Components' })).toBeVisible()
})
