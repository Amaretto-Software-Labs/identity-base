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
import { createError, useAuth, useIdentityContext } from '@identity-base/react-client'

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
  tenantId?: string | null
  slug: string
  displayName: string
  status: string
  roleIds: string[]
  createdAtUtc: string
  updatedAtUtc?: string | null
}

interface OrganizationMembershipDto {
  organizationId: string
  userId: string
  tenantId?: string | null
  roleIds: string[]
  createdAtUtc: string
  updatedAtUtc?: string | null
  email?: string | null
  displayName?: string | null
}

interface OrganizationRoleDto {
  id: string
  organizationId?: string | null
  tenantId?: string | null
  name: string
  description?: string | null
  isSystemRole: boolean
  createdAtUtc: string
  updatedAtUtc?: string | null
}

interface OrganizationInvitationDto {
  code: string
  organizationId: string
  organizationSlug: string
  organizationName: string
  email: string
  roleIds: string[]
  createdAtUtc: string
  createdBy?: string | null
  expiresAtUtc: string
  usedAtUtc?: string | null
  usedByUserId?: string | null
}

interface PagedResponseDto<T> {
  page: number
  pageSize: number
  totalCount: number
  items: T[]
}

type OrganizationMemberListResponseDto = PagedResponseDto<OrganizationMembershipDto>
type OrganizationRoleListResponseDto = PagedResponseDto<OrganizationRoleDto>
type MembershipListResponseDto = PagedResponseDto<MembershipDto>
type OrganizationInvitationListResponseDto = PagedResponseDto<OrganizationInvitationDto>

export interface Membership extends MembershipDto {}

export interface CreateOrganizationOptions {
  tenantId?: string | null
  slug: string
  displayName: string
  metadata?: Record<string, string | null>
}

export interface UpdateOrganizationOptions {
  displayName?: string | null
  metadata?: Record<string, string | null>
  status?: string | null
}

export interface AddOrganizationMemberOptions {
  userId: string
  roleIds: string[]
}

export interface CreateOrganizationRoleOptions {
  name: string
  description?: string | null
  isSystemRole?: boolean
}

export interface CreateOrganizationInvitationOptions {
  email: string
  roleIds?: string[]
  expiresInHours?: number | null
}

export interface OrganizationInvitationPreview {
  code: string
  organizationSlug: string
  organizationName: string
  expiresAtUtc: string
}

export interface ClaimOrganizationInvitationResult {
  organizationId: string
  organizationSlug: string
  organizationName: string
  roleIds: string[]
  wasExistingMember: boolean
  wasExistingUser: boolean
  requiresTokenRefresh: boolean
}

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

export interface OrganizationRolePermissions {
  effective: string[]
  explicit: string[]
}

export interface OrganizationInvitation {
  code: string
  organizationId: string
  organizationSlug: string
  organizationName: string
  email: string
  roleIds: string[]
  createdAtUtc: string
  createdBy?: string | null
  expiresAtUtc: string
  usedAtUtc?: string | null
  usedByUserId?: string | null
}

export interface OrganizationMember {
  organizationId: string
  userId: string
  tenantId?: string | null
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
  sort?: OrganizationMemberSort
}

export interface OrganizationMemberQueryState {
  page: number
  pageSize: number
  search?: string
  roleId?: string
  sort: OrganizationMemberSort
}

export interface OrganizationMembersPage {
  members: OrganizationMember[]
  page: number
  pageSize: number
  totalCount: number
}

export interface OrganizationRoleListQuery {
  page?: number
  pageSize?: number
  search?: string
  sort?: string | string[]
}

export interface OrganizationInvitationListQuery {
  page?: number
  pageSize?: number
  search?: string
  sort?: string | string[]
}

export interface OrganizationListQuery {
  tenantId?: string
  page?: number
  pageSize?: number
  search?: string
  sort?: string | string[]
  status?: string
}

export interface OrganizationPage {
  organizations: OrganizationSummary[]
  page: number
  pageSize: number
  totalCount: number
}

export interface UpdateOrganizationMemberOptions {
  roleIds?: string[]
}

