import { useState } from 'react'
import { errorMessage } from '../api/client'
import { useCompleteRun, useCompletions, useMapDetails, usePlayers } from '../api/queries'
import type { Player, Realm, RunCompletion } from '../api/types'
import { PlayerChecklist } from '../components/PlayerPicker'

const skipReasons: Record<string, string> = {
  'already-held': 'already holds it',
  'unique-held': 'can’t have it: the map is Unique and someone else holds it',
  'map-missing': 'can’t have it: the map was deleted',
}

/**
 * GM: records that a party completed an objective and hands the map to the players who don't have it yet (ADR 0011).
 * Repeatable: a realm can be visited again, so any party can complete it as often as the GM runs it. Rituals never
 * grant maps, so this (or a plain assign on the Components page) is how a map reaches a player.
 */
export function CompleteRealmCard({ realm }: { realm: Realm }) {
  const { summary, document } = realm
  const players = usePlayers()
  const map = useMapDetails(summary.mapComponentId)
  const complete = useCompleteRun(summary.id)
  const objectives = [...(summary.pinnedObjective ? [summary.pinnedObjective] : []), ...(document.objectives ?? []).filter((o) => o.key !== summary.pinnedObjective?.key)]
  const [objectiveKey, setObjectiveKey] = useState('')
  const [party, setParty] = useState<Player[] | undefined>()
  const [handOut, setHandOut] = useState(true)
  const [keeper, setKeeper] = useState('')
  // Names of the party just submitted, for the result banner (the party list resets after a completion).
  const [submittedNames, setSubmittedNames] = useState(new Map<string, string>())

  // The weaver joins the party by default once the players have loaded.
  const weaver = players.data?.find((p) => p.id === summary.playerId)
  const chosen = party ?? (weaver ? [weaver] : [])
  const unique = map.data?.map.map?.flags?.unique ?? false
  const keeperId = chosen.some((p) => p.id === keeper) ? keeper : (chosen[0]?.id ?? '')
  const chosenObjective = objectiveKey || objectives[0]?.key || ''

  const submit = () => {
    setSubmittedNames(new Map(chosen.map((p) => [p.id, p.name])))
    complete.mutate(
      {
        objectiveKey: chosenObjective,
        playerIds: chosen.map((p) => p.id),
        // Leaving recipients out hands the map to everyone in the party who doesn't hold it yet.
        mapRecipientIds: !handOut ? [] : unique ? [keeperId] : null,
      },
      { onSuccess: () => setParty(undefined) },
    )
  }

  return (
    <section className="card complete-realm" aria-label="Completion">
      <h2>Complete an objective (GM)</h2>
      <p className="muted">
        Mark an objective done for the players who ran it. Any party can complete this realm again later, and only players without the map receive
        it.
      </p>
      <form
        className="form"
        aria-label="Complete an objective"
        onSubmit={(e) => {
          e.preventDefault()
          submit()
        }}
      >
        <label className="field__label">
          <span>Objective completed</span>
          <select value={chosenObjective} onChange={(e) => setObjectiveKey(e.target.value)}>
            {objectives.map((o) => (
              <option key={o.key} value={o.key}>
                {o.title}
                {o.key === summary.pinnedObjective?.key ? ' (GM pin)' : ''}
              </option>
            ))}
          </select>
        </label>
        <PlayerChecklist legend="Players who completed it" chosen={chosen} onChange={setParty} note={(p) => (p.id === summary.playerId ? '(weaver)' : undefined)} />
        <label className="checkbox">
          <input type="checkbox" checked={handOut} onChange={(e) => setHandOut(e.target.checked)} />
          Hand the map to players who don’t have it yet
        </label>
        {handOut && unique && chosen.length > 1 && (
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
          <button type="submit" className="button--primary" disabled={complete.isPending || chosen.length === 0 || !chosenObjective}>
            Complete objective
          </button>
        </div>
        {complete.isError && <p className="error-text">{errorMessage(complete.error)}</p>}
        {complete.data && <CompletedBanner result={complete.data} names={submittedNames} />}
      </form>
      <PlayerCompletion realmId={summary.id} />
    </section>
  )
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

/** Everyone who has completed something on this realm's map, and the history. Collapsed so it doesn't crowd the page. */
function PlayerCompletion({ realmId }: { realmId: string }) {
  const completions = useCompletions(realmId)
  const data = completions.data
  if (completions.isError) return <p className="error-text">{errorMessage(completions.error)}</p>
  if (!data) return null
  if (data.completions.length === 0) return <p className="muted">No one has completed an objective here yet.</p>

  return (
    <details className="player-completion">
      <summary>
        Player completion ({data.players.length} {data.players.length === 1 ? 'player' : 'players'}, {data.completions.length}{' '}
        {data.completions.length === 1 ? 'completion' : 'completions'})
      </summary>
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
      <h3>History</h3>
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
    </details>
  )
}
