import { useState } from 'react'
import { errorMessage } from '../api/client'
import { useArchiveComponent, useAssignComponent, useComponents, useSettings, useUpdateSettings, type ComponentFilters, type ComponentSort } from '../api/queries'
import type { Component } from '../api/types'
import { biomes, componentTypes, label, rarities } from '../api/types'
import { Modal } from '../components/Modal'
import { Pager } from '../components/Pager'
import { PlayerSelect } from '../components/PlayerPicker'
import { MapDetailsView } from '../maps/MapDetailsView'
import { ComponentForm } from './ComponentForm'
import { creatableTypes } from './randomComponent'
import { describe } from './describe'

const pageSize = 25

type Editing = { mode: 'new' } | { mode: 'edit'; component: Component; tab?: 'edit' | 'details' } | { mode: 'clone'; component: Component }

/** Sortable columns and the API's sort key for each. Details and actions don't sort. */
const columns: { label: string; sort?: ComponentSort }[] = [
  { label: 'Name', sort: 'name' },
  { label: 'Type', sort: 'type' },
  { label: 'Rarity', sort: 'rarity' },
  { label: 'Rank', sort: 'rank' },
  { label: 'Details' },
  { label: 'Self-add', sort: 'assignable' },
]

const canClone = (c: Component) => (creatableTypes as readonly string[]).includes(c.type)

