import { useState } from 'react'
import { errorMessage } from '../api/client'
import { useSaveNotes } from '../api/queries'
import type { InventoryItem } from '../api/types'
import { label } from '../api/types'
import { describe } from '../catalog/describe'
import { Modal } from '../components/Modal'
import { MapDetailsView } from '../maps/MapDetailsView'

interface ItemModalProps {
  playerId: string
  item: InventoryItem
  onClose: () => void
}

/** An owned item up close: a map's seed, recipe and contents (the player's view), and the player's notes for any item. */
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
      {component.type === 'map' && <MapDetailsView mapId={component.id} audience="player" />}
      <NotesForm key={component.id} playerId={playerId} item={item} />
      <div className="actions">
        <button type="button" className="button--quiet" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
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
