import { useState } from 'react'
import { errorMessage } from '../api/client'
import { useArchiveComponent, useAssignComponent, useComponents, usePlayers, useSettings, useUpdateSettings, type ComponentFilters } from '../api/queries'
import type { Component } from '../api/types'
import { biomes, componentTypes, label, rarities } from '../api/types'
import { ComponentForm } from './ComponentForm'
import { describe } from './describe'

const pageSize = 25

/** Admin catalog. There's no access control yet: a TODO for when authentication arrives (roadmap v1 default). */
export function ComponentsPage() {
  const [filters, setFilters] = useState<ComponentFilters>({ page: 1, pageSize })
  const [editing, setEditing] = useState<Component | 'new' | undefined>()
  const [assigning, setAssigning] = useState<Component | undefined>()
  const components = useComponents(filters)
  const archive = useArchiveComponent()

  const filter = (change: Partial<ComponentFilters>) => setFilters({ ...filters, ...change, page: 1 })
  const result = components.data
  const pages = result ? Math.max(1, Math.ceil(result.totalCount / result.pageSize)) : 1

  return (
    <section>
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
        <button type="button" className="button--primary" onClick={() => setEditing('new')}>
          New component
        </button>
      </div>

      {editing && <ComponentForm key={editing === 'new' ? 'new' : editing.id} component={editing === 'new' ? undefined : editing} onDone={() => setEditing(undefined)} />}
      {assigning && <AssignForm component={assigning} onDone={() => setAssigning(undefined)} />}
      {components.isError && <p className="error-text">{errorMessage(components.error)}</p>}
      {archive.isError && <p className="error-text">{errorMessage(archive.error)}</p>}

      <table className="table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Type</th>
            <th>Rarity</th>
            <th>Rank</th>
            <th>Details</th>
            <th>Self-add</th>
            <th>
              <span className="visually-hidden">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {result?.items.map((c) => (
            <tr key={c.id} className={c.isArchived ? 'row--archived' : undefined}>
              <td>
                {c.name}
                {c.isArchived && <span className="badge">Archived</span>}
              </td>
              <td>{label(c.type)}</td>
              <td>{label(c.rarity)}</td>
              <td>{c.rank}</td>
              <td className="muted">{describe(c)}</td>
              <td>{c.isPlayerAssignable ? 'Yes' : 'No'}</td>
              <td className="row-actions">
                <button type="button" onClick={() => setEditing(c)} aria-label={`Edit ${c.name}`}>
                  Edit
                </button>
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
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {result && (
        <div className="pager">
          <button type="button" disabled={(filters.page ?? 1) <= 1} onClick={() => setFilters({ ...filters, page: (filters.page ?? 1) - 1 })}>
            Previous
          </button>
          <span>
            Page {result.page} of {pages} · {result.totalCount} components
          </span>
          <button type="button" disabled={(filters.page ?? 1) >= pages} onClick={() => setFilters({ ...filters, page: (filters.page ?? 1) + 1 })}>
            Next
          </button>
        </div>
      )}
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
  const players = usePlayers()
  const assign = useAssignComponent()
  const [playerId, setPlayerId] = useState('')
  const [quantity, setQuantity] = useState(1)
  const chosen = playerId || players.data?.[0]?.id || ''

  return (
    <form
      className="card form form--inline"
      aria-label={`Assign ${component.name}`}
      onSubmit={(e) => {
        e.preventDefault()
        assign.mutate({ componentId: component.id, playerId: chosen, quantity: component.isConsumable ? quantity : 1 }, { onSuccess: onDone })
      }}
    >
      <strong>Give {component.name} to</strong>
      <select aria-label="Player" value={chosen} onChange={(e) => setPlayerId(e.target.value)}>
        {players.data?.map((p) => (
          <option key={p.id} value={p.id}>
            {p.name}
          </option>
        ))}
      </select>
      {component.isConsumable && (
        <input type="number" aria-label="Quantity" min={1} max={99} value={quantity} onChange={(e) => setQuantity(Number(e.target.value))} />
      )}
      <button type="submit" className="button--primary" disabled={!chosen || assign.isPending}>
        Assign
      </button>
      <button type="button" className="button--quiet" onClick={onDone}>
        Cancel
      </button>
      {assign.isError && <span className="error-text">{errorMessage(assign.error)}</span>}
    </form>
  )
}
