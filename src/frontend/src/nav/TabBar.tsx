import { useState } from 'react'
import { NavLink, useLocation } from 'react-router'
import { useModalDialog } from '../components/useModalDialog'
import { useRole } from '../player/roleContext'
import { NavIcon } from './NavMenu'
import { itemsFor, type NavGroup } from './navConfig'

interface TabBarProps {
  /** The id of the group whose sheet is open, if any. Layout holds it so the bars stay in view while a sheet is open. */
  sheet: string | undefined
  onSheetChange: (sheet: string | undefined) => void
}

/**
 * The phone navigation: a tab per top-level item, from the same config as the desktop rail (navConfig.ts). A single
 * page navigates straight away. A group opens a bottom sheet of its pages, each card with its one-line description.
 */
export function TabBar({ sheet, onSheetChange }: TabBarProps) {
  const { role } = useRole()
  const { pathname } = useLocation()
  const items = itemsFor(role)
  const close = () => onSheetChange(undefined)
  const { shown, props } = useModalDialog(sheet !== undefined, close)
  // The sheet keeps showing its group while it closes (see useModalDialog), so remember the last one opened.
  const [lastSheet, setLastSheet] = useState(sheet)
  if (sheet !== undefined && sheet !== lastSheet) setLastSheet(sheet)
  const group = shown ? items.find((item): item is NavGroup => item.kind === 'group' && item.id === lastSheet) : undefined

  return (
    <>
      <nav aria-label="Sections" className="app-tabbar">
        <ul className="app-tabbar__list">
          {items.map((item) => (
            <li key={item.kind === 'link' ? item.to : item.id}>
              {item.kind === 'link' ? (
                <NavLink to={item.to} className="app-tab">
                  <NavIcon icon={item.icon} tone={item.tone} size={24} />
                  <span className="app-tab__label">{item.label}</span>
                </NavLink>
              ) : (
                <button
                  type="button"
                  className={`app-tab${item.children.some((c) => pathname.startsWith(c.to)) ? ' active' : ''}`}
                  aria-haspopup="dialog"
                  aria-expanded={sheet === item.id}
                  onClick={() => onSheetChange(item.id)}
                >
                  <NavIcon icon={item.icon} tone={item.tone} size={24} />
                  <span className="app-tab__label">{item.label}</span>
                </button>
              )}
            </li>
          ))}
        </ul>
      </nav>

      <dialog {...props} className="app-sheet" aria-label={group?.label}>
        {group && (
          <div className="app-sheet__body">
            <div className="app-sheet__head">
              <span className="nav-cards__title">{group.label}</span>
              <button type="button" className="icon-button" aria-label="Close" onClick={close}>
                <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                  <path d="M6 6l12 12M18 6L6 18" />
                </svg>
              </button>
            </div>
            <div className="nav-cards">
              {group.children.map((child) => (
                <NavLink key={child.to} to={child.to} className="nav-card" onClick={close}>
                  <NavIcon icon={child.icon} tone={child.tone} size={26} />
                  <span className="nav-card__text">
                    <span className="nav-card__label">{child.label}</span>
                    {child.description && <span className="nav-card__description">{child.description}</span>}
                  </span>
                </NavLink>
              ))}
            </div>
          </div>
        )}
      </dialog>
    </>
  )
}

