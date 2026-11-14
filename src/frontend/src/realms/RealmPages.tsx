import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { errorMessage } from '../api/client'
import { exportUrl, useRealm, useRealms, useRealmSvg } from '../api/queries'
import type { Realm, RealmSummary } from '../api/types'
import { label } from '../api/types'
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
          <li key={realm.id} className="card">
            <Link to={`/realms/${realm.id}`}>
              <strong>{title(realm)}</strong>
            </Link>
            <span className="muted">
              {realm.width}×{realm.height} · threat {realm.threat}
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
      <h1>{title(summary)}</h1>
      <p className="muted">
        Seed {summary.seed} · {summary.width}×{summary.height} · threat {summary.threat}
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
            {area?.events.map((e) => (
              <li key={e.id}>
                <button type="button" aria-pressed={highlight === e.id} onClick={() => setHighlight(highlight === e.id ? undefined : e.id)}>
                  <strong>{e.id}</strong> {e.name} <span className="muted">{e.location}</span>
                </button>
                {e.mobs && e.mobs.length > 0 && <span className="placements__mobs">{e.mobs.map((m) => `${m.count}× ${m.name}`).join(', ')}</span>}
              </li>
            ))}
            {area?.features?.map((f) => (
              <li key={f.id}>
                <button type="button" aria-pressed={highlight === f.id} onClick={() => setHighlight(highlight === f.id ? undefined : f.id)}>
                  <strong>{f.id}</strong> {label(f.kind)} <span className="muted">{f.location}</span>
                </button>
                <span className="placements__mobs">{f.description}</span>
              </li>
            ))}
          </ul>
        </aside>
      </div>
    </section>
  )
}

function title(realm: RealmSummary) {
  return `${label(realm.biome)} ${label(realm.kind).toLowerCase()}`
}
