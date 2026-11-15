import { NavLink, Outlet } from 'react-router'
import { PlayerSwitcher } from '../player/PlayerSwitcher'
import { ApiStatus } from './ApiStatus'

/**
 * Realm Weaver is a GM tool first, so the GM's pages lead. The player pages are a preview: groundwork for a later
 * session client where players log in, perform rituals together and trade (ADR 0010).
 */
const groups = [
  {
    label: 'GM',
    links: [
      { to: '/gm/weave', label: 'Quick Weave' },
      { to: '/realms', label: 'Realms' },
      { to: '/components', label: 'Components' },
    ],
  },
  {
    label: 'Player preview',
    hint: 'Groundwork for player sessions',
    links: [
      { to: '/ritual', label: 'Ritual' },
      { to: '/player', label: 'Player' },
    ],
  },
]

export function Layout() {
  return (
    <div className="app">
      <header className="app-header">
        <span className="brand">Realm Weaver</span>
        <nav aria-label="Main">
          {groups.map((group) => (
            <div key={group.label} className="nav-group" role="group" aria-label={group.label} title={group.hint}>
              <span className="nav-group__label">{group.label}</span>
              {group.links.map((link) => (
                <NavLink key={link.to} to={link.to}>
                  {link.label}
                </NavLink>
              ))}
            </div>
          ))}
          <NavLink to="/about">About</NavLink>
        </nav>
        <PlayerSwitcher />
        <ApiStatus />
      </header>
      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
