import { Inject, Injectable } from '@angular/core'
import { createError, type ApiError } from '@identity-base/client-core'
import { IdentityAuthService } from '@identity-base/angular-client'
import type { IdentityAngularOrganizationsConfig } from '../public-types'
import { IDENTITY_ORGANIZATIONS_CONFIG } from '../tokens'
import { ActiveOrganizationService } from './ActiveOrganizationService'
import type {
  AddMembershipRequest,
  ClaimInvitationResponse,
  ClaimOrganizationInvitationRequest,
  CreateOrganizationInvitationRequest,
  CreateOrganizationRequest,
  CreateOrganizationRoleRequest,
  InvitationListQuery,
  InvitationListResponse,
  MemberListQuery,
  MemberListResponse,
  MembershipListQuery,
  MembershipListResponse,
  OrganizationDto,
  OrganizationInvitationPreviewDto,
  OrganizationListQuery,
  OrganizationListResponse,
  OrganizationRoleDto,
  OrganizationRolePermissionsResponse,
  RoleListQuery,
  RoleListResponse,
  UpdateMembershipRequest,
  UpdateOrganizationRequest,
  UpdateOrganizationRolePermissionsRequest,
  UpdateOrganizationRoleRequest,
} from '../types'

@Injectable()
export class OrganizationsService {
  constructor(
    private readonly auth: IdentityAuthService,
    private readonly activeOrg: ActiveOrganizationService,
    @Inject(IDENTITY_ORGANIZATIONS_CONFIG) private readonly config: IdentityAngularOrganizationsConfig,
  ) {}

  readonly invitations = {
    preview: async (code: string): Promise<OrganizationInvitationPreviewDto> => {
      const encoded = encodeURIComponent(code)
      return await this.request<OrganizationInvitationPreviewDto>(`/invitations/${encoded}`, { method: 'GET', auth: false })
    },
    claim: async (payload: ClaimOrganizationInvitationRequest): Promise<ClaimInvitationResponse> => {
      return await this.request<ClaimInvitationResponse>('/invitations/claim', { method: 'POST', body: payload })
    },
  }

