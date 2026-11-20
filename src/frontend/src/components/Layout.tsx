import { NavLink, Outlet } from 'react-router'
import { useRole } from '../player/roleContext'
import { RoleSwitcher } from '../player/RoleSwitcher'
import { ThemePicker } from '../theme/ThemePicker'
import { ApiStatus } from './ApiStatus'

/**
 * Realm Weaver is a GM tool first, so the GM's pages lead. The player pages are a preview: groundwork for a later
 * session client where players log in, perform rituals together and trade (ADR 0010). GM-only links are hidden in
 * player view (ADR 0012).
 */
const groups = [
  {
    label: 'GM',
    links: [
      { to: '/gm/weave', label: 'Quick Weave', gmOnly: true },
      { to: '/realms', label: 'Realms' },
      { to: '/components', label: 'Components', gmOnly: true },
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
  const { role } = useRole()

  return (
    <div className="app">
      <header className="app-header">
        <span className="brand">Realm Weaver</span>
        <nav aria-label="Main">
          {groups.map((group) => (
            <div key={group.label} className="nav-group" role="group" aria-label={group.label} title={group.hint}>
              <span className="nav-group__label">{group.label}</span>
              {group.links
                .filter((link) => role === 'gm' || !('gmOnly' in link && link.gmOnly))
                .map((link) => (
                  <NavLink key={link.to} to={link.to}>
                    {link.label}
                  </NavLink>
                ))}
            </div>
          ))}
          <NavLink to="/about">About</NavLink>
        </nav>
        <RoleSwitcher />
        <ThemePicker />
        <ApiStatus />
      </header>
      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
