import { useQuery } from '@tanstack/react-query'
import { fetchApiInfo } from '../api/client'

export function ApiStatus() {
  const { data, isError, isPending } = useQuery({ queryKey: ['api-info'], queryFn: fetchApiInfo, retry: false })

  if (isPending) return <span className="api-status">API: checking…</span>
  if (isError) return <span className="api-status api-status--down">API: offline</span>
  return <span className="api-status api-status--up">API: {data.version}</span>
}
