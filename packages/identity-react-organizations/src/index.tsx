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

interface OrganizationDto {
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
  organizationId: string
  userId: string
  tenantId?: string | null
  isPrimary: boolean
  roleIds: string[]
  createdAtUtc: string
  updatedAtUtc?: string | null
}

interface OrganizationMembershipDto extends MembershipDto {
  email?: string | null
  displayName?: string | null
}

interface OrganizationMemberListResponseDto {
  page: number
  pageSize: number
  totalCount: number
  members: OrganizationMembershipDto[]
}

interface ActiveOrganizationResponse {
  organization: OrganizationDto
  roleIds: string[]
  requiresTokenRefresh: boolean
}

export interface Membership extends MembershipDto {}

export interface OrganizationSummary {
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

export interface OrganizationRole {
  id: string
  organizationId?: string | null
  tenantId?: string | null
  name: string
  description?: string | null
  isSystemRole: boolean
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface OrganizationMember {
  organizationId: string
  userId: string
  tenantId?: string | null
  isPrimary: boolean
  roleIds: string[]
  createdAtUtc: string
  updatedAtUtc: string | null
  email?: string | null
  displayName?: string | null
}

export type OrganizationMemberSort = 'createdAt:asc' | 'createdAt:desc'

export interface OrganizationMemberQuery {
  page?: number
  pageSize?: number
  search?: string
  roleId?: string
  isPrimary?: boolean
  sort?: OrganizationMemberSort
}

export interface OrganizationMemberQueryState {
  page: number
  pageSize: number
  search?: string
  roleId?: string
  isPrimary?: boolean
  sort: OrganizationMemberSort
}

export interface OrganizationMembersPage {
  members: OrganizationMember[]
  page: number
  pageSize: number
  totalCount: number
}

export interface UpdateOrganizationMemberOptions {
  roleIds?: string[]
  isPrimary?: boolean
}

export interface SwitchOrganizationResult {
  organization: OrganizationSummary
  roleIds: string[]
  requiresTokenRefresh: boolean
  tokensRefreshed: boolean
}

interface OrganizationsClient {
  listMemberships: () => Promise<Membership[]>
  getOrganization: (organizationId: string) => Promise<OrganizationSummary>
  listRoles: (organizationId: string) => Promise<OrganizationRole[]>
  listMembers: (organizationId: string, query?: OrganizationMemberQuery) => Promise<OrganizationMembersPage>
  updateMember: (
    organizationId: string,
    userId: string,
    options: UpdateOrganizationMemberOptions,
  ) => Promise<OrganizationMember>
  removeMember: (organizationId: string, userId: string) => Promise<void>
  setActiveOrganization: (organizationId: string) => Promise<ActiveOrganizationResponse>
}

interface OrganizationsContextValue {
  memberships: Membership[]
  activeOrganizationId: string | null
  isLoadingMemberships: boolean
  membershipError: unknown
  organizations: Record<string, OrganizationSummary>
  isLoadingOrganizations: boolean
  organizationsError: unknown
  reloadMemberships: () => Promise<void>
  setActiveOrganizationId: (organizationId: string | null, options?: { persist?: boolean }) => void
  switchActiveOrganization: (organizationId: string) => Promise<SwitchOrganizationResult>
  client: OrganizationsClient
}

const OrganizationsContext = createContext<OrganizationsContextValue | undefined>(undefined)

const DEFAULT_STORAGE_KEY = 'identity-base:active-organization-id'

const DEFAULT_MEMBERS_PAGE_SIZE = 25
const MAX_MEMBERS_PAGE_SIZE = 200

function ensureHeaders(initHeaders?: HeadersInit): Headers {
  if (initHeaders instanceof Headers) {
    return initHeaders
  }

  return new Headers(initHeaders ?? undefined)
}

function mapOrganization(dto: OrganizationDto): OrganizationSummary {
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
    organizationId: dto.organizationId,
    userId: dto.userId,
    tenantId: dto.tenantId,
    isPrimary: dto.isPrimary,
    roleIds: dto.roleIds,
    createdAtUtc: dto.createdAtUtc,
    updatedAtUtc: dto.updatedAtUtc ?? null,
  }
}

function mapOrganizationMember(dto: OrganizationMembershipDto): OrganizationMember {
  return {
    organizationId: dto.organizationId,
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

function mapOrganizationMembersPage(dto: OrganizationMemberListResponseDto): OrganizationMembersPage {
  return {
    page: dto.page,
    pageSize: dto.pageSize,
    totalCount: dto.totalCount,
    members: dto.members.map(mapOrganizationMember),
  }
}

function buildMemberListPath(organizationId: string, query?: OrganizationMemberQuery): string {
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
    ? `/organizations/${organizationId}/members?${queryString}`
    : `/organizations/${organizationId}/members`
}

function assertFetcher(fetcher: Fetcher | undefined): Fetcher {
  if (fetcher) {
    return fetcher
  }

  const globalFetch = typeof fetch !== 'undefined' ? fetch.bind(globalThis) : undefined
  if (!globalFetch) {
    throw new Error('OrganizationsProvider requires a fetch implementation.')
  }

  return globalFetch
}

export interface OrganizationsProviderProps {
  children: ReactNode
  apiBase?: string
  storageKey?: string
  fetcher?: Fetcher
}

export function OrganizationsProvider({
  children,
  apiBase,
  storageKey = DEFAULT_STORAGE_KEY,
  fetcher,
}: OrganizationsProviderProps) {
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

    throw new Error('OrganizationsProvider requires apiBase when not running in a browser environment.')
  }, [apiBase])

  const [memberships, setMemberships] = useState<Membership[]>([])
  const [membershipsLoading, setMembershipsLoading] = useState(false)
  const [membershipsError, setMembershipsError] = useState<unknown>(null)

  const [organizations, setOrganizations] = useState<Record<string, OrganizationSummary>>({})
  const [organizationsLoading, setOrganizationsLoading] = useState(false)
  const [organizationsError, setOrganizationsError] = useState<unknown>(null)

  const [activeOrganizationId, setActiveOrganizationIdState] = useState<string | null>(() => {
    if (typeof window === 'undefined') {
      return null
    }

    return window.localStorage.getItem(storageKey)
  })

  const persistActiveOrganization = useCallback((organizationId: string | null) => {
    if (typeof window === 'undefined') {
      return
    }

    if (organizationId) {
      window.localStorage.setItem(storageKey, organizationId)
    } else {
      window.localStorage.removeItem(storageKey)
    }
  }, [storageKey])

  const setActiveOrganizationId = useCallback((organizationId: string | null, options?: { persist?: boolean }) => {
    setActiveOrganizationIdState((previous) => {
      if (previous === organizationId) {
        return previous
      }
      return organizationId
    })

    if (options?.persist ?? true) {
      persistActiveOrganization(organizationId)
    }
  }, [persistActiveOrganization])

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

  const client = useMemo<OrganizationsClient>(() => ({
    listMemberships: async () => {
      const result = await authorizedFetch<MembershipDto[]>('/users/me/organizations')
      return result.map(mapMembership)
    },
    getOrganization: async (organizationId: string) => {
      const dto = await authorizedFetch<OrganizationDto>(`/organizations/${organizationId}`)
      return mapOrganization(dto)
    },
    listRoles: async (organizationId: string) => authorizedFetch<OrganizationRole[]>(`/organizations/${organizationId}/roles`),
    listMembers: async (organizationId: string, query?: OrganizationMemberQuery) => {
      const dto = await authorizedFetch<OrganizationMemberListResponseDto>(buildMemberListPath(organizationId, query))
      return mapOrganizationMembersPage(dto)
    },
    updateMember: async (organizationId: string, userId: string, options: UpdateOrganizationMemberOptions) => {
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

      const dto = await authorizedFetch<OrganizationMembershipDto>(
        `/organizations/${organizationId}/members/${userId}`,
        {
          method: 'PUT',
          body: JSON.stringify(payload),
        },
      )

      return mapOrganizationMember(dto)
    },
    removeMember: async (organizationId: string, userId: string) => {
      await authorizedFetch<void>(`/organizations/${organizationId}/members/${userId}`, {
        method: 'DELETE',
      })
    },
    setActiveOrganization: async (organizationId: string) => authorizedFetch<ActiveOrganizationResponse>(
      '/users/me/organizations/active',
      {
        method: 'POST',
        body: JSON.stringify({ organizationId }),
      },
    ),
  }), [authorizedFetch])

  const loadMemberships = useCallback(async () => {
    if (!isAuthenticated) {
      setMemberships([])
      setOrganizations({})
      setActiveOrganizationId(null, { persist: true })
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
  }, [client, isAuthenticated, setActiveOrganizationId])

  useEffect(() => {
    if (!isAuthenticated) {
      setMemberships([])
      setOrganizations({})
      setActiveOrganizationId(null)
      return
    }

    loadMemberships().catch(() => undefined)
  }, [isAuthenticated, loadMemberships, setActiveOrganizationId])

  useEffect(() => {
    if (!isAuthenticated) {
      setOrganizations({})
      setOrganizationsError(null)
      setOrganizationsLoading(false)
      return
    }

    const uniqueOrganizationIds = Array.from(new Set(memberships.map((membership) => membership.organizationId)))
    if (uniqueOrganizationIds.length === 0) {
      setOrganizations({})
      setOrganizationsError(null)
      setOrganizationsLoading(false)
      setActiveOrganizationId(null)
      return
    }

    let cancelled = false
    setOrganizationsLoading(true)
    setOrganizationsError(null)

    ;(async () => {
      const results = await Promise.allSettled(
        uniqueOrganizationIds.map(async (organizationId) => {
          const organization = await client.getOrganization(organizationId)
          return { organizationId, organization } as const
        }),
      )

      if (cancelled) {
        return
      }

      let firstError: unknown = null

      setOrganizations((previous) => {
        const next: Record<string, OrganizationSummary> = { ...previous }

        results.forEach((result) => {
          if (result.status === 'fulfilled') {
            const { organizationId, organization } = result.value
            next[organizationId] = organization
          } else {
            if (firstError === null) {
              firstError = result.reason
            }
          }
        })

        return next
      })

      setOrganizationsError(firstError)
      setOrganizationsLoading(false)
    })().catch((error) => {
      if (cancelled) {
        return
      }

      setOrganizationsError(error)
      setOrganizationsLoading(false)
    })

    return () => {
      cancelled = true
    }
  }, [client, isAuthenticated, memberships, setActiveOrganizationId])

  useEffect(() => {
    if (!isAuthenticated || memberships.length === 0) {
      return
    }

    const activeExists = activeOrganizationId
      ? memberships.some((membership) => membership.organizationId === activeOrganizationId)
      : false

    if (activeExists) {
      return
    }

    const primary = memberships.find((membership) => membership.isPrimary)
    const fallback = memberships[0] ?? null
    const nextActive = primary?.organizationId ?? fallback?.organizationId ?? null

    if (nextActive !== activeOrganizationId) {
      setActiveOrganizationId(nextActive)
    }
  }, [activeOrganizationId, isAuthenticated, memberships, setActiveOrganizationId])

  const switchActiveOrganization = useCallback(async (organizationId: string): Promise<SwitchOrganizationResult> => {
    const response = await client.setActiveOrganization(organizationId)
    setActiveOrganizationId(organizationId)

    const organization = mapOrganization(response.organization)
    setOrganizations((previous) => ({
      ...previous,
      [organization.id]: organization,
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
      organization,
      roleIds: response.roleIds,
      requiresTokenRefresh: response.requiresTokenRefresh,
      tokensRefreshed,
    }
  }, [authManager, client, loadMemberships, refreshUser, setActiveOrganizationId])

  const contextValue = useMemo<OrganizationsContextValue>(() => ({
    memberships,
    activeOrganizationId,
    isLoadingMemberships: membershipsLoading,
    membershipError: membershipsError,
    organizations,
    isLoadingOrganizations: organizationsLoading,
    organizationsError,
    reloadMemberships: loadMemberships,
    setActiveOrganizationId,
    switchActiveOrganization,
    client,
  }), [
    memberships,
    activeOrganizationId,
    membershipsLoading,
    membershipsError,
    organizations,
    organizationsLoading,
    organizationsError,
    loadMemberships,
    setActiveOrganizationId,
    switchActiveOrganization,
    client,
  ])

  return (
    <OrganizationsContext.Provider value={contextValue}>
      {children}
    </OrganizationsContext.Provider>
  )
}

export function useOrganizations() {
  const context = useContext(OrganizationsContext)
  if (!context) {
    throw new Error('useOrganizations must be used within an OrganizationsProvider')
  }

  return context
}

export function useOrganizationSwitcher() {
  const { switchActiveOrganization } = useOrganizations()
  const [isSwitching, setIsSwitching] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const handleSwitch = useCallback(async (organizationId: string) => {
    setIsSwitching(true)
    setError(null)
    try {
      return await switchActiveOrganization(organizationId)
    } catch (err) {
      setError(err)
      throw err
    } finally {
      setIsSwitching(false)
    }
  }, [switchActiveOrganization])

  return {
    isSwitching,
    error,
    switchOrganization: handleSwitch,
  }
}

export interface UseOrganizationMembersOptions {
  fetchOnMount?: boolean
  initialQuery?: OrganizationMemberQuery
}

export interface UseOrganizationMembersResult {
  members: OrganizationMember[]
  isLoading: boolean
  error: unknown
  page: number
  pageSize: number
  totalCount: number
  pageCount: number
  query: OrganizationMemberQueryState
  setQuery: Dispatch<SetStateAction<OrganizationMemberQueryState>>
  reload: () => Promise<OrganizationMembersPage | undefined>
  ensurePage: (page: number, options?: { force?: boolean }) => Promise<OrganizationMembersPage | undefined>
  isPageLoaded: (page: number) => boolean
  getMemberAt: (index: number) => OrganizationMember | undefined
  updateMember: (userId: string, update: UpdateOrganizationMemberOptions) => Promise<OrganizationMember>
  removeMember: (userId: string) => Promise<void>
}

function normalizeMemberQuery(input?: OrganizationMemberQuery | OrganizationMemberQueryState): OrganizationMemberQueryState {
  const pageSizeRaw = input?.pageSize ?? DEFAULT_MEMBERS_PAGE_SIZE
  const pageSize = Math.min(Math.max(Math.trunc(pageSizeRaw) || DEFAULT_MEMBERS_PAGE_SIZE, 1), MAX_MEMBERS_PAGE_SIZE)
  const pageRaw = input?.page ?? 1
  const page = Math.max(Math.trunc(pageRaw) || 1, 1)
  const search = input?.search?.trim()
  const roleId = input?.roleId?.trim()
  const sort: OrganizationMemberSort = input?.sort ?? 'createdAt:desc'

  return {
    page,
    pageSize,
    search: search && search.length > 0 ? search : undefined,
    roleId: roleId && roleId.length > 0 ? roleId : undefined,
    isPrimary: typeof input?.isPrimary === 'boolean' ? input.isPrimary : undefined,
    sort,
  }
}

function hasBaseQueryChanged(a: OrganizationMemberQueryState, b: OrganizationMemberQueryState): boolean {
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

export function useOrganizationMembers(
  organizationId?: string,
  options: UseOrganizationMembersOptions = {},
): UseOrganizationMembersResult {
  const { client } = useOrganizations()

  const fetchOnMount = options.fetchOnMount ?? true
  const normalizedInitialQuery = useMemo(() => normalizeMemberQuery(options.initialQuery), [options.initialQuery])

  const [queryState, setQueryStateInternal] = useState<OrganizationMemberQueryState>(normalizedInitialQuery)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [totalCount, setTotalCount] = useState(0)
  const cacheRef = useRef<Map<number, OrganizationMember[]>>(new Map())
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
  }, [organizationId])

  const setQuery = useCallback<Dispatch<SetStateAction<OrganizationMemberQueryState>>>((updater) => {
    setQueryStateInternal((previous) => {
      const nextInput = typeof updater === 'function'
        ? (updater as (prev: OrganizationMemberQueryState) => OrganizationMemberQueryState)(previous)
        : updater
      const normalized = normalizeMemberQuery(nextInput)
      const maxPage = totalCountRef.current > 0
        ? calculatePageCount(totalCountRef.current, normalized.pageSize)
        : normalized.page
      const adjusted: OrganizationMemberQueryState = {
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

  const ensurePage = useCallback(async (pageNumber: number, options?: { force?: boolean }): Promise<OrganizationMembersPage | undefined> => {
    if (!organizationId) {
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
      const response = await client.listMembers(organizationId, {
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
  }, [client, organizationId, queryState.page, queryState.pageSize, queryState.search, queryState.roleId, queryState.isPrimary, queryState.sort])

  const reload = useCallback(async () => {
    if (!organizationId) {
      return undefined
    }

    cacheRef.current.delete(queryState.page)
    setCacheVersion((version) => version + 1)
    return ensurePage(queryState.page, { force: true })
  }, [ensurePage, organizationId, queryState.page])

  const updateMember = useCallback(async (userId: string, update: UpdateOrganizationMemberOptions) => {
    if (!organizationId) {
      throw new Error('Organization identifier is required.')
    }

    const updated = await client.updateMember(organizationId, userId, update)

    let found = false
    cacheRef.current.forEach((pageMembers, pageNumber) => {
      const index = pageMembers.findIndex((member) => member.userId === userId)
      if (index !== -1) {
        const merged: OrganizationMember = {
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
  }, [client, organizationId, ensurePage, queryState.page])

  const removeMember = useCallback(async (userId: string) => {
    if (!organizationId) {
      throw new Error('Organization identifier is required.')
    }

    await client.removeMember(organizationId, userId)

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
  }, [client, organizationId, ensurePage, queryState.page, queryState.pageSize])

  useEffect(() => {
    if (!organizationId) {
      return
    }

    if (!fetchOnMount && !hasFetchedOnceRef.current) {
      return
    }

    if (!cacheRef.current.has(queryState.page)) {
      ensurePage(queryState.page).catch(() => undefined)
    }
  }, [organizationId, queryState.page, queryState.pageSize, queryState.search, queryState.roleId, queryState.isPrimary, queryState.sort, ensurePage, fetchOnMount])

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
