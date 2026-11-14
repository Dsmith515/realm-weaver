import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, unwrap } from './client'
import type { ComponentInput, RitualPreviewRequest, RitualSelection } from './types'

/** Query keys in one place, so mutations invalidate exactly what they change. */
export const keys = {
  players: ['players'] as const,
  inventory: (playerId: string) => ['inventory', playerId] as const,
  components: (filters: ComponentFilters) => ['components', filters] as const,
  allComponents: ['components'] as const,
  settings: ['settings'] as const,
  realms: (playerId: string) => ['realms', playerId] as const,
  allRealms: ['realms'] as const,
  realm: (id: string) => ['realm', id] as const,
  preview: (request: RitualPreviewRequest) => ['ritual-preview', request] as const,
  allPreviews: ['ritual-preview'] as const,
}

export interface ComponentFilters {
  type?: string
  rarity?: string
  biome?: string
  search?: string
  assignable?: boolean
  includeArchived?: boolean
  page?: number
  pageSize?: number
}

export function usePlayers() {
  return useQuery({ queryKey: keys.players, queryFn: () => unwrap(api.GET('/api/players')) })
}

export function useCreatePlayer() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (name: string) => unwrap(api.POST('/api/players', { body: { name } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.players }),
  })
}

export function useInventory(playerId: string | undefined) {
  return useQuery({
    queryKey: keys.inventory(playerId ?? ''),
    queryFn: () => unwrap(api.GET('/api/players/{id}/inventory', { params: { path: { id: playerId! } } })),
    enabled: !!playerId,
  })
}

export function useSelfAdd(playerId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { componentId: string; quantity: number }) =>
      unwrap(api.POST('/api/players/{id}/inventory', { params: { path: { id: playerId } }, body: input })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.inventory(playerId) }),
  })
}

export function useComponents(filters: ComponentFilters) {
  return useQuery({
    queryKey: keys.components(filters),
    queryFn: () => unwrap(api.GET('/api/components', { params: { query: clean(filters) } })),
    placeholderData: keepPreviousData,
  })
}

export function useSaveComponent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id?: string; input: ComponentInput }) =>
      id
        ? unwrap(api.PUT('/api/components/{id}', { params: { path: { id } }, body: input }))
        : unwrap(api.POST('/api/components', { body: input })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.allComponents }),
  })
}

export function useArchiveComponent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE('/api/components/{id}', { params: { path: { id } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.allComponents }),
  })
}

export function useAssignComponent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ componentId, playerId, quantity }: { componentId: string; playerId: string; quantity: number }) =>
      unwrap(api.POST('/api/components/{id}/assign', { params: { path: { id: componentId } }, body: { playerId, quantity } })),
    onSuccess: (_, { playerId }) => queryClient.invalidateQueries({ queryKey: keys.inventory(playerId) }),
  })
}

export function useSettings() {
  return useQuery({ queryKey: keys.settings, queryFn: () => unwrap(api.GET('/api/settings')) })
}

export function useUpdateSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (playersCanSelfAssign: boolean) => unwrap(api.PUT('/api/settings', { body: { playersCanSelfAssign } })),
    onSuccess: (settings) => queryClient.setQueryData(keys.settings, settings),
  })
}

export function useRealms(playerId: string | undefined) {
  return useQuery({
    queryKey: keys.realms(playerId ?? ''),
    queryFn: () => unwrap(api.GET('/api/realms', { params: { query: { playerId } } })),
    enabled: !!playerId,
  })
}

export function useRealm(id: string) {
  return useQuery({
    queryKey: keys.realm(id),
    queryFn: () => unwrap(api.GET('/api/realms/{id}', { params: { path: { id } } })),
    staleTime: Infinity, // realms never change
  })
}

/** The rendered SVG for one area. Fetched as text so the page can highlight placements inside it. */
export function useRealmSvg(id: string, area: string | undefined) {
  return useQuery({
    queryKey: ['realm-svg', id, area],
    queryFn: async () => {
      const response = await fetch(exportUrl(id, 'svg', area))
      if (!response.ok) throw new Error(`The map couldn't be loaded (${response.status}).`)
      return response.text()
    },
    enabled: !!area,
    staleTime: Infinity,
  })
}

export function exportUrl(realmId: string, format: 'svg' | 'prompt' | 'json' | 'md', area?: string) {
  const query = new URLSearchParams({ format })
  if (area) query.set('area', area)
  return `/api/realms/${realmId}/export?${query}`
}

export function useRitualPreview(request: RitualPreviewRequest | undefined) {
  return useQuery({
    queryKey: keys.preview(request ?? ({} as RitualPreviewRequest)),
    queryFn: () => unwrap(api.POST('/api/rituals/preview', { body: request! })),
    enabled: !!request,
    placeholderData: keepPreviousData,
  })
}

export function useRandomFill() {
  return useMutation({
    mutationFn: (request: RitualPreviewRequest) => unwrap(api.POST('/api/rituals/preview', { body: { ...request, randomize: true } })),
  })
}

export function usePerformRitual() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ selection, idempotencyKey }: { selection: RitualSelection; idempotencyKey: string }) =>
      unwrap(api.POST('/api/rituals', { body: selection, params: { header: { 'Idempotency-Key': idempotencyKey } } })),
    onSettled: (_, __, { selection }) => {
      // Success spends items and adds a map and a realm; a 409 means our view of the inventory is stale.
      void queryClient.invalidateQueries({ queryKey: keys.inventory(selection.playerId) })
      void queryClient.invalidateQueries({ queryKey: keys.allPreviews })
      void queryClient.invalidateQueries({ queryKey: keys.allRealms })
      void queryClient.invalidateQueries({ queryKey: keys.allComponents })
    },
  })
}

function clean<T extends object>(value: T): T {
  return Object.fromEntries(Object.entries(value).filter(([, v]) => v !== undefined && v !== '')) as T
}
