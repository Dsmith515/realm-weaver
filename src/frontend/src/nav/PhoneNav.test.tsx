import { fireEvent, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fakeApi } from '../test/fakeApi'
import { inventory, player } from '../test/fixtures'
import { renderApp } from '../test/render'
import { useScrollDirection } from './useScrollDirection'

// Layout's half of hide-on-scroll is tested here with the hook stubbed. The hook's own tests are in
// useScrollDirection.test.ts.
vi.mock('./useScrollDirection', () => ({ useScrollDirection: vi.fn(() => 'up') }))

const routes = {
  'GET /api/info': { name: 'Realm Weaver', version: '1.0.0' },
  'GET /api/players': [player],
  'GET /api/players/:id/inventory': inventory,
  'GET /api/components': { items: [], page: 1, pageSize: 50, totalCount: 0 },
}

// CSS isn't loaded in these tests, so the tab bar is in the page next to the desktop rail. Queries stay inside it.
const tabBar = () => screen.getByRole('navigation', { name: 'Sections' })
const viewAsPlayer = () => localStorage.setItem('realm-weaver.role', 'player')

describe('Phone tab bar', () => {
  afterEach(() => vi.mocked(useScrollDirection).mockReturnValue('up'))

  it('has a tab per section, with groups opening a sheet and single pages as links', () => {
    fakeApi(routes)
    renderApp('/about')

    const tabs = within(tabBar())
    expect(tabs.getByRole('button', { name: 'Weave' })).toHaveAttribute('aria-haspopup', 'dialog')
    expect(tabs.getByRole('link', { name: 'Realms' })).toHaveAttribute('href', '/realms')
    expect(tabs.getByRole('button', { name: 'Codex' })).toBeInTheDocument()
    expect(tabs.getByRole('link', { name: 'Player' })).toHaveAttribute('href', '/player')
    expect(tabs.queryByRole('link', { name: 'About' })).not.toBeInTheDocument()
  })

  it('leaves out Codex in player view, since its only page is for the GM', () => {
    viewAsPlayer()
    fakeApi(routes)
    renderApp('/about')

    expect(within(tabBar()).queryByRole('button', { name: 'Codex' })).not.toBeInTheDocument()
    expect(within(tabBar()).getByRole('button', { name: 'Weave' })).toBeInTheDocument()
  })

  it('opens a group as a sheet of described pages, which closes after navigating', async () => {
    fakeApi(routes)
    renderApp('/about')
    const user = userEvent.setup()

    const weave = within(tabBar()).getByRole('button', { name: 'Weave' })
    await user.click(weave)

    const sheet = screen.getByRole('dialog', { name: 'Weave' })
    expect(weave).toHaveAttribute('aria-expanded', 'true')
    expect(within(sheet).getByText('Generate from the whole catalog. Nothing is used up.')).toBeInTheDocument()

    await user.click(within(sheet).getByRole('link', { name: /Ritual/ }))
    expect(screen.getByRole('heading', { level: 1, name: 'Ritual' })).toBeInTheDocument()
    expect(screen.queryByRole('dialog', { name: 'Weave' })).not.toBeInTheDocument()
    expect(weave).toHaveAttribute('aria-expanded', 'false')
    expect(weave).toHaveClass('active')
  })

  it('closes a sheet with its close button', async () => {
    fakeApi(routes)
    renderApp('/about')
    const user = userEvent.setup()

    await user.click(within(tabBar()).getByRole('button', { name: 'Codex' }))
    await user.click(within(screen.getByRole('dialog', { name: 'Codex' })).getByRole('button', { name: 'Close' }))

    expect(screen.queryByRole('dialog', { name: 'Codex' })).not.toBeInTheDocument()
  })
})

describe('Phone sheet swipe', () => {
  const drag = (to: number) => {
    const grab = screen.getByTestId('sheet-grab')
    fireEvent.pointerDown(grab, { clientY: 100, pointerId: 1 })
    fireEvent.pointerMove(grab, { clientY: 100 + to, pointerId: 1 })
    fireEvent.pointerUp(grab, { clientY: 100 + to, pointerId: 1 })
  }

  it('closes the sheet when its grab bar is dragged far enough down', async () => {
    fakeApi(routes)
    renderApp('/about')
    await userEvent.setup().click(within(tabBar()).getByRole('button', { name: 'Weave' }))

    drag(120)

    expect(screen.queryByRole('dialog', { name: 'Weave' })).not.toBeInTheDocument()
  })

  it('springs back on a short drag, without counting it as a tap on the handle', async () => {
    fakeApi(routes)
    renderApp('/about')
    await userEvent.setup().click(within(tabBar()).getByRole('button', { name: 'Weave' }))

    drag(30)
    fireEvent.click(screen.getByRole('button', { name: 'Close' }))

    expect(screen.getByRole('dialog', { name: 'Weave' })).toBeInTheDocument()
    const panel = screen.getByTestId('sheet-grab').parentElement!
    expect(panel.style.transform).toBe('')
    expect(panel).not.toHaveClass('app-sheet--dragging')
  })
})

