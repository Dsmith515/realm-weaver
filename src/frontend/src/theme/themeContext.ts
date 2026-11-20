import { createContext, useContext } from 'react'

/** The palettes in index.css. The ids match its `[data-theme='…']` blocks. */
export const themes = [
  { id: 'ember', label: 'Ink & Ember' },
  { id: 'arcana', label: 'Gilded Arcana' },
  { id: 'gilded', label: 'Gilded Night' },
  { id: 'emerald', label: 'Emerald Night' },
  { id: 'abyss', label: 'Abyssal Teal' },
  { id: 'arcane', label: 'Arcane' },
  { id: 'parchment', label: 'Parchment (light)' },
] as const

export type ThemeId = (typeof themes)[number]['id']

export const defaultTheme: ThemeId = 'ember'

export interface ThemeSelection {
  theme: ThemeId
  setTheme: (theme: ThemeId) => void
}

export const ThemeContext = createContext<ThemeSelection | undefined>(undefined)

export function useTheme(): ThemeSelection {
  const value = useContext(ThemeContext)
  if (!value) throw new Error('useTheme must be used inside <ThemeProvider>.')
  return value
}

/** Also read by the inline script in index.html, which applies the theme before React renders. */
export const themeStorageKey = 'realm-weaver.theme'

export function isThemeId(value: unknown): value is ThemeId {
  return themes.some((t) => t.id === value)
}

export function loadTheme(): ThemeId {
  try {
    const saved = localStorage.getItem(themeStorageKey)
    return isThemeId(saved) ? saved : defaultTheme
  } catch {
    return defaultTheme
  }
}

export function saveTheme(theme: ThemeId) {
  try {
    localStorage.setItem(themeStorageKey, theme)
  } catch {
    // Private mode or blocked storage: the choice just won't survive a reload.
  }
}
