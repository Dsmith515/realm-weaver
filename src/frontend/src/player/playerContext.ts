import { createContext, useContext } from 'react'

export interface PlayerSelection {
  playerId: string | undefined
  setPlayerId: (id: string | undefined) => void
}

export const PlayerContext = createContext<PlayerSelection | undefined>(undefined)

/** The player chosen in the header's switcher. There is no login yet (roadmap v1 default). */
export function usePlayerSelection(): PlayerSelection {
  const value = useContext(PlayerContext)
  if (!value) throw new Error('usePlayerSelection must be used inside <PlayerProvider>.')
  return value
}

const storageKey = 'realm-weaver.player'

export function loadPlayerId(): string | undefined {
  try {
    return localStorage.getItem(storageKey) ?? undefined
  } catch {
    return undefined
  }
}

export function savePlayerId(id: string | undefined) {
  try {
    if (id) localStorage.setItem(storageKey, id)
    else localStorage.removeItem(storageKey)
  } catch {
    // Private mode or blocked storage: the choice just won't survive a reload.
  }
}