export interface SwitchOrganizationResult {
  organization: OrganizationSummary
  roleIds: string[]
  requiresTokenRefresh: boolean
  tokensRefreshed: boolean
}

export interface OrganizationsClient {
  invitations: OrganizationsInvitationClient
  user: OrganizationsUserClient
  admin: OrganizationsAdminClient
}

export interface OrganizationsInvitationClient {
  preview: (code: string) => Promise<OrganizationInvitationPreview>
  claim: (code: string) => Promise<ClaimOrganizationInvitationResult>
}

export interface OrganizationsUserClient {
  // User-scoped endpoints under /users/me/organizations/{orgId}
  listMemberships: () => Promise<Membership[]>
  createOrganization: (options: CreateOrganizationOptions) => Promise<OrganizationSummary>
  getOrganization: (organizationId: string) => Promise<OrganizationSummary>
  updateOrganization: (organizationId: string, options: UpdateOrganizationOptions) => Promise<OrganizationSummary>
  listMembers: (organizationId: string, query?: OrganizationMemberQuery) => Promise<OrganizationMembersPage>
  addMember: (organizationId: string, options: AddOrganizationMemberOptions) => Promise<OrganizationMember>
  updateMember: (organizationId: string, userId: string, options: UpdateOrganizationMemberOptions) => Promise<OrganizationMember>
  removeMember: (organizationId: string, userId: string) => Promise<void>
  listRoles: (organizationId: string, query?: OrganizationRoleListQuery) => Promise<OrganizationRole[]>
  createRole: (organizationId: string, options: CreateOrganizationRoleOptions) => Promise<OrganizationRole>
  deleteRole: (organizationId: string, roleId: string) => Promise<void>
  getRolePermissions: (organizationId: string, roleId: string) => Promise<OrganizationRolePermissions>
  updateRolePermissions: (organizationId: string, roleId: string, permissions: string[]) => Promise<void>
  listInvitations: (organizationId: string, query?: OrganizationInvitationListQuery) => Promise<OrganizationInvitation[]>
  createInvitation: (organizationId: string, options: CreateOrganizationInvitationOptions) => Promise<OrganizationInvitation>
  revokeInvitation: (organizationId: string, code: string) => Promise<void>
}

export interface OrganizationsAdminClient {
  // Admin endpoints under /admin/organizations/{orgId}
  listOrganizations: (query?: OrganizationListQuery) => Promise<OrganizationPage>
  createOrganization: (options: CreateOrganizationOptions) => Promise<OrganizationSummary>
  getOrganization: (organizationId: string) => Promise<OrganizationSummary>
  updateOrganization: (organizationId: string, options: UpdateOrganizationOptions) => Promise<OrganizationSummary>
  archiveOrganization: (organizationId: string) => Promise<void>
  listMembers: (organizationId: string, query?: OrganizationMemberQuery) => Promise<OrganizationMembersPage>
  addMember: (organizationId: string, options: AddOrganizationMemberOptions) => Promise<OrganizationMember>
  updateMember: (organizationId: string, userId: string, options: UpdateOrganizationMemberOptions) => Promise<OrganizationMember>
  removeMember: (organizationId: string, userId: string) => Promise<void>
  listRoles: (organizationId: string, query?: OrganizationRoleListQuery) => Promise<OrganizationRole[]>
  createRole: (organizationId: string, options: CreateOrganizationRoleOptions) => Promise<OrganizationRole>
  deleteRole: (organizationId: string, roleId: string) => Promise<void>
  getRolePermissions: (organizationId: string, roleId: string) => Promise<OrganizationRolePermissions>
  updateRolePermissions: (organizationId: string, roleId: string, permissions: string[]) => Promise<void>
  listInvitations: (organizationId: string, query?: OrganizationInvitationListQuery) => Promise<OrganizationInvitation[]>
  createInvitation: (organizationId: string, options: CreateOrganizationInvitationOptions) => Promise<OrganizationInvitation>
  revokeInvitation: (organizationId: string, code: string) => Promise<void>
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
const ORGANIZATION_HEADER = 'X-Organization-Id'

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
    tenantId: dto.tenantId ?? null,
    slug: dto.slug,
    displayName: dto.displayName,
    status: dto.status,
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
    members: dto.items.map(mapOrganizationMember),
  }
}

