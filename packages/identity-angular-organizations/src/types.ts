import type { PagedResult } from '@identity-base/client-core'

export type OrganizationStatus = 'Active' | 'Archived'

export interface OrganizationDto {
  id: string
  slug: string
  displayName: string
  status: OrganizationStatus | string
  metadata?: Record<string, string | null>
  createdAtUtc: string
  updatedAtUtc?: string | null
  archivedAtUtc?: string | null
  tenantId?: string | null
}

export interface UserOrganizationMembershipDto {
  organizationId: string
  organizationSlug: string
  organizationName: string
  status: OrganizationStatus | string
  metadata?: Record<string, string | null>
  roleIds: string[]
  createdAtUtc: string
  updatedAtUtc?: string | null
  archivedAtUtc?: string | null
  tenantId?: string | null
}

export interface OrganizationMemberDto {
  organizationId: string
  userId: string
  tenantId?: string | null
  roleIds: string[]
  createdAtUtc: string
  updatedAtUtc?: string | null
  email?: string | null
  displayName?: string | null
}

export interface OrganizationRoleDto {
  id: string
  organizationId?: string | null
  tenantId?: string | null
  name: string
  description?: string | null
  isSystemRole: boolean
  createdAtUtc: string
  updatedAtUtc?: string | null
}

export interface OrganizationRolePermissionsResponse {
  effective: string[]
  explicit: string[]
}

export interface OrganizationInvitationDto {
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

export interface OrganizationInvitationPreviewDto {
  code: string
  organizationSlug: string
  organizationName: string
  expiresAtUtc: string
}

export interface ClaimInvitationResponse {
  organizationId: string
  organizationSlug: string
  organizationName: string
  roleIds: string[]
  wasExistingMember: boolean
  wasExistingUser: boolean
  requiresTokenRefresh: boolean
}

export interface CreateOrganizationRequest {
  tenantId?: string | null
  slug: string
  displayName: string
  metadata?: Record<string, string | null>
}

export interface UpdateOrganizationRequest {
  displayName?: string | null
  metadata?: Record<string, string | null>
  status?: OrganizationStatus | string | null
}

export interface AddMembershipRequest {
  userId: string
  roleIds: string[]
}

export interface UpdateMembershipRequest {
  roleIds: string[]
}

export interface CreateOrganizationRoleRequest {
  name: string
  description?: string | null
}

export interface UpdateOrganizationRoleRequest {
  name?: string | null
  description?: string | null
}

export interface UpdateOrganizationRolePermissionsRequest {
  permissions: string[]
}

export interface CreateOrganizationInvitationRequest {
  email: string
  roleIds?: string[]
  expiresInHours?: number | null
}

export interface ClaimOrganizationInvitationRequest {
  code: string
}

export interface OrganizationListQuery {
  tenantId?: string
  page?: number
  pageSize?: number
  search?: string
  sort?: string | string[]
  status?: string
}

export interface MembershipListQuery {
  tenantId?: string
  page?: number
  pageSize?: number
  search?: string
  sort?: string | string[]
  includeArchived?: boolean
}

export interface MemberListQuery {
  page?: number
  pageSize?: number
  search?: string
  roleId?: string
  sort?: string
}

export interface RoleListQuery {
  page?: number
  pageSize?: number
  search?: string
  sort?: string | string[]
}

export interface InvitationListQuery {
  page?: number
  pageSize?: number
  search?: string
  sort?: string | string[]
}

export type OrganizationListResponse = PagedResult<OrganizationDto>
export type MembershipListResponse = PagedResult<UserOrganizationMembershipDto>
export type MemberListResponse = PagedResult<OrganizationMemberDto>
export type RoleListResponse = PagedResult<OrganizationRoleDto>
export type InvitationListResponse = PagedResult<OrganizationInvitationDto>

