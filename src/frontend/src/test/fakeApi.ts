import { vi } from 'vitest'

export interface ApiCall {
  method: string
  path: string
  query: URLSearchParams
  body: unknown
  headers: Headers
}

type Handler = (call: ApiCall) => unknown

/**
 * Stubs `fetch` with a tiny router. Keys look like `GET /api/players` or `POST /api/players/:id/inventory`.
 * A handler returns a body (sent as JSON with 200), a Response, or a function result of either.
 * Unmatched requests get a 404 problem, and every call is recorded in `calls`.
 */
export function fakeApi(routes: Record<string, unknown>) {
  const calls: ApiCall[] = []
  const compiled = Object.entries(routes).map(([key, handler]) => {
    const [method, pattern] = key.split(' ')
    const regex = new RegExp(`^${pattern.replace(/:[^/]+/g, '[^/]+')}$`)
    return { method, regex, handler }
  })

  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = input instanceof Request ? input : new Request(new URL(String(input), 'http://localhost:3000'), init)
    const url = new URL(request.url)
    const text = request.method === 'GET' || request.method === 'HEAD' ? '' : await request.clone().text()
    const call: ApiCall = {
      method: request.method,
      path: url.pathname,
      query: url.searchParams,
      body: text ? JSON.parse(text) : undefined,
      headers: request.headers,
    }
    calls.push(call)

    const route = compiled.find((r) => r.method === call.method && r.regex.test(call.path))
    if (!route) return problem(404, `No fake for ${call.method} ${call.path}`)
    const result = typeof route.handler === 'function' ? await (route.handler as Handler)(call) : route.handler
    return result instanceof Response ? result : json(result)
  })

  vi.stubGlobal('fetch', fetchMock)
  return {
    calls,
    /** Calls to one method and path, oldest first. */
    callsTo: (method: string, path: string) => calls.filter((c) => c.method === method && c.path === path),
  }
}

export function json(body: unknown, status = 200, headers: Record<string, string> = {}) {
  return new Response(status === 204 ? null : JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json', ...headers },
  })
}

export function problem(status: number, title: string, errors?: Record<string, string[]>) {
  return new Response(JSON.stringify({ status, title, errors }), {
    status,
    headers: { 'content-type': 'application/problem+json' },
  })
}
