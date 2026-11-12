/**
 * Keeps one Idempotency-Key per ritual attempt. Retrying the same request reuses the key, so a ritual that
 * actually went through (but whose response was lost) is replayed instead of spending twice. Any change to
 * the request, or a finished ritual, starts a new attempt with a new key.
 */
export class IdempotencyKeys {
  private request: string | undefined
  private key: string | undefined
  private readonly newKey: () => string

  constructor(newKey: () => string = () => crypto.randomUUID()) {
    this.newKey = newKey
  }

  keyFor(request: unknown): string {
    const serialized = JSON.stringify(request)
    if (this.key === undefined || serialized !== this.request) {
      this.request = serialized
      this.key = this.newKey()
    }
    return this.key
  }

  /** Call when the server gave a definite answer, so the next ritual gets a fresh key. */
  reset() {
    this.request = undefined
    this.key = undefined
  }
}
