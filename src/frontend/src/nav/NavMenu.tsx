import { useEffect, useRef, useState, type CSSProperties } from 'react'
import { NavLink, useLocation } from 'react-router'
import { useRole } from '../player/roleContext'
import { aboutItem, itemsFor, type IconTone, type NavGroup, type NavLink as NavLinkItem } from './navConfig'

/**
 * - rail: icons only. Hovering or focusing an icon shows its name, and a group opens a flyout of cards beside the rail.
 * - pinned: the rail held open, with labels and each group's pages listed inline.
 * Phones use TabBar instead, from the same config.
 */
export type NavVariant = 'rail' | 'pinned'

export function NavIcon({ icon, tone, size = 28 }: { icon: string; tone: IconTone; size?: number }) {
  const style = { '--icon': `url("${icon}")`, width: size, height: size } as CSSProperties
  return <span aria-hidden="true" className={`nav-icon nav-icon--${tone}`} style={style} />
}

export function NavMenu({ variant }: { variant: NavVariant }) {
  const { role } = useRole()
  const { pathname } = useLocation()
  const items = itemsFor(role)
  const activeGroup = items.find((item): item is NavGroup => item.kind === 'group' && item.children.some((c) => pathname.startsWith(c.to)))
  // When pinned, the group holding the current page starts open. The flyout starts closed.
  const [open, setOpen] = useState<string | undefined>(variant === 'rail' ? undefined : activeGroup?.id)
  const menu = useRef<HTMLUListElement>(null)
  const flyoutOpen = variant === 'rail' && open !== undefined

  // A flyout closes on Escape or a click anywhere outside the menu.
  useEffect(() => {
    if (!flyoutOpen) return
    const onKey = (event: KeyboardEvent) => event.key === 'Escape' && setOpen(undefined)
    const onPointer = (event: PointerEvent) => {
      if (!menu.current?.contains(event.target as Node)) setOpen(undefined)
    }
    document.addEventListener('keydown', onKey)
    document.addEventListener('pointerdown', onPointer)
    return () => {
      document.removeEventListener('keydown', onKey)
      document.removeEventListener('pointerdown', onPointer)
    }
  }, [flyoutOpen])

  const navigated = () => {
    if (variant === 'rail') setOpen(undefined)
  }

  const link = (item: NavLinkItem) => (
    <NavLink to={item.to} className="nav-item" onClick={navigated}>
      <NavIcon icon={item.icon} tone={item.tone} />
      <span className="nav-item__label">{item.label}</span>
    </NavLink>
  )

  return (
    <div className={`nav-menu nav-menu--${variant}`}>
      <ul ref={menu} className="nav-menu__list">
        {items.map((item) => {
          if (item.kind === 'link') return <li key={item.to}>{link(item)}</li>

          const isOpen = open === item.id
          const panelId = `nav-${variant}-${item.id}`
          return (
            <li key={item.id} className="nav-menu__group">
              <button
                type="button"
                className={`nav-item${activeGroup?.id === item.id ? ' active' : ''}`}
                aria-expanded={isOpen}
                aria-controls={panelId}
                onClick={() => setOpen(isOpen ? undefined : item.id)}
              >
                <NavIcon icon={item.icon} tone={item.tone} />
                <span className="nav-item__label">{item.label}</span>
                <span aria-hidden="true" className="nav-item__caret" />
              </button>
              {isOpen && (
                <div id={panelId} className={variant === 'pinned' ? 'nav-sub' : 'nav-cards'} role="group" aria-label={item.label}>
                  {variant === 'rail' && <span className="nav-cards__title">{item.label}</span>}
                  {item.children.map((child) =>
                    variant === 'pinned' ? (
                      <NavLink key={child.to} to={child.to} className="nav-sub__item" onClick={navigated}>
                        <NavIcon icon={child.icon} tone={child.tone} size={22} />
                        {child.label}
                      </NavLink>
                    ) : (
                      <NavLink key={child.to} to={child.to} className="nav-card" onClick={navigated}>
                        <NavIcon icon={child.icon} tone={child.tone} size={26} />
                        <span className="nav-card__text">
                          <span className="nav-card__label">{child.label}</span>
                          {child.description && <span className="nav-card__description">{child.description}</span>}
                        </span>
                      </NavLink>
                    ),
                  )}
                </div>
              )}
            </li>
          )
        })}
      </ul>
      <div className="nav-menu__footer">{link(aboutItem)}</div>
    </div>
  )
}