/** Admin catalog. There's no access control yet: a TODO for when authentication arrives (roadmap v1 default). */
export function ComponentsPage() {
  const [filters, setFilters] = useState<ComponentFilters>({ page: 1, pageSize })
  const [editing, setEditing] = useState<Editing | undefined>()
  const [assigning, setAssigning] = useState<Component | undefined>()
  const components = useComponents(filters)
  const archive = useArchiveComponent()

  const filter = (change: Partial<ComponentFilters>) => setFilters({ ...filters, ...change, page: 1 })
  const result = components.data
  const sort = filters.sort ?? 'name'
  // Clicking the sorted column flips it; another column sorts ascending.
  const sortBy = (column: ComponentSort) => filter({ sort: column, desc: column === sort ? !filters.desc : undefined })

  return (
    <section className="page-fill">
      <h1>Components</h1>
      <SelfAssignToggle />

      <div className="filters" role="search">
        <select aria-label="Type" value={filters.type ?? ''} onChange={(e) => filter({ type: e.target.value || undefined })}>
          <option value="">All types</option>
          {componentTypes.map((t) => (
            <option key={t} value={t}>
              {label(t)}
            </option>
          ))}
        </select>
        <select aria-label="Rarity" value={filters.rarity ?? ''} onChange={(e) => filter({ rarity: e.target.value || undefined })}>
          <option value="">All rarities</option>
          {rarities.map((r) => (
            <option key={r} value={r}>
              {label(r)}
            </option>
          ))}
        </select>
        <select aria-label="Biome" value={filters.biome ?? ''} onChange={(e) => filter({ biome: e.target.value || undefined })}>
          <option value="">Any biome</option>
          {biomes.map((b) => (
            <option key={b} value={b}>
              {label(b)}
            </option>
          ))}
        </select>
        <input aria-label="Search components" placeholder="Search" value={filters.search ?? ''} onChange={(e) => filter({ search: e.target.value })} />
        <label className="checkbox">
          <input type="checkbox" checked={!!filters.includeArchived} onChange={(e) => filter({ includeArchived: e.target.checked || undefined })} />
          Show archived
        </label>
        <button type="button" className="button--primary" onClick={() => setEditing({ mode: 'new' })}>
          New component
        </button>
      </div>

      {editing && (
        <Modal title={editing.mode === 'new' ? 'New component' : `${editing.mode === 'edit' ? 'Edit' : 'Clone'} ${editing.component.name}`} onClose={() => setEditing(undefined)}>
          {editing.mode === 'edit' && editing.component.type === 'map' && (
            <div role="tablist" aria-label="Map views" className="tabs">
              <button type="button" role="tab" aria-selected={editing.tab !== 'details'} onClick={() => setEditing({ ...editing, tab: 'edit' })}>
                Edit
              </button>
              <button type="button" role="tab" aria-selected={editing.tab === 'details'} onClick={() => setEditing({ ...editing, tab: 'details' })}>
                Map details
              </button>
            </div>
          )}
          {editing.mode === 'edit' && editing.tab === 'details' ? (
            <>
              <h2>{editing.component.name}</h2>
              <MapDetailsView mapId={editing.component.id} audience="gm" />
              <div className="actions">
                <button type="button" className="button--quiet" onClick={() => setEditing(undefined)}>
                  Close
                </button>
              </div>
            </>
          ) : (
            <ComponentForm
              component={editing.mode === 'edit' ? editing.component : undefined}
              template={editing.mode === 'clone' ? editing.component : undefined}
              onDone={() => setEditing(undefined)}
            />
          )}
        </Modal>
      )}
      {assigning && (
        <Modal title={`Assign ${assigning.name}`} onClose={() => setAssigning(undefined)}>
          <AssignForm component={assigning} onDone={() => setAssigning(undefined)} />
        </Modal>
      )}
      {components.isError && <p className="error-text">{errorMessage(components.error)}</p>}
      {archive.isError && <p className="error-text">{errorMessage(archive.error)}</p>}

      <div className="scroll-region">
      <table className="table table--sticky">
        <thead>
          <tr>
            {columns.map((column) =>
              column.sort ? (
                <th key={column.label} aria-sort={sort === column.sort ? (filters.desc ? 'descending' : 'ascending') : undefined}>
                  <button type="button" className="sort-header" onClick={() => sortBy(column.sort!)}>
                    {column.label}
                    <span className="sort-header__arrow" aria-hidden="true">
                      {sort === column.sort ? (filters.desc ? '▼' : '▲') : '↕'}
                    </span>
                  </button>
                </th>
              ) : (
                <th key={column.label}>{column.label}</th>
              ),
            )}
            <th>
              <span className="visually-hidden">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {result?.items.map((c) => (
            <tr key={c.id} className={c.isArchived ? 'row--archived' : undefined}>
              <td>
                <button
                  type="button"
                  className="link-button"
                  onClick={() => setEditing({ mode: 'edit', component: c, tab: c.type === 'map' ? 'details' : 'edit' })}
                  title={c.type === 'map' ? 'Map details and edit' : 'Edit'}
                >
                  {c.name}
                </button>
                {c.isArchived && <span className="badge">Archived</span>}
                {c.usedByMapCount > 0 && (
                  <span className="badge" title="Its recipe fields are locked">
                    {c.usedByMapCount === 1 ? '1 map' : `${c.usedByMapCount} maps`}
                  </span>
                )}
              </td>
              <td>{label(c.type)}</td>
              <td>{label(c.rarity)}</td>
              <td>{c.rank}</td>
              <td className="muted">{describe(c)}</td>
              <td>{c.isPlayerAssignable ? 'Yes' : 'No'}</td>
              <td>
                <div className="row-actions">
                  <button type="button" onClick={() => setEditing({ mode: 'edit', component: c })} aria-label={`Edit ${c.name}`}>
                    Edit
                  </button>
                  {canClone(c) && (
                    <button type="button" onClick={() => setEditing({ mode: 'clone', component: c })} aria-label={`Clone ${c.name}`}>
                      Clone
                    </button>
                  )}
                  <button type="button" onClick={() => setAssigning(c)} disabled={c.isArchived} aria-label={`Assign ${c.name}`}>
                    Assign
                  </button>
                  <button
                    type="button"
                    className="button--quiet"
                    disabled={c.isArchived || archive.isPending}
                    aria-label={`Archive ${c.name}`}
                    onClick={() => {
                      if (window.confirm(`Archive ${c.name}? Players keep the ones they own.`)) archive.mutate(c.id)
                    }}
                  >
                    Archive
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      </div>

      {result && <Pager page={result.page} pageSize={result.pageSize} totalCount={result.totalCount} noun="components" onPage={(page) => setFilters({ ...filters, page })} />}
    </section>
  )
}

function SelfAssignToggle() {
  const settings = useSettings()
  const update = useUpdateSettings()
  return (
    <label className="checkbox toggle">
      <input
        type="checkbox"
        checked={settings.data?.playersCanSelfAssign ?? false}
        disabled={!settings.data || update.isPending}
        onChange={(e) => update.mutate(e.target.checked)}
      />
      Players can add assignable components to their own inventory
    </label>
  )
}

function AssignForm({ component, onDone }: { component: Component; onDone: () => void }) {
  const assign = useAssignComponent()
  const [playerId, setPlayerId] = useState('')
  const [quantity, setQuantity] = useState(1)
  const chosen = playerId

  return (
    <form
      className="form"
      aria-label={`Assign ${component.name}`}
      onSubmit={(e) => {
        e.preventDefault()
        assign.mutate({ componentId: component.id, playerId: chosen, quantity: component.isConsumable ? quantity : 1 }, { onSuccess: onDone })
      }}
    >
      <h2>Give {component.name} to</h2>
      <PlayerSelect label="Player" value={chosen} onChange={setPlayerId} />
      {component.isConsumable && (
        <input type="number" aria-label="Quantity" min={1} max={99} value={quantity} onChange={(e) => setQuantity(Number(e.target.value))} />
      )}
      <div className="actions">
        <button type="submit" className="button--primary" disabled={!chosen || assign.isPending}>
          Assign
        </button>
        <button type="button" className="button--quiet" onClick={onDone}>
          Cancel
        </button>
      </div>
      {assign.isError && <span className="error-text">{errorMessage(assign.error)}</span>}
    </form>
  )
}
