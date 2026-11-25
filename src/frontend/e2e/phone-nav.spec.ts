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

  // Tab all the way round: nothing behind the drawer ever takes focus. After the drawer's last control, Chrome moves
  // focus out to its own toolbar (the page then reports no focused element) and the next Tab wraps back into the drawer.
  for (let i = 0; i < 8; i++) {
    await page.keyboard.press('Tab')
    await expect(page.locator(':focus:not(.app-drawer *)')).toHaveCount(0)
  }
  await expect(drawer.locator(':focus')).toHaveCount(1)

  await page.keyboard.press('Escape')
  await expect(drawer).toBeHidden()
  await expect(menuButton).toBeFocused()
})

test('a phone sheet closes with a tap on its grab bar, or a long drag down', async ({ page }) => {
  await page.goto('/about')
  const tabs = page.getByRole('navigation', { name: 'Sections' })
  const sheet = page.getByRole('dialog', { name: 'Weave' })

  // A tap: a press and release without moving.
  await tabs.getByRole('button', { name: 'Weave' }).click()
  await sheet.getByRole('button', { name: 'Close' }).click()
  await expect(sheet).toBeHidden()

  // A drag down well past the close distance.
  await tabs.getByRole('button', { name: 'Weave' }).click()
  const bar = await sheet.getByRole('button', { name: 'Close' }).boundingBox()
  if (!bar) throw new Error('The grab bar has no box')
  await page.mouse.move(bar.x + bar.width / 2, bar.y + bar.height / 2)
  await page.mouse.down()
  await page.mouse.move(bar.x + bar.width / 2, bar.y + 150, { steps: 10 })
  await page.mouse.up()
  await expect(sheet).toBeHidden()
})
