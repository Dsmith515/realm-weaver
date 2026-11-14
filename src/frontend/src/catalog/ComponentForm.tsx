import { useState, type FormEvent, type ReactNode } from 'react'
import { ApiError, errorMessage } from '../api/client'
import { useSaveComponent } from '../api/queries'
import type { Component, ComponentInput, ComponentType } from '../api/types'
import { biomes, elements, label, rarities, realmKinds } from '../api/types'

interface ComponentFormProps {
  /** Edit this component, or create a new one when undefined. */
  component?: Component
  onDone: () => void
}

const creatableTypes = ['needle', 'thread', 'coating'] as const satisfies readonly ComponentType[]

/** Create or edit a component. Validation is the server's: its field errors are shown next to the fields. */
export function ComponentForm({ component, onDone }: ComponentFormProps) {
  const [input, setInput] = useState<ComponentInput>(() => (component ? toInput(component) : blank('needle')))
  const [abilities, setAbilities] = useState(() => (input.needle?.abilities ?? []).join(', '))
  const [mobTypes, setMobTypes] = useState(() => (input.coating?.mobTypes ?? []).join(', '))
  const save = useSaveComponent()
  const error = save.error instanceof ApiError ? save.error : undefined
  const fieldError = (field: string) => error?.fieldError(field)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const body: ComponentInput = {
      ...input,
      needle: input.needle && { ...input.needle, abilities: splitList(abilities) },
      coating: input.coating && { ...input.coating, mobTypes: splitList(mobTypes) },
    }
    try {
      await save.mutateAsync({ id: component?.id, input: body })
      onDone()
    } catch {
      // Shown from save.error below.
    }
  }

  return (
    <form className="card form" onSubmit={(e) => void submit(e)} aria-label={component ? `Edit ${component.name}` : 'New component'}>
      <h2>{component ? `Edit ${component.name}` : 'New component'}</h2>

      {!component && (
        <Field label="Type" error={fieldError('type')}>
          <select value={input.type} onChange={(e) => setInput(blank(e.target.value as ComponentType, input))}>
            {creatableTypes.map((t) => (
              <option key={t} value={t}>
                {label(t)}
              </option>
            ))}
          </select>
        </Field>
      )}
      <Field label="Name" error={fieldError('name')}>
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
      <label className="checkbox">
        <input type="checkbox" checked={input.isPlayerAssignable} onChange={(e) => setInput({ ...input, isPlayerAssignable: e.target.checked })} />
        Players may add this themselves
      </label>

      {input.needle && (
        <fieldset>
          <legend>Needle</legend>
          <Field label="Difficulty tier" error={fieldError('needle.difficultyTier')}>
            <input
              type="number"
              value={input.needle.difficultyTier}
              onChange={(e) => setInput({ ...input, needle: { ...input.needle!, difficultyTier: Number(e.target.value) } })}
            />
          </Field>
          <Field label="Category" error={fieldError('needle.category')}>
            <input value={input.needle.category} onChange={(e) => setInput({ ...input, needle: { ...input.needle!, category: e.target.value } })} />
          </Field>
          <Field label="Abilities (comma separated)" error={fieldError('needle.abilities')}>
            <input value={abilities} onChange={(e) => setAbilities(e.target.value)} />
          </Field>
        </fieldset>
      )}

      {input.thread && (
        <fieldset>
          <legend>Thread</legend>
          <Field label="Biome" error={fieldError('thread.biome')}>
            <select value={input.thread.biome} onChange={(e) => setInput({ ...input, thread: { ...input.thread!, biome: e.target.value as (typeof biomes)[number] } })}>
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
            <input value={mobTypes} onChange={(e) => setMobTypes(e.target.value)} placeholder="beast, undead" />
          </Field>
          <Field label="Realm kind" error={fieldError('coating.realmKind')}>
            <select
              value={input.coating.realmKind}
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
              disabled={input.coating.realmKind === 'field'}
              onChange={(e) => setInput({ ...input, coating: { ...input.coating!, floorCount: Number(e.target.value) } })}
            />
          </Field>
        </fieldset>
      )}

      {input.map && <p className="muted">Maps are made by rituals; their seed and components can't be edited.</p>}

      {save.isError && !error?.problem.errors && (
        <p className="error-text" role="alert">
          {errorMessage(save.error)}
        </p>
      )}
      <div className="actions">
        <button type="submit" className="button--primary" disabled={save.isPending}>
          {component ? 'Save' : 'Create'}
        </button>
        <button type="button" className="button--quiet" onClick={onDone}>
          Cancel
        </button>
      </div>
    </form>
  )
}

function Field({ label: text, error, children }: { label: string; error?: string; children: ReactNode }) {
  return (
    <label className="field">
      <span>{text}</span>
      {children}
      {error && <span className="error-text">{error}</span>}
    </label>
  )
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
