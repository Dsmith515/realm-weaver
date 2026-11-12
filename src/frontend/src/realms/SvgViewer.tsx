import { useEffect, useRef, useState, type PointerEvent } from 'react'

interface SvgViewerProps {
  /** SVG markup from our own export endpoint (the exporter escapes every text value). */
  svg: string
  /** The id of an event (`E3`) or feature (`T1`) to highlight. The exporter tags them with data attributes. */
  highlight?: string
}

const minScale = 0.25
const maxScale = 6

/** Shows the server-rendered map with wheel or button zoom and drag to pan. */
export function SvgViewer({ svg, highlight }: SvgViewerProps) {
  const frame = useRef<HTMLDivElement>(null)
  const content = useRef<HTMLDivElement>(null)
  const drag = useRef<{ pointerX: number; pointerY: number; x: number; y: number } | undefined>(undefined)
  const [view, setView] = useState({ scale: 1, x: 0, y: 0 })

  const zoom = (factor: number) =>
    setView((v) => ({ ...v, scale: Math.min(maxScale, Math.max(minScale, v.scale * factor)) }))

  // React's onWheel is passive, so preventDefault (to stop the page scrolling) needs a native listener.
  useEffect(() => {
    const element = frame.current
    if (!element) return
    const onWheel = (event: WheelEvent) => {
      event.preventDefault()
      zoom(event.deltaY < 0 ? 1.15 : 1 / 1.15)
    }
    element.addEventListener('wheel', onWheel, { passive: false })
    return () => element.removeEventListener('wheel', onWheel)
  }, [])

  useEffect(() => {
    const root = content.current
    if (!root) return
    root.querySelectorAll('.rw-highlight').forEach((el) => el.classList.remove('rw-highlight'))
    if (highlight && /^[\w-]+$/.test(highlight)) {
      root.querySelector(`[data-event-id="${highlight}"], [data-feature-id="${highlight}"]`)?.classList.add('rw-highlight')
    }
  }, [svg, highlight])

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    drag.current = { pointerX: event.clientX, pointerY: event.clientY, x: view.x, y: view.y }
    event.currentTarget.setPointerCapture?.(event.pointerId)
  }
  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const start = drag.current
    if (!start) return
    setView((v) => ({ ...v, x: start.x + event.clientX - start.pointerX, y: start.y + event.clientY - start.pointerY }))
  }
  const endDrag = () => {
    drag.current = undefined
  }

  return (
    <div className="svg-viewer">
      <div className="svg-viewer__tools">
        <button type="button" onClick={() => zoom(1.25)} aria-label="Zoom in">
          +
        </button>
        <button type="button" onClick={() => zoom(0.8)} aria-label="Zoom out">
          −
        </button>
        <button type="button" onClick={() => setView({ scale: 1, x: 0, y: 0 })}>
          Reset view
        </button>
        <span className="muted">{Math.round(view.scale * 100)}%</span>
      </div>
      <div
        ref={frame}
        className="svg-viewer__frame"
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
      >
        <div
          ref={content}
          className="svg-viewer__content"
          data-testid="realm-svg"
          style={{ transform: `translate(${view.x}px, ${view.y}px) scale(${view.scale})` }}
          dangerouslySetInnerHTML={{ __html: svg }}
        />
      </div>
    </div>
  )
}
