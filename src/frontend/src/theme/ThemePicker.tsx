import { useId } from 'react'
import { themes, useTheme } from './themeContext'

export function ThemePicker() {
  const { theme, setTheme } = useTheme()
  const menuId = useId()

  return (
    <>
      <button type="button" className="theme-picker" popoverTarget={menuId} aria-label="Theme" title="Theme">
        <svg aria-hidden="true" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="12" cy="12" r="9" />
          <path d="M12 3a9 9 0 0 1 0 18z" fill="currentColor" stroke="none" />
        </svg>
      </button>

      <div id={menuId} popover="auto" className="theme-menu">
        <fieldset>
          <legend>Theme</legend>
          {themes.map((t) => (
            <label key={t.id} className="theme-menu__option">
              <input
                type="radio"
                className="visually-hidden"
                name={menuId}
                value={t.id}
                checked={theme === t.id}
                onChange={() => setTheme(t.id)}
              />
              <span className="theme-swatch" data-theme={t.id} aria-hidden="true" />
              {t.label}
            </label>
          ))}
        </fieldset>
      </div>
    </>
  )
}
