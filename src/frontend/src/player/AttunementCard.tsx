import { useState } from 'react'
import { errorMessage } from '../api/client'
import { useAttune, useInventory } from '../api/queries'
import type { Player } from '../api/types'
import { boonLabel } from '../api/types'

/**
 * The adventurer side of a player: the one attuned needle. Its abilities and boon apply during an adventure, whatever
 * needle was used to weave, and the realm sheet shows it to the GM.
 */
export function AttunementCard({ player }: { player: Player }) {
  const inventory = useInventory(player.id)
  const attune = useAttune(player.id)
  const needles = inventory.data?.filter((i) => i.component.type === 'needle') ?? []
  const attuned = needles.find((i) => i.component.id === player.attunedNeedleId)?.component
  // What the user just asked for, shown until the server's answer arrives. Local state updates within the click,
  // so the controlled select never snaps back to the old value while the request is in flight.
  const [askedNeedle, setAskedNeedle] = useState<string>()
  const attunedId = askedNeedle ?? player.attunedNeedleId ?? ''

  return (
    <section className="card attunement" aria-label="Attunement">
      <h2>Attunement</h2>
      <label className="field__label">
        <span>Attuned needle</span>
        <select
          value={attunedId}
          disabled={attune.isPending || inventory.isPending}
          onChange={(e) => {
            setAskedNeedle(e.target.value)
            attune.mutate(e.target.value || null, { onSettled: () => setAskedNeedle(undefined) })
          }}
        >
          <option value="">None</option>
          {needles.map((n) => (
            <option key={n.component.id} value={n.component.id}>
              {n.component.name}
            </option>
          ))}
        </select>
      </label>
      <p className="muted">Any needle you own can weave a realm. The attuned one supplies its abilities and boon during the adventure.</p>
      {attuned?.needle && (
        <dl className="facts">
          <dt>Abilities</dt>
          <dd>{attuned.needle.abilities.length > 0 ? attuned.needle.abilities.join(', ') : 'None'}</dd>
          <dt>Boon</dt>
          <dd>{attuned.needle.boon ? boonLabel(attuned.needle.boon) : 'None'}</dd>
        </dl>
      )}

      {attune.isError && <p className="error-text">{errorMessage(attune.error)}</p>}
    </section>
  )
}
