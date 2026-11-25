import { useState } from 'react'
import { useNavigate } from 'react-router'
import { errorMessage } from '../api/client'
import { useComponents, useQuickWeave, useQuickWeavePreview } from '../api/queries'
import type { Biome, QuickWeaveLimits, QuickWeavePreview, QuickWeaveRequest, Rarity, RealmKind, SettlementPopulation } from '../api/types'
import { biomes, label, rarities, realmKinds } from '../api/types'
import { describe } from '../catalog/describe'

type Slot = 'needle' | 'thread' | 'coating' | 'map'
type Picks = Partial<Record<Slot, string>>

const slots: Slot[] = ['needle', 'thread', 'coating', 'map']
const populations: SettlementPopulation[] = ['abandoned', 'sparse', 'modest', 'bustling', 'overcrowded']

/**
 * GM Quick Weave: a realm from anything in the catalog, for a one-shot. Pick a slot or leave it random within the
 * limits, roll to see what comes out, re-roll single slots, then weave exactly what's shown. Nothing is consumed.
 */
export function QuickWeavePage() {
  const [picks, setPicks] = useState<Picks>({})
  const [limits, setLimits] = useState<QuickWeaveLimits>({ rarities: [], biomes: [], kinds: [], includeArchived: false })
  const [size, setSize] = useState({ width: '', height: '', eventCount: '' })
  const [population, setPopulation] = useState<SettlementPopulation | ''>('')
  // Off by default: image generators add buildings on their own, and a GM may prefer to place them by hand.
  const [includeStructures, setIncludeStructures] = useState(false)
  const [suggestStructures, setSuggestStructures] = useState(false)
  const preview = useQuickWeavePreview()
  const weave = useQuickWeave()
  const navigate = useNavigate()
  const rolled = preview.data

  const request = (ids: Picks): QuickWeaveRequest => ({
    needleId: ids.needle || null,
    threadId: ids.thread || null,
    coatingId: ids.coating || null,
    mapId: ids.map || null,
    limits,
    width: toNumber(size.width),
    height: toNumber(size.height),
    eventCount: toNumber(size.eventCount),
    population: population || null,
    includeStructures,
    suggestStructures: !includeStructures && suggestStructures,
  })

  const roll = () => preview.mutate(request(picks))
  const reroll = (slot: Slot) => rolled && preview.mutate(request({ ...pinned(rolled), [slot]: picks[slot] }))
  const weaveIt = () =>
    rolled && weave.mutate(request(pinned(rolled)), { onSuccess: (result) => void navigate(`/realms/${result.realmId}`) })

  return (
    <section>
      <h1>Quick Weave</h1>
      <p className="muted">Generate a realm from the whole catalog for a one-shot. Nothing is consumed; the new map belongs to no one until you assign it.</p>

      <div className="quick-weave">
        <div className="card">
          <h2>Components</h2>
          {slots.map((slot) => (
            <SlotPicker key={slot} slot={slot} value={picks[slot] ?? ''} onChange={(id) => setPicks({ ...picks, [slot]: id })} />
          ))}

          <details className="advanced">
            <summary>Size (new maps only)</summary>
            <div className="advanced__fields">
              {(['width', 'height', 'eventCount'] as const).map((field) => (
                <label key={field}>
                  {field === 'eventCount' ? 'Events' : label(field)}
                  <input type="number" value={size[field]} onChange={(e) => setSize({ ...size, [field]: e.target.value })} />
                </label>
              ))}
            </div>
          </details>

          <details className="advanced">
            <summary>Settlements (new maps only)</summary>
            <div className="advanced__fields advanced__fields--rows">
              <label>
                Population
                <select value={population} onChange={(e) => setPopulation(e.target.value as SettlementPopulation | '')}>
                  <option value="">Random per settlement</option>
                  {populations.map((p) => (
                    <option key={p} value={p}>
                      {label(p)}
                    </option>
                  ))}
                </select>
              </label>
              <label className="checkbox switch switch--stacked">
                Include settlement structures
                <input type="checkbox" role="switch" checked={includeStructures} onChange={(e) => setIncludeStructures(e.target.checked)} />
              </label>
              <label className="checkbox switch switch--stacked" title="A few structure ideas for each settlement, listed on the realm page and placement sheet but not drawn on the map">
                Suggest structure placement only
                <input
                  type="checkbox"
                  role="switch"
                  checked={!includeStructures && suggestStructures}
                  disabled={includeStructures}
                  onChange={(e) => setSuggestStructures(e.target.checked)}
                />
              </label>
            </div>
          </details>
        </div>

        <fieldset className="card limits">
          <legend>Limits for random picks</legend>
          <Chips legend="Rarity" options={rarities} selected={limits.rarities ?? []} onChange={(r: Rarity[]) => setLimits({ ...limits, rarities: r })} />
          <div className="advanced__fields">
            <label>
              Lowest tier
              <input type="number" min={1} max={10} value={limits.minTier ?? ''} onChange={(e) => setLimits({ ...limits, minTier: toNumber(e.target.value) })} />
            </label>
            <label>
              Highest tier
              <input type="number" min={1} max={10} value={limits.maxTier ?? ''} onChange={(e) => setLimits({ ...limits, maxTier: toNumber(e.target.value) })} />
            </label>
          </div>
          <Chips legend="Biome" options={biomes} selected={limits.biomes ?? []} onChange={(b: Biome[]) => setLimits({ ...limits, biomes: b })} />
          <Chips legend="Realm kind" options={realmKinds} selected={limits.kinds ?? []} onChange={(k: RealmKind[]) => setLimits({ ...limits, kinds: k })} />
          <label className="checkbox switch switch--stacked field-label">
            Include archived components
            <input type="checkbox" role="switch" checked={!!limits.includeArchived} onChange={(e) => setLimits({ ...limits, includeArchived: e.target.checked })} />
          </label>
        </fieldset>
      </div>

      <div className="actions">
        <button type="button" onClick={roll} disabled={preview.isPending}>
          Roll
        </button>
        <button type="button" className="button--primary" onClick={weaveIt} disabled={!rolled?.canWeave || weave.isPending}>
          Weave
        </button>
      </div>
      {preview.isError && <p className="error-text">{errorMessage(preview.error)}</p>}
      {weave.isError && (
        <p className="error-text" role="alert">
          {errorMessage(weave.error)}
        </p>
      )}

      {rolled && <Rolled preview={rolled} canReroll={(slot) => !picks[slot] && slot !== 'map'} onReroll={reroll} />}
    </section>
  )
}