  readonly user = {
    organizations: {
      list: async (query: MembershipListQuery = {}): Promise<MembershipListResponse> => {
        const qs = toQueryString(query)
        return await this.request<MembershipListResponse>(qs ? `/users/me/organizations?${qs}` : '/users/me/organizations', { method: 'GET' })
      },
      create: async (payload: CreateOrganizationRequest): Promise<OrganizationDto> => {
        return await this.request<OrganizationDto>('/users/me/organizations', { method: 'POST', body: payload })
      },
      get: async (organizationId: string): Promise<OrganizationDto> => {
        const id = encodeURIComponent(organizationId)
        return await this.request<OrganizationDto>(`/users/me/organizations/${id}`, { method: 'GET' })
      },
      patch: async (organizationId: string, payload: UpdateOrganizationRequest): Promise<OrganizationDto> => {
        const id = encodeURIComponent(organizationId)
        return await this.request<OrganizationDto>(`/users/me/organizations/${id}`, { method: 'PATCH', body: payload })
      },
    },
    members: {
      list: async (organizationId: string, query: MemberListQuery = {}): Promise<MemberListResponse> => {
        const id = encodeURIComponent(organizationId)
        const qs = toQueryString(query)
        const path = qs ? `/users/me/organizations/${id}/members?${qs}` : `/users/me/organizations/${id}/members`
        return await this.request<MemberListResponse>(path, { method: 'GET' })
      },
      add: async (organizationId: string, payload: AddMembershipRequest) => {
        const id = encodeURIComponent(organizationId)
        return await this.request(`/users/me/organizations/${id}/members`, { method: 'POST', body: payload })
      },
      update: async (organizationId: string, userId: string, payload: UpdateMembershipRequest) => {
        const org = encodeURIComponent(organizationId)
        const user = encodeURIComponent(userId)
        return await this.request(`/users/me/organizations/${org}/members/${user}`, { method: 'PUT', body: payload })
      },
      remove: async (organizationId: string, userId: string) => {
        const org = encodeURIComponent(organizationId)
        const user = encodeURIComponent(userId)
        return await this.request(`/users/me/organizations/${org}/members/${user}`, { method: 'DELETE' })
      },
    },
    roles: {
      list: async (organizationId: string, query: RoleListQuery = {}): Promise<RoleListResponse> => {
        const org = encodeURIComponent(organizationId)
        const qs = toQueryString(query)
        const path = qs ? `/users/me/organizations/${org}/roles?${qs}` : `/users/me/organizations/${org}/roles`
        return await this.request<RoleListResponse>(path, { method: 'GET' })
      },
      create: async (organizationId: string, payload: CreateOrganizationRoleRequest): Promise<OrganizationRoleDto> => {
        const org = encodeURIComponent(organizationId)
        return await this.request<OrganizationRoleDto>(`/users/me/organizations/${org}/roles`, { method: 'POST', body: payload })
      },
      update: async (organizationId: string, roleId: string, payload: UpdateOrganizationRoleRequest): Promise<OrganizationRoleDto> => {
        const org = encodeURIComponent(organizationId)
        const role = encodeURIComponent(roleId)
        return await this.request<OrganizationRoleDto>(`/users/me/organizations/${org}/roles/${role}`, { method: 'PUT', body: payload })
      },
      delete: async (organizationId: string, roleId: string) => {
        const org = encodeURIComponent(organizationId)
        const role = encodeURIComponent(roleId)
        return await this.request(`/users/me/organizations/${org}/roles/${role}`, { method: 'DELETE' })
      },
      getPermissions: async (organizationId: string, roleId: string): Promise<OrganizationRolePermissionsResponse> => {
        const org = encodeURIComponent(organizationId)
        const role = encodeURIComponent(roleId)
        return await this.request<OrganizationRolePermissionsResponse>(`/users/me/organizations/${org}/roles/${role}/permissions`, { method: 'GET' })
      },
      updatePermissions: async (organizationId: string, roleId: string, payload: UpdateOrganizationRolePermissionsRequest) => {
        const org = encodeURIComponent(organizationId)
        const role = encodeURIComponent(roleId)
        return await this.request(`/users/me/organizations/${org}/roles/${role}/permissions`, { method: 'PUT', body: payload })
      },
    },
    invitations: {
      list: async (organizationId: string, query: InvitationListQuery = {}): Promise<InvitationListResponse> => {
        const org = encodeURIComponent(organizationId)
        const qs = toQueryString(query)
        const path = qs ? `/users/me/organizations/${org}/invitations?${qs}` : `/users/me/organizations/${org}/invitations`
        return await this.request<InvitationListResponse>(path, { method: 'GET' })
      },
      create: async (organizationId: string, payload: CreateOrganizationInvitationRequest) => {
        const org = encodeURIComponent(organizationId)
        return await this.request(`/users/me/organizations/${org}/invitations`, { method: 'POST', body: payload })
      },
      revoke: async (organizationId: string, code: string) => {
        const org = encodeURIComponent(organizationId)
        const invite = encodeURIComponent(code)
        return await this.request(`/users/me/organizations/${org}/invitations/${invite}`, { method: 'DELETE' })
      },
    },
  }

