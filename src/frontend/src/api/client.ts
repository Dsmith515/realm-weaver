export interface ApiInfo {
  name: string
  version: string
}

export async function getJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, { headers: { Accept: 'application/json' }, ...init })
  if (!response.ok) {
    throw new Error(`Request to ${path} failed with ${response.status}`)
  }
  return (await response.json()) as T
}

export const fetchApiInfo = () => getJson<ApiInfo>('/api/info')
