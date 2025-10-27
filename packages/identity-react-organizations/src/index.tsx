import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
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
  listMembers: (organizationId: string) => Promise<OrganizationMember[]>
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
    listMembers: async (organizationId: string) => authorizedFetch<OrganizationMember[]>(`/organizations/${organizationId}/members`),
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

      return authorizedFetch<OrganizationMember>(
        `/organizations/${organizationId}/members/${userId}`,
        {
          method: 'PUT',
          body: JSON.stringify(payload),
        },
      )
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
}

export function useOrganizationMembers(organizationId?: string, options: UseOrganizationMembersOptions = {}) {
  const { client } = useOrganizations()

  const [members, setMembers] = useState<OrganizationMember[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const load = useCallback(async () => {
    if (!organizationId) {
      setMembers([])
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      const response = await client.listMembers(organizationId)
      setMembers(response)
    } catch (err) {
      setError(err)
      throw err
    } finally {
      setIsLoading(false)
    }
  }, [client, organizationId])

  useEffect(() => {
    if (options.fetchOnMount ?? true) {
      load().catch(() => undefined)
    }
  }, [load, options.fetchOnMount])

  const updateMember = useCallback(async (userId: string, update: UpdateOrganizationMemberOptions) => {
    if (!organizationId) {
      throw new Error('Organization identifier is required.')
    }

    const updated = await client.updateMember(organizationId, userId, update)
    setMembers((previous) => previous.map((member) => (member.userId === userId ? updated : member)))
    return updated
  }, [client, organizationId])

  const removeMember = useCallback(async (userId: string) => {
    if (!organizationId) {
      throw new Error('Organization identifier is required.')
    }

    await client.removeMember(organizationId, userId)
    setMembers((previous) => previous.filter((member) => member.userId !== userId))
  }, [client, organizationId])

  return {
    members,
    isLoading,
    error,
    reload: load,
    updateMember,
    removeMember,
  }
}
