import { API_ROUTES } from '../config'
import { apiFetch } from './client'
import type {
  OrganizationRole,
  OrganizationRolePermissions,
  InvitationResponse,
  CreateInvitationRequest,
  ClaimInvitationRequest,
  InvitationDetailsResponse,
} from './types'

export function getOrganization(organizationId: string) {
  return apiFetch<{
    id: string
    slug: string
    displayName: string
    metadata: Record<string, string | null>
    status: string
    createdAtUtc: string
    updatedAtUtc: string | null
  }>(API_ROUTES.organization(organizationId))
}

export function getOrganizationRoles(organizationId: string) {
  return apiFetch<OrganizationRole[]>(API_ROUTES.organizationRoles(organizationId))
}

export function getOrganizationRolePermissions(organizationId: string, roleId: string) {
  return apiFetch<OrganizationRolePermissions>(API_ROUTES.organizationRolePermissions(organizationId, roleId))
}

export function updateOrganizationRolePermissions(organizationId: string, roleId: string, permissions: string[]) {
  return apiFetch<void>(API_ROUTES.organizationRolePermissions(organizationId, roleId), {
    method: 'PUT',
    body: JSON.stringify({ permissions }),
  })
}

export function listInvitations(organizationId: string) {
  return apiFetch<InvitationResponse[]>(API_ROUTES.invitations(organizationId))
}

export function createInvitation(organizationId: string, payload: CreateInvitationRequest) {
  return apiFetch<InvitationResponse>(API_ROUTES.invitations(organizationId), {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function revokeInvitation(organizationId: string, code: string) {
  return apiFetch<void>(`${API_ROUTES.invitations(organizationId)}/${code}`, {
    method: 'DELETE',
  })
}

export function claimInvitation(payload: ClaimInvitationRequest) {
  return apiFetch<{
    organizationId: string
    organizationSlug: string
    organizationName: string
    roleIds: string[]
    wasExistingMember: boolean
    wasExistingUser: boolean
    requiresTokenRefresh: boolean
  }>(API_ROUTES.claimInvitation, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function getInvitationDetails(code: string) {
  return apiFetch<InvitationDetailsResponse>(API_ROUTES.invitationInfo(code))
}
