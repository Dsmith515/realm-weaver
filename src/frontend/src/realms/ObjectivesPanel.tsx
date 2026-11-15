import { useState } from 'react'
import { errorMessage } from '../api/client'
import { useCompleteRun, useObjectiveCandidates, usePinObjective } from '../api/queries'
import type { Realm, RealmObjective } from '../api/types'

interface Props {
  realm: Realm
  /** The objective the user clicked, so the page can show its event on the map. */
  onShow: (objective: RealmObjective) => void
  /** The event currently highlighted on the map, as `areaId/eventId`. */
  highlighted?: string
}

/**
 * The run's goals: completing any one completes it. Each event objective links to its event on the map.
 * A GM can pin any candidate objective; the pin lives on the realm, never on the map. Completing the run records the map
 * into the weaver's inventory when they're a Cartographer.
 */
export function ObjectivesPanel({ realm, onShow, highlighted }: Props) {
  const { summary, document } = realm
  const objectives = document.objectives ?? []
  const pinned = summary.pinnedObjective ?? undefined
  const completion = summary.completion ?? undefined
  const [choosing, setChoosing] = useState(false)
  const pin = usePinObjective(summary.id)
  const complete = useCompleteRun(summary.id)
  const canComplete = !completion && !complete.isPending
  const completeButton = (o: RealmObjective) =>
    canComplete && (
      <button type="button" className="button--quiet" onClick={() => complete.mutate(o.key)} aria-label={`Complete: ${o.title}`}>
        Complete
      </button>
    )

  return (
    <section className="objectives" aria-label="Objectives">
      <h2>Objectives</h2>
      {objectives.length > 0 ? (
        <p className="muted">Complete any one to complete the run.</p>
      ) : (
        <p className="muted">This realm was woven before objectives existed (generator {summary.generatorVersion}). A GM can still pin one.</p>
      )}
      {summary.isMirror && (
        <p className="objectives__mirror">This is a mirror, so its objectives were rolled for this weave and may differ from the recorded map's.</p>
      )}

      {completion && (
        <p className="banner banner--ok" role="status">
          <strong>Run complete:</strong> {completion.objective.title}.{' '}
          {completion.mapRecorded
            ? 'A Cartographer recorded the map into the weaver’s inventory.'
            : reasonText(complete.data?.mapNotRecordedReason ?? undefined)}
        </p>
      )}

      {pinned && (
        <div className="objectives__pinned">
          <span className="badge">GM pin</span>
          <ObjectiveButton objective={pinned} onShow={onShow} highlighted={highlighted} />
          {completeButton(pinned)}
          {!completion && (
            <button type="button" className="button--quiet" onClick={() => pin.mutate(null)} disabled={pin.isPending}>
              Unpin
            </button>
          )}
        </div>
      )}

      {objectives.length > 0 && (
        <ul className="objectives__list">
          {objectives.map((o) => (
            <li
              key={o.key}
              className={[pinned?.key === o.key && 'objectives__item--pinned', completion?.objective.key === o.key && 'objectives__item--done'].filter(Boolean).join(' ') || undefined}
            >
              <ObjectiveButton objective={o} onShow={onShow} highlighted={highlighted} />
              {completeButton(o)}
            </li>
          ))}
        </ul>
      )}

      {completion ? null : choosing ? (
        <PinChooser
          realmId={summary.id}
          current={pinned?.key}
          busy={pin.isPending}
          onPin={(key) => pin.mutate(key, { onSuccess: () => setChoosing(false) })}
          onCancel={() => setChoosing(false)}
        />
      ) : (
        <button type="button" className="button--quiet" onClick={() => setChoosing(true)}>
          {pinned ? 'Pin a different objective…' : 'Pin an objective (GM)…'}
        </button>
      )}
      {pin.isError && <p className="error-text">{errorMessage(pin.error)}</p>}
      {complete.isError && <p className="error-text">{errorMessage(complete.error)}</p>}
    </section>
  )
}

function ObjectiveButton({ objective, onShow, highlighted }: { objective: RealmObjective; onShow: Props['onShow']; highlighted?: string }) {
  const target = objective.eventId ? `${objective.areaId}/${objective.eventId}` : undefined
  return (
    <button
      type="button"
      className="objectives__objective"
      aria-pressed={target !== undefined && highlighted === target}
      onClick={() => onShow(objective)}
      title={objective.description}
    >
      <strong>{objective.title}</strong>{' '}
      <span className="muted">{whereOf(objective)}</span>
    </button>
  )
}

function PinChooser({ realmId, current, busy, onPin, onCancel }: { realmId: string; current?: string; busy: boolean; onPin: (key: string) => void; onCancel: () => void }) {
  const candidates = useObjectiveCandidates(realmId, true)
  const [key, setKey] = useState(current ?? '')

  if (candidates.isPending) return <p className="muted">Loading objectives…</p>
  if (candidates.isError) return <p className="error-text">{errorMessage(candidates.error)}</p>

  return (
    <form
      className="objectives__chooser"
      aria-label="Pin an objective"
      onSubmit={(e) => {
        e.preventDefault()
        if (key) onPin(key)
      }}
    >
      <label className="field__label">
        <span>Objective to pin</span>
        <select value={key} onChange={(e) => setKey(e.target.value)}>
          <option value="">Choose…</option>
          {candidates.data.map((c) => (
            <option key={c.key} value={c.key}>
              {c.id !== 'P' ? `${c.id} · ` : ''}
              {c.title} ({whereOf(c)})
            </option>
          ))}
        </select>
      </label>
      <div className="actions">
        <button type="submit" className="button--primary" disabled={!key || key === current || busy}>
          Pin
        </button>
        <button type="button" className="button--quiet" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </form>
  )
}

/** Why completing the run didn't record a map. Without the reason (a reload), a neutral line. */
function reasonText(reason: string | undefined) {
  switch (reason) {
    case 'not-cartographer':
      return 'No map was recorded: the weaver isn’t a Cartographer. Train the skill or attune a needle with that boon next time.'
    case 'already-held':
      return 'The weaver already holds this map.'
    case 'gm-realm':
      return 'A GM Quick Weave has no weaver to record the map for; assign the map from the Components page.'
    default:
      return 'No new map was recorded.'
  }
}

function whereOf(o: RealmObjective) {
  return o.eventId ? `${o.eventId} ${o.eventName ?? ''} · ${o.location ?? ''}`.replace(/\s+/g, ' ').trim() : 'Whole realm'
}
