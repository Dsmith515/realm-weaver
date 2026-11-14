import { useState } from 'react'
import { errorMessage } from '../api/client'
import { useComponents, useInventory, usePlayers, useSelfAdd, useSettings } from '../api/queries'
import { label } from '../api/types'
import { describe } from '../catalog/describe'
import { groupByType } from '../ritual/slots'
import { usePlayerSelection } from './playerContext'

export function PlayerPage() {
  const { playerId } = usePlayerSelection()
  const players = usePlayers()
  const player = players.data?.find((p) => p.id === playerId)

  return (
    <section>
      <h1>Player</h1>
      {!playerId && <p>Choose or create a player in the header.</p>}
      {player && <p className="muted">Inventory of {player.name}.</p>}
      {playerId && <Inventory playerId={playerId} />}
      {playerId && <SelfAdd playerId={playerId} />}
    </section>
  )
}

function Inventory({ playerId }: { playerId: string }) {
  const inventory = useInventory(playerId)
  if (inventory.isPending) return <p className="muted">Loading…</p>
  if (inventory.isError) return <p className="error-text">{errorMessage(inventory.error)}</p>

  return (
    <div className="inventory-groups">
      {groupByType(inventory.data).map((group) => (
        <section key={group.type} aria-label={`${label(group.type)}s`} className="card">
          <h2>
            {label(group.type)}s <span className="muted">({group.items.reduce((n, i) => n + i.quantity, 0)})</span>
          </h2>
          {group.items.length === 0 ? (
            <p className="muted">None yet.</p>
          ) : (
            <ul className="item-grid">
              {group.items.map((item) => (
                <li key={item.component.id} className={`item item--${item.component.rarity}`}>
                  <strong>{item.component.name}</strong>
                  <span className="item__qty" aria-label={`Quantity ${item.quantity}`}>
                    ×{item.quantity}
                  </span>
                  <span className="muted">{describe(item.component)}</span>
                  {item.component.isArchived && <span className="badge">Retired</span>}
                </li>
              ))}
            </ul>
          )}
        </section>
      ))}
    </div>
  )
}

function SelfAdd({ playerId }: { playerId: string }) {
  const settings = useSettings()
  const assignable = useComponents({ assignable: true, pageSize: 200 })
  const selfAdd = useSelfAdd(playerId)
  const [quantities, setQuantities] = useState<Record<string, number>>({})
  const enabled = settings.data?.playersCanSelfAssign ?? false

  return (
    <section className="card">
      <h2>Add components yourself</h2>
      {!enabled && settings.data && <p className="muted">An admin has turned off adding components yourself.</p>}
      {selfAdd.isError && (
        <p className="error-text" role="alert">
          {errorMessage(selfAdd.error)}
        </p>
      )}
      <table className="table">
        <thead>
          <tr>
            <th>Component</th>
            <th>Type</th>
            <th>Quantity</th>
            <th>
              <span className="visually-hidden">Add</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {assignable.data?.items.map((c) => (
            <tr key={c.id}>
              <td>{c.name}</td>
              <td>{label(c.type)}</td>
              <td>
                {c.isConsumable ? (
                  <input
                    type="number"
                    min={1}
                    max={10}
                    aria-label={`Quantity of ${c.name}`}
                    value={quantities[c.id] ?? 1}
                    onChange={(e) => setQuantities({ ...quantities, [c.id]: Number(e.target.value) })}
                  />
                ) : (
                  1
                )}
              </td>
              <td>
                <button
                  type="button"
                  disabled={!enabled || selfAdd.isPending}
                  onClick={() => selfAdd.mutate({ componentId: c.id, quantity: c.isConsumable ? (quantities[c.id] ?? 1) : 1 })}
                >
                  Add {c.name}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}
