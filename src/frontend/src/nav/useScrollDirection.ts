import { useEffect, useState } from 'react'

/** Which way the page last moved by more than the threshold. */
export type ScrollDirection = 'up' | 'down'

/**
 * Hide-on-scroll for the phone header and tab bar (Layout.tsx). Scrolling down hides them; scrolling up shows them.
 *
 * @param threshold Ignore movements smaller than this many pixels, so a shaky thumb doesn't flicker the bars.
 */
export function useScrollDirection(threshold = 8): ScrollDirection {
  const [direction, setDirection] = useState<ScrollDirection>('up')

  useEffect(() => {
    let lastY = window.scrollY

    const onScroll = () => {
      const y = window.scrollY
      
      if (y <= threshold) {
        setDirection('up')    // near the top: always show the bars
        lastY = y
        return
      }
      const delta = y - lastY
      if (Math.abs(delta) < threshold) return
      setDirection(delta > 0 ? 'down' : 'up')
      lastY = y
    }

    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [threshold])

  return direction
}