describe('Phone sheet pointer capture', () => {
  // jsdom has no pointer capture, so stand in a spy. In a real browser, capturing on press would send a tap's click to
  // the strip instead of the handle (e2e/phone-nav.spec.ts covers the tap end to end).
  const capture = vi.fn()
  beforeEach(() => {
    capture.mockClear()
    Object.defineProperty(HTMLElement.prototype, 'setPointerCapture', { value: capture, configurable: true })
  })
  afterEach(() => {
    delete (HTMLElement.prototype as Partial<HTMLElement>).setPointerCapture
  })

  const openWeave = async () => {
    fakeApi(routes)
    renderApp('/about')
    await userEvent.setup().click(within(tabBar()).getByRole('button', { name: 'Weave' }))
    return screen.getByTestId('sheet-grab')
  }

  it('leaves a tap uncaptured, so its click reaches the handle and closes the sheet', async () => {
    const grab = await openWeave()
    fireEvent.pointerDown(grab, { clientY: 100, pointerId: 1 })
    fireEvent.pointerMove(grab, { clientY: 103, pointerId: 1 })
    fireEvent.pointerUp(grab, { clientY: 103, pointerId: 1 })
    expect(capture).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'Close' }))
    expect(screen.queryByRole('dialog', { name: 'Weave' })).not.toBeInTheDocument()
  })

  it('captures the pointer once a press moves far enough to be a drag', async () => {
    const grab = await openWeave()
    fireEvent.pointerDown(grab, { clientY: 100, pointerId: 1 })
    fireEvent.pointerMove(grab, { clientY: 130, pointerId: 1 })
    fireEvent.pointerMove(grab, { clientY: 140, pointerId: 1 })

    expect(capture).toHaveBeenCalledTimes(1)
    expect(capture).toHaveBeenCalledWith(1)
    fireEvent.pointerUp(grab, { clientY: 140, pointerId: 1 })
  })
})

describe('Phone menu drawer', () => {
  it('holds About, the role switch and the theme picker, and closes after choosing About', async () => {
    fakeApi(routes)
    renderApp('/realms')
    const user = userEvent.setup()

    const menuButton = screen.getByRole('button', { name: 'Open menu' })
    await user.click(menuButton)
    expect(menuButton).toHaveAttribute('aria-expanded', 'true')

    const drawer = within(screen.getByRole('dialog', { name: 'Menu' }))
    expect(drawer.getByRole('group', { name: 'Viewing as' })).toBeInTheDocument()
    expect(drawer.getByRole('button', { name: 'Theme' })).toBeInTheDocument()

    await user.click(drawer.getByRole('link', { name: 'About' }))
    expect(screen.queryByRole('dialog', { name: 'Menu' })).not.toBeInTheDocument()
    expect(menuButton).toHaveAttribute('aria-expanded', 'false')
  })

  it('shows the player picker once switched to player view', async () => {
    fakeApi(routes)
    renderApp('/realms')
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Open menu' }))
    const drawer = within(screen.getByRole('dialog', { name: 'Menu' }))
    expect(drawer.queryByRole('combobox', { name: 'Player' })).not.toBeInTheDocument()

    await user.click(drawer.getByRole('button', { name: 'Player' }))
    expect(await drawer.findByRole('combobox', { name: 'Player' })).toBeInTheDocument()
  })
})

describe('Hide on scroll', () => {
  afterEach(() => vi.mocked(useScrollDirection).mockReturnValue('up'))

  const app = (container: HTMLElement) => container.querySelector('.app')

  it('hides the bars while the page is scrolling down', () => {
    vi.mocked(useScrollDirection).mockReturnValue('down')
    fakeApi(routes)
    const { container } = renderApp('/about')

    expect(app(container)).toHaveClass('app--bars-hidden')
  })

  it('shows the bars while scrolling up', () => {
    fakeApi(routes)
    const { container } = renderApp('/about')

    expect(app(container)).not.toHaveClass('app--bars-hidden')
  })

  it('keeps the bars shown while a sheet or the drawer is open', async () => {
    vi.mocked(useScrollDirection).mockReturnValue('down')
    fakeApi(routes)
    const { container } = renderApp('/about')
    const user = userEvent.setup()

    await user.click(within(tabBar()).getByRole('button', { name: 'Weave' }))
    expect(app(container)).not.toHaveClass('app--bars-hidden')
    await user.click(within(screen.getByRole('dialog', { name: 'Weave' })).getByRole('button', { name: 'Close' }))
    expect(app(container)).toHaveClass('app--bars-hidden')

    await user.click(screen.getByRole('button', { name: 'Open menu' }))
    expect(app(container)).not.toHaveClass('app--bars-hidden')
  })
})
