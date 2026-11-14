import { useEffect, useRef, type ReactNode } from 'react'

interface ModalProps {
  /** The dialog's accessible name. */
  title: string
  onClose: () => void
  children: ReactNode
}

/**
 * A native modal `<dialog>`: the browser handles the backdrop, focus trapping, Escape and returning focus to the
 * control that opened it. Clicking the backdrop closes it too.
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
      <div className="modal__body">{children}</div>
    </dialog>
  )
}