  readonly admin = {
    organizations: {
      list: async (query: OrganizationListQuery = {}): Promise<OrganizationListResponse> => {
        const qs = toQueryString(query)
        return await this.request<OrganizationListResponse>(qs ? `/admin/organizations?${qs}` : '/admin/organizations', { method: 'GET' })
      },
      create: async (payload: CreateOrganizationRequest): Promise<OrganizationDto> => {
        return await this.request<OrganizationDto>('/admin/organizations', { method: 'POST', body: payload })
      },
      get: async (organizationId: string): Promise<OrganizationDto> => {
        const org = encodeURIComponent(organizationId)
        return await this.request<OrganizationDto>(`/admin/organizations/${org}`, { method: 'GET' })
      },
      patch: async (organizationId: string, payload: UpdateOrganizationRequest): Promise<OrganizationDto> => {
        const org = encodeURIComponent(organizationId)
        return await this.request<OrganizationDto>(`/admin/organizations/${org}`, { method: 'PATCH', body: payload })
      },
      delete: async (organizationId: string) => {
        const org = encodeURIComponent(organizationId)
        return await this.request(`/admin/organizations/${org}`, { method: 'DELETE' })
      },
    },
    members: {
      list: async (organizationId: string, query: MemberListQuery = {}): Promise<MemberListResponse> => {
        const org = encodeURIComponent(organizationId)
        const qs = toQueryString(query)
        const path = qs ? `/admin/organizations/${org}/members?${qs}` : `/admin/organizations/${org}/members`
        return await this.request<MemberListResponse>(path, { method: 'GET' })
      },
      add: async (organizationId: string, payload: AddMembershipRequest) => {
        const org = encodeURIComponent(organizationId)
        return await this.request(`/admin/organizations/${org}/members`, { method: 'POST', body: payload })
      },
      update: async (organizationId: string, userId: string, payload: UpdateMembershipRequest) => {
        const org = encodeURIComponent(organizationId)
        const user = encodeURIComponent(userId)
        return await this.request(`/admin/organizations/${org}/members/${user}`, { method: 'PUT', body: payload })
      },
      remove: async (organizationId: string, userId: string) => {
        const org = encodeURIComponent(organizationId)
        const user = encodeURIComponent(userId)
        return await this.request(`/admin/organizations/${org}/members/${user}`, { method: 'DELETE' })
      },
    },
    roles: {
      list: async (organizationId: string, query: RoleListQuery = {}): Promise<RoleListResponse> => {
        const org = encodeURIComponent(organizationId)
        const qs = toQueryString(query)
        const path = qs ? `/admin/organizations/${org}/roles?${qs}` : `/admin/organizations/${org}/roles`
        return await this.request<RoleListResponse>(path, { method: 'GET' })
      },
      create: async (organizationId: string, payload: CreateOrganizationRoleRequest): Promise<OrganizationRoleDto> => {
        const org = encodeURIComponent(organizationId)
        return await this.request<OrganizationRoleDto>(`/admin/organizations/${org}/roles`, { method: 'POST', body: payload })
      },
      update: async (organizationId: string, roleId: string, payload: UpdateOrganizationRoleRequest): Promise<OrganizationRoleDto> => {
        const org = encodeURIComponent(organizationId)
        const role = encodeURIComponent(roleId)
        return await this.request<OrganizationRoleDto>(`/admin/organizations/${org}/roles/${role}`, { method: 'PUT', body: payload })
      },
      delete: async (organizationId: string, roleId: string) => {
        const org = encodeURIComponent(organizationId)
        const role = encodeURIComponent(roleId)
        return await this.request(`/admin/organizations/${org}/roles/${role}`, { method: 'DELETE' })
      },
      getPermissions: async (organizationId: string, roleId: string): Promise<OrganizationRolePermissionsResponse> => {
        const org = encodeURIComponent(organizationId)
        const role = encodeURIComponent(roleId)
        return await this.request<OrganizationRolePermissionsResponse>(`/admin/organizations/${org}/roles/${role}/permissions`, { method: 'GET' })
      },
      updatePermissions: async (organizationId: string, roleId: string, payload: UpdateOrganizationRolePermissionsRequest) => {
        const org = encodeURIComponent(organizationId)
        const role = encodeURIComponent(roleId)
        return await this.request(`/admin/organizations/${org}/roles/${role}/permissions`, { method: 'PUT', body: payload })
      },
    },
    invitations: {
      list: async (organizationId: string, query: InvitationListQuery = {}): Promise<InvitationListResponse> => {
        const org = encodeURIComponent(organizationId)
        const qs = toQueryString(query)
        const path = qs ? `/admin/organizations/${org}/invitations?${qs}` : `/admin/organizations/${org}/invitations`
        return await this.request<InvitationListResponse>(path, { method: 'GET' })
      },
      create: async (organizationId: string, payload: CreateOrganizationInvitationRequest) => {
        const org = encodeURIComponent(organizationId)
        return await this.request(`/admin/organizations/${org}/invitations`, { method: 'POST', body: payload })
      },
      revoke: async (organizationId: string, code: string) => {
        const org = encodeURIComponent(organizationId)
        const invite = encodeURIComponent(code)
        return await this.request(`/admin/organizations/${org}/invitations/${invite}`, { method: 'DELETE' })
      },
    },
  }

