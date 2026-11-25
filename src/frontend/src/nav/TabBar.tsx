import { useRef, useState, type MouseEvent, type PointerEvent } from 'react'
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
  const { panel, handlers: swipeHandlers, onClickHandle } = useSwipeDown(close)
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
          <div ref={panel} className="app-sheet__body">
            {/* Drag this strip down to close the sheet. The bar is also a button, for a tap or the keyboard. */}
            <div className="app-sheet__grab" data-testid="sheet-grab" {...swipeHandlers}>
              <button type="button" className="app-sheet__handle" aria-label="Close" onClick={onClickHandle}>
                <span aria-hidden="true" />
              </button>
              <span className="nav-cards__title">{group.label}</span>
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

/** How far down (px) a drag has to go before letting go closes the sheet. */
const closeDistance = 80
/** Movement (px) below which a press still counts as a tap on the handle. */
const tapSlop = 6

/**
 * Swipe-down-to-close for a bottom sheet. While dragging, the panel follows the finger (downwards only). Let go past
 * closeDistance and it closes; otherwise it springs back. A drag never also counts as a tap on the handle.
 *
 * The pointer is captured only once a press has moved past tapSlop. Capturing sends every later pointer event to the
 * strip, including the click that ends the press. Capturing on pointerdown would send a plain tap's click to the strip
 * instead of the handle button, and the tap would do nothing.
 */
function useSwipeDown(onClose: () => void) {
  const sheet = useRef<HTMLDivElement>(null)
  const drag = useRef<{ startY: number; dy: number } | undefined>(undefined)
  const dragged = useRef(false)

  const move = (dy: number) => {
    const el = sheet.current
    if (el) el.style.transform = dy > 0 ? `translateY(${dy}px)` : ''
  }

  const end = () => {
    const dy = drag.current?.dy ?? 0
    drag.current = undefined
    sheet.current?.classList.remove('app-sheet--dragging')
    move(0)
    // A drag's click goes to the strip, not the handle, so onClickHandle may never clear the flag. Clear it once that
    // click has had its turn, or the next keyboard press on the handle would be swallowed.
    if (dragged.current) setTimeout(() => (dragged.current = false))
    if (dy > closeDistance) onClose()
  }

  return {
    /** Goes on the sheet's panel: the part that moves. */
    panel: sheet,
    handlers: {
      onPointerDown: (event: PointerEvent<HTMLDivElement>) => {
        drag.current = { startY: event.clientY, dy: 0 }
        dragged.current = false
        sheet.current?.classList.add('app-sheet--dragging')
      },
      onPointerMove: (event: PointerEvent<HTMLDivElement>) => {
        const current = drag.current
        if (!current) return
        current.dy = Math.max(0, event.clientY - current.startY)
        if (current.dy > tapSlop && !dragged.current) {
          // Now it's a drag: keep getting its events even if the finger leaves the strip.
          dragged.current = true
          event.currentTarget.setPointerCapture?.(event.pointerId)
        }
        move(current.dy)
      },
      onPointerUp: end,
      onPointerCancel: end,
    },
    onClickHandle: (event: MouseEvent) => {
      if (dragged.current) {
        event.preventDefault()
        dragged.current = false
        return
      }
      onClose()
    },
  }
}