function mapOrganizationRole(dto: OrganizationRoleDto): OrganizationRole {
  return {
    id: dto.id,
    organizationId: dto.organizationId ?? null,
    tenantId: dto.tenantId ?? null,
    name: dto.name,
    description: dto.description ?? null,
    isSystemRole: dto.isSystemRole,
    createdAtUtc: dto.createdAtUtc,
    updatedAtUtc: dto.updatedAtUtc ?? null,
  }
}

function mapOrganizationInvitation(dto: OrganizationInvitationDto): OrganizationInvitation {
  return {
    code: dto.code,
    organizationId: dto.organizationId,
    organizationSlug: dto.organizationSlug,
    organizationName: dto.organizationName,
    email: dto.email,
    roleIds: dto.roleIds,
    createdAtUtc: dto.createdAtUtc,
    createdBy: dto.createdBy ?? null,
    expiresAtUtc: dto.expiresAtUtc,
    usedAtUtc: dto.usedAtUtc ?? null,
    usedByUserId: dto.usedByUserId ?? null,
  }
}

const ADMIN_ORG_PREFIX = '/admin/organizations'
const USER_ME_ORG_PREFIX = '/users/me/organizations'

function encodePathSegment(value: string): string {
  return encodeURIComponent(value)
}

function toOrganizationRequest(options: CreateOrganizationOptions | UpdateOrganizationOptions): Record<string, unknown> {
  const { metadata, ...rest } = options
  return metadata === undefined
    ? rest
    : { ...rest, metadata: { values: metadata } }
}

function appendSort(params: URLSearchParams, sort?: string | string[]): void {
  const values = Array.isArray(sort) ? sort : sort ? [sort] : []
  values.map((value) => value.trim()).filter(Boolean).forEach((value) => params.append('sort', value))
}

function buildOrganizationListPath(query?: OrganizationListQuery): string {
  const params = new URLSearchParams()

  if (query?.tenantId) {
    params.set('tenantId', query.tenantId)
  }
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
  appendSort(params, query?.sort)
  if (query?.status) {
    params.set('status', query.status)
  }

  const queryString = params.toString()
  return queryString ? `${ADMIN_ORG_PREFIX}?${queryString}` : ADMIN_ORG_PREFIX
}

function buildMemberListPathBase(prefix: string, organizationId: string, query?: OrganizationMemberQuery): string {
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

  appendSort(params, query?.sort)

  const queryString = params.toString()
  return queryString.length > 0
    ? `${prefix}/${encodePathSegment(organizationId)}/members?${queryString}`
    : `${prefix}/${encodePathSegment(organizationId)}/members`
}

function buildMemberListPath(organizationId: string, query?: OrganizationMemberQuery): string {
  return buildMemberListPathBase(ADMIN_ORG_PREFIX, organizationId, query)
}

function buildUserMemberListPath(organizationId: string, query?: OrganizationMemberQuery): string {
  return buildMemberListPathBase(USER_ME_ORG_PREFIX, organizationId, query)
}

function buildRoleListPathBase(prefix: string, organizationId: string, query?: OrganizationRoleListQuery): string {
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

  appendSort(params, query?.sort)

  const queryString = params.toString()
  return queryString.length > 0
    ? `${prefix}/${encodePathSegment(organizationId)}/roles?${queryString}`
    : `${prefix}/${encodePathSegment(organizationId)}/roles`
}

function buildRoleListPath(organizationId: string, query?: OrganizationRoleListQuery): string {
  return buildRoleListPathBase(ADMIN_ORG_PREFIX, organizationId, query)
}

function buildUserRoleListPath(organizationId: string, query?: OrganizationRoleListQuery): string {
  return buildRoleListPathBase(USER_ME_ORG_PREFIX, organizationId, query)
}

