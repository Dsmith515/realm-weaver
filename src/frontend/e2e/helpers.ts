import { expect, type Page } from '@playwright/test'

/** Creates a player through the header switcher (they get the starter kit) and leaves them selected. */
export async function newPlayer(page: Page, prefix: string): Promise<string> {
  const name = `${prefix} ${Date.now().toString(36)}${Math.floor(Math.random() * 1e4)}`
  await page.goto('/')
  await page.getByRole('button', { name: 'New player' }).click()
  await page.getByLabel('New player name').fill(name)
  await page.getByRole('button', { name: 'Create' }).click()
  await expect(page.getByRole('combobox', { name: 'Player' }).locator('option:checked')).toHaveText(name)
  return name
}

export const nav = (page: Page, link: string) => page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: link }).click()

/** The realm page has drawn its map: the server-rendered SVG is inside the viewer. */
export async function expectMapDrawn(page: Page) {
  await expect(page.locator('.svg-viewer svg').first()).toBeVisible()
}
