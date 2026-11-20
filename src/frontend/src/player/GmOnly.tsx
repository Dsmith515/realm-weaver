import type { ReactNode } from 'react'
import { useRole } from './roleContext'

/**
 * Shows its page only in GM view. In player view it explains why the page is hidden and offers to switch back,
 * rather than redirecting, so a shared link still makes sense. This is a preview of what players will see, not
 * access control: the API is open until authentication arrives (ADR 0012).
 */
export function GmOnly({ title, children }: { title: string; children: ReactNode }) {
  const { role, setRole } = useRole()
  if (role === 'gm') return children

  return (
    <section className="gm-only">
      <h1>{title}</h1>
      <p>This page is for the GM. Players don't see it.</p>
      <button type="button" onClick={() => setRole('gm')}>
        Switch to GM view
      </button>
    </section>
  )
}
