import { useState } from 'react'
import { errorMessage } from '../api/client'
import { useObjectiveCandidates, usePinObjective } from '../api/queries'
import type { Realm, RealmObjective } from '../api/types'

interface Props {
  realm: Realm
  /** The objective the user clicked, so the page can show its event on the map. */
  onShow: (objective: RealmObjective) => void
  /** The event currently highlighted on the map, as `areaId/eventId`. */
  highlighted?: string
}

/**
 * The realm's goals. Each event objective links to its event on the map. A GM can pin any candidate objective; the pin
 * lives on the realm, never on the map. The GM records completions, as often as parties visit, in the fold below it.
 */
export function ObjectivesPanel({ realm, onShow, highlighted }: Props) {
  const { summary, document } = realm
  const objectives = document.objectives ?? []
  const pinned = summary.pinnedObjective ?? undefined
  const [choosing, setChoosing] = useState(false)
  const pin = usePinObjective(summary.id)

  return (
    <div className="objectives">
      {objectives.length > 0 ? (
        <p className="muted">Any of these can be completed, by any party, as often as the realm is run.</p>
      ) : (
        <p className="muted">This realm was woven before objectives existed (generator {summary.generatorVersion}). A GM can still pin one.</p>
      )}
      {summary.isMirror && (
        <p className="objectives__mirror">This is a mirror, so its objectives were rolled for this weave and may differ from the recorded map's.</p>
      )}

      {pinned && (
        <div className="objectives__pinned">
          <span className="badge">GM pin</span>
          <ObjectiveButton objective={pinned} onShow={onShow} highlighted={highlighted} />
          <button type="button" className="button--quiet" onClick={() => pin.mutate(null)} disabled={pin.isPending}>
            Unpin
          </button>
        </div>
      )}

      {objectives.length > 0 && (
        <ul className="objectives__list">
          {objectives.map((o) => (
            <li key={o.key} className={pinned?.key === o.key ? 'objectives__item--pinned' : undefined}>
              <ObjectiveButton objective={o} onShow={onShow} highlighted={highlighted} />
            </li>
          ))}
        </ul>
      )}

      {choosing ? (
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
    </div>
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

function whereOf(o: RealmObjective) {
  return o.eventId ? `${o.eventId} ${o.eventName ?? ''} · ${o.location ?? ''}`.replace(/\s+/g, ' ').trim() : 'Whole realm'
}