  private async request<T = unknown>(
    path: string,
    options: { method: string; body?: unknown; auth?: boolean } = { method: 'GET' },
  ): Promise<T> {
    const requireAuth = options.auth !== false

    const headers = new Headers()
    headers.set('Content-Type', 'application/json')

    if (requireAuth) {
      const token = await this.auth.getAccessToken()
      if (token) {
        headers.set('Authorization', `Bearer ${token}`)
      }
    }

    const orgId = this.activeOrg.organizationId
    const headerName = this.config.organizationHeader?.headerName ?? 'X-Organization-Id'
    if (orgId && shouldAttachHeader(`${this.config.apiBase}${path}`, this.config)) {
      headers.set(headerName, orgId)
    }

    const controller = new AbortController()
    const timeoutId = setTimeout(() => controller.abort(), this.config.timeoutMs ?? 10000)

    try {
      const response = await fetch(`${this.config.apiBase}${path}`, {
        method: options.method,
        credentials: 'include',
        headers,
        signal: controller.signal,
        body: options.body === undefined ? undefined : JSON.stringify(options.body),
      })

      clearTimeout(timeoutId)

      if (!response.ok) {
        let errorBody: ApiError | string | null = null
        try {
          errorBody = await response.json()
        } catch {
          errorBody = await response.text()
        }

        const error: ApiError = typeof errorBody === 'string' ? { detail: errorBody } : errorBody ?? {}
        error.status = response.status
        throw createError(error)
      }

      if (response.status === 204) {
        return undefined as T
      }

      const raw = await response.text()
      if (!raw) {
        return undefined as T
      }

      try {
        return JSON.parse(raw) as T
      } catch {
        throw createError({ status: response.status, detail: raw })
      }
    } catch (error: any) {
      clearTimeout(timeoutId)
      if (error?.name === 'AbortError') {
        throw createError('Request timeout')
      }
      throw createError(error)
    }
  }
}

function toQueryString(query: object): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query as Record<string, unknown>)) {
    if (value === undefined || value === null) continue
    if (Array.isArray(value)) {
      for (const v of value) {
        const trimmed = `${v}`.trim()
        if (trimmed.length > 0) params.append(key, trimmed)
      }
      continue
    }
    const trimmed = `${value}`.trim()
    if (trimmed.length > 0) params.set(key, trimmed)
  }
  return params.toString()
}

function shouldAttachHeader(url: string, config: IdentityAngularOrganizationsConfig): boolean {
  const include = config.organizationHeader?.include
  const exclude = config.organizationHeader?.exclude

  if (exclude?.some(rule => matchesRule(url, rule))) {
    return false
  }

  if (!include || include.length === 0) {
    return typeof config.apiBase === 'string' && url.startsWith(config.apiBase)
  }

  return include.some(rule => matchesRule(url, rule))
}

function matchesRule(url: string, rule: any): boolean {
  if (typeof rule === 'string') {
    return url.startsWith(rule)
  }
  if (rule instanceof RegExp) {
    return rule.test(url)
  }
  return rule(url)
}
