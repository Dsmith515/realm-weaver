import { expect, test } from '@playwright/test'
import { phoneNav } from './helpers.ts'

// Playwright's project is desktop Chromium, so this spec sets a phone viewport itself.
test.use({ viewport: { width: 375, height: 812 }, hasTouch: true })

test('the phone tab bar reaches every section, and its sheets close after choosing a page', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeHidden()

  await phoneNav(page, 'Ritual')
  await expect(page.getByRole('heading', { level: 1, name: 'Ritual' })).toBeVisible()
  await expect(page.getByRole('dialog', { name: 'Weave' })).toBeHidden()

  await phoneNav(page, 'Components')
  await expect(page.getByRole('heading', { level: 1, name: 'Components' })).toBeVisible()

  await phoneNav(page, 'Realms')
  await expect(page.getByRole('heading', { level: 1, name: 'Realms' })).toBeVisible()
})

test('the phone menu keeps keyboard focus inside and gives it back to the menu button (#51)', async ({ page }) => {
  await page.goto('/')
  const menuButton = page.getByRole('button', { name: 'Open menu' })
  await menuButton.click()

  const drawer = page.getByRole('dialog', { name: 'Menu' })
  await expect(drawer.getByRole('group', { name: 'Viewing as' })).toBeVisible()

  // Tab all the way round: focus never lands behind the drawer.
  for (let i = 0; i < 8; i++) {
    await page.keyboard.press('Tab')
    await expect(drawer.locator(':focus')).toHaveCount(1)
  }

  await page.keyboard.press('Escape')
  await expect(drawer).toBeHidden()
  await expect(menuButton).toBeFocused()
})
