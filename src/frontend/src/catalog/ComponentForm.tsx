import { useState, type FormEvent, type ReactNode } from 'react'
import { ApiError, errorMessage } from '../api/client'
import { useNameSuffixes, useSaveComponent } from '../api/queries'
import type { Component, ComponentInput, ComponentType, NeedleBoon } from '../api/types'
import { biomes, boonLabel, elements, label, needleBoons, rarities, realmKinds } from '../api/types'
import { creatableTypes, fallbackSuffixes, randomComponent, type RandomizableType, type Rng } from './randomComponent'

interface ComponentFormProps {
  /** Edit this component, or create a new one when undefined. */
  component?: Component
  /** Create a new component pre-filled from this one (Clone). Ignored when editing. */
  template?: Component
  onDone: () => void
  /** For tests: the randomness behind the Randomize button. */
  rng?: Rng
}

/** Create or edit a component. Validation is the server's: its field errors are shown next to the fields. */
export function ComponentForm({ component, template, onDone, rng }: ComponentFormProps) {
  const [input, setInput] = useState<ComponentInput>(() => (component ? toInput(component) : template ? toInput(template) : blank('needle')))
  const [abilities, setAbilities] = useState(() => (input.needle?.abilities ?? []).join(', '))
  const [mobTypes, setMobTypes] = useState(() => (input.coating?.mobTypes ?? []).join(', '))
  const save = useSaveComponent()
  const suffixes = useNameSuffixes().data ?? fallbackSuffixes
  const error = save.error instanceof ApiError ? save.error : undefined
  const fieldError = (field: string) => error?.fieldError(field)
  const usedBy = component?.usedByMapCount ?? 0
  // Once a map was woven from it, the fields that shape a realm can't change (the server answers 409).
  const locked = usedBy > 0
  const allowedSuffixes = input.type === 'map' ? undefined : suffixes[input.type]
  const title = component ? `Edit ${component.name}` : template ? `Clone ${template.name}` : 'New component'

  const randomize = () => {
    const next = randomComponent(input.type as RandomizableType, suffixes, rng)
    setInput(next)
    setAbilities((next.needle?.abilities ?? []).join(', '))
    setMobTypes((next.coating?.mobTypes ?? []).join(', '))
  }

  const body = withLists(input, abilities, mobTypes)
  // A clone identical to its template would just be a duplicate, so it needs at least one change. A new name counts,
  // but keeping the old one gets a warning: two components with one name are hard to tell apart.
  const original = template && !component ? toInput(template) : undefined
  const unchangedClone = original !== undefined && sameInput(body, withLists(original, (original.needle?.abilities ?? []).join(', '), (original.coating?.mobTypes ?? []).join(', ')))
  const sameNameAsOriginal = original !== undefined && !unchangedClone && body.name.trim() === original.name.trim()

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (unchangedClone) return
    try {
      await save.mutateAsync({ id: component?.id, input: body })
      onDone()
    } catch {
      // Shown from save.error below.
    }
  }

  return (
    <form className="form" onSubmit={(e) => void submit(e)} aria-label={title}>
      <h2>{title}</h2>

      {!component && !template && (
        <div className="form__row">
          <Field label="Type" error={fieldError('type')}>
            <select value={input.type} onChange={(e) => setInput(blank(e.target.value as ComponentType, input))}>
              {creatableTypes.map((t) => (
                <option key={t} value={t}>
                  {label(t)}
                </option>
              ))}
            </select>
          </Field>
          <button type="button" onClick={randomize}>
            Randomize
          </button>
        </div>
      )}
      {locked && (
        <p className="banner banner--warn" role="note">
          Used by {usedBy === 1 ? '1 map' : `${usedBy} maps`}, so the fields that shape a realm are locked. Clone it to make a variant.
        </p>
      )}
      <Field label="Name" error={fieldError('name')} hint={allowedSuffixes && `Ends with: ${allowedSuffixes.join(', ')}`}>
        <input value={input.name} onChange={(e) => setInput({ ...input, name: e.target.value })} />
      </Field>
      <Field label="Rarity" error={fieldError('rarity')}>
        <select value={input.rarity} onChange={(e) => setInput({ ...input, rarity: e.target.value as ComponentInput['rarity'] })}>
          {rarities.map((r) => (
            <option key={r} value={r}>
              {label(r)}
            </option>
          ))}
        </select>
      </Field>
      <Field label="Rank" error={fieldError('rank')}>
        <input type="number" value={input.rank} onChange={(e) => setInput({ ...input, rank: Number(e.target.value) })} />
      </Field>
      <Field label="Description" error={fieldError('description')}>
        <textarea value={input.description ?? ''} onChange={(e) => setInput({ ...input, description: e.target.value })} rows={2} />
      </Field>
      <label className="checkbox switch">
        <input type="checkbox" role="switch" checked={input.isPlayerAssignable} onChange={(e) => setInput({ ...input, isPlayerAssignable: e.target.checked })} />
        Players may add this themselves
      </label>

      {input.needle && (
        <fieldset>
          <legend>Needle</legend>
          <Field label="Difficulty tier" error={fieldError('needle.difficultyTier')}>
            <input
              type="number"
              value={input.needle.difficultyTier}
              disabled={locked}
              onChange={(e) => setInput({ ...input, needle: { ...input.needle!, difficultyTier: Number(e.target.value) } })}
            />
          </Field>
          <Field label="Category" error={fieldError('needle.category')}>
            <input value={input.needle.category} onChange={(e) => setInput({ ...input, needle: { ...input.needle!, category: e.target.value } })} />
          </Field>
          <Field label="Abilities (comma separated)" error={fieldError('needle.abilities')}>
            <input value={abilities} onChange={(e) => setAbilities(e.target.value)} />
          </Field>
          <Field
            label="Base type"
            error={fieldError('needle.baseType')}
            hint="Map recipes match this, so any needle of the same base type fits. Blank means the name."
          >
            <input
              value={input.needle.baseType ?? ''}
              disabled={locked}
              placeholder={input.name}
              onChange={(e) => setInput({ ...input, needle: { ...input.needle!, baseType: e.target.value || null } })}
            />
          </Field>
          <Field label="Boon" error={fieldError('needle.boon')} hint="A passive benefit while attuned. At most one.">
            <select
              value={input.needle.boon ?? ''}
              onChange={(e) => setInput({ ...input, needle: { ...input.needle!, boon: (e.target.value || null) as NeedleBoon | null } })}
            >
              <option value="">None</option>
              {needleBoons.map((b) => (
                <option key={b} value={b}>
                  {boonLabel(b)}
                </option>
              ))}
            </select>
          </Field>
        </fieldset>
      )}

      {input.thread && (
        <fieldset>
          <legend>Thread</legend>
          <Field label="Biome" error={fieldError('thread.biome')}>
            <select value={input.thread.biome} disabled={locked} onChange={(e) => setInput({ ...input, thread: { ...input.thread!, biome: e.target.value as (typeof biomes)[number] } })}>
              {biomes.map((b) => (
                <option key={b} value={b}>
                  {label(b)}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Element" error={fieldError('thread.element')}>
            <select
              value={input.thread.element}
              disabled={locked}
              onChange={(e) => setInput({ ...input, thread: { ...input.thread!, element: e.target.value as (typeof elements)[number] } })}
            >
              {elements.map((el) => (
                <option key={el} value={el}>
                  {label(el)}
                </option>
              ))}
            </select>
          </Field>
        </fieldset>
      )}

      {input.coating && (
        <fieldset>
          <legend>Coating</legend>
          <Field label="Mob types (comma separated)" error={fieldError('coating.mobTypes')}>
            <input value={mobTypes} disabled={locked} onChange={(e) => setMobTypes(e.target.value)} placeholder="beast, undead" />
          </Field>
          <Field label="Realm kind" error={fieldError('coating.realmKind')}>
            <select
              value={input.coating.realmKind}
              disabled={locked}
              onChange={(e) => {
                const realmKind = e.target.value as (typeof realmKinds)[number]
                setInput({ ...input, coating: { ...input.coating!, realmKind, floorCount: realmKind === 'field' ? 0 : Math.max(1, input.coating!.floorCount) } })
              }}
            >
              {realmKinds.map((k) => (
                <option key={k} value={k}>
                  {label(k)}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Dungeon floors" error={fieldError('coating.floorCount')}>
            <input
              type="number"
              value={input.coating.floorCount}
              disabled={locked || input.coating.realmKind === 'field'}
              onChange={(e) => setInput({ ...input, coating: { ...input.coating!, floorCount: Number(e.target.value) } })}
            />
          </Field>
        </fieldset>
      )}

      {input.map && (
        <fieldset>
          <legend>Map rules</legend>
          <p className="muted">Maps are made by rituals; their seed and components can't be edited.</p>
          {(
            [
              ['mapRequired', 'Map required', 'Sealed: players must hold this map to enter. Off: anyone with the components can.'],
              ['componentsMustMatch', 'Components must match', 'Only the recorded recipe opens it: no mirrors.'],
              ['unique', 'Unique', 'Only one player can hold it, so it can’t be shared or assigned twice.'],
            ] as const
          ).map(([flag, text, hint]) => (
            <label key={flag} className="checkbox switch" title={hint}>
              <input
                type="checkbox"
                role="switch"
                checked={flagsOf(input.map!)[flag]}
                onChange={(e) => setInput({ ...input, map: { ...input.map!, flags: { ...flagsOf(input.map!), [flag]: e.target.checked } } })}
              />
              {text} <span className="muted">— {hint}</span>
            </label>
          ))}
        </fieldset>
      )}

      {save.isError && !error?.problem.errors && (
        <p className="error-text" role="alert">
          {errorMessage(save.error)}
        </p>
      )}
      {unchangedClone && (
        <p className="field__hint" role="note">
          Change something to create a copy. An identical clone would just be a duplicate.
        </p>
      )}
      {sameNameAsOriginal && (
        <p className="banner banner--warn" role="note">
          The copy keeps the name {original.name}. Consider renaming it so the two are easy to tell apart.
        </p>
      )}
      <div className="actions">
        <button type="submit" className="button--primary" disabled={save.isPending || unchangedClone}>
          {component ? 'Save' : template ? 'Create copy' : 'Create'}
        </button>
        <button type="button" className="button--quiet" onClick={onDone}>
          Cancel
        </button>
      </div>
    </form>
  )
}

function Field({ label: text, error, hint, children }: { label: string; error?: string; hint?: string; children: ReactNode }) {
  return (
    <div className="field">
      <label className="field__label">
        <span>{text}</span>
        {children}
      </label>
      {hint && !error && <span className="field__hint">{hint}</span>}
      {error && <span className="error-text">{error}</span>}
    </div>
  )
}

/** The input with the comma-separated fields split into lists, as the API takes it. */
function withLists(input: ComponentInput, abilities: string, mobTypes: string): ComponentInput {
  return {
    ...input,
    needle: input.needle && { ...input.needle, abilities: splitList(abilities) },
    coating: input.coating && { ...input.coating, mobTypes: splitList(mobTypes) },
  }
}

/** Equal once trimmed, treating a missing value and null alike, the way the server would store them. */
function sameInput(a: ComponentInput, b: ComponentInput): boolean {
  const normalize = (value: ComponentInput) =>
    JSON.stringify(value, (_, v: unknown) => (v === undefined || v === '' ? null : typeof v === 'string' ? v.trim() : v))
  return normalize(a) === normalize(b)
}

function splitList(text: string): string[] {
  return text
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean)
}

function blank(type: ComponentType, from?: ComponentInput): ComponentInput {
  return {
    type,
    name: from?.name ?? '',
    rarity: from?.rarity ?? 'common',
    rank: from?.rank ?? 1,
    description: from?.description ?? '',
    isPlayerAssignable: from?.isPlayerAssignable ?? false,
    needle: type === 'needle' ? { difficultyTier: 1, category: '', abilities: [] } : null,
    thread: type === 'thread' ? { biome: 'forest', element: 'none' } : null,
    coating: type === 'coating' ? { mobTypes: [], realmKind: 'field', floorCount: 0 } : null,
    map: null,
  }
}

function toInput(c: Component): ComponentInput {
  return {
    type: c.type,
    name: c.name,
    rarity: c.rarity,
    rank: c.rank,
    description: c.description,
    isPlayerAssignable: c.isPlayerAssignable,
    needle: c.needle ?? null,
    thread: c.thread ?? null,
    coating: c.coating ?? null,
    map: c.map ?? null,
  }
}

/** A map's rules, with the server's defaults (open, mirrors allowed, shareable; ADR 0010). */
function flagsOf(map: NonNullable<ComponentInput['map']>) {
  return { mapRequired: false, componentsMustMatch: false, unique: false, ...(map.flags ?? {}) }
}
