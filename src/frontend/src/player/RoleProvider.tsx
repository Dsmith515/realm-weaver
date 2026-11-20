import { useState, type ReactNode } from 'react'
import { loadRole, RoleContext, saveRole, type Role } from './roleContext'

/** Remembers whether the app is viewed as the GM or as a player. Starts as GM: Realm Weaver is a GM tool first. */
export function RoleProvider({ children }: { children: ReactNode }) {
  const [role, setChosen] = useState<Role>(loadRole)

  const setRole = (next: Role) => {
    saveRole(next)
    setChosen(next)
  }

  return <RoleContext.Provider value={{ role, setRole }}>{children}</RoleContext.Provider>
}
