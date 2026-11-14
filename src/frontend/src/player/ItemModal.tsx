import { useState } from 'react'
import { Link } from 'react-router'
import { errorMessage } from '../api/client'
import { useMapDetails, useSaveNotes } from '../api/queries'
import type { DigestEntry, InventoryItem } from '../api/types'
import { label } from '../api/types'
import { describe } from '../catalog/describe'
import { Modal } from '../components/Modal'

interface ItemModalProps {
  playerId: string
  item: InventoryItem
  onClose: () => void
}

/** An owned item up close: a map's seed, recipe and contents, and the player's notes for any item. */
export function ItemModal({ playerId, item, onClose }: ItemModalProps) {
  const { component } = item
  return (
    <Modal title={component.name} onClose={onClose}>
      <h2>{component.name}</h2>
      <p className="muted">
        {label(component.type)} · {label(component.rarity)}
        {component.type !== 'map' && ` · ${describe(component)}`}
      </p>
      {component.description && <p>{component.description}</p>}
      {component.type === 'map' && <MapDetailsView mapId={component.id} />}
      <NotesForm key={component.id} playerId={playerId} item={item} />
      <div className="actions">
        <button type="button" className="button--quiet" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
  )
}

function MapDetailsView({ mapId }: { mapId: string }) {
  const details = useMapDetails(mapId)
  if (details.isPending) return <p className="muted">Loading the map…</p>
  if (details.isError) return <p className="error-text">{errorMessage(details.error)}</p>

  const { map, needle, thread, coating, originalRealm } = details.data
  const recipe = map.map?.recipe
  const digest = originalRealm?.digest
  return (
    <>
      <dl className="facts">
        <dt>Seed</dt>
        <dd>{map.map?.seed}</dd>
        <dt>Realm kind</dt>
        <dd>{recipe ? `${label(recipe.kind)}${recipe.floorCount > 0 ? ` (${recipe.floorCount} floor${recipe.floorCount > 1 ? 's' : ''})` : ''}` : '—'}</dd>
        <dt>Threat</dt>
        <dd>{map.map?.threat ?? originalRealm?.threat ?? '—'}</dd>
        <dt>Woven from</dt>
        <dd>{[needle?.name, thread?.name, coating?.name].filter(Boolean).join(' · ') || '—'}</dd>
      </dl>

      {!originalRealm && <p className="muted">No realm has been opened from this map yet.</p>}
      {digest && (
        <>
          <h3>Mobs</h3>
          {digest.mobs.length === 0 ? (
            <p className="muted">None.</p>
          ) : (
            <ul className="digest">
              {digest.mobs.map((m) => (
                <li key={m.archetypeId}>
                  {m.count}× {m.name}
                </li>
              ))}
            </ul>
          )}
          <DigestList title="Potential loot" entries={digest.loot} />
          <DigestList title="Resource nodes" entries={digest.resourceNodes} />
          <p>
            <Link to={`/realms/${originalRealm.realmId}`}>Open the original realm</Link>
          </p>
        </>
      )}
    </>
  )
}

function DigestList({ title, entries }: { title: string; entries: DigestEntry[] }) {
  return (
    <>
      <h3>{title}</h3>
      {entries.length === 0 ? (
        <p className="muted">None.</p>
      ) : (
        <ul className="digest">
          {entries.map((e) => (
            <li key={`${e.areaName}-${e.id}`}>
              {e.name} <span className="muted">
                {e.areaName}, {e.location}
              </span>
            </li>
          ))}
        </ul>
      )}
    </>
  )
}

function NotesForm({ playerId, item }: { playerId: string; item: InventoryItem }) {
  const [notes, setNotes] = useState(item.notes ?? '')
  const save = useSaveNotes(playerId)
  const changed = notes !== (item.notes ?? '')

  return (
    <form
      className="form"
      aria-label="Notes"
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate({ componentId: item.component.id, notes })
      }}
    >
      <label className="field__label">
        <span>Your notes</span>
        <textarea rows={3} maxLength={1000} value={notes} onChange={(e) => setNotes(e.target.value)} />
      </label>
      <div className="actions">
        <button type="submit" className="button--primary" disabled={!changed || save.isPending}>
          Save notes
        </button>
        {save.isSuccess && !changed && <span className="muted">Saved.</span>}
        {save.isError && <span className="error-text">{errorMessage(save.error)}</span>}
      </div>
    </form>
  )
}
