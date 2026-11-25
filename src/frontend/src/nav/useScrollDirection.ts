/** Which way the page last moved by more than the threshold. */
export type ScrollDirection = 'up' | 'down'

/**
 * Hide-on-scroll for the phone header and tab bar (Layout.tsx). Scrolling down hides them; scrolling up shows them.
 *
 * The maintainer's piece for the phone nav (Milestone 8i). Until it's written this always answers 'up', so the bars
 * simply stay put. The tests waiting for it are in useScrollDirection.test.ts.
 *
 * @param threshold Ignore movements smaller than this many pixels, so a shaky thumb doesn't flicker the bars.
 */
export function useScrollDirection(threshold = 8): ScrollDirection {
  void threshold
  return 'up'
}
