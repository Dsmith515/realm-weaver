import type { ReactNode } from 'react'
import type { Realm, RealmObjective } from '../api/types'
import { useCompletions } from '../api/queries'
import { CompleteRealmCard, PlayerCompletion } from './CompleteRealmCard'
import { ObjectivesPanel } from './ObjectivesPanel'

interface Props {
  realm: Realm
  onShow: (objective: RealmObjective) => void
  highlighted?: string
}

/**
 * Everything about a realm's objectives in one collapsible region: the list and the GM pin, completing an objective
 * (or just handing out the map), and who has completed it. Each part folds on its own.
 */
export function RealmObjectives({ realm, onShow, highlighted }: Props) {
  const counts = useCompletionCounts(realm.summary.id)
  return (
    <section className="card realm-objectives" aria-label="Objectives">
      <details open className="fold fold--top">
        <summary>
          <h2>Objectives</h2>
        </summary>
        <div className="fold__body">
          <Fold title="Objective list" open>
            <ObjectivesPanel realm={realm} onShow={onShow} highlighted={highlighted} />
          </Fold>
          <Fold title="Complete an objective (GM)" open>
            <CompleteRealmCard key={realm.summary.id} realm={realm} />
          </Fold>
          <Fold title="Player completion" note={counts}>
            <PlayerCompletion realmId={realm.summary.id} />
          </Fold>
        </div>
      </details>
    </section>
  )
}

function Fold({ title, note, open, children }: { title: string; note?: string; open?: boolean; children: ReactNode }) {
  return (
    <details open={open} className="fold">
      <summary>
        <h3>
          {title}
          {note && <span className="muted fold__note"> ({note})</span>}
        </h3>
      </summary>
      <div className="fold__body">{children}</div>
    </details>
  )
}

/** "(2 players, 3 completions)" for the Player completion heading, once loaded. */
function useCompletionCounts(realmId: string) {
  const data = useCompletions(realmId).data
  if (!data || data.completions.length === 0) return undefined
  const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`
  return `${plural(data.players.length, 'player')}, ${plural(data.completions.length, 'completion')}`
}