function buildInvitationListPathBase(prefix: string, organizationId: string, query?: OrganizationInvitationListQuery): string {
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

  appendSort(params, query?.sort)

  const queryString = params.toString()
  return queryString.length > 0
    ? `${prefix}/${encodePathSegment(organizationId)}/invitations?${queryString}`
    : `${prefix}/${encodePathSegment(organizationId)}/invitations`
}

function buildInvitationListPath(organizationId: string, query?: OrganizationInvitationListQuery): string {
  return buildInvitationListPathBase(ADMIN_ORG_PREFIX, organizationId, query)
}

function buildUserInvitationListPath(organizationId: string, query?: OrganizationInvitationListQuery): string {
  return buildInvitationListPathBase(USER_ME_ORG_PREFIX, organizationId, query)
}

async function readResponseBody(response: Response): Promise<ApiError | string | null> {
  const rawBody = await response.text()
  if (!rawBody) {
    return null
  }

  try {
    return JSON.parse(rawBody) as ApiError
  } catch {
    return rawBody
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
  const { authManager } = useIdentityContext()

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
  const membershipsRequestRef = useRef(0)

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
    init: RequestInit & { auth?: boolean; parse?: 'json' | 'text' } = {},
  ): Promise<T> => {
    const { auth = true, parse = 'json', ...rest } = init
    const headers = ensureHeaders(rest.headers)

    if (rest.body && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json')
    }

    if (!headers.has('Accept')) {
      headers.set('Accept', 'application/json')
    }

    const token = auth && authManager ? await authManager.getAccessToken() : null
    if (token) {
      headers.set('Authorization', `Bearer ${token}`)
    }

    if (activeOrganizationId) {
      let pathOnly = path
      try {
        if (path.startsWith('http')) {
          pathOnly = new URL(path).pathname
        }
      } catch {
        // Use the raw path if a custom fetch implementation accepts non-standard URLs.
      }

      const isUnscopedRoute = pathOnly.startsWith(USER_ME_ORG_PREFIX)
        || pathOnly.startsWith('/invitations')
      if (!isUnscopedRoute) {
        headers.set(ORGANIZATION_HEADER, activeOrganizationId)
      }
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
      const errorBody = await readResponseBody(response)

      const error: ApiError = typeof errorBody === 'string'
        ? { detail: errorBody }
        : errorBody ?? {}
      error.status = response.status
      throw createError(error)
    }

    if (parse === 'text') {
      return await response.text() as unknown as T
    }

    if (response.status === 204) {
      return undefined as T
    }

    const rawBody = await response.text()
    if (!rawBody) {
      return undefined as T
    }

    try {
      return JSON.parse(rawBody) as T
    } catch {
      throw createError({ status: response.status, detail: rawBody })
    }
  }, [activeOrganizationId, authManager, baseUrl, resolvedFetch])

  const client = useMemo<OrganizationsClient>(() => ({
    invitations: {
      preview: (code: string) =>
        authorizedFetch<OrganizationInvitationPreview>(
          `/invitations/${encodePathSegment(code)}`,
          { auth: false },
        ),
      claim: (code: string) =>
        authorizedFetch<ClaimOrganizationInvitationResult>('/invitations/claim', {
          method: 'POST',
          body: JSON.stringify({ code }),
        }),
    },
    user: {
      listMemberships: async () => {
        const memberships: Membership[] = []
        let page = 1
        let totalCount = 0

        do {
          const result = await authorizedFetch<MembershipListResponseDto>(
            `/users/me/organizations?page=${page}&pageSize=200`,
          )
          memberships.push(...result.items.map(mapMembership))
          totalCount = result.totalCount
          page += 1

          if (result.items.length === 0) {
            break
          }
        } while (memberships.length < totalCount)

        return memberships
      },
      createOrganization: async (options: CreateOrganizationOptions) => {
        const dto = await authorizedFetch<OrganizationDto>(USER_ME_ORG_PREFIX, {
          method: 'POST',
          body: JSON.stringify(toOrganizationRequest(options)),
        })
        return mapOrganization(dto)
      },
      getOrganization: async (organizationId: string) => {
        const dto = await authorizedFetch<OrganizationDto>(
          `${USER_ME_ORG_PREFIX}/${encodePathSegment(organizationId)}`,
        )
        return mapOrganization(dto)
      },
      updateOrganization: async (organizationId: string, options: UpdateOrganizationOptions) => {
        const dto = await authorizedFetch<OrganizationDto>(
          `${USER_ME_ORG_PREFIX}/${encodePathSegment(organizationId)}`,
          { method: 'PATCH', body: JSON.stringify(toOrganizationRequest(options)) },
        )
        return mapOrganization(dto)
      },
      listMembers: async (organizationId: string, query?: OrganizationMemberQuery) => {
        const dto = await authorizedFetch<OrganizationMemberListResponseDto>(buildUserMemberListPath(organizationId, query))
        return mapOrganizationMembersPage(dto)
      },
      addMember: async (organizationId: string, options: AddOrganizationMemberOptions) => {
        const dto = await authorizedFetch<OrganizationMembershipDto>(
          `${USER_ME_ORG_PREFIX}/${encodePathSegment(organizationId)}/members`,
          { method: 'POST', body: JSON.stringify(options) },
        )
        return mapOrganizationMember(dto)
      },
      updateMember: async (organizationId: string, userId: string, options: UpdateOrganizationMemberOptions) => {
        const payload: Record<string, unknown> = {}
        if (Array.isArray(options.roleIds)) {
          payload.roleIds = options.roleIds
        }
        if (Object.keys(payload).length === 0) {
          throw new Error('At least one role must be provided to update a membership.')
        }
        const dto = await authorizedFetch<OrganizationMembershipDto>(
          `${USER_ME_ORG_PREFIX}/${encodePathSegment(organizationId)}/members/${encodePathSegment(userId)}`,
          { method: 'PUT', body: JSON.stringify(payload) },
        )
        return mapOrganizationMember(dto)
      },
      removeMember: async (organizationId: string, userId: string) => {
        await authorizedFetch<void>(
          `${USER_ME_ORG_PREFIX}/${encodePathSegment(organizationId)}/members/${encodePathSegment(userId)}`,
          { method: 'DELETE' },
        )
      },
      listRoles: async (organizationId: string, query?: OrganizationRoleListQuery) => {
        const dto = await authorizedFetch<OrganizationRoleListResponseDto>(buildUserRoleListPath(organizationId, query))
        return dto.items.map(mapOrganizationRole)
      },
      createRole: async (organizationId: string, options: CreateOrganizationRoleOptions) => {
        const dto = await authorizedFetch<OrganizationRoleDto>(
          `${USER_ME_ORG_PREFIX}/${encodePathSegment(organizationId)}/roles`,
          { method: 'POST', body: JSON.stringify(options) },
        )
        return mapOrganizationRole(dto)
      },
      deleteRole: (organizationId: string, roleId: string) =>
        authorizedFetch<void>(
          `${USER_ME_ORG_PREFIX}/${encodePathSegment(organizationId)}/roles/${encodePathSegment(roleId)}`,
          { method: 'DELETE' },
        ),
      getRolePermissions: async (organizationId: string, roleId: string) =>
        authorizedFetch<OrganizationRolePermissions>(
          `${USER_ME_ORG_PREFIX}/${encodePathSegment(organizationId)}/roles/${encodePathSegment(roleId)}/permissions`,
        ),
      updateRolePermissions: async (organizationId: string, roleId: string, permissions: string[]) =>
        authorizedFetch<void>(`${USER_ME_ORG_PREFIX}/${encodePathSegment(organizationId)}/roles/${encodePathSegment(roleId)}/permissions`, {
          method: 'PUT',
          body: JSON.stringify({ permissions }),
        }),
      listInvitations: async (organizationId: string, query?: OrganizationInvitationListQuery) => {
        const dto = await authorizedFetch<OrganizationInvitationListResponseDto>(buildUserInvitationListPath(organizationId, query))
        return dto.items.map(mapOrganizationInvitation)
      },
      createInvitation: async (organizationId: string, options: CreateOrganizationInvitationOptions) => {
        const dto = await authorizedFetch<OrganizationInvitationDto>(
          `${USER_ME_ORG_PREFIX}/${encodePathSegment(organizationId)}/invitations`,
          { method: 'POST', body: JSON.stringify(options) },
        )
        return mapOrganizationInvitation(dto)
      },
      revokeInvitation: (organizationId: string, code: string) =>
        authorizedFetch<void>(
          `${USER_ME_ORG_PREFIX}/${encodePathSegment(organizationId)}/invitations/${encodePathSegment(code)}`,
          { method: 'DELETE' },
        ),
    },
    admin: {
      listOrganizations: async (query?: OrganizationListQuery) => {
        const dto = await authorizedFetch<PagedResponseDto<OrganizationDto>>(buildOrganizationListPath(query))
        return {
          organizations: dto.items.map(mapOrganization),
          page: dto.page,
          pageSize: dto.pageSize,
          totalCount: dto.totalCount,
        }
      },
      createOrganization: async (options: CreateOrganizationOptions) => {
        const dto = await authorizedFetch<OrganizationDto>(ADMIN_ORG_PREFIX, {
          method: 'POST',
          body: JSON.stringify(toOrganizationRequest(options)),
        })
        return mapOrganization(dto)
      },
      getOrganization: async (organizationId: string) => {
        const dto = await authorizedFetch<OrganizationDto>(
          `${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}`,
        )
        return mapOrganization(dto)
      },
      updateOrganization: async (organizationId: string, options: UpdateOrganizationOptions) => {
        const dto = await authorizedFetch<OrganizationDto>(
          `${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}`,
          { method: 'PATCH', body: JSON.stringify(toOrganizationRequest(options)) },
        )
        return mapOrganization(dto)
      },
      archiveOrganization: (organizationId: string) =>
        authorizedFetch<void>(`${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}`, { method: 'DELETE' }),
      listMembers: async (organizationId: string, query?: OrganizationMemberQuery) => {
        const dto = await authorizedFetch<OrganizationMemberListResponseDto>(buildMemberListPath(organizationId, query))
        return mapOrganizationMembersPage(dto)
      },
      addMember: async (organizationId: string, options: AddOrganizationMemberOptions) => {
        const dto = await authorizedFetch<OrganizationMembershipDto>(
          `${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}/members`,
          { method: 'POST', body: JSON.stringify(options) },
        )
        return mapOrganizationMember(dto)
      },
      updateMember: async (organizationId: string, userId: string, options: UpdateOrganizationMemberOptions) => {
        const payload: Record<string, unknown> = {}
        if (Array.isArray(options.roleIds)) {
          payload.roleIds = options.roleIds
        }
        if (Object.keys(payload).length === 0) {
          throw new Error('At least one role must be provided to update a membership.')
        }
        const dto = await authorizedFetch<OrganizationMembershipDto>(
          `${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}/members/${encodePathSegment(userId)}`,
          { method: 'PUT', body: JSON.stringify(payload) },
        )
        return mapOrganizationMember(dto)
      },
      removeMember: async (organizationId: string, userId: string) => {
        await authorizedFetch<void>(
          `${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}/members/${encodePathSegment(userId)}`,
          { method: 'DELETE' },
        )
      },
      listRoles: async (organizationId: string, query?: OrganizationRoleListQuery) => {
        const dto = await authorizedFetch<OrganizationRoleListResponseDto>(buildRoleListPath(organizationId, query))
        return dto.items.map(mapOrganizationRole)
      },
      createRole: async (organizationId: string, options: CreateOrganizationRoleOptions) => {
        const dto = await authorizedFetch<OrganizationRoleDto>(
          `${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}/roles`,
          { method: 'POST', body: JSON.stringify(options) },
        )
        return mapOrganizationRole(dto)
      },
      deleteRole: (organizationId: string, roleId: string) =>
        authorizedFetch<void>(
          `${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}/roles/${encodePathSegment(roleId)}`,
          { method: 'DELETE' },
        ),
      getRolePermissions: async (organizationId: string, roleId: string) =>
        authorizedFetch<OrganizationRolePermissions>(
          `${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}/roles/${encodePathSegment(roleId)}/permissions`,
        ),
      updateRolePermissions: async (organizationId: string, roleId: string, permissions: string[]) =>
        authorizedFetch<void>(`${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}/roles/${encodePathSegment(roleId)}/permissions`, {
          method: 'PUT',
          body: JSON.stringify({ permissions }),
        }),
      listInvitations: async (organizationId: string, query?: OrganizationInvitationListQuery) => {
        const dto = await authorizedFetch<OrganizationInvitationListResponseDto>(buildInvitationListPath(organizationId, query))
        return dto.items.map(mapOrganizationInvitation)
      },
      createInvitation: async (organizationId: string, options: CreateOrganizationInvitationOptions) => {
        const dto = await authorizedFetch<OrganizationInvitationDto>(
          `${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}/invitations`,
          { method: 'POST', body: JSON.stringify(options) },
        )
        return mapOrganizationInvitation(dto)
      },
      revokeInvitation: (organizationId: string, code: string) =>
        authorizedFetch<void>(
          `${ADMIN_ORG_PREFIX}/${encodePathSegment(organizationId)}/invitations/${encodePathSegment(code)}`,
          { method: 'DELETE' },
        ),
    },
  }), [authorizedFetch])

  const loadMemberships = useCallback(async () => {
    const requestId = ++membershipsRequestRef.current

    if (!isAuthenticated) {
      setMemberships([])
      setMembershipsLoading(false)
      setMembershipsError(null)
      setOrganizations({})
      setActiveOrganizationId(null, { persist: true })
      return
    }

    setMembershipsLoading(true)
    setMembershipsError(null)
    try {
      const response = await client.user.listMemberships()
      if (requestId === membershipsRequestRef.current) {
        setMemberships(response)
      }
    } catch (error) {
      if (requestId === membershipsRequestRef.current) {
        setMembershipsError(error)
        setMemberships([])
      }
      throw error
    } finally {
      if (requestId === membershipsRequestRef.current) {
        setMembershipsLoading(false)
      }
    }
  }, [client, isAuthenticated, setActiveOrganizationId])

  useEffect(() => {
    if (!isAuthenticated) {
      membershipsRequestRef.current += 1
      setMemberships([])
      setMembershipsLoading(false)
      setMembershipsError(null)
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
          const organization = await client.user.getOrganization(organizationId)
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

    const fallback = memberships[0] ?? null
    const nextActive = fallback?.organizationId ?? null

    if (nextActive !== activeOrganizationId) {
      setActiveOrganizationId(nextActive)
    }
  }, [activeOrganizationId, isAuthenticated, memberships, setActiveOrganizationId])

  const switchActiveOrganization = useCallback(async (organizationId: string): Promise<SwitchOrganizationResult> => {
    const membership = memberships.find((item) => item.organizationId === organizationId)
    if (!membership) {
      throw createError({
        status: 403,
        detail: 'You are not a member of the requested organization.',
      })
    }

    let summary: OrganizationSummary
    const cached = organizations[organizationId]

    if (cached) {
      summary = cached
    } else {
      const fetched = await client.user.getOrganization(organizationId)
      summary = fetched
      setOrganizations((previous) => ({
        ...previous,
        [fetched.id]: fetched,
      }))
    }

    setActiveOrganizationId(organizationId)

    return {
      organization: summary,
      roleIds: membership.roleIds,
      requiresTokenRefresh: false,
      tokensRefreshed: false,
    }
  }, [client, memberships, organizations, setActiveOrganizationId])

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
    sort,
  }
}

