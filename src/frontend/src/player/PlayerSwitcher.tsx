import { useState, type FormEvent } from 'react'
import { errorMessage } from '../api/client'
import { useCreatePlayer, usePlayers } from '../api/queries'
import { PlayerSelect } from '../components/PlayerPicker'
import { usePlayerSelection } from './playerContext'

export function PlayerSwitcher() {
  const players = usePlayers()
  const { playerId, setPlayerId } = usePlayerSelection()
  const createPlayer = useCreatePlayer()
  const [adding, setAdding] = useState(false)
  const [name, setName] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const player = await createPlayer.mutateAsync(name)
    setPlayerId(player.id)
    setName('')
    setAdding(false)
  }

  if (adding) {
    return (
      <form className="player-switcher" onSubmit={(e) => void submit(e).catch(() => undefined)}>
        <label className="visually-hidden" htmlFor="new-player-name">
          New player name
        </label>
        <input id="new-player-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Player name" autoFocus maxLength={60} />
        <button type="submit" disabled={createPlayer.isPending}>
          Create
        </button>
        <button type="button" className="button--quiet" onClick={() => setAdding(false)}>
          Cancel
        </button>
        {createPlayer.isError && <span className="error-text">{errorMessage(createPlayer.error)}</span>}
      </form>
    )
  }

  return (
    <div className="player-switcher">
      <span className="player-switcher__label" aria-hidden="true">
        Player
      </span>
      <PlayerSelect label="Player" value={playerId ?? ''} onChange={(id) => setPlayerId(id || undefined)} selected={players.data?.find((p) => p.id === playerId)} />
      <button type="button" className="button--quiet" onClick={() => setAdding(true)}>
        New player
      </button>
    </div>
  )
}
