import { expect, type Page } from '@playwright/test'

/** Switches the header's "Viewing as" toggle (ADR 0012). The player picker only shows in player view. */
export const viewAs = (page: Page, role: 'GM' | 'Player') =>
  page.getByRole('group', { name: 'Viewing as' }).getByRole('button', { name: role }).click()

/** Creates a player through the header switcher (they get the starter kit) and leaves them selected, in player view. */
export async function newPlayer(page: Page, prefix: string): Promise<string> {
  const name = `${prefix} ${Date.now().toString(36)}${Math.floor(Math.random() * 1e4)}`
  await page.goto('/')
  await viewAs(page, 'Player')
  await page.getByRole('button', { name: 'New player' }).click()
  await page.getByLabel('New player name').fill(name)
  await page.getByRole('button', { name: 'Create' }).click()
  await expect(page.getByRole('combobox', { name: 'Player' }).locator('option:checked')).toHaveText(name)
  return name
}

/** Pages that sit inside a menu group (src/nav/navConfig.ts): the group opens first. */
const groupOf: Record<string, string> = { 'Quick Weave': 'Weave', Ritual: 'Weave', Components: 'Codex' }

export async function nav(page: Page, link: string) {
  const menu = page.getByRole('navigation', { name: 'Main' })
  const group = groupOf[link]
  if (group) {
    await menu.getByRole('button', { name: group }).click()
    await menu.getByRole('group', { name: group }).getByRole('link', { name: new RegExp(`^${link}`) }).click()
  } else {
    await menu.getByRole('link', { name: link, exact: true }).click()
  }
}

/** The realm page has drawn its map: the server-rendered SVG is inside the viewer. */
export async function expectMapDrawn(page: Page) {
  await expect(page.locator('.svg-viewer svg').first()).toBeVisible()
}
