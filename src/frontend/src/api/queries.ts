import { keepPreviousData, type QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, errorMessage, unwrap } from './client'
import type { CompleteRunInput, ComponentInput, Player, QuickWeaveRequest, Realm, RitualPreviewRequest, RitualSelection } from './types'

/** Query keys in one place, so mutations invalidate exactly what they change. */
export const keys = {
  players: ['players'] as const,
  inventory: (playerId: string) => ['inventory', playerId] as const,
  components: (filters: ComponentFilters) => ['components', filters] as const,
  allComponents: ['components'] as const,
  settings: ['settings'] as const,
  realms: (filter: RealmFilter) => ['realms', filter] as const,
  allRealms: ['realms'] as const,
  realm: (id: string) => ['realm', id] as const,
  completions: (realmId: string) => ['completions', realmId] as const,
  roster: (realmId: string, filter: RosterFilter) => ['roster', realmId, filter] as const,
  allRosters: ['roster'] as const,
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
  sort?: ComponentSort
  desc?: boolean
}

export type ComponentSort = 'name' | 'type' | 'rarity' | 'rank' | 'assignable' | 'created'

/** Whose realms to list: one player's, or the GM's Quick Weaves. */
export interface RealmFilter {
  playerId?: string
  gm?: boolean
  page?: number
  pageSize?: number
}

/** Players by name; with a search, only those whose name contains it (the API does the matching, so this scales). */
export function usePlayers(search = '') {
  const text = search.trim()
  return useQuery({
    queryKey: [...keys.players, text],
    queryFn: () => unwrap(api.GET('/api/players', { params: { query: text ? { search: text } : {} } })),
    placeholderData: keepPreviousData,
  })
}

export function useCreatePlayer() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (name: string) => unwrap(api.POST('/api/players', { body: { name } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.players }),
  })
}

/** Attunes one of the player's needles, or clears the attunement with null. */
export function useAttune(playerId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (needleId: string | null) =>
      needleId === null
        ? unwrap(api.DELETE('/api/players/{id}/attunement', { params: { path: { id: playerId } } }))
        : unwrap(api.PUT('/api/players/{id}/attunement', { params: { path: { id: playerId } }, body: { needleId } })),
    onSuccess: (player) => replacePlayer(queryClient, player),
  })
}

/** Puts the player the API returned into the cached list, so the page updates without waiting for a refetch. */
function replacePlayer(queryClient: QueryClient, player: Player) {
  queryClient.setQueriesData<Player[]>({ queryKey: keys.players }, (old) => old?.map((p) => (p.id === player.id ? player : p)))
  void queryClient.invalidateQueries({ queryKey: keys.players })
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

/** Replaces the player's notes on an item they own. */
export function useSaveNotes(playerId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ componentId, notes }: { componentId: string; notes: string }) =>
      unwrap(api.PUT('/api/players/{id}/inventory/{componentId}/notes', { params: { path: { id: playerId, componentId } }, body: { notes } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.inventory(playerId) }),
  })
}

/** A map with its components and a digest of its original realm, composed by the API from several modules. */
export function useMapDetails(mapId: string | undefined) {
  return useQuery({
    queryKey: ['map-details', mapId],
    queryFn: () => unwrap(api.GET('/api/maps/{id}/details', { params: { path: { id: mapId! } } })),
    enabled: !!mapId,
  })
}

export function useComponents(filters: ComponentFilters) {
  return useQuery({
    queryKey: keys.components(filters),
    queryFn: () => unwrap(api.GET('/api/components', { params: { query: clean(filters) } })),
    placeholderData: keepPreviousData,
  })
}

/** The words a Needle, Thread or Coating name may end with. They never change while the app runs. */
export function useNameSuffixes() {
  return useQuery({ queryKey: ['name-suffixes'], queryFn: () => unwrap(api.GET('/api/components/name-suffixes')), staleTime: Infinity })
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

/** A page of realms: one player's, or the GM's Quick Weaves (which belong to no player). */
export function useRealms(filter: RealmFilter, enabled = true) {
  return useQuery({
    queryKey: keys.realms(filter),
    queryFn: () => unwrap(api.GET('/api/realms', { params: { query: clean(filter) } })),
    enabled,
    placeholderData: keepPreviousData,
  })
}

/** Resolves a Quick Weave (random picks included) without saving anything. */
export function useQuickWeavePreview() {
  return useMutation({
    mutationFn: (request: QuickWeaveRequest) => unwrap(api.POST('/api/gm/weave/preview', { body: request })),
  })
}

export function useQuickWeave() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: QuickWeaveRequest) => unwrap(api.POST('/api/gm/weave', { body: request })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: keys.allRealms })
      void queryClient.invalidateQueries({ queryKey: keys.allComponents })
    },
  })
}

