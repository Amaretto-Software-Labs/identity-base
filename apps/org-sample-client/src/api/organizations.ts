import { API_ROUTES } from '../config'
import { apiFetch } from './client'
import type {
  Membership,
  OrganizationRole,
  InvitationResponse,
  CreateInvitationRequest,
  ClaimInvitationRequest,
} from './types'

export function getMemberships() {
  return apiFetch<Membership[]>(API_ROUTES.organizationsMe)
}

export function setActiveOrganization(payload: { organizationId?: string; organizationSlug?: string }) {
  return apiFetch<{ requiresTokenRefresh: boolean }>(API_ROUTES.setActiveOrganization, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

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

export function getOrganizationMembers(organizationId: string) {
  return apiFetch<Array<{
    organizationId: string
    userId: string
    isPrimary: boolean
    roleIds: string[]
    createdAtUtc: string
    updatedAtUtc: string | null
  }>>(API_ROUTES.organizationMembers(organizationId))
}

export function getOrganizationRoles(organizationId: string) {
  return apiFetch<OrganizationRole[]>(API_ROUTES.organizationRoles(organizationId))
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
    requiresTokenRefresh: boolean
  }>(API_ROUTES.claimInvitation, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

