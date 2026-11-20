import { isThemeId, themes, useTheme } from './themeContext'

export function ThemePicker() {
  const { theme, setTheme } = useTheme()

  return (
    <label className="theme-picker">
      <span className="theme-picker__label">Theme</span>
      <select
        value={theme}
        onChange={(e) => {
          if (isThemeId(e.target.value)) setTheme(e.target.value)
        }}
      >
        {themes.map((t) => (
          <option key={t.id} value={t.id}>
            {t.label}
          </option>
        ))}
      </select>
    </label>
  )
}
