import { DndContext, PointerSensor, useSensor, useSensors, type DragEndEvent, type DragStartEvent } from '@dnd-kit/core'
import { useMemo, useRef, useState } from 'react'
import { Link } from 'react-router'
import { ApiError, errorMessage } from '../api/client'
import { useInventory, usePerformRitual, useRandomFill, useRitualPreview } from '../api/queries'
import type { Component, ComponentType, RitualResult } from '../api/types'
import { componentTypes, label, ritualSlots } from '../api/types'
import { usePlayerSelection } from '../player/playerContext'
import { IdempotencyKeys } from './idempotency'
import { InventoryChip, SlotCard } from './RitualSlots'
import {
  chosenId,
  choiceFromRandom,
  clearSlot,
  filterInventory,
  groupByType,
  placeInSlot,
  slotFor,
  toggleInSlot,
  toPreviewRequest,
  toSelection,
  type AdvancedOptions,
  type SlotChoice,
} from './slots'

export function RitualPage() {
  const { playerId } = usePlayerSelection()
  return (
    <section>
      <h1>Ritual</h1>
      {playerId ? <Ritual key={playerId} playerId={playerId} /> : <p>Choose or create a player in the header to begin.</p>}
    </section>
  )
}

function Ritual({ playerId }: { playerId: string }) {
  const [choice, setChoice] = useState<SlotChoice>({})
  const [advanced, setAdvanced] = useState<AdvancedOptions>({})
  const [typeFilter, setTypeFilter] = useState<ComponentType | undefined>()
  const [text, setText] = useState('')
  const [dragging, setDragging] = useState<Component | undefined>()
  const [notice, setNotice] = useState<string | undefined>()
  const [result, setResult] = useState<RitualResult | undefined>()
  const keys = useRef(new IdempotencyKeys())

  const inventory = useInventory(playerId)
  const previewRequest = useMemo(() => toPreviewRequest(playerId, choice, advanced), [playerId, choice, advanced])
  const preview = useRitualPreview(previewRequest)
  const randomFill = useRandomFill()
  const perform = usePerformRitual()
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }))

  const items = filterInventory(inventory.data ?? [], { type: typeFilter, text })
  const chosenIds = new Set(Object.values(choice).filter(Boolean))
  const p = preview.data

  const update = (next: SlotChoice) => {
    setChoice(next)
    setNotice(undefined)
    setResult(undefined)
  }

  const onDragStart = (event: DragStartEvent) => setDragging(event.active.data.current?.component as Component | undefined)
  const onDragEnd = (event: DragEndEvent) => {
    setDragging(undefined)
    const component = event.active.data.current?.component as Component | undefined
    if (!component || !event.over) return
    if (event.over.id === slotFor(component.type)) {
      update(placeInSlot(choice, component))
    } else {
      setNotice(`${component.name} is a ${label(component.type)}: it goes in the ${label(component.type)} slot.`)
    }
  }

  const random = async () => {
    try {
      const filled = await randomFill.mutateAsync(previewRequest)
      update(choiceFromRandom(choice, filled))
    } catch (error) {
      setNotice(errorMessage(error))
    }
  }

  const performRitual = async () => {
    const selection = toSelection(playerId, choice, advanced)
    try {
      const done = await perform.mutateAsync({ selection, idempotencyKey: keys.current.keyFor(selection) })
      keys.current.reset()
      setResult(done)
      setChoice({})
    } catch (error) {
      // Keep the key unless the server answered definitively, so a retry can't perform the ritual twice.
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) keys.current.reset()
    }
  }

  return (
    <DndContext sensors={sensors} onDragStart={onDragStart} onDragEnd={onDragEnd} onDragCancel={() => setDragging(undefined)}>
      <div className="ritual">
        <div className="ritual__main">
          <div className="slots">
            {ritualSlots.map((slot) => (
              <SlotCard
                key={slot}
                slot={slot}
                preview={p}
                chosen={!!chosenId(choice, slot)}
                mismatched={!!p?.mismatchedSlots.includes(slot)}
                draggingType={dragging?.type}
                onClear={() => update(clearSlot(choice, slot))}
              />
            ))}
          </div>

          {p?.isMismatch && (
            <div className="banner banner--warn" role="status">
              <strong>Mismatched map.</strong> The {p.mismatchedSlots.map(label).join(' and ').toLowerCase()} differ
              {p.mismatchedSlots.length === 1 ? 's' : ''} from the map&apos;s. Same layout, rerolled encounters, danger +{p.dangerModifier}.
            </div>
          )}
          {p && !p.isMismatch && p.map?.source === 'matchingMap' && (
            <div className="banner" role="status">
              You own a map made with these components, so this ritual reopens <strong>{p.map.component.name}</strong>.
            </div>
          )}
          {notice && (
            <div className="banner banner--warn" role="status">
              {notice}
            </div>
          )}

          <details className="advanced">
            <summary>Advanced</summary>
            {p?.advancedOptionsIgnored && <p className="muted">These only apply when a new map is made; the map sets its own size.</p>}
            <div className="advanced__fields">
              <NumberField label="Width" min={15} max={100} value={advanced.width} onChange={(width) => setAdvanced({ ...advanced, width })} />
              <NumberField label="Height" min={15} max={100} value={advanced.height} onChange={(height) => setAdvanced({ ...advanced, height })} />
              <NumberField
                label="Events"
                min={5}
                max={60}
                value={advanced.eventCount}
                onChange={(eventCount) => setAdvanced({ ...advanced, eventCount })}
              />
            </div>
            {p?.issues
              .filter((i) => i.slot === null || i.slot === undefined)
              .map((i) => (
                <p key={i.field} className="error-text">
                  {i.message}
                </p>
              ))}
          </details>

          <div className="actions">
            <button type="button" onClick={() => void random()} disabled={randomFill.isPending}>
              Random
            </button>
            <button
              type="button"
              className="button--primary"
              onClick={() => void performRitual()}
              disabled={!p?.canPerform || preview.isFetching || perform.isPending}
            >
              {perform.isPending ? 'Weaving…' : 'Perform ritual'}
            </button>
            <button type="button" className="button--quiet" onClick={() => update({})}>
              Clear all
            </button>
            {p && <span className="muted">{p.createsNewMap ? 'Creates a new map.' : 'Reuses a map you own.'}</span>}
          </div>

          {perform.isError && (
            <div className="banner banner--error" role="alert">
              {errorMessage(perform.error)}
              {perform.error instanceof ApiError && perform.error.status === 409 && ' Your inventory has been reloaded.'}
            </div>
          )}
          {result && (
            <div className="banner banner--ok" role="status">
              <strong>The portal opens.</strong> {result.mapCreated ? 'A new map was added to your inventory.' : 'You reopened a map you own.'}
              {result.isMismatch && ` Danger +${result.dangerModifier}.`} <Link to={`/realms/${result.realmId}`}>View the realm</Link>
            </div>
          )}
        </div>

        <aside className="inventory-panel" aria-label="Inventory">
          <h2>Inventory</h2>
          <div className="inventory-panel__filters">
            <select aria-label="Filter by type" value={typeFilter ?? ''} onChange={(e) => setTypeFilter((e.target.value || undefined) as ComponentType | undefined)}>
              <option value="">All types</option>
              {componentTypes.map((t) => (
                <option key={t} value={t}>
                  {label(t)}
                </option>
              ))}
            </select>
            <input aria-label="Search inventory" placeholder="Search" value={text} onChange={(e) => setText(e.target.value)} />
          </div>
          {inventory.isPending && <p className="muted">Loading…</p>}
          {inventory.isError && <p className="error-text">{errorMessage(inventory.error)}</p>}
          {groupByType(items)
            .filter((g) => g.items.length > 0)
            .map((group) => (
              <div key={group.type}>
                <h3>{label(group.type)}s</h3>
                <ul className="chips">
                  {group.items.map((item) => (
                    <InventoryChip
                      key={item.component.id}
                      item={item}
                      selected={chosenIds.has(item.component.id)}
                      onPick={(c) => update(toggleInSlot(choice, c))}
                    />
                  ))}
                </ul>
              </div>
            ))}
          {inventory.data && items.length === 0 && <p className="muted">Nothing matches.</p>}
        </aside>
      </div>
    </DndContext>
  )
}

interface NumberFieldProps {
  label: string
  min: number
  max: number
  value: number | undefined
  onChange: (value: number | undefined) => void
}

function NumberField({ label: text, min, max, value, onChange }: NumberFieldProps) {
  return (
    <label>
      {text}
      <input
        type="number"
        min={min}
        max={max}
        placeholder="Random"
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value === '' ? undefined : Number(e.target.value))}
      />
    </label>
  )
}
