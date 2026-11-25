import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { errorMessage } from '../api/client'
import { exportUrl, useRealm, useRealms, useRealmSvg } from '../api/queries'
import type { Realm, RealmArea, RealmSummary } from '../api/types'
import { boonLabel, label, tileLabel } from '../api/types'
import { Pager } from '../components/Pager'
import { MapDetailsModal } from '../maps/MapDetailsView'
import { usePlayerSelection } from '../player/playerContext'
import { useRole } from '../player/roleContext'
import { MirrorBadge } from './MirrorBadge'
import { RealmBriefing } from './RealmBriefing'
import { RealmObjectives } from './RealmObjectives'
import { PromptDialog } from './PromptDialog'
import { SvgViewer } from './SvgViewer'

const realmsPageSize = 25

export function RealmsPage() {
  const { playerId } = usePlayerSelection()
  const { role } = useRole()
  // The GM can look at a player's realms or their own Quick Weaves; a player only ever sees their own (ADR 0012).
  const [gmTab, setGmTab] = useState(false)
  const gm = role === 'gm' && gmTab
  // The page belongs to the list it was chosen on. Switching tab, role or player shows another list, which starts at
  // page 1, without an effect to reset it.
  const audience = gm ? 'gm' : `player:${playerId ?? ''}`
  const [paging, setPaging] = useState({ audience, page: 1 })
  const page = paging.audience === audience ? paging.page : 1
  const setPage = (next: number) => setPaging({ audience, page: next })
  const realms = useRealms(gm ? { gm: true, page, pageSize: realmsPageSize } : { playerId, page, pageSize: realmsPageSize }, gm || !!playerId)
  const showTab = (toGm: boolean) => setGmTab(toGm)
  const result = realms.data

  return (
    <section className="page-fill">
      <h1>Realms</h1>
      {role === 'gm' && (
        <div role="tablist" aria-label="Whose realms" className="tabs">
          <button type="button" role="tab" aria-selected={!gm} onClick={() => showTab(false)}>
            Player
          </button>
          <button type="button" role="tab" aria-selected={gm} onClick={() => showTab(true)}>
            GM Quick Weaves
          </button>
        </div>
      )}
      {!gm && !playerId && <p>Choose a player in the header.</p>}
      {realms.isError && <p className="error-text">{errorMessage(realms.error)}</p>}
      {result?.totalCount === 0 && (
        <p>
          No realms yet. {gm ? <Link to="/weave/quick">Quick Weave</Link> : <Link to="/weave/ritual">Perform a ritual</Link>} to open one.
        </p>
      )}
      <div className="scroll-region">
        <ul className="realm-list">
          {result?.items.map((realm) => (
            <li key={realm.id} className="card realm-list__item">
              <div>
                <Link to={`/realms/${realm.id}`}>
                  <strong>{title(realm)}</strong>
                </Link>
                {realm.isMirror && <MirrorBadge />}
                {realm.completionCount > 0 && (
                  <span className="badge badge--ok" title="Objectives completed on this realm's map, across every run">
                    Completed ×{realm.completionCount}
                  </span>
                )}
                {realm.components && <p className="realm-list__components">{componentsLine(realm)}</p>}
              </div>
              <span className="muted">
                {label(realm.kind)} · threat {realm.threat}
                {realm.dangerModifier > 0 && ` (danger +${realm.dangerModifier})`} · {new Date(realm.createdAt).toLocaleString()}
              </span>
            </li>
          ))}
        </ul>
      </div>
      {result && result.totalCount > 0 && <Pager page={result.page} pageSize={result.pageSize} totalCount={result.totalCount} noun="realms" onPage={setPage} />}
    </section>
  )
}

/** The GM sees the whole realm; a player gets a briefing without the map's secrets (Milestone 8i, ADR 0012). */
export function RealmPage() {
  const { id = '' } = useParams()
  const { role } = useRole()
  return role === 'gm' ? <GmRealmPage id={id} /> : <RealmBriefing realmId={id} />
}

