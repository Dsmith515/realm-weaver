import { useId, useState } from 'react'
import { usePlayers } from '../api/queries'
import type { Player } from '../api/types'

interface PlayerSelectProps {
  /** The select's accessible name. */
  label: string
  value: string
  onChange: (playerId: string) => void
  /** The chosen player, so they stay in the list while a search hides them. */
  selected?: Player
  className?: string
  /** Show the search box above the list. Defaults to true. */
  searchable?: boolean
}

/**
 * Pick one player: a search box narrows the list on the server, so it keeps working as the player base grows.
 * The chosen player stays listed even when the search doesn't match them.
 */
export function PlayerSelect({ label, value, onChange, selected, className, searchable = true }: PlayerSelectProps) {
  const [search, setSearch] = useState('')
  const players = usePlayers(search)
  const matches = players.data ?? []
  const options = selected && !matches.some((p) => p.id === selected.id) ? [selected, ...matches] : matches
  const selectId = useId()

  return (
    <span className={`player-select ${className ?? ''}`}>
      {searchable && (
        <input
          type="search"
          aria-label={`Search players for ${label.toLowerCase()}`}
          placeholder="Search players"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      )}
      <label className="visually-hidden" htmlFor={selectId}>
        {label}
      </label>
      <select id={selectId} value={value} onChange={(e) => onChange(e.target.value)} disabled={options.length === 0}>
        {options.length === 0 && <option value="">{search ? 'No matches' : 'No players'}</option>}
        {options.length > 0 && !value && <option value="">Choose a player</option>}
        {options.map((p) => (
          <option key={p.id} value={p.id}>
            {p.name}
          </option>
        ))}
      </select>
    </span>
  )
}

interface PlayerChecklistProps {
  legend: string
  /** Chosen players, kept in view (as checked) whatever the search shows. */
  chosen: Player[]
  onChange: (players: Player[]) => void
  /** A short note after a player's name, e.g. "(weaver)". */
  note?: (player: Player) => string | undefined
}

/** Pick several players: the chosen ones stay at the top, and a server-side search finds the rest. */
export function PlayerChecklist({ legend, chosen, onChange, note }: PlayerChecklistProps) {
  const [search, setSearch] = useState('')
  const players = usePlayers(search)
  const chosenIds = new Set(chosen.map((p) => p.id))
  const others = (players.data ?? []).filter((p) => !chosenIds.has(p.id))
  const toggle = (player: Player, on: boolean) => onChange(on ? [...chosen, player] : chosen.filter((p) => p.id !== player.id))

  const row = (p: Player) => (
    <label key={p.id} className="checkbox">
      <input type="checkbox" checked={chosenIds.has(p.id)} onChange={(e) => toggle(p, e.target.checked)} />
      {p.name}
      {note?.(p) && <span className="muted"> {note(p)}</span>}
    </label>
  )

  return (
    <fieldset className="player-checklist">
      <legend>{legend}</legend>
      <input type="search" aria-label={`Search players for ${legend.toLowerCase()}`} placeholder="Search players" value={search} onChange={(e) => setSearch(e.target.value)} />
      <div className="player-checklist__list">
        {chosen.map(row)}
        {players.isPending && <p className="muted">Loading players…</p>}
        {others.map(row)}
        {players.data && others.length === 0 && search && <p className="muted">No other players match.</p>}
      </div>
    </fieldset>
  )
}
