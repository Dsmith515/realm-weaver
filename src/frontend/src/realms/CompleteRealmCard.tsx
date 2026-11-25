import { useState } from 'react'
import { errorMessage } from '../api/client'
import { type HandOutResult, rosterPageSize, useCompleteRun, useCompletions, useHandOutMap, useMapDetails, useRealmRoster } from '../api/queries'
import type { Realm, RealmObjective, RealmSummary, RosterPlayer, RunCompletion } from '../api/types'
import { Pager } from '../components/Pager'

const skipReasons: Record<string, string> = {
  'already-held': 'already holds it',
  'unique-held': 'can’t have it: the map is Unique and someone else holds it',
  'map-missing': 'can’t have it: the map was deleted',
}

/** Has nothing left to get here: completed an objective and holds the map. Hidden from the table unless asked for. */
const isDone = (p: RosterPlayer) => p.completions > 0 && p.holdsMap

/**
 * GM: records that a party completed an objective, or just hands out the map (ADR 0011). Repeatable: a realm can be
 * visited again, so any party can complete it as often as the GM runs it. Rituals never grant maps, so this (or an
 * assign on the Components page) is how a map reaches a player.
 */
export function CompleteRealmCard({ realm }: { realm: Realm }) {
  const { summary, document } = realm
  const map = useMapDetails(summary.mapComponentId)
  const complete = useCompleteRun(summary.id)
  const handOut = useHandOutMap(summary.mapComponentId)
  const objectives = [...(summary.pinnedObjective ? [summary.pinnedObjective] : []), ...(document.objectives ?? []).filter((o) => o.key !== summary.pinnedObjective?.key)]
  const [objectiveKey, setObjectiveKey] = useState('')
  const [search, setSearch] = useState('')
  const [all, setAll] = useState(false)
  const [page, setPage] = useState(1)
  const roster = useRealmRoster(summary.id, { search: search.trim(), all, page })
  const [picked, setPicked] = useState<Map<string, RosterPlayer> | undefined>()
  const [giveMap, setGiveMap] = useState(true)
  const [keeper, setKeeper] = useState('')
  // Names of the players just submitted, for the result banners (the selection resets afterwards).
  const [submittedNames, setSubmittedNames] = useState(new Map<string, string>())

  // The weaver starts selected, unless they've already completed it and hold the map.
  const weaver = roster.data?.weaver ?? undefined
  const selected = picked ?? new Map(weaver && !isDone(weaver) ? [[weaver.id, weaver]] : [])
  // The newest status for anyone on this page.
  const rows = roster.data?.items ?? []
  const fresh = (p: RosterPlayer) => rows.find((r) => r.id === p.id) ?? (weaver?.id === p.id ? weaver : p)
  const chosen = [...selected.values()].map(fresh)
  const withoutMap = chosen.filter((p) => !p.holdsMap)
  const unique = map.data?.map.map?.flags?.unique ?? false
  const keeperId = chosen.some((p) => p.id === keeper) ? keeper : (chosen[0]?.id ?? '')
  const chosenObjective = objectiveKey || objectives[0]?.key || ''
  const busy = complete.isPending || handOut.isPending

  const toggle = (players: RosterPlayer[], on: boolean) => {
    const next = new Map(selected)
    for (const p of players) {
      if (on) next.set(p.id, p)
      else next.delete(p.id)
    }
    setPicked(next)
  }

  const remember = (players: RosterPlayer[]) => setSubmittedNames(new Map(players.map((p) => [p.id, p.name])))
  const reset = () => setPicked(undefined)

  const submit = () => {
    remember(chosen)
    handOut.reset()
    complete.mutate(
      {
        objectiveKey: chosenObjective,
        playerIds: chosen.map((p) => p.id),
        // Leaving recipients out hands the map to everyone selected who doesn't hold it yet.
        mapRecipientIds: !giveMap ? [] : unique ? [keeperId] : null,
      },
      { onSuccess: reset },
    )
  }

  const assign = () => {
    const recipients = unique ? withoutMap.filter((p) => p.id === keeperId) : withoutMap
    remember(recipients)
    complete.reset()
    handOut.mutate(
      recipients.map((p) => p.id),
      { onSuccess: reset },
    )
  }

  // Selected players not on this page stay in view at the top, so it's clear who'll be included.
  const offPage = chosen.filter((p) => !rows.some((r) => r.id === p.id))
  const pageAllSelected = rows.length > 0 && rows.every((p) => selected.has(p.id))
  const assignTargets = unique ? (withoutMap.some((p) => p.id === keeperId) ? 1 : 0) : withoutMap.length

  return (
    <section className="complete-realm" aria-label="Completion">
      <p className="muted">
        Tick the players who ran the realm, then complete an objective for them, or just assign the map. Any party can complete this realm again later.
      </p>
      <form
        className="form complete-realm__form"
        aria-label="Complete an objective"
        onSubmit={(e) => {
          e.preventDefault()
          submit()
        }}
      >
        <label className="field__label">
          <span>Objective</span>
          <select value={chosenObjective} onChange={(e) => setObjectiveKey(e.target.value)}>
            {objectives.map((o) => (
              <option key={o.key} value={o.key}>
                {objectiveOption(o, summary)}
                {o.key === summary.pinnedObjective?.key ? ' (GM pin)' : ''}
              </option>
            ))}
          </select>
        </label>

        <fieldset className="roster">
          <legend>Players</legend>
          <div className="roster__filters">
            <input
              type="search"
              aria-label="Search players"
              placeholder="Search players"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value)
                setPage(1)
              }}
            />
            <label className="checkbox switch">
              <input
                type="checkbox"
                role="switch"
                checked={all}
                onChange={(e) => {
                  setAll(e.target.checked)
                  setPage(1)
                }}
              />
              Include players who completed it and hold the map
            </label>
          </div>
          <div className="roster__table">
            <table className="table" aria-label="Players">
              <thead>
                <tr>
                  <th>
                    <label className="checkbox">
                      <input
                        type="checkbox"
                        aria-label="Select everyone on this page"
                        checked={pageAllSelected}
                        disabled={rows.length === 0}
                        onChange={(e) => toggle(rows, e.target.checked)}
                      />
                      Player
                    </label>
                  </th>
                  <th>Completed</th>
                  <th>Map</th>
                </tr>
              </thead>
              <tbody>
                {offPage.map((p) => (
                  <RosterRow key={p.id} player={p} checked onToggle={(on) => toggle([p], on)} className="roster__pinned" />
                ))}
                {rows.map((p) => (
                  <RosterRow key={p.id} player={p} checked={selected.has(p.id)} onToggle={(on) => toggle([p], on)} />
                ))}
                {roster.data && rows.length === 0 && (
                  <tr>
                    <td colSpan={3} className="muted">
                      {search ? 'No players match.' : all ? 'No players yet.' : 'Everyone has completed it and holds the map.'}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          {roster.isPending && <p className="muted">Loading players…</p>}
          {roster.isError && <p className="error-text">{errorMessage(roster.error)}</p>}
          {roster.data && roster.data.totalCount > rosterPageSize && (
            <Pager page={roster.data.page} pageSize={roster.data.pageSize} totalCount={roster.data.totalCount} noun="players" onPage={setPage} />
          )}
          <p className="roster__selected" aria-live="polite">
            {chosen.length === 0 ? 'No one selected.' : `${chosen.length} selected${withoutMap.length > 0 ? `, ${withoutMap.length} without the map` : ''}.`}
            {chosen.length > 0 && (
              <button type="button" className="button--quiet" onClick={() => setPicked(new Map())}>
                Clear
              </button>
            )}
          </p>
        </fieldset>

        <label className="checkbox switch">
          <input type="checkbox" role="switch" checked={giveMap} onChange={(e) => setGiveMap(e.target.checked)} />
          When completing, hand the map to selected players who don’t have it yet
        </label>
        {unique && withoutMap.length > 1 && (
          <label className="field__label">
            <span>This map is Unique. Who keeps it?</span>
            <select value={keeperId} onChange={(e) => setKeeper(e.target.value)}>
              {chosen.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
        )}
        <div className="actions">
          <button type="submit" className="button--primary" disabled={busy || chosen.length === 0 || !chosenObjective}>
            Complete objective
          </button>
          <button
            type="button"
            onClick={assign}
            disabled={busy || assignTargets === 0}
            title="Give the map to the selected players who don’t hold it, without recording a completion"
          >
            Assign map{assignTargets > 0 ? ` (${assignTargets})` : ''}
          </button>
        </div>
        {complete.isError && <p className="error-text">{errorMessage(complete.error)}</p>}
        {handOut.isError && <p className="error-text">{errorMessage(handOut.error)}</p>}
        {complete.data && <CompletedBanner result={complete.data} names={submittedNames} />}
        {handOut.data && <HandOutBanner result={handOut.data} names={submittedNames} />}
      </form>
    </section>
  )
}

function RosterRow({ player, checked, onToggle, className }: { player: RosterPlayer; checked: boolean; onToggle: (on: boolean) => void; className?: string }) {
  return (
    <tr className={className}>
      <td>
        <label className="checkbox">
          <input type="checkbox" checked={checked} onChange={(e) => onToggle(e.target.checked)} />
          {player.name}
          {player.isWeaver && <span className="muted"> (weaver)</span>}
        </label>
      </td>
      <td>{player.completions > 0 ? `${player.completions}×` : <span className="muted">Not yet</span>}</td>
      <td>{player.holdsMap ? <span className="badge badge--ok">Has map</span> : <span className="muted">No map</span>}</td>
    </tr>
  )
}

/** "E4 · Destroy the nest", with the area when the realm has several, so two objectives with one name can be told apart. */
function objectiveOption(o: RealmObjective, summary: RealmSummary) {
  if (!o.eventId) return `${o.title} (whole realm)`
  const area = summary.areas.length > 1 ? summary.areas.find((a) => a.id === o.areaId) : undefined
  const where = area ? ` (${area.kind === 'field' ? 'Field' : `Floor ${area.floorNumber}`})` : ''
  return `${o.eventId} · ${o.title}${where}`
}

function CompletedBanner({ result, names }: { result: RunCompletion; names: Map<string, string> }) {
  const nameOf = (id: string) => names.get(id) ?? 'Unknown player'
  return (
    <p className="banner banner--ok" role="status">
      <strong>
        {result.objective.title} completed{result.isFirst ? ', and they’re the first to complete this realm' : ''}.
      </strong>{' '}
      {result.mapGivenTo.length > 0 ? `Map handed to ${result.mapGivenTo.map(nameOf).join(', ')}.` : 'No one new received the map.'}
      {result.skipped.length > 0 && ` ${result.skipped.map((s) => `${nameOf(s.playerId)} ${skipReasons[s.reason] ?? `was skipped (${s.reason})`}.`).join(' ')}`}
    </p>
  )
}

function HandOutBanner({ result, names }: { result: HandOutResult; names: Map<string, string> }) {
  const nameOf = (id: string) => names.get(id) ?? 'Unknown player'
  return (
    <p className={`banner ${result.failed.length > 0 ? 'banner--warn' : 'banner--ok'}`} role="status">
      {result.given.length > 0 ? `Map assigned to ${result.given.map(nameOf).join(', ')}. No completion was recorded.` : 'No one received the map.'}
      {result.failed.map((f) => ` ${nameOf(f.playerId)}: ${f.message}`).join('')}
    </p>
  )
}

/** Everyone who has completed something on this realm's map, and the history. */
export function PlayerCompletion({ realmId }: { realmId: string }) {
  const completions = useCompletions(realmId)
  const data = completions.data
  if (completions.isError) return <p className="error-text">{errorMessage(completions.error)}</p>
  if (!data) return null
  if (data.completions.length === 0) return <p className="muted">No one has completed an objective here yet.</p>

  return (
    <div className="player-completion">
      <ul className="player-completion__players" aria-label="Players who completed an objective">
        {data.players.map((p) => (
          <li key={p.id}>
            {p.name}
            {p.inFirstCompletion && (
              <span className="badge badge--ok" title="In the first party to complete this realm">
                First to complete
              </span>
            )}
            <span className="muted">
              {' '}
              {p.completions} {p.completions === 1 ? 'objective' : 'objectives'} · since {new Date(p.firstCompletedAt).toLocaleDateString()}
            </span>
          </li>
        ))}
      </ul>
      <h4>History</h4>
      <ol className="player-completion__history" aria-label="Completion history">
        {data.completions.map((c) => (
          <li key={c.id}>
            <strong>{c.objective.title}</strong>
            {c.isFirst && <span className="badge badge--ok">First</span>}
            {c.realmId !== realmId && <span className="badge">Another run</span>}
            <span className="muted"> {new Date(c.completedAt).toLocaleString()}</span>
            <br />
            {c.players.map((p) => `${p.name}${p.mapGiven ? ' (got the map)' : ''}`).join(', ')}
          </li>
        ))}
      </ol>
    </div>
  )
}
