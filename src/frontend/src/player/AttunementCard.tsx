import { errorMessage } from '../api/client'
import { useAttune, useInventory, useSetSkills } from '../api/queries'
import type { Player } from '../api/types'
import { boonLabel } from '../api/types'

/**
 * The adventurer side of a player: the one attuned needle (its abilities and boon apply during an adventure, whatever
 * needle was used to weave) and Cartographer status, which decides whether a completed run can record a map.
 */
export function AttunementCard({ player }: { player: Player }) {
  const inventory = useInventory(player.id)
  const attune = useAttune(player.id)
  const setSkills = useSetSkills(player.id)
  const needles = inventory.data?.filter((i) => i.component.type === 'needle') ?? []
  const attuned = needles.find((i) => i.component.id === player.attunedNeedleId)?.component
  const trained = player.skills?.includes('cartographer') ?? false

  return (
    <section className="card attunement" aria-label="Attunement">
      <h2>Attunement</h2>
      <label className="field__label">
        <span>Attuned needle</span>
        <select
          value={player.attunedNeedleId ?? ''}
          disabled={attune.isPending || inventory.isPending}
          onChange={(e) => attune.mutate(e.target.value || null)}
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

      <p role="status" className="attunement__status">
        {player.cartographer === 'trainedSkill' && 'Cartographer: a trained skill. Completed runs can record maps whatever needle is attuned.'}
        {player.cartographer === 'attunedNeedle' && 'Cartographer: from the attuned needle’s boon. Completed runs can record maps.'}
        {!player.cartographer && 'Not a Cartographer: completed runs can’t record maps until the player trains the skill or attunes a needle with that boon.'}
      </p>
      <label className="checkbox">
        <input
          type="checkbox"
          checked={trained}
          disabled={setSkills.isPending}
          onChange={(e) => setSkills.mutate(e.target.checked ? ['cartographer'] : [])}
        />
        Trained Cartographer (GM)
      </label>
      {(attune.isError || setSkills.isError) && <p className="error-text">{errorMessage(attune.error ?? setSkills.error)}</p>}
    </section>
  )
}
