import createClient from 'openapi-fetch'
import type { components, paths } from './schema'

export type Schemas = components['schemas']

export interface ApiInfo {
  name: string
  version: string
}

/** RFC 9457 problem details, as every API error is returned. `errors` holds per-field validation messages. */
export interface Problem {
  status?: number
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: Problem

  constructor(status: number, problem: Problem) {
    super(problem.detail ?? problem.title ?? `Request failed with ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  /** The first message for a field, matched case-insensitively (the API uses camelCase paths like `needle.category`). */
  fieldError(field: string): string | undefined {
    const entry = Object.entries(this.problem.errors ?? {}).find(([key]) => key.toLowerCase() === field.toLowerCase())
    return entry?.[1][0]
  }
}

/**
 * Typed client generated from the API's OpenAPI document (see `npm run api:types`).
 * `fetch` is looked up on every call so tests can stub it.
 */
export const api = createClient<paths>({
  baseUrl: typeof window === 'undefined' ? '' : window.location.origin,
  fetch: (request) => globalThis.fetch(request),
})

interface FetchResult<T> {
  data?: T
  error?: unknown
  response: Response
}

/** Returns the data, or throws an {@link ApiError} carrying the ProblemDetails body. */
export async function unwrap<T>(call: Promise<FetchResult<T>>): Promise<T> {
  let result: FetchResult<T>
  try {
    result = await call
  } catch (cause) {
    throw new ApiError(0, { title: 'The API is unreachable.', detail: cause instanceof Error ? cause.message : String(cause) })
  }

  const { data, error, response } = result
  if (!response.ok) {
    const problem = typeof error === 'object' && error !== null ? (error as Problem) : { title: String(error ?? '') }
    throw new ApiError(response.status, { status: response.status, ...problem })
  }
  return data as T
}

export async function getJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, { headers: { Accept: 'application/json' }, ...init })
  if (!response.ok) {
    throw new Error(`Request to ${path} failed with ${response.status}`)
  }
  return (await response.json()) as T
}

export const fetchApiInfo = () => getJson<ApiInfo>('/api/info')

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const fields = Object.values(error.problem.errors ?? {}).flat()
    return [error.problem.title, error.problem.detail, ...fields].filter(Boolean).join(' ')
  }
  return error instanceof Error ? error.message : String(error)
}
