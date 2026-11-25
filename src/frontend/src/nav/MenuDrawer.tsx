import { NavLink } from 'react-router'
import { useModalDialog } from '../components/useModalDialog'
import { RoleSwitcher } from '../player/RoleSwitcher'
import { ThemePicker } from '../theme/ThemePicker'
import { NavIcon } from './NavMenu'
import { aboutItem } from './navConfig'

/**
 * The phone's header menu. The pages live in the tab bar, so this holds what's left: About, the "Viewing as" switch
 * (with the player picker in player view) and the theme picker. It's a native modal `<dialog>`, so focus stays inside
 * while it's open and goes back to the menu button when it closes (#51).
 */
export function MenuDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { shown, props } = useModalDialog(open, onClose)

  return (
    <dialog {...props} className="app-drawer" aria-label="Menu">
      {shown && (
        <div className="app-drawer__body">
          <div className="app-drawer__head">
            <span className="app-drawer__title">Menu</span>
            <button type="button" className="icon-button" aria-label="Close menu" onClick={onClose}>
              <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                <path d="M6 6l12 12M18 6L6 18" />
              </svg>
            </button>
          </div>
          <NavLink to={aboutItem.to} className="nav-item" onClick={onClose}>
            <NavIcon icon={aboutItem.icon} tone={aboutItem.tone} />
            <span className="nav-item__label">{aboutItem.label}</span>
          </NavLink>
          <div className="app-drawer__section">
            <span className="nav-cards__title">Viewing as</span>
            <RoleSwitcher />
          </div>
          <div className="app-drawer__section app-drawer__row">
            <span className="nav-cards__title">Theme</span>
            <ThemePicker />
          </div>
        </div>
      )}
    </dialog>
  )
}