export function useRealm(id: string) {
  return useQuery({
    queryKey: keys.realm(id),
    queryFn: () => unwrap(api.GET('/api/realms/{id}', { params: { path: { id } } })),
    staleTime: Infinity, // the document never changes; a pin updates the cache below
  })
}

/** Every objective a GM can pin on a realm, rolled ones first. */
export function useObjectiveCandidates(realmId: string, enabled: boolean) {
  return useQuery({
    queryKey: ['objective-candidates', realmId],
    queryFn: () => unwrap(api.GET('/api/realms/{id}/objectives/candidates', { params: { path: { id: realmId } } })),
    enabled,
    staleTime: Infinity,
  })
}

/** The completion history of the realm's map and everyone who has completed something there. */
export function useCompletions(realmId: string) {
  return useQuery({
    queryKey: keys.completions(realmId),
    queryFn: () => unwrap(api.GET('/api/realms/{id}/completions', { params: { path: { id: realmId } } })),
  })
}

export interface RosterFilter {
  search: string
  /** Include players who completed an objective here and already hold the map. */
  all: boolean
  page: number
}

export const rosterPageSize = 10

/** GM: players for the completion table, a page at a time, with their completions here and whether they hold the map. */
export function useRealmRoster(realmId: string, filter: RosterFilter) {
  return useQuery({
    queryKey: keys.roster(realmId, filter),
    queryFn: () =>
      unwrap(
        api.GET('/api/realms/{id}/roster', {
          params: { path: { id: realmId }, query: { search: filter.search || undefined, all: filter.all || undefined, page: filter.page, pageSize: rosterPageSize } },
        }),
      ),
    placeholderData: keepPreviousData,
  })
}

export interface HandOutResult {
  given: string[]
  failed: { playerId: string; message: string }[]
}

/**
 * GM: gives the map to each player without recording a completion. One assign per player, so one refusal (a Unique
 * map someone else holds) doesn't stop the rest.
 */
export function useHandOutMap(mapId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (playerIds: string[]): Promise<HandOutResult> => {
      const result: HandOutResult = { given: [], failed: [] }
      for (const playerId of playerIds) {
        try {
          await unwrap(api.POST('/api/components/{id}/assign', { params: { path: { id: mapId } }, body: { playerId, quantity: 1 } }))
          result.given.push(playerId)
        } catch (error) {
          result.failed.push({ playerId, message: errorMessage(error) })
        }
      }
      return result
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: keys.allRosters })
      void queryClient.invalidateQueries({ queryKey: ['inventory'] })
    },
  })
}

/** GM: records that a party completed an objective and hands the map to those who don't have it. Repeatable. */
export function useCompleteRun(realmId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CompleteRunInput) => unwrap(api.POST('/api/realms/{id}/completion', { params: { path: { id: realmId } }, body })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: keys.realm(realmId) })
      void queryClient.invalidateQueries({ queryKey: ['completions'] })
      void queryClient.invalidateQueries({ queryKey: keys.allRosters })
      void queryClient.invalidateQueries({ queryKey: keys.allRealms })
      void queryClient.invalidateQueries({ queryKey: ['inventory'] })
      void queryClient.invalidateQueries({ queryKey: keys.allPreviews })
    },
  })
}

/** Pins an objective by key, or clears the pin with null. */
export function usePinObjective(realmId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (key: string | null) =>
      key === null
        ? unwrap(api.DELETE('/api/realms/{id}/pinned-objective', { params: { path: { id: realmId } } }))
        : unwrap(api.PUT('/api/realms/{id}/pinned-objective', { params: { path: { id: realmId } }, body: { key } })),
    onSuccess: (summary) => {
      queryClient.setQueryData<Realm>(keys.realm(realmId), (old) => (old ? { ...old, summary } : old))
      void queryClient.invalidateQueries({ queryKey: keys.allRealms })
    },
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

/** The image prompt for one area, as plain text. Fetched only while the prompt window is open. */
export function useRealmPrompt(id: string, area: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: ['realm-prompt', id, area],
    queryFn: async () => {
      const response = await fetch(exportUrl(id, 'prompt', area))
      if (!response.ok) throw new Error(`The image prompt couldn't be loaded (${response.status}).`)
      return response.text()
    },
    enabled: enabled && !!area,
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