function hasBaseQueryChanged(a: OrganizationMemberQueryState, b: OrganizationMemberQueryState): boolean {
  return a.pageSize !== b.pageSize
    || a.search !== b.search
    || a.roleId !== b.roleId
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
  const normalizedInitialQuery = useMemo(
    () => normalizeMemberQuery(options.initialQuery),
    [
      options.initialQuery?.page,
      options.initialQuery?.pageSize,
      options.initialQuery?.search,
      options.initialQuery?.roleId,
      options.initialQuery?.sort,
    ],
  )

  const [queryState, setQueryStateInternal] = useState<OrganizationMemberQueryState>(normalizedInitialQuery)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [totalCount, setTotalCount] = useState(0)
  const cacheRef = useRef<Map<number, OrganizationMember[]>>(new Map())
  const loadingPagesRef = useRef<Set<string>>(new Set())
  const requestGenerationRef = useRef(0)
  const totalCountRef = useRef(0)
  const [cacheVersion, setCacheVersion] = useState(0)
  const hasFetchedOnceRef = useRef(false)

  useEffect(() => {
    setQueryStateInternal((previous) => {
      if (previous.page === normalizedInitialQuery.page && !hasBaseQueryChanged(previous, normalizedInitialQuery)) {
        return previous
      }

      requestGenerationRef.current += 1
      cacheRef.current.clear()
      loadingPagesRef.current.clear()
      totalCountRef.current = 0
      setTotalCount(0)
      setCacheVersion((version) => version + 1)
      setError(null)
      setIsLoading(false)
      hasFetchedOnceRef.current = false

      return normalizedInitialQuery
    })
  }, [normalizedInitialQuery])

  useEffect(() => {
    requestGenerationRef.current += 1
    cacheRef.current.clear()
    loadingPagesRef.current.clear()
    totalCountRef.current = 0
    setTotalCount(0)
    setCacheVersion((version) => version + 1)
    setError(null)
    setIsLoading(false)
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
        requestGenerationRef.current += 1
        cacheRef.current.clear()
        loadingPagesRef.current.clear()
        totalCountRef.current = 0
        setTotalCount(0)
        setCacheVersion((version) => version + 1)
        setError(null)
        setIsLoading(false)
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
    const requestGeneration = requestGenerationRef.current
    const loadingKey = `${requestGeneration}:${targetPage}`

    if (!options?.force && cacheRef.current.has(targetPage)) {
      const cachedMembers = cacheRef.current.get(targetPage) ?? []
      return {
        page: targetPage,
        pageSize: queryState.pageSize,
        totalCount: totalCountRef.current,
        members: cachedMembers,
      }
    }

    if (loadingPagesRef.current.has(loadingKey)) {
      return undefined
    }

    loadingPagesRef.current.add(loadingKey)
    setIsLoading(true)

    try {
      const response = await client.user.listMembers(organizationId, {
        page: targetPage,
        pageSize: queryState.pageSize,
        search: queryState.search,
        roleId: queryState.roleId,
        sort: queryState.sort,
      })

      if (requestGeneration !== requestGenerationRef.current) {
        return undefined
      }

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
      if (requestGeneration !== requestGenerationRef.current) {
        return undefined
      }

      setError(err)
      throw err
    } finally {
      loadingPagesRef.current.delete(loadingKey)
      if (requestGeneration === requestGenerationRef.current) {
        const generationPrefix = `${requestGeneration}:`
        const hasPendingRequest = Array.from(loadingPagesRef.current)
          .some((key) => key.startsWith(generationPrefix))
        setIsLoading(hasPendingRequest)
      }
    }
  }, [client, organizationId, queryState.page, queryState.pageSize, queryState.search, queryState.roleId, queryState.sort])

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

    const requestGeneration = requestGenerationRef.current
    const updated = await client.user.updateMember(organizationId, userId, update)
    if (requestGeneration !== requestGenerationRef.current) {
      return updated
    }

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

    const requestGeneration = requestGenerationRef.current
    await client.user.removeMember(organizationId, userId)
    if (requestGeneration !== requestGenerationRef.current) {
      return
    }

    requestGenerationRef.current += 1
    cacheRef.current.clear()
    loadingPagesRef.current.clear()
    setIsLoading(false)

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
  }, [organizationId, queryState.page, queryState.pageSize, queryState.search, queryState.roleId, queryState.sort, ensurePage, fetchOnMount])

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
