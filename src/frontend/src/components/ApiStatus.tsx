import { useQuery } from '@tanstack/react-query'
import { fetchApiInfo } from '../api/client'

/** A small dot in the header: green when the API answers, red when it doesn't. Hover shows the details. */
export function ApiStatus() {
  const { data, isError, isPending } = useQuery({ queryKey: ['api-info'], queryFn: fetchApiInfo, retry: false })

  if (isPending) return null
  const label = isError ? 'API offline' : `API online (version ${data.version})`
  return <span role="status" className={`api-status ${isError ? 'api-status--down' : 'api-status--up'}`} aria-label={label} title={label} />
}
