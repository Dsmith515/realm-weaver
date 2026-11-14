import { useState } from 'react'
import { errorMessage } from '../api/client'
import { exportUrl, useRealmPrompt } from '../api/queries'
import { Modal } from '../components/Modal'

interface PromptDialogProps {
  realmId: string
  areaId: string | undefined
  onClose: () => void
}

type CopyState = 'idle' | 'copied' | 'failed'

/** Shows the image prompt for one area, with buttons to copy it or download it as a text file. */
export function PromptDialog({ realmId, areaId, onClose }: PromptDialogProps) {
  const prompt = useRealmPrompt(realmId, areaId, true)
  const [copy, setCopy] = useState<CopyState>('idle')

  async function copyPrompt() {
    if (!prompt.data) return
    try {
      await navigator.clipboard.writeText(prompt.data)
      setCopy('copied')
    } catch {
      setCopy('failed')
    }
  }

  return (
    <Modal title="Image prompt" onClose={onClose}>
      <h2>Image prompt</h2>
      {prompt.isPending && <p className="muted">Loading the prompt…</p>}
      {prompt.isError && <p className="error-text">{errorMessage(prompt.error)}</p>}
      {prompt.data && (
        <pre className="prompt-text" tabIndex={0} aria-label="Prompt text">
          {prompt.data}
        </pre>
      )}
      <div className="actions">
        <button type="button" className="button--primary" onClick={copyPrompt} disabled={!prompt.data}>
          Copy to clipboard
        </button>
        <a className="button" href={exportUrl(realmId, 'prompt', areaId)} download>
          Download prompt
        </a>
        <button type="button" className="button--quiet" onClick={onClose}>
          Close
        </button>
      </div>
      <p role="status" className={copy === 'failed' ? 'error-text' : 'muted'}>
        {copy === 'copied' && 'Copied to the clipboard.'}
        {copy === 'failed' && "Couldn't copy. Select the text and copy it yourself."}
      </p>
    </Modal>
  )
}
