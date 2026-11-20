import { useEffect, useState, type ReactNode } from 'react'
import { loadTheme, saveTheme, ThemeContext, type ThemeId } from './themeContext'

/** Remembers the chosen palette and mirrors it onto <html data-theme>, where index.css picks it up. */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setChosen] = useState<ThemeId>(loadTheme)

  useEffect(() => {
    document.documentElement.dataset.theme = theme
  }, [theme])

  const setTheme = (next: ThemeId) => {
    saveTheme(next)
    setChosen(next)
  }

  return <ThemeContext.Provider value={{ theme, setTheme }}>{children}</ThemeContext.Provider>
}
