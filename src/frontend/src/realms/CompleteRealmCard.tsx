import { useState } from 'react'
import { errorMessage } from '../api/client'
import { useCompleteRun, usePlayers } from '../api/queries'
import type { Realm, RunCompletion } from '../api/types'

const skipReasons: Record<string, string> = {
  'already-held': 'already holds it',
  'unknown-player': 'no longer exists',
  'unique-held': 'can’t have it: the map is Unique and someone else holds it',
  'map-missing': 'can’t have it: the map was deleted',
}

/**
 * GM: closes the run and hands its map to the players who should keep it, in one step (ADR 0010). The objective is
 * optional, so a GM can also close a run the party abandoned. Rituals never grant maps, so this (or a plain assign on
 * the Components page) is how a map reaches a player.
 */
export function CompleteRealmCard({ realm }: { realm: Realm }) {
  const { summary, document } = realm
  const players = usePlayers()
  const complete = useCompleteRun(summary.id)
  const objectives = [...(summary.pinnedObjective ? [summary.pinnedObjective] : []), ...(document.objectives ?? []).filter((o) => o.key !== summary.pinnedObjective?.key)]
  const [objectiveKey, setObjectiveKey] = useState('')
  const [recipients, setRecipients] = useState<string[]>(summary.playerId ? [summary.playerId] : [])
  const nameOf = (id: string) => players.data?.find((p) => p.id === id)?.name ?? 'Unknown player'

  if (summary.completion) {
    const { objective, mapGivenTo } = summary.completion
    return (
      <section className="card complete-realm" aria-label="Completion">
        <p className="banner banner--ok" role="status">
          <strong>Run complete{objective ? `: ${objective.title}` : ''}.</strong>{' '}
          {mapGivenTo.length > 0 ? `Map handed to ${mapGivenTo.map(nameOf).join(', ')}.` : 'No one was handed the map.'}
          {complete.data && <SkippedText result={complete.data} nameOf={nameOf} />}
        </p>
      </section>
    )
  }

  const toggle = (id: string, on: boolean) => setRecipients((current) => (on ? [...current, id] : current.filter((r) => r !== id)))

  return (
    <section className="card complete-realm" aria-label="Completion">
      <h2>Complete the run (GM)</h2>
      <p className="muted">Mark the run done and hand its map to the players who earned it. They can share or trade it later.</p>
      <form
        className="form"
        aria-label="Complete the run"
        onSubmit={(e) => {
          e.preventDefault()
          complete.mutate({ objectiveKey: objectiveKey || null, recipientPlayerIds: recipients })
        }}
      >
        <label className="field__label">
          <span>Objective completed</span>
          <select value={objectiveKey} onChange={(e) => setObjectiveKey(e.target.value)}>
            <option value="">None (just close the run)</option>
            {objectives.map((o) => (
              <option key={o.key} value={o.key}>
                {o.title}
                {o.key === summary.pinnedObjective?.key ? ' (GM pin)' : ''}
              </option>
            ))}
          </select>
        </label>
        <fieldset className="complete-realm__players">
          <legend>Hand the map to</legend>
          {players.isPending && <p className="muted">Loading players…</p>}
          {players.data?.map((p) => (
            <label key={p.id} className="checkbox">
              <input type="checkbox" checked={recipients.includes(p.id)} onChange={(e) => toggle(p.id, e.target.checked)} />
              {p.name}
              {p.id === summary.playerId && <span className="muted"> (weaver)</span>}
            </label>
          ))}
        </fieldset>
        <div className="actions">
          <button type="submit" className="button--primary" disabled={complete.isPending}>
            {recipients.length > 0 ? 'Complete and hand out the map' : 'Complete the run'}
          </button>
        </div>
        {complete.isError && <p className="error-text">{errorMessage(complete.error)}</p>}
      </form>
    </section>
  )
}

function SkippedText({ result, nameOf }: { result: RunCompletion; nameOf: (id: string) => string }) {
  if (result.skipped.length === 0) return null
  return <> {result.skipped.map((s) => `${nameOf(s.playerId)} ${skipReasons[s.reason] ?? `was skipped (${s.reason})`}.`).join(' ')}</>
}