function SlotPicker({ slot, value, onChange }: { slot: Slot; value: string; onChange: (id: string) => void }) {
  const components = useComponents({ type: slot, pageSize: 200 })
  return (
    <label className="field__label">
      <span>{label(slot)}</span>
      <select value={value} onChange={(e) => onChange(e.target.value)}>
        <option value="">{slot === 'map' ? 'None: weave a new map' : 'Random within the limits'}</option>
        {components.data?.items.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </select>
    </label>
  )
}

/** Toggle chips for one limit. Nothing selected means no limit; the header button selects or clears them all at once. */
function Chips<T extends string>({ legend, options, selected, onChange }: { legend: string; options: readonly T[]; selected: T[]; onChange: (values: T[]) => void }) {
  const allSelected = options.every((o) => selected.includes(o))
  const toggle = (value: T) => onChange(selected.includes(value) ? selected.filter((v) => v !== value) : [...selected, value])
  return (
    <div role="group" aria-label={legend} className="limit-chips">
      <div className="limit-chips__header">
        <span className="muted">{legend}</span>
        <button
          type="button"
          className="limit-chips__all"
          onClick={() => onChange(allSelected ? [] : [...options])}
        >
          {allSelected ? 'Unselect all' : 'Select all'}
        </button>
      </div>
      {options.map((o) => (
        <button key={o} type="button" aria-pressed={selected.includes(o)} onClick={() => toggle(o)}>
          {label(o)}
        </button>
      ))}
    </div>
  )
}

function Rolled({ preview, canReroll, onReroll }: { preview: QuickWeavePreview; canReroll: (slot: Slot) => boolean; onReroll: (slot: Slot) => void }) {
  return (
    <section className="card" aria-label="Rolled components">
      <h2>{preview.createsNewMap ? 'A new map from' : 'Reweaving a map'}</h2>
      {preview.isMismatch && <p className="banner banner--warn">A mirror: different components on a recorded map (danger +{preview.dangerModifier}).</p>}
      <ul className="rolled">
        {slots.map((slot) => {
          const filled = preview[slot]
          if (!filled) return null
          return (
            <li key={slot}>
              <strong>{label(slot)}:</strong> {filled.component.name} <span className="muted">{describe(filled.component)}</span>
              {filled.source === 'random' && <span className="badge">Random</span>}
              {canReroll(slot) && (
                <button type="button" className="button--quiet" onClick={() => onReroll(slot)} aria-label={`Re-roll ${slot}`}>
                  Re-roll
                </button>
              )}
            </li>
          )
        })}
      </ul>
      {preview.issues.length > 0 && (
        <ul className="error-text" role="alert">
          {preview.issues.map((i) => (
            <li key={`${i.field}-${i.code}`}>{i.message}</li>
          ))}
        </ul>
      )}
    </section>
  )
}

/** The rolled components as explicit picks, so weaving creates exactly what the preview showed. */
function pinned(preview: QuickWeavePreview): Picks {
  return {
    needle: preview.needle?.component.id,
    thread: preview.thread?.component.id,
    coating: preview.coating?.component.id,
    map: preview.map?.component.id,
  }
}

function toNumber(text: string): number | null {
  return text.trim() === '' ? null : Number(text)
}
