import { useState } from 'react'
import { Link } from 'react-router'
import { errorMessage } from '../api/client'
import { useRealmBriefing, useRealmSvg } from '../api/queries'
import type { RealmBriefing as Briefing } from '../api/types'
import { label } from '../api/types'
import { usePlayerSelection } from '../player/playerContext'
import { MirrorBadge } from './MirrorBadge'
import { SvgViewer } from './SvgViewer'

/**
 * A realm as a player sees it (Milestone 8i): what kind of place it is, how dangerous, and what the party can aim for.
 * No map, no locations and no GM controls, unless the player holds the realm's map, which adds the map and the kinds
 * of things to find. The API sends this trimmed view, so the page never has the GM's document to leak. Still a
 * preview, not access control (ADR 0012).
 */
export function RealmBriefing({ realmId }: { realmId: string }) {
  const { playerId } = usePlayerSelection()
  const briefing = useRealmBriefing(realmId, playerId)

  if (briefing.isPending) return <p className="muted">Loading realm…</p>
  if (briefing.isError) {
    return (
      <section>
        <h1>Realm</h1>
        <p className="error-text">{errorMessage(briefing.error)}</p>
        <Link to="/realms">Back to realms</Link>
      </section>
    )
  }
  return <BriefingView briefing={briefing.data} hasPlayer={!!playerId} />
}

function BriefingView({ briefing, hasPlayer }: { briefing: Briefing; hasPlayer: boolean }) {
  const b = briefing
  const floors = b.floorCount > 0 ? ` (${b.floorCount} floor${b.floorCount > 1 ? 's' : ''})` : ''
  return (
    <section aria-label="Realm briefing">
      <p>
        <Link to="/realms">← Realms</Link>
      </p>
      <h1>
        {b.mapName ?? `${label(b.biome)} ${label(b.kind).toLowerCase()}`}
        {b.isMirror && <MirrorBadge />}
      </h1>
      {b.wovenFrom.length > 0 && <p>{b.wovenFrom.join(' · ')}</p>}
      <dl className="facts">
        <dt>Realm kind</dt>
        <dd>
          {label(b.kind)}
          {floors}
        </dd>
        <dt>Biome</dt>
        <dd>{label(b.biome)}</dd>
        <dt>Element</dt>
        <dd>{label(b.element)}</dd>
        <dt>Threat</dt>
        <dd>
          {b.threat}
          {b.dangerModifier > 0 && ` (danger +${b.dangerModifier})`}
        </dd>
        <dt>Completed</dt>
        <dd>{b.completionCount === 0 ? 'Not yet' : `×${b.completionCount}`}</dd>
      </dl>

      <section className="card" aria-label="Briefing objectives">
        <h2>Objectives (complete any one)</h2>
        {b.objectives.length === 0 ? (
          <p className="muted">No objectives were rolled for this realm. Ask the GM what the party is after.</p>
        ) : (
          <ul className="digest">
            {b.objectives.map((o) => (
              <li key={o.id}>
                <strong>{o.title}</strong> {o.isPinned && <span className="badge">GM pick</span>} <span className="muted">{o.hint}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      {b.holdsMap ? (
        <HeldMap briefing={b} />
      ) : (
        <p className="muted">
          {hasPlayer
            ? 'You don’t hold this realm’s map, so its layout stays hidden. The GM can hand it out after a run.'
            : 'Choose a player in the header to see whether they hold this realm’s map.'}
        </p>
      )}
    </section>
  )
}

/** For a player who holds the map: what kinds of things are there, and the map itself, one area at a time. */
function HeldMap({ briefing }: { briefing: Briefing }) {
  const areas = briefing.areas ?? []
  const [areaId, setAreaId] = useState(areas[0]?.id)
  const svg = useRealmSvg(briefing.realmId, areaId)
  const finds = briefing.finds
  return (
    <>
      {finds && (
        <>
          <FindList title="Creatures" names={finds.creatures} />
          <FindList title="Potential loot" names={finds.loot} />
          <FindList title="Resource nodes" names={finds.resourceNodes} />
        </>
      )}
      {areas.length > 1 && (
        <div role="tablist" aria-label="Areas" className="tabs">
          {areas.map((a) => (
            <button key={a.id} role="tab" type="button" aria-selected={a.id === areaId} onClick={() => setAreaId(a.id)}>
              {a.kind === 'field' ? 'Field' : `Floor ${a.floorNumber}`}
            </button>
          ))}
        </div>
      )}
      <div className="realm__map">
        {svg.isPending && <p className="muted">Drawing the map…</p>}
        {svg.isError && <p className="error-text">{errorMessage(svg.error)}</p>}
        {svg.data && <SvgViewer svg={svg.data} />}
      </div>
    </>
  )
}

function FindList({ title, names }: { title: string; names: string[] }) {
  return (
    <>
      <h3>{title}</h3>
      {names.length === 0 ? (
        <p className="muted">None.</p>
      ) : (
        <ul className="digest" aria-label={title}>
          {names.map((name) => (
            <li key={name}>{name}</li>
          ))}
        </ul>
      )}
    </>
  )
}
