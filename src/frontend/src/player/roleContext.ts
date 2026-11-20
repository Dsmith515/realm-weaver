import { createContext, useContext } from 'react'

/**
 * Who the app is showing itself to. This is a *preview* switch, not security: there is no login yet, and the API
 * doesn't check roles (ADR 0012). When authentication arrives, the role comes from the signed-in user instead and
 * nothing that calls useRole() has to change.
 */
export type Role = 'gm' | 'player'

export const defaultRole: Role = 'gm'

export interface RoleSelection {
  role: Role
  setRole: (role: Role) => void
}

export const RoleContext = createContext<RoleSelection | undefined>(undefined)

/** The role chosen in the header's "Viewing as" switch. */
export function useRole(): RoleSelection {
  const value = useContext(RoleContext)
  if (!value) throw new Error('useRole must be used inside <RoleProvider>.')
  return value
}

const storageKey = 'realm-weaver.role'

export const isRole = (value: unknown): value is Role => value === 'gm' || value === 'player'

export function loadRole(): Role {
  try {
    const saved = localStorage.getItem(storageKey)
    return isRole(saved) ? saved : defaultRole
  } catch {
    return defaultRole
  }
}

export function saveRole(role: Role) {
  try {
    localStorage.setItem(storageKey, role)
  } catch {
    // Private mode or blocked storage: the choice just won't survive a reload.
  }
}
