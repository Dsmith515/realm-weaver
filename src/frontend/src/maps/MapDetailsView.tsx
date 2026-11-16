import { Link } from 'react-router'
import { errorMessage } from '../api/client'
import { useMapDetails } from '../api/queries'
import type { DigestEntry, MapDetails } from '../api/types'
import { label } from '../api/types'
import { Modal } from '../components/Modal'

/**
 * Who's looking. A player sees what a map promises: kinds of creatures and loot, objectives by name, and tags for the
 * rules that limit them. The GM also sees counts, where everything is and the original realm.
 */
export type Audience = 'player' | 'gm'

type Flags = NonNullable<NonNullable<MapDetails['map']['map']>['flags']>

const tagHelp = {
  mapRequired: 'Map required: only players holding this map can enter. The same components alone won’t open it.',
  unique: 'Unique: only one player can hold this map at a time, so it can’t be shared or handed out twice.',
  noMirrors: 'No mirrors: it must be woven with its own components. Different ones won’t open a mirrored version.',
}

/** Styled tags for the rules that limit a map, with the explanation on hover or focus. Nothing for an open map. */
export function MapTags({ flags }: { flags: Flags | null | undefined }) {
  const tags = [
    flags?.mapRequired && { key: 'mapRequired', text: 'Map required', help: tagHelp.mapRequired },
    flags?.unique && { key: 'unique', text: 'Unique', help: tagHelp.unique },
    flags?.componentsMustMatch && { key: 'noMirrors', text: 'No mirrors', help: tagHelp.noMirrors },
  ].filter((t): t is { key: string; text: string; help: string } => !!t)
  if (tags.length === 0) return null
  return (
    <ul className="map-tags" aria-label="Map rules">
      {tags.map((t) => (
        <li key={t.key} className={`map-tag map-tag--${t.key}`} tabIndex={0} data-tip={t.help} aria-label={t.help}>
          {t.text}
        </li>
      ))}
    </ul>
  )
}

/** A map's seed, recipe and contents, loaded by id. */
export function MapDetailsView({ mapId, audience }: { mapId: string; audience: Audience }) {
  const details = useMapDetails(mapId)
  if (details.isPending) return <p className="muted">Loading the map…</p>
  if (details.isError) return <p className="error-text">{errorMessage(details.error)}</p>
  return <MapDetailsBody details={details.data} audience={audience} />
}

function MapDetailsBody({ details, audience }: { details: MapDetails; audience: Audience }) {
  const { map, needle, thread, coating, originalRealm } = details
  const gm = audience === 'gm'
  const recipe = map.map?.recipe
  const digest = originalRealm?.digest
  const objectives = map.map?.objectives ?? []
  return (
    <div className="map-details">
      <MapTags flags={map.map?.flags} />
      <dl className="facts">
        <dt>Seed</dt>
        <dd>{map.map?.seed}</dd>
        <dt>Realm kind</dt>
        <dd>{recipe ? `${label(recipe.kind)}${recipe.floorCount > 0 ? ` (${recipe.floorCount} floor${recipe.floorCount > 1 ? 's' : ''})` : ''}` : '—'}</dd>
        <dt>Threat</dt>
        <dd>{map.map?.threat ?? originalRealm?.threat ?? '—'}</dd>
        <dt>Woven from</dt>
        <dd>{[needle?.name, thread?.name, coating?.name].filter(Boolean).join(' · ') || '—'}</dd>
        {gm && recipe && (
          <>
            <dt>Settlements</dt>
            <dd>
              {recipe.settlementPopulation ? label(recipe.settlementPopulation) : 'Random population'} ·{' '}
              {recipe.includeStructures ? 'structures placed' : recipe.suggestStructures ? 'structure ideas only' : 'no structures'}
            </dd>
          </>
        )}
      </dl>

      {objectives.length > 0 && (
        <>
          <h3>Objectives (complete any one)</h3>
          <ul className="digest" aria-label="Recorded objectives">
            {objectives.map((o) => (
              <li key={o.key}>
                {o.title} {gm && <span className="muted">{o.eventId ? `${o.eventName}, ${o.location}` : 'whole realm'}</span>}
              </li>
            ))}
          </ul>
        </>
      )}

      {!originalRealm && <p className="muted">No realm has been opened from this map yet.</p>}
      {digest && (
        <>
          <h3>{gm ? 'Mobs' : 'Creatures'}</h3>
          {digest.mobs.length === 0 ? (
            <p className="muted">None.</p>
          ) : (
            <ul className="digest" aria-label={gm ? 'Mobs' : 'Creatures'}>
              {digest.mobs.map((m) => (
                <li key={m.archetypeId}>{gm ? `${m.count}× ${m.name}` : m.name}</li>
              ))}
            </ul>
          )}
          <DigestList title="Potential loot" entries={digest.loot} gm={gm} />
          <DigestList title="Resource nodes" entries={digest.resourceNodes} gm={gm} />
          {gm && (
            <p>
              <Link to={`/realms/${originalRealm.realmId}`}>Open the original realm</Link>
            </p>
          )}
        </>
      )}
    </div>
  )
}

/** The GM sees each find and where it is; a player sees only what kinds of things can be found. */
function DigestList({ title, entries, gm }: { title: string; entries: DigestEntry[]; gm: boolean }) {
  const names = [...new Set(entries.map((e) => e.name))]
  return (
    <>
      <h3>{title}</h3>
      {entries.length === 0 ? (
        <p className="muted">None.</p>
      ) : (
        <ul className="digest" aria-label={title}>
          {gm
            ? entries.map((e) => (
                <li key={`${e.areaName}-${e.id}`}>
                  {e.name} <span className="muted">
                    {e.areaName}, {e.location}
                  </span>
                </li>
              ))
            : names.map((name) => <li key={name}>{name}</li>)}
        </ul>
      )}
    </>
  )
}

/** GM: a quick look at what a map holds, from anywhere on the GM side. */
export function MapDetailsModal({ mapId, name, onClose }: { mapId: string; name: string; onClose: () => void }) {
  return (
    <Modal title={`${name} details`} onClose={onClose}>
      <h2>{name}</h2>
      <MapDetailsView mapId={mapId} audience="gm" />
      <div className="actions">
        <button type="button" className="button--quiet" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
  )
}
