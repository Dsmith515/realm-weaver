import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { errorMessage } from '../api/client'
import { exportUrl, useRealm, useRealms, useRealmSvg } from '../api/queries'
import type { Realm, RealmArea, RealmSummary } from '../api/types'
import { label, tileLabel } from '../api/types'
import { usePlayerSelection } from '../player/playerContext'
import { SvgViewer } from './SvgViewer'

export function RealmsPage() {
  const { playerId } = usePlayerSelection()
  const realms = useRealms(playerId)

  return (
    <section>
      <h1>Realms</h1>
      {!playerId && <p>Choose a player in the header.</p>}
      {realms.isError && <p className="error-text">{errorMessage(realms.error)}</p>}
      {realms.data?.length === 0 && (
        <p>
          No realms yet. <Link to="/ritual">Perform a ritual</Link> to open one.
        </p>
      )}
      <ul className="realm-list">
        {realms.data?.map((realm) => (
          <li key={realm.id} className="card realm-list__item">
            <div>
              <Link to={`/realms/${realm.id}`}>
                <strong>{title(realm)}</strong>
              </Link>
              {realm.isMirror && <MirrorBadge />}
              {realm.components && <p className="realm-list__components">{componentsLine(realm)}</p>}
            </div>
            <span className="muted">
              {label(realm.kind)} · threat {realm.threat}
              {realm.dangerModifier > 0 && ` (danger +${realm.dangerModifier})`} · {new Date(realm.createdAt).toLocaleString()}
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}

export function RealmPage() {
  const { id = '' } = useParams()
  const realm = useRealm(id)

  if (realm.isPending) return <p className="muted">Loading realm…</p>
  if (realm.isError) {
    return (
      <section>
        <h1>Realm</h1>
        <p className="error-text">{errorMessage(realm.error)}</p>
        <Link to="/realms">Back to realms</Link>
      </section>
    )
  }
  return <RealmView realm={realm.data} />
}

function RealmView({ realm }: { realm: Realm }) {
  const { summary, document } = realm
  const [areaId, setAreaId] = useState(summary.areas[0]?.id)
  const [highlight, setHighlight] = useState<string>()
  const svg = useRealmSvg(summary.id, areaId)
  const area = document.areas.find((a) => a.id === areaId)

  const selectArea = (id: string) => {
    setAreaId(id)
    setHighlight(undefined)
  }

  return (
    <section>
      <p>
        <Link to="/realms">← Realms</Link>
      </p>
      <h1>
        {title(summary)}
        {summary.isMirror && <MirrorBadge />}
      </h1>
      {summary.components && <p>{componentsLine(summary)}</p>}
      <p className="muted">
        {label(summary.kind)} · Seed {summary.seed} · {summary.width}×{summary.height} · threat {summary.threat}
        {summary.dangerModifier > 0 && ` (mismatch danger +${summary.dangerModifier})`} · generator {summary.generatorVersion}
      </p>

      {summary.areas.length > 1 && (
        <div role="tablist" aria-label="Areas" className="tabs">
          {summary.areas.map((a) => (
            <button key={a.id} role="tab" type="button" aria-selected={a.id === areaId} onClick={() => selectArea(a.id)}>
              {a.kind === 'field' ? 'Field' : `Floor ${a.floorNumber}`}
            </button>
          ))}
        </div>
      )}

      <div className="realm">
        <div className="realm__map">
          {svg.isPending && <p className="muted">Drawing the map…</p>}
          {svg.isError && <p className="error-text">{errorMessage(svg.error)}</p>}
          {svg.data && <SvgViewer svg={svg.data} highlight={highlight} />}
          <div className="actions">
            <a className="button" href={exportUrl(summary.id, 'svg', areaId)} download>
              Download SVG
            </a>
            <a className="button" href={exportUrl(summary.id, 'prompt', areaId)} download>
              Image prompt
            </a>
            <a className="button" href={exportUrl(summary.id, 'md')} download>
              Placement sheet (Markdown)
            </a>
            <a className="button" href={exportUrl(summary.id, 'json')} download>
              JSON
            </a>
          </div>
        </div>

        <aside className="placements" aria-label="Placements">
          <h2>Placements</h2>
          <p className="muted">Click one to highlight it on the map.</p>
          <ul>
            {area && placementsOf(area).map((p) => (
              <li key={p.id} className={p.isEntry ? 'placements__entry' : undefined}>
                <button type="button" aria-pressed={highlight === p.id} onClick={() => setHighlight(highlight === p.id ? undefined : p.id)}>
                  <strong>{p.id}</strong> {p.name} <span className="muted">{p.location}</span>
                </button>
                {p.detail && <span className="placements__mobs">{p.detail}</span>}
              </li>
            ))}
          </ul>
        </aside>
      </div>
    </section>
  )
}

function title(realm: RealmSummary) {
  return realm.components?.mapName ?? `${label(realm.biome)} ${label(realm.kind).toLowerCase()}`
}

function componentsLine(realm: RealmSummary) {
  const c = realm.components!
  return [c.needle.name, c.thread.name, c.coating.name].join(' · ')
}

function MirrorBadge() {
  return (
    <span className="badge badge--warn" title="Woven from a recorded map with different components: same layout, rerolled encounters.">
      Mirror
    </span>
  )
}

interface Placement {
  id: string
  name: string
  location: string
  detail?: string
  /** Where the party enters the area: the spawn, a dungeon entrance, the way up, or the host structure. */
  isEntry: boolean
}

const entryFeatures = new Set(['dungeonEntrance', 'stairsUp'])

/** Events and features for the placements list, with the ways in first so they're easy to find. */
function placementsOf(area: RealmArea): Placement[] {
  const spawn: Placement[] = area.spawn
    ? [{ id: 'S', name: 'Spawn point', location: tileLabel(area.spawn.x ?? 0, area.spawn.y ?? 0), isEntry: true }]
    : []
  const events: Placement[] = area.events.map((e) => ({
    id: e.id,
    name: e.name,
    location: e.location ?? '',
    detail: e.mobs && e.mobs.length > 0 ? e.mobs.map((m) => `${m.count}× ${m.name}`).join(', ') : undefined,
    isEntry: e.category === 'structure',
  }))
  const features: Placement[] = (area.features ?? []).map((f) => ({
    id: f.id,
    name: label(f.kind),
    location: f.location ?? '',
    detail: f.description,
    isEntry: entryFeatures.has(f.kind),
  }))
  const all = [...events, ...features]
  return [...spawn, ...all.filter((p) => p.isEntry), ...all.filter((p) => !p.isEntry)]
}
