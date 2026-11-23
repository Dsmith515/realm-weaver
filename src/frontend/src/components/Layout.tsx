import { useEffect, useState } from 'react'
import { Link, Outlet } from 'react-router'
import { NavMenu } from '../nav/NavMenu'
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
 * The app shell. Desktop has a left rail: icons by default, or pinned open with labels. On a phone the rail becomes a
 * drawer behind the menu button. Realm Weaver is a GM tool first (ADR 0010); in player view the GM-only pages drop out
 * of the menu (ADR 0012).
 */
export function Layout() {
  const [pinned, setPinned] = useState(loadPinned)
  const [drawerOpen, setDrawerOpen] = useState(false)

  const togglePinned = () => {
    savePinned(!pinned)
    setPinned(!pinned)
  }

  useEffect(() => {
    if (!drawerOpen) return
    const onKey = (event: KeyboardEvent) => event.key === 'Escape' && setDrawerOpen(false)
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [drawerOpen])

  return (
    <div className={`app${pinned ? ' app--pinned' : ''}`}>
      <header className="app-header">
        <button type="button" className="icon-button app-header__menu" aria-label="Open menu" aria-expanded={drawerOpen} onClick={() => setDrawerOpen(true)}>
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
          <RoleSwitcher />
          <ThemePicker />
          <ApiStatus />
        </div>
      </header>

      <nav aria-label="Main" className="app-rail">
        <NavMenu key={pinned ? 'pinned' : 'rail'} variant={pinned ? 'pinned' : 'rail'} />
      </nav>

      {drawerOpen && (
        <>
          <div className="app-drawer__backdrop" onClick={() => setDrawerOpen(false)} />
          <nav aria-label="Menu" className="app-drawer">
            <div className="app-drawer__head">
              <span className="app-drawer__title">Menu</span>
              <button type="button" className="icon-button" aria-label="Close menu" onClick={() => setDrawerOpen(false)} autoFocus>
                <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                  <path d="M6 6l12 12M18 6L6 18" />
                </svg>
              </button>
            </div>
            <NavMenu variant="drawer" onNavigate={() => setDrawerOpen(false)} />
          </nav>
        </>
      )}

      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
