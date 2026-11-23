import aboutIcon from '../assets/icons/about.svg'
import codexIcon from '../assets/icons/codex.svg'
import componentsIcon from '../assets/icons/components.svg'
import playerIcon from '../assets/icons/player.svg'
import quickWeaveIcon from '../assets/icons/quick-weave.svg'
import realmsIcon from '../assets/icons/realms.svg'
import ritualIcon from '../assets/icons/ritual.svg'
import weaveIcon from '../assets/icons/weave.svg'
import type { Role } from '../player/roleContext'

/** Which theme colour an icon takes (see .nav-icon--* in nav.css). Icons follow the palette. */
export type IconTone = 'accent' | 'accent-2' | 'info' | 'ok' | 'soft' | 'muted'

export interface NavLink {
  kind: 'link'
  label: string
  to: string
  icon: string
  tone: IconTone
  /** One line shown on the sub-page cards (flyout and phone drawer). */
  description?: string
  gmOnly?: boolean
}

export interface NavGroup {
  kind: 'group'
  id: string
  label: string
  icon: string
  tone: IconTone
  children: NavLink[]
}

export type NavItem = NavLink | NavGroup

/**
 * The main navigation, used by the desktop rail and the phone drawer alike. Adding a page is one entry here.
 * Monsters and Abilities join Codex with the Milestone 10 content tables; there are no empty placeholders until then.
 */
export const navItems: NavItem[] = [
  {
    kind: 'group',
    id: 'weave',
    label: 'Weave',
    icon: weaveIcon,
    tone: 'accent',
    children: [
      {
        kind: 'link',
        label: 'Quick Weave',
        to: '/weave/quick',
        icon: quickWeaveIcon,
        tone: 'accent',
        description: 'Generate from the whole catalog. Nothing is used up.',
        gmOnly: true,
      },
      {
        kind: 'link',
        label: 'Ritual',
        to: '/weave/ritual',
        icon: ritualIcon,
        tone: 'accent-2',
        description: 'Use a needle and spend a thread and coating to weave a realm.',
      },
    ],
  },
  { kind: 'link', label: 'Realms', to: '/realms', icon: realmsIcon, tone: 'info' },
  {
    kind: 'group',
    id: 'codex',
    label: 'Codex',
    icon: codexIcon,
    tone: 'accent-2',
    children: [
      {
        kind: 'link',
        label: 'Components',
        to: '/components',
        icon: componentsIcon,
        tone: 'soft',
        description: 'Needles, threads and coatings in the catalog.',
        gmOnly: true,
      },
    ],
  },
  { kind: 'link', label: 'Player', to: '/player', icon: playerIcon, tone: 'ok' },
]

/** Pinned to the bottom of the rail. */
export const aboutItem: NavLink = { kind: 'link', label: 'About', to: '/about', icon: aboutIcon, tone: 'muted' }

/** The items a role sees: GM-only pages drop out in player view, and so does a group left with no pages. */
export function itemsFor(role: Role): NavItem[] {
  const visible = (link: NavLink) => role === 'gm' || !link.gmOnly
  return navItems.flatMap((item): NavItem[] => {
    if (item.kind === 'link') return visible(item) ? [item] : []
    const children = item.children.filter(visible)
    return children.length > 0 ? [{ ...item, children }] : []
  })
}
