import { useState } from 'react'
import { Link, Outlet } from 'react-router'
import { MenuDrawer } from '../nav/MenuDrawer'
import { NavMenu } from '../nav/NavMenu'
import { TabBar } from '../nav/TabBar'
import { useScrollDirection } from '../nav/useScrollDirection'
import { RoleSwitcher } from '../player/RoleSwitcher'
import { ThemePicker } from '../theme/ThemePicker'
import { ApiStatus } from './ApiStatus'

const railKey = 'realm-weaver.rail'

function loadPinned(): boolean {
  try {
    return localStorage.getItem(railKey) === 'pinned'
  } catch {
    return false
  }
}

function savePinned(pinned: boolean) {
  try {
    localStorage.setItem(railKey, pinned ? 'pinned' : 'collapsed')
  } catch {
    // Private mode or blocked storage: the rail just won't remember.
  }
}

/**
 * The app shell. Desktop has a left rail: icons by default, or pinned open with labels. On a phone the pages move to a
 * bottom tab bar, and the menu button opens a drawer with About, the role switch and the theme. Scrolling down slides
 * the phone's header and tab bar away, and scrolling up brings them back (nav.css). Realm Weaver is a GM tool first
 * (ADR 0010); in player view the GM-only pages drop out of the menu (ADR 0012).
 */
export function Layout() {
  const [pinned, setPinned] = useState(loadPinned)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [sheet, setSheet] = useState<string | undefined>()
  // The bars never hide while the drawer or a sheet is open. The CSS also keeps them shown while keyboard focus is in them.
  const barsHidden = useScrollDirection() === 'down' && !drawerOpen && sheet === undefined

  const togglePinned = () => {
    savePinned(!pinned)
    setPinned(!pinned)
  }

  return (
    <div className={`app${pinned ? ' app--pinned' : ''}${barsHidden ? ' app--bars-hidden' : ''}`}>
      <header className="app-header">
        <button type="button" className="icon-button app-header__menu" aria-label="Open menu" aria-haspopup="dialog" aria-expanded={drawerOpen} onClick={() => setDrawerOpen(true)}>
          <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
            <path d="M4 6h16M4 12h16M4 18h16" />
          </svg>
        </button>
        <button
          type="button"
          className="icon-button app-header__pin"
          aria-label={pinned ? 'Collapse navigation' : 'Pin navigation open'}
          aria-pressed={pinned}
          title={pinned ? 'Collapse navigation' : 'Pin navigation open'}
          onClick={togglePinned}
        >
          <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M4 5v14" />
            <path d={pinned ? 'M20 12H10M14 8l-4 4 4 4' : 'M10 12h10M16 8l4 4-4 4'} />
          </svg>
        </button>
        <Link to="/" className="brand">
          <img src="/brand/icon-64.webp" srcSet="/brand/icon-128.webp 2x" alt="" width="40" height="40" />
          <span className="brand__name">Realm Weaver</span>
        </Link>
        <div className="app-header__tools">
          {/* On a phone these two move into the menu drawer. */}
          <div className="app-header__wide">
            <RoleSwitcher />
            <ThemePicker />
          </div>
          <ApiStatus />
        </div>
      </header>

      <nav aria-label="Main" className="app-rail">
        <NavMenu key={pinned ? 'pinned' : 'rail'} variant={pinned ? 'pinned' : 'rail'} />
      </nav>

      <MenuDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />

      <main className="app-main">
        <Outlet />
      </main>

      <TabBar sheet={sheet} onSheetChange={setSheet} />
    </div>
  )
}
