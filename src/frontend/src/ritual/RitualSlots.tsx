import { useDraggable, useDroppable } from '@dnd-kit/core'
import type { Component, InventoryItem, RitualPreview, RitualSlot } from '../api/types'
import { label } from '../api/types'
import { sourceLabel, slotPreview } from './slots'

interface SlotCardProps {
  slot: RitualSlot
  preview: RitualPreview | undefined
  chosen: boolean
  mismatched: boolean
  draggingType: string | undefined
  onClear: () => void
}

/** One of the four ritual slots. Accepts a dropped inventory item of the same type. */
export function SlotCard({ slot, preview, chosen, mismatched, draggingType, onClear }: SlotCardProps) {
  const { isOver, setNodeRef } = useDroppable({ id: slot })
  const filled = slotPreview(preview, slot)
  // An empty slot already says what to do, so only real problems (not owned, wrong id) are shown as errors.
  const issue = preview?.issues.find((i) => i.slot === slot && i.code !== 'missing')
  const accepts = draggingType === slot
  const source = filled ? sourceLabel(filled.source) : undefined

  const classes = ['slot']
  if (accepts) classes.push('slot--accepts')
  if (accepts && isOver) classes.push('slot--over')
  if (mismatched) classes.push('slot--mismatch')
  if (issue) classes.push('slot--issue')

  return (
    <section ref={setNodeRef} className={classes.join(' ')} aria-label={`${label(slot)} slot`}>
      <h2 className="slot__title">
        {label(slot)}
        {slot === 'map' && <span className="slot__hint"> (optional)</span>}
      </h2>
      {filled ? (
        <div className="slot__content">
          <strong>{filled.component.name}</strong>
          <span className="slot__meta">
            {label(filled.component.rarity)} · you have {filled.ownedQuantity}
          </span>
          {source && <span className="badge">{source}</span>}
          {mismatched && <span className="badge badge--warn">Mismatch</span>}
        </div>
      ) : (
        <p className="slot__empty">{slot === 'map' ? 'A new map will be made' : 'Drag or click an item'}</p>
      )}
      {issue && <p className="error-text">{issue.message}</p>}
      {chosen && (
        <button type="button" className="button--quiet slot__clear" onClick={onClear} aria-label={`Clear the ${slot} slot`}>
          Clear
        </button>
      )}
    </section>
  )
}

interface InventoryChipProps {
  item: InventoryItem
  selected: boolean
  onPick: (component: Component) => void
}

/** An inventory item: drag it onto its slot, or click it to put it in (or take it out of) its slot. */
export function InventoryChip({ item, selected, onPick }: InventoryChipProps) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: item.component.id,
    data: { component: item.component },
  })
  const style = transform ? { transform: `translate(${transform.x}px, ${transform.y}px)` } : undefined

  return (
    <li>
      <button
        ref={setNodeRef}
        {...attributes}
        {...listeners}
        type="button"
        className={`chip chip--${item.component.type}${selected ? ' chip--selected' : ''}${isDragging ? ' chip--dragging' : ''}`}
        style={style}
        aria-pressed={selected}
        onClick={() => onPick(item.component)}
      >
        <span>{item.component.name}</span>
        {item.component.isConsumable && <span className="chip__qty">×{item.quantity}</span>}
      </button>
    </li>
  )
}
