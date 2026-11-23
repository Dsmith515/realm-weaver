import { useEffect, useRef, type ReactNode } from 'react'

interface ModalProps {
  /** The dialog's accessible name. */
  title: string
  onClose: () => void
  children: ReactNode
}

/**
 * A native modal `<dialog>`: the browser handles the backdrop, focus trapping, Escape and returning focus to the
 * control that opened it. Clicking the backdrop or the × in the corner closes it too.
 */
export function Modal({ title, onClose, children }: ModalProps) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialog = ref.current
    if (dialog && !dialog.open) dialog.showModal()
    return () => {
      if (dialog?.open) dialog.close()
    }
  }, [])

  return (
    <dialog
      ref={ref}
      className="modal"
      aria-label={title}
      onCancel={(e) => {
        e.preventDefault()
        onClose()
      }}
      onClick={(e) => {
        if (e.target === ref.current) onClose()
      }}
    >
      <button type="button" className="modal__close" aria-label="Close dialog" title="Close" onClick={onClose}>
        <svg aria-hidden="true" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.25" strokeLinecap="round">
          <path d="M6 6l12 12M18 6L6 18" />
        </svg>
      </button>
      <div className="modal__body">{children}</div>
    </dialog>
  )
}
