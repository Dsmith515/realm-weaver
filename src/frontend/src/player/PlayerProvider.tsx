import { useState, type ReactNode } from 'react'
import { usePlayers } from '../api/queries'
import { loadPlayerId, PlayerContext, savePlayerId } from './playerContext'

/** Remembers the chosen player, and falls back to the first player when there's no (valid) choice. */
export function PlayerProvider({ children }: { children: ReactNode }) {
  const [chosen, setChosen] = useState<string | undefined>(loadPlayerId)
  const players = usePlayers()

  const list = players.data
  const playerId = list && list.length > 0 && !list.some((p) => p.id === chosen) ? list[0].id : chosen

  const setPlayerId = (id: string | undefined) => {
    savePlayerId(id)
    setChosen(id)
  }

  return <PlayerContext.Provider value={{ playerId, setPlayerId }}>{children}</PlayerContext.Provider>
}
