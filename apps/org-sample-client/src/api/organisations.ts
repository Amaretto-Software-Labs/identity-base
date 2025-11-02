import { API_ROUTES } from '../config'
import { apiFetch } from './client'
import type {
  OrganisationRole,
  OrganisationRolePermissions,
  InvitationResponse,
  CreateInvitationRequest,
  ClaimInvitationRequest,
  InvitationDetailsResponse,
} from './types'

export function getOrganisation(organisationId: string) {
  return apiFetch<{
    id: string
    slug: string
    displayName: string
    metadata: Record<string, string | null>
    status: string
    createdAtUtc: string
    updatedAtUtc: string | null
  }>(API_ROUTES.organisation(organisationId))
}

export function getOrganisationRoles(organisationId: string) {
  return apiFetch<OrganisationRole[]>(API_ROUTES.organisationRoles(organisationId))
}

export function getOrganisationRolePermissions(organisationId: string, roleId: string) {
  return apiFetch<OrganisationRolePermissions>(API_ROUTES.organisationRolePermissions(organisationId, roleId))
}

export function updateOrganisationRolePermissions(organisationId: string, roleId: string, permissions: string[]) {
  return apiFetch<void>(API_ROUTES.organisationRolePermissions(organisationId, roleId), {
    method: 'PUT',
    body: JSON.stringify({ permissions }),
  })
}

export function listInvitations(organisationId: string) {
  return apiFetch<InvitationResponse[]>(API_ROUTES.invitations(organisationId))
}

export function createInvitation(organisationId: string, payload: CreateInvitationRequest) {
  return apiFetch<InvitationResponse>(API_ROUTES.invitations(organisationId), {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function revokeInvitation(organisationId: string, code: string) {
  return apiFetch<void>(`${API_ROUTES.invitations(organisationId)}/${code}`, {
    method: 'DELETE',
  })
}

export function claimInvitation(payload: ClaimInvitationRequest) {
  return apiFetch<{
    organisationId: string
    organisationSlug: string
    organisationName: string
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
