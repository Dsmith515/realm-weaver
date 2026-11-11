import { NavLink, Outlet } from 'react-router'
import { ApiStatus } from './ApiStatus'

const links = [
  { to: '/ritual', label: 'Ritual' },
  { to: '/player', label: 'Player' },
  { to: '/realms', label: 'Realms' },
  { to: '/components', label: 'Components' },
]

export function Layout() {
  return (
    <div className="app">
      <header className="app-header">
        <span className="brand">Realm Weaver</span>
        <nav aria-label="Main">
          {links.map((link) => (
            <NavLink key={link.to} to={link.to}>
              {link.label}
            </NavLink>
          ))}
        </nav>
        <ApiStatus />
      </header>
      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
