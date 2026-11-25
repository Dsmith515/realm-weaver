import { useEffect, useRef, useState, type MouseEvent, type SyntheticEvent } from 'react'

/**
 * Drives a native modal `<dialog>` from React state. The dialog stays mounted and is opened with `showModal()` and
 * closed with `close()`, so the browser traps focus inside it and, on closing, returns focus to the control that opened
 * it. Escape, a click on the backdrop, and any close the browser makes on its own all report back through `onClose`.
 *
 * Spread `props` on the `<dialog>`. Give it no padding and put the content in a child element, so a click whose target
 * is the dialog itself can only be a click on the backdrop.
 *
 * Render the content while `shown`. It stays true until the dialog has actually closed, so the focused element is still
 * inside the dialog when the browser hands focus back. Unmounting it first would drop focus onto the page body.
 */
export function useModalDialog(open: boolean, onClose: () => void) {
  const ref = useRef<HTMLDialogElement>(null)
  const [shown, setShown] = useState(open)
  if (open && !shown) setShown(true)

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  return {
    shown,
    props: {
      ref,
      onCancel: (event: SyntheticEvent) => {
        event.preventDefault()
        onClose()
      },
      // Fires after every close, including one the browser makes without a cancellable cancel event (a repeated Escape).
      onClose: () => {
        setShown(false)
        onClose()
      },
      onClick: (event: MouseEvent) => {
        if (event.target === ref.current) onClose()
      },
    },
  }
}
