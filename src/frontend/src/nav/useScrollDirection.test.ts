import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useScrollDirection } from './useScrollDirection'

function scrollTo(y: number) {
  act(() => {
    Object.defineProperty(window, 'scrollY', { value: y, configurable: true })
    window.dispatchEvent(new Event('scroll'))
  })
}
afterEach(() => Object.defineProperty(window, 'scrollY', { value: 0, configurable: true }))

describe('useScrollDirection', () => {
  it('starts as up', () => {
    const { result } = renderHook(() => useScrollDirection())
    expect(result.current).toBe('up')
  })

  it('turns down after scrolling down past the threshold', () => {
    const { result } = renderHook(() => useScrollDirection())
    scrollTo(100)
    expect(result.current).toBe('down')
  })

  it('turns up again after scrolling back up past the threshold', () => {
    const { result } = renderHook(() => useScrollDirection())
    scrollTo(100)
    expect(result.current).toBe('down')
    scrollTo(50)
    expect(result.current).toBe('up')
  })

  it('ignores movements smaller than the threshold', () => {
    const { result } = renderHook(() => useScrollDirection())
    scrollTo(100)
    expect(result.current).toBe('down')
    scrollTo(95)
    expect(result.current).toBe('down')
  })

  it('is always up near the top of the page', () => {
    const { result } = renderHook(() => useScrollDirection())
    scrollTo(100)
    expect(result.current).toBe('down')
    scrollTo(4)
    expect(result.current).toBe('up')
  })

  it('stops listening when the component unmounts', () => {
    const add = vi.spyOn(window, 'addEventListener')
    const remove = vi.spyOn(window, 'removeEventListener')
    const { unmount } = renderHook(() => useScrollDirection())

    const handler = add.mock.calls.find(([type]) => type === 'scroll')?.[1]
    expect(handler).toBeDefined()

    unmount()
    expect(remove).toHaveBeenCalledWith('scroll', handler)
  })
})
