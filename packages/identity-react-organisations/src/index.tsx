import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type Dispatch,
  type SetStateAction,
} from 'react'
import type { ReactNode } from 'react'
import { useAuth, useIdentityContext } from '@identity-base/react-client'

type Fetcher = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>

interface ApiError extends Record<string, unknown> {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

interface OrganisationDto {
  id: string
  slug: string
  displayName: string
  status: string
  metadata?: Record<string, string | null>
  createdAtUtc: string
  updatedAtUtc?: string | null
  archivedAtUtc?: string | null
  tenantId?: string | null
}

interface MembershipDto {
  organisationId: string
  userId: string
  tenantId?: string | null
  isPrimary: boolean
  roleIds: string[]
  createdAtUtc: string
  updatedAtUtc?: string | null
}

interface OrganisationMembershipDto extends MembershipDto {
  email?: string | null
  displayName?: string | null
}

interface OrganisationMemberListResponseDto {
  page: number
  pageSize: number
  totalCount: number
  members: OrganisationMembershipDto[]
}

interface ActiveOrganisationResponse {
  organisation: OrganisationDto
  roleIds: string[]
  requiresTokenRefresh: boolean
}

export interface Membership extends MembershipDto {}

export interface OrganisationSummary {
  id: string
  slug: string
  displayName: string
  status: string
  metadata: Record<string, string | null>
  createdAtUtc: string
  updatedAtUtc: string | null
  archivedAtUtc: string | null
  tenantId: string | null
}

export interface OrganisationRole {
  id: string
  organisationId?: string | null
  tenantId?: string | null
  name: string
  description?: string | null
  isSystemRole: boolean
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface OrganisationRolePermissions {
  effective: string[]
  explicit: string[]
}

export interface OrganisationMember {
  organisationId: string
  userId: string
  tenantId?: string | null
  isPrimary: boolean
  roleIds: string[]
  createdAtUtc: string
  updatedAtUtc: string | null
  email?: string | null
  displayName?: string | null
}

export type OrganisationMemberSort = 'createdAt:asc' | 'createdAt:desc'

export interface OrganisationMemberQuery {
  page?: number
  pageSize?: number
  search?: string
  roleId?: string
  isPrimary?: boolean
  sort?: OrganisationMemberSort
}

export interface OrganisationMemberQueryState {
  page: number
  pageSize: number
  search?: string
  roleId?: string
  isPrimary?: boolean
  sort: OrganisationMemberSort
}

export interface OrganisationMembersPage {
  members: OrganisationMember[]
  page: number
  pageSize: number
  totalCount: number
}

export interface UpdateOrganisationMemberOptions {
  roleIds?: string[]
  isPrimary?: boolean
}

export interface SwitchOrganisationResult {
  organisation: OrganisationSummary
  roleIds: string[]
  requiresTokenRefresh: boolean
  tokensRefreshed: boolean
}

interface OrganisationsClient {
  listMemberships: () => Promise<Membership[]>
  getOrganisation: (organisationId: string) => Promise<OrganisationSummary>
  listRoles: (organisationId: string) => Promise<OrganisationRole[]>
  getRolePermissions: (organisationId: string, roleId: string) => Promise<OrganisationRolePermissions>
  updateRolePermissions: (organisationId: string, roleId: string, permissions: string[]) => Promise<void>
  listMembers: (organisationId: string, query?: OrganisationMemberQuery) => Promise<OrganisationMembersPage>
  updateMember: (
    organisationId: string,
    userId: string,
    options: UpdateOrganisationMemberOptions,
  ) => Promise<OrganisationMember>
  removeMember: (organisationId: string, userId: string) => Promise<void>
  setActiveOrganisation: (organisationId: string) => Promise<ActiveOrganisationResponse>
}

interface OrganisationsContextValue {
  memberships: Membership[]
  activeOrganisationId: string | null
  isLoadingMemberships: boolean
  membershipError: unknown
  organisations: Record<string, OrganisationSummary>
  isLoadingOrganisations: boolean
  organisationsError: unknown
  reloadMemberships: () => Promise<void>
  setActiveOrganisationId: (organisationId: string | null, options?: { persist?: boolean }) => void
  switchActiveOrganisation: (organisationId: string) => Promise<SwitchOrganisationResult>
  client: OrganisationsClient
}

const OrganisationsContext = createContext<OrganisationsContextValue | undefined>(undefined)

const DEFAULT_STORAGE_KEY = 'identity-base:active-organisation-id'

const DEFAULT_MEMBERS_PAGE_SIZE = 25
const MAX_MEMBERS_PAGE_SIZE = 200

function ensureHeaders(initHeaders?: HeadersInit): Headers {
  if (initHeaders instanceof Headers) {
    return initHeaders
  }

  return new Headers(initHeaders ?? undefined)
}

function mapOrganisation(dto: OrganisationDto): OrganisationSummary {
  return {
    id: dto.id,
    slug: dto.slug,
    displayName: dto.displayName,
    status: dto.status,
    metadata: dto.metadata ?? {},
    createdAtUtc: dto.createdAtUtc,
    updatedAtUtc: dto.updatedAtUtc ?? null,
    archivedAtUtc: dto.archivedAtUtc ?? null,
    tenantId: dto.tenantId ?? null,
  }
}

function mapMembership(dto: MembershipDto): Membership {
  return {
    organisationId: dto.organisationId,
    userId: dto.userId,
    tenantId: dto.tenantId,
    isPrimary: dto.isPrimary,
    roleIds: dto.roleIds,
    createdAtUtc: dto.createdAtUtc,
    updatedAtUtc: dto.updatedAtUtc ?? null,
  }
}

function mapOrganisationMember(dto: OrganisationMembershipDto): OrganisationMember {
  return {
    organisationId: dto.organisationId,
    userId: dto.userId,
    tenantId: dto.tenantId ?? null,
    isPrimary: dto.isPrimary,
    roleIds: dto.roleIds,
    createdAtUtc: dto.createdAtUtc,
    updatedAtUtc: dto.updatedAtUtc ?? null,
    email: dto.email ?? null,
    displayName: dto.displayName ?? null,
  }
}

function mapOrganisationMembersPage(dto: OrganisationMemberListResponseDto): OrganisationMembersPage {
  return {
    page: dto.page,
    pageSize: dto.pageSize,
    totalCount: dto.totalCount,
    members: dto.members.map(mapOrganisationMember),
  }
}

function buildMemberListPath(organisationId: string, query?: OrganisationMemberQuery): string {
  const params = new URLSearchParams()

  if (query?.page && query.page > 1) {
    params.set('page', String(query.page))
  }

  if (query?.pageSize) {
    params.set('pageSize', String(query.pageSize))
  }

  const trimmedSearch = query?.search?.trim()
  if (trimmedSearch) {
    params.set('search', trimmedSearch)
  }

  if (query?.roleId) {
    params.set('roleId', query.roleId)
  }

  if (typeof query?.isPrimary === 'boolean') {
    params.set('isPrimary', String(query.isPrimary))
  }

  if (query?.sort) {
    params.set('sort', query.sort)
  }

  const queryString = params.toString()
  return queryString.length > 0
    ? `/organisations/${organisationId}/members?${queryString}`
    : `/organisations/${organisationId}/members`
}

function assertFetcher(fetcher: Fetcher | undefined): Fetcher {
  if (fetcher) {
    return fetcher
  }

  const globalFetch = typeof fetch !== 'undefined' ? fetch.bind(globalThis) : undefined
  if (!globalFetch) {
    throw new Error('OrganisationsProvider requires a fetch implementation.')
  }

  return globalFetch
}

export interface OrganisationsProviderProps {
  children: ReactNode
  apiBase?: string
  storageKey?: string
  fetcher?: Fetcher
}

export function OrganisationsProvider({
  children,
  apiBase,
  storageKey = DEFAULT_STORAGE_KEY,
  fetcher,
}: OrganisationsProviderProps) {
  const { isAuthenticated } = useAuth()
  const { authManager, refreshUser } = useIdentityContext()

  const resolvedFetch = useMemo(() => assertFetcher(fetcher), [fetcher])

  const baseUrl = useMemo(() => {
    if (apiBase) {
      return apiBase.replace(/\/+$/, '')
    }

    if (typeof window !== 'undefined') {
      return window.location.origin.replace(/\/+$/, '')
    }

    throw new Error('OrganisationsProvider requires apiBase when not running in a browser environment.')
  }, [apiBase])

  const [memberships, setMemberships] = useState<Membership[]>([])
  const [membershipsLoading, setMembershipsLoading] = useState(false)
  const [membershipsError, setMembershipsError] = useState<unknown>(null)

  const [organisations, setOrganisations] = useState<Record<string, OrganisationSummary>>({})
  const [organisationsLoading, setOrganisationsLoading] = useState(false)
  const [organisationsError, setOrganisationsError] = useState<unknown>(null)

  const [activeOrganisationId, setActiveOrganisationIdState] = useState<string | null>(() => {
    if (typeof window === 'undefined') {
      return null
    }

    return window.localStorage.getItem(storageKey)
  })

  const persistActiveOrganisation = useCallback((organisationId: string | null) => {
    if (typeof window === 'undefined') {
      return
    }

    if (organisationId) {
      window.localStorage.setItem(storageKey, organisationId)
    } else {
      window.localStorage.removeItem(storageKey)
    }
  }, [storageKey])

  const setActiveOrganisationId = useCallback((organisationId: string | null, options?: { persist?: boolean }) => {
    setActiveOrganisationIdState((previous) => {
      if (previous === organisationId) {
        return previous
      }
      return organisationId
    })

    if (options?.persist ?? true) {
      persistActiveOrganisation(organisationId)
    }
  }, [persistActiveOrganisation])

  const authorizedFetch = useCallback(async <T,>(
    path: string,
    init: RequestInit & { parse?: 'json' | 'text' } = {},
  ): Promise<T> => {
    const { parse = 'json', ...rest } = init
    const headers = ensureHeaders(rest.headers)

    if (rest.body && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json')
    }

    if (!headers.has('Accept')) {
      headers.set('Accept', 'application/json')
    }

    const token = authManager ? await authManager.getAccessToken() : null
    if (token) {
      headers.set('Authorization', `Bearer ${token}`)
    }

    const response = await resolvedFetch(
      path.startsWith('http') ? path : `${baseUrl}${path}`,
      {
        ...rest,
        headers,
        credentials: 'include',
      },
    )

    if (!response.ok) {
      let errorBody: ApiError | string | null = null
      try {
        errorBody = await response.json()
      } catch {
        errorBody = await response.text()
      }

      const error: ApiError = typeof errorBody === 'string'
        ? { detail: errorBody }
        : errorBody ?? {}
      error.status = response.status
      throw error
    }

    if (parse === 'text') {
      return await response.text() as unknown as T
    }

    if (response.status === 204) {
      return undefined as T
    }

    return await response.json() as T
  }, [authManager, baseUrl, resolvedFetch])

  const client = useMemo<OrganisationsClient>(() => ({
    listMemberships: async () => {
      const result = await authorizedFetch<MembershipDto[]>('/users/me/organisations')
      return result.map(mapMembership)
    },
    getOrganisation: async (organisationId: string) => {
      const dto = await authorizedFetch<OrganisationDto>(`/organisations/${organisationId}`)
      return mapOrganisation(dto)
    },
    listRoles: async (organisationId: string) => authorizedFetch<OrganisationRole[]>(`/organisations/${organisationId}/roles`),
    getRolePermissions: async (organisationId: string, roleId: string) =>
      authorizedFetch<OrganisationRolePermissions>(`/organisations/${organisationId}/roles/${roleId}/permissions`),
    updateRolePermissions: async (organisationId: string, roleId: string, permissions: string[]) =>
      authorizedFetch<void>(`/organisations/${organisationId}/roles/${roleId}/permissions`, {
        method: 'PUT',
        body: JSON.stringify({ permissions }),
      }),
    listMembers: async (organisationId: string, query?: OrganisationMemberQuery) => {
      const dto = await authorizedFetch<OrganisationMemberListResponseDto>(buildMemberListPath(organisationId, query))
      return mapOrganisationMembersPage(dto)
    },
    updateMember: async (organisationId: string, userId: string, options: UpdateOrganisationMemberOptions) => {
      const payload: Record<string, unknown> = {}
      if (Array.isArray(options.roleIds)) {
        payload.roleIds = options.roleIds
      }
      if (typeof options.isPrimary === 'boolean') {
        payload.isPrimary = options.isPrimary
      }

      if (Object.keys(payload).length === 0) {
        throw new Error('At least one property (roleIds, isPrimary) must be provided to update a membership.')
      }

      const dto = await authorizedFetch<OrganisationMembershipDto>(
        `/organisations/${organisationId}/members/${userId}`,
        {
          method: 'PUT',
          body: JSON.stringify(payload),
        },
      )

      return mapOrganisationMember(dto)
    },
    removeMember: async (organisationId: string, userId: string) => {
      await authorizedFetch<void>(`/organisations/${organisationId}/members/${userId}`, {
        method: 'DELETE',
      })
    },
    setActiveOrganisation: async (organisationId: string) => authorizedFetch<ActiveOrganisationResponse>(
      '/users/me/organisations/active',
      {
        method: 'POST',
        body: JSON.stringify({ organisationId }),
      },
    ),
  }), [authorizedFetch])

  const loadMemberships = useCallback(async () => {
    if (!isAuthenticated) {
      setMemberships([])
      setOrganisations({})
      setActiveOrganisationId(null, { persist: true })
      return
    }

    setMembershipsLoading(true)
    setMembershipsError(null)
    try {
      const response = await client.listMemberships()
      setMemberships(response)
    } catch (error) {
      setMembershipsError(error)
      setMemberships([])
      throw error
    } finally {
      setMembershipsLoading(false)
    }
  }, [client, isAuthenticated, setActiveOrganisationId])

  useEffect(() => {
    if (!isAuthenticated) {
      setMemberships([])
      setOrganisations({})
      setActiveOrganisationId(null)
      return
    }

    loadMemberships().catch(() => undefined)
  }, [isAuthenticated, loadMemberships, setActiveOrganisationId])

  useEffect(() => {
    if (!isAuthenticated) {
      setOrganisations({})
      setOrganisationsError(null)
      setOrganisationsLoading(false)
      return
    }

    const uniqueOrganisationIds = Array.from(new Set(memberships.map((membership) => membership.organisationId)))
    if (uniqueOrganisationIds.length === 0) {
      setOrganisations({})
      setOrganisationsError(null)
      setOrganisationsLoading(false)
      setActiveOrganisationId(null)
      return
    }

    let cancelled = false
    setOrganisationsLoading(true)
    setOrganisationsError(null)

    ;(async () => {
      const results = await Promise.allSettled(
        uniqueOrganisationIds.map(async (organisationId) => {
          const organisation = await client.getOrganisation(organisationId)
          return { organisationId, organisation } as const
        }),
      )

      if (cancelled) {
        return
      }

      let firstError: unknown = null

      setOrganisations((previous) => {
        const next: Record<string, OrganisationSummary> = { ...previous }

        results.forEach((result) => {
          if (result.status === 'fulfilled') {
            const { organisationId, organisation } = result.value
            next[organisationId] = organisation
          } else {
            if (firstError === null) {
              firstError = result.reason
            }
          }
        })

        return next
      })

      setOrganisationsError(firstError)
      setOrganisationsLoading(false)
    })().catch((error) => {
      if (cancelled) {
        return
      }

      setOrganisationsError(error)
      setOrganisationsLoading(false)
    })

    return () => {
      cancelled = true
    }
  }, [client, isAuthenticated, memberships, setActiveOrganisationId])

  useEffect(() => {
    if (!isAuthenticated || memberships.length === 0) {
      return
    }

    const activeExists = activeOrganisationId
      ? memberships.some((membership) => membership.organisationId === activeOrganisationId)
      : false

    if (activeExists) {
      return
    }

    const primary = memberships.find((membership) => membership.isPrimary)
    const fallback = memberships[0] ?? null
    const nextActive = primary?.organisationId ?? fallback?.organisationId ?? null

    if (nextActive !== activeOrganisationId) {
      setActiveOrganisationId(nextActive)
    }
  }, [activeOrganisationId, isAuthenticated, memberships, setActiveOrganisationId])

  const switchActiveOrganisation = useCallback(async (organisationId: string): Promise<SwitchOrganisationResult> => {
    const response = await client.setActiveOrganisation(organisationId)
    setActiveOrganisationId(organisationId)

    const organisation = mapOrganisation(response.organisation)
    setOrganisations((previous) => ({
      ...previous,
      [organisation.id]: organisation,
    }))

    let tokensRefreshed = false
    if (response.requiresTokenRefresh && authManager && typeof (authManager as unknown as { refreshTokens?: () => Promise<unknown> }).refreshTokens === 'function') {
      try {
        await (authManager as unknown as { refreshTokens: () => Promise<unknown> }).refreshTokens()
        tokensRefreshed = true
      } catch {
        tokensRefreshed = false
      }
    }

    await refreshUser()
    await loadMemberships().catch(() => undefined)

    return {
      organisation,
      roleIds: response.roleIds,
      requiresTokenRefresh: response.requiresTokenRefresh,
      tokensRefreshed,
    }
  }, [authManager, client, loadMemberships, refreshUser, setActiveOrganisationId])

  const contextValue = useMemo<OrganisationsContextValue>(() => ({
    memberships,
    activeOrganisationId,
    isLoadingMemberships: membershipsLoading,
    membershipError: membershipsError,
    organisations,
    isLoadingOrganisations: organisationsLoading,
    organisationsError,
    reloadMemberships: loadMemberships,
    setActiveOrganisationId,
    switchActiveOrganisation,
    client,
  }), [
    memberships,
    activeOrganisationId,
    membershipsLoading,
    membershipsError,
    organisations,
    organisationsLoading,
    organisationsError,
    loadMemberships,
    setActiveOrganisationId,
    switchActiveOrganisation,
    client,
  ])

  return (
    <OrganisationsContext.Provider value={contextValue}>
      {children}
    </OrganisationsContext.Provider>
  )
}

export function useOrganisations() {
  const context = useContext(OrganisationsContext)
  if (!context) {
    throw new Error('useOrganisations must be used within an OrganisationsProvider')
  }

  return context
}

export function useOrganisationSwitcher() {
  const { switchActiveOrganisation } = useOrganisations()
  const [isSwitching, setIsSwitching] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const handleSwitch = useCallback(async (organisationId: string) => {
    setIsSwitching(true)
    setError(null)
    try {
      return await switchActiveOrganisation(organisationId)
    } catch (err) {
      setError(err)
      throw err
    } finally {
      setIsSwitching(false)
    }
  }, [switchActiveOrganisation])

  return {
    isSwitching,
    error,
    switchOrganisation: handleSwitch,
  }
}

export interface UseOrganisationMembersOptions {
  fetchOnMount?: boolean
  initialQuery?: OrganisationMemberQuery
}

export interface UseOrganisationMembersResult {
  members: OrganisationMember[]
  isLoading: boolean
  error: unknown
  page: number
  pageSize: number
  totalCount: number
  pageCount: number
  query: OrganisationMemberQueryState
  setQuery: Dispatch<SetStateAction<OrganisationMemberQueryState>>
  reload: () => Promise<OrganisationMembersPage | undefined>
  ensurePage: (page: number, options?: { force?: boolean }) => Promise<OrganisationMembersPage | undefined>
  isPageLoaded: (page: number) => boolean
  getMemberAt: (index: number) => OrganisationMember | undefined
  updateMember: (userId: string, update: UpdateOrganisationMemberOptions) => Promise<OrganisationMember>
  removeMember: (userId: string) => Promise<void>
}

function normalizeMemberQuery(input?: OrganisationMemberQuery | OrganisationMemberQueryState): OrganisationMemberQueryState {
  const pageSizeRaw = input?.pageSize ?? DEFAULT_MEMBERS_PAGE_SIZE
  const pageSize = Math.min(Math.max(Math.trunc(pageSizeRaw) || DEFAULT_MEMBERS_PAGE_SIZE, 1), MAX_MEMBERS_PAGE_SIZE)
  const pageRaw = input?.page ?? 1
  const page = Math.max(Math.trunc(pageRaw) || 1, 1)
  const search = input?.search?.trim()
  const roleId = input?.roleId?.trim()
  const sort: OrganisationMemberSort = input?.sort ?? 'createdAt:desc'

  return {
    page,
    pageSize,
    search: search && search.length > 0 ? search : undefined,
    roleId: roleId && roleId.length > 0 ? roleId : undefined,
    isPrimary: typeof input?.isPrimary === 'boolean' ? input.isPrimary : undefined,
    sort,
  }
}

function hasBaseQueryChanged(a: OrganisationMemberQueryState, b: OrganisationMemberQueryState): boolean {
  return a.pageSize !== b.pageSize
    || a.search !== b.search
    || a.roleId !== b.roleId
    || a.isPrimary !== b.isPrimary
    || a.sort !== b.sort
}

function calculatePageCount(totalCount: number, pageSize: number): number {
  if (totalCount <= 0) {
    return 1
  }

  return Math.max(1, Math.ceil(totalCount / Math.max(pageSize, 1)))
}

export function useOrganisationMembers(
  organisationId?: string,
  options: UseOrganisationMembersOptions = {},
): UseOrganisationMembersResult {
  const { client } = useOrganisations()

  const fetchOnMount = options.fetchOnMount ?? true
  const normalizedInitialQuery = useMemo(
    () => normalizeMemberQuery(options.initialQuery),
    [
      options.initialQuery?.page,
      options.initialQuery?.pageSize,
      options.initialQuery?.search,
      options.initialQuery?.roleId,
      options.initialQuery?.isPrimary,
      options.initialQuery?.sort,
    ],
  )

  const [queryState, setQueryStateInternal] = useState<OrganisationMemberQueryState>(normalizedInitialQuery)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [totalCount, setTotalCount] = useState(0)
  const cacheRef = useRef<Map<number, OrganisationMember[]>>(new Map())
  const loadingPagesRef = useRef<Set<number>>(new Set())
  const totalCountRef = useRef(0)
  const [cacheVersion, setCacheVersion] = useState(0)
  const hasFetchedOnceRef = useRef(false)

  useEffect(() => {
    setQueryStateInternal((previous) => {
      if (previous.page === normalizedInitialQuery.page && !hasBaseQueryChanged(previous, normalizedInitialQuery)) {
        return previous
      }

      cacheRef.current.clear()
      loadingPagesRef.current.clear()
      totalCountRef.current = 0
      setTotalCount(0)
      setCacheVersion((version) => version + 1)
      setError(null)
      hasFetchedOnceRef.current = false

      return normalizedInitialQuery
    })
  }, [normalizedInitialQuery])

  useEffect(() => {
    cacheRef.current.clear()
    loadingPagesRef.current.clear()
    totalCountRef.current = 0
    setTotalCount(0)
    setCacheVersion((version) => version + 1)
    setError(null)
    hasFetchedOnceRef.current = false
    setQueryStateInternal((previous) => ({ ...previous, page: 1 }))
  }, [organisationId])

  const setQuery = useCallback<Dispatch<SetStateAction<OrganisationMemberQueryState>>>((updater) => {
    setQueryStateInternal((previous) => {
      const nextInput = typeof updater === 'function'
        ? (updater as (prev: OrganisationMemberQueryState) => OrganisationMemberQueryState)(previous)
        : updater
      const normalized = normalizeMemberQuery(nextInput)
      const maxPage = totalCountRef.current > 0
        ? calculatePageCount(totalCountRef.current, normalized.pageSize)
        : normalized.page
      const adjusted: OrganisationMemberQueryState = {
        ...normalized,
        page: Math.max(1, Math.min(normalized.page, maxPage)),
      }

      if (hasBaseQueryChanged(previous, adjusted)) {
        cacheRef.current.clear()
        loadingPagesRef.current.clear()
        totalCountRef.current = 0
        setTotalCount(0)
        setCacheVersion((version) => version + 1)
        setError(null)
        hasFetchedOnceRef.current = false
      }

      return adjusted
    })
  }, [])

  const isPageLoaded = useCallback((pageNumber: number) => cacheRef.current.has(pageNumber), [])

  const getMemberAt = useCallback((index: number) => {
    if (index < 0) {
      return undefined
    }

    const pageSize = queryState.pageSize
    const pageNumber = Math.floor(index / pageSize) + 1
    const pageMembers = cacheRef.current.get(pageNumber)
    if (!pageMembers) {
      return undefined
    }

    const offset = index % pageSize
    return pageMembers[offset]
  }, [queryState.pageSize, cacheVersion])

  const members = useMemo(() => cacheRef.current.get(queryState.page) ?? [], [queryState.page, cacheVersion])
  const pageCount = useMemo(() => calculatePageCount(totalCount, queryState.pageSize), [totalCount, queryState.pageSize])

  const ensurePage = useCallback(async (pageNumber: number, options?: { force?: boolean }): Promise<OrganisationMembersPage | undefined> => {
    if (!organisationId) {
      return undefined
    }

    const targetPage = Math.max(1, pageNumber)

    if (!options?.force && cacheRef.current.has(targetPage)) {
      const cachedMembers = cacheRef.current.get(targetPage) ?? []
      return {
        page: targetPage,
        pageSize: queryState.pageSize,
        totalCount: totalCountRef.current,
        members: cachedMembers,
      }
    }

    if (loadingPagesRef.current.has(targetPage)) {
      return undefined
    }

    loadingPagesRef.current.add(targetPage)
    setIsLoading(true)

    try {
      const response = await client.listMembers(organisationId, {
        page: targetPage,
        pageSize: queryState.pageSize,
        search: queryState.search,
        roleId: queryState.roleId,
        isPrimary: queryState.isPrimary,
        sort: queryState.sort,
      })

      cacheRef.current.set(response.page, response.members)
      totalCountRef.current = response.totalCount
      setTotalCount(response.totalCount)
      setCacheVersion((version) => version + 1)
      setError(null)
      hasFetchedOnceRef.current = true

      const maxPage = calculatePageCount(response.totalCount, response.pageSize)
      if (queryState.page > maxPage) {
        setQueryStateInternal((prev) => ({ ...prev, page: maxPage }))
      }

      return response
    } catch (err) {
      setError(err)
      throw err
    } finally {
      loadingPagesRef.current.delete(targetPage)
      setIsLoading(loadingPagesRef.current.size > 0)
    }
  }, [client, organisationId, queryState.page, queryState.pageSize, queryState.search, queryState.roleId, queryState.isPrimary, queryState.sort])

  const reload = useCallback(async () => {
    if (!organisationId) {
      return undefined
    }

    cacheRef.current.delete(queryState.page)
    setCacheVersion((version) => version + 1)
    return ensurePage(queryState.page, { force: true })
  }, [ensurePage, organisationId, queryState.page])

  const updateMember = useCallback(async (userId: string, update: UpdateOrganisationMemberOptions) => {
    if (!organisationId) {
      throw new Error('Organisation identifier is required.')
    }

    const updated = await client.updateMember(organisationId, userId, update)

    let found = false
    cacheRef.current.forEach((pageMembers, pageNumber) => {
      const index = pageMembers.findIndex((member) => member.userId === userId)
      if (index !== -1) {
        const merged: OrganisationMember = {
          ...pageMembers[index],
          ...updated,
          email: updated.email ?? pageMembers[index].email ?? null,
          displayName: updated.displayName ?? pageMembers[index].displayName ?? null,
        }
        const nextMembers = [...pageMembers]
        nextMembers[index] = merged
        cacheRef.current.set(pageNumber, nextMembers)
        found = true
      }
    })

    if (found) {
      setCacheVersion((version) => version + 1)
      return updated
    }

    cacheRef.current.clear()
    setCacheVersion((version) => version + 1)
    await ensurePage(queryState.page, { force: true })
    return updated
  }, [client, organisationId, ensurePage, queryState.page])

  const removeMember = useCallback(async (userId: string) => {
    if (!organisationId) {
      throw new Error('Organisation identifier is required.')
    }

    await client.removeMember(organisationId, userId)

    cacheRef.current.clear()
    loadingPagesRef.current.clear()

    const nextTotal = Math.max(0, totalCountRef.current - 1)
    totalCountRef.current = nextTotal
    setTotalCount(nextTotal)
    setCacheVersion((version) => version + 1)

    if (nextTotal === 0) {
      setQueryStateInternal((prev) => ({ ...prev, page: 1 }))
      return
    }

    const maxPage = calculatePageCount(nextTotal, queryState.pageSize)
    const targetPage = Math.min(queryState.page, maxPage)
    setQueryStateInternal((prev) => ({ ...prev, page: targetPage }))
    await ensurePage(targetPage, { force: true })
  }, [client, organisationId, ensurePage, queryState.page, queryState.pageSize])

  useEffect(() => {
    if (!organisationId) {
      return
    }

    if (!fetchOnMount && !hasFetchedOnceRef.current) {
      return
    }

    if (!cacheRef.current.has(queryState.page)) {
      ensurePage(queryState.page).catch(() => undefined)
    }
  }, [organisationId, queryState.page, queryState.pageSize, queryState.search, queryState.roleId, queryState.isPrimary, queryState.sort, ensurePage, fetchOnMount])

  return {
    members,
    isLoading,
    error,
    page: queryState.page,
    pageSize: queryState.pageSize,
    totalCount,
    pageCount,
    query: queryState,
    setQuery,
    reload,
    ensurePage,
    isPageLoaded,
    getMemberAt,
    updateMember,
    removeMember,
  }
}
