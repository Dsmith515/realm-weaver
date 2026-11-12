import type { Component } from '../api/types'
import { label } from '../api/types'

/** A one-line summary of a component's type-specific details. */
export function describe(c: Component): string {
  if (c.needle) return `Tier ${c.needle.difficultyTier} · ${c.needle.category}`
  if (c.thread) return `${label(c.thread.biome)} · ${label(c.thread.element)}`
  if (c.coating) return `${c.coating.mobTypes.join(', ')} · ${label(c.coating.realmKind)}${c.coating.floorCount ? ` · ${c.coating.floorCount} floor${c.coating.floorCount > 1 ? 's' : ''}` : ''}`
  if (c.map) return `${c.map.width}×${c.map.height} · seed ${c.map.seed}`
  return ''
}