function GmRealmPage({ id }: { id: string }) {
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
  const [promptOpen, setPromptOpen] = useState(false)
  const [detailsOpen, setDetailsOpen] = useState(false)
  const [placementsOpen, setPlacementsOpen] = useState(true)
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
      {summary.components?.attunedNeedle && (
        <p className="realm__attuned">
          <strong>Attuned needle:</strong> {summary.components.attunedNeedle.name}
          {summary.components.attunedNeedle.abilities.length > 0 && ` · abilities: ${summary.components.attunedNeedle.abilities.join(', ')}`}
          {summary.components.attunedNeedle.boon && ` · boon: ${boonLabel(summary.components.attunedNeedle.boon)}`}
        </p>
      )}
      <p className="actions">
        <button type="button" onClick={() => setDetailsOpen(true)}>
          Map details
        </button>
      </p>
      {detailsOpen && (
        <MapDetailsModal
          mapId={summary.mapComponentId}
          name={summary.components?.mapName ?? title(summary)}
          fromRealm={{ id: summary.id, isMirror: !!summary.isMirror }}
          onClose={() => setDetailsOpen(false)}
        />
      )}
      <p className="muted">
        {label(summary.kind)} · Seed {summary.seed} · {summary.width}×{summary.height} · threat {summary.threat}
        {summary.dangerModifier > 0 && ` (mismatch danger +${summary.dangerModifier})`} · generator {summary.generatorVersion}
      </p>

      <RealmObjectives
        realm={realm}
        highlighted={highlight ? `${areaId}/${highlight}` : undefined}
        onShow={(objective) => {
          if (!objective.eventId) return
          if (objective.areaId) setAreaId(objective.areaId)
          setHighlight(objective.eventId)
        }}
      />

      {summary.areas.length > 1 && (
        <div role="tablist" aria-label="Areas" className="tabs">
          {summary.areas.map((a) => (
            <button key={a.id} role="tab" type="button" aria-selected={a.id === areaId} onClick={() => selectArea(a.id)}>
              {a.kind === 'field' ? 'Field' : `Floor ${a.floorNumber}`}
            </button>
          ))}
        </div>
      )}

      <div className={placementsOpen ? 'realm' : 'realm realm--wide'}>
        <div className="realm__map">
          {svg.isPending && <p className="muted">Drawing the map…</p>}
          {svg.isError && <p className="error-text">{errorMessage(svg.error)}</p>}
          {svg.data && (
            <SvgViewer
              svg={svg.data}
              highlight={highlight}
              tools={
                !placementsOpen && (
                  <button type="button" aria-expanded={false} aria-controls="placements" onClick={() => setPlacementsOpen(true)}>
                    Show placements
                  </button>
                )
              }
            />
          )}
          <div className="actions">
            <a className="button" href={exportUrl(summary.id, 'svg', areaId)} download>
              Download SVG
            </a>
            <button type="button" onClick={() => setPromptOpen(true)}>
              Image prompt
            </button>
            <a className="button" href={exportUrl(summary.id, 'md')} download>
              Placement sheet (Markdown)
            </a>
            <a className="button" href={exportUrl(summary.id, 'json')} download>
              JSON
            </a>
          </div>
          {promptOpen && <PromptDialog realmId={summary.id} areaId={areaId} onClose={() => setPromptOpen(false)} />}
        </div>

        <aside className="placements" aria-label="Placements" id="placements" hidden={!placementsOpen}>
          <div className="placements__header">
            <h2>Placements</h2>
            <button type="button" className="button--quiet" aria-expanded aria-controls="placements" onClick={() => setPlacementsOpen(false)} title="Hide placements so the map can use the full width">
              Hide
            </button>
          </div>
          <p className="muted">Click one to highlight it on the map.</p>
          <ul>
            {area && placementsOf(area).map((p) => (
              <li key={p.id} className={p.isEntry ? 'placements__entry' : undefined}>
                <button type="button" aria-pressed={highlight === p.id} onClick={() => setHighlight(highlight === p.id ? undefined : p.id)}>
                  <strong>{p.id}</strong> {p.name} <span className="muted">{p.location}</span>
                </button>
                {p.detail && <span className="placements__mobs">{p.detail}</span>}
                {p.structures && p.structures.length > 0 && (
                  <ul className="placements__structures" aria-label={`Structures in ${p.id}`}>
                    {p.structures.map((s) => (
                      <li key={s}>{s}</li>
                    ))}
                  </ul>
                )}
                {p.ideas && p.ideas.length > 0 && (
                  <p className="placements__ideas" aria-label={`Structure ideas for ${p.id}`}>
                    <span className="badge">Ideas, not placed</span> {p.ideas.join(', ')}
                  </p>
                )}
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

interface Placement {
  id: string
  name: string
  location: string
  detail?: string
  /** Buildings inside a settlement, one line each. */
  structures?: string[]
  /** Structure suggestions for the GM to place themselves. */
  ideas?: string[]
  /** Where the party enters the area: the spawn, a dungeon entrance, the way up, or the host structure. */
  isEntry: boolean
}

const entryFeatures = new Set(['dungeonEntrance', 'stairsUp'])

/** Events and features for the placements list, with the ways in first so they're easy to find. */
function placementsOf(area: RealmArea): Placement[] {
  const spawn: Placement[] = area.spawn
    ? [{ id: 'S', name: 'Spawn point', location: tileLabel(area.spawn.x ?? 0, area.spawn.y ?? 0), isEntry: true }]
    : []
  const events: Placement[] = area.events.map((e) => {
    const mobs = e.mobs && e.mobs.length > 0 ? e.mobs.map((m) => `${m.count}× ${m.name}`).join(', ') : undefined
    const detail = [e.population ? label(e.population) : undefined, e.wealth ? label(e.wealth) : undefined, mobs].filter(Boolean).join(' · ')
    return {
      id: e.id,
      name: e.name,
      location: e.location ?? '',
      detail: detail || undefined,
      structures: e.structures?.map((b) => `${b.id} ${b.name}${b.ruined ? ' (ruined)' : ''} ${b.location ?? ''}`.trim()),
      ideas: e.suggestedStructures?.map((s) => s.name),
      isEntry: e.category === 'structure',
    }
  })
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
