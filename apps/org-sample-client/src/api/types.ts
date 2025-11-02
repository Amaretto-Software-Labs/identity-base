export interface ProfileSchemaField {
  name: string
  displayName: string
  required: boolean
  maxLength: number
  pattern?: string | null
  type?: string | null
}

export interface ProfileSchemaResponse {
  fields: ProfileSchemaField[]
}

export interface RegisterRequest {
  email: string
  password: string
  metadata: Record<string, string | null>
}

export interface RegisterResponse {
  correlationId: string
}

export interface InvitationRegisterRequest {
  invitationCode: string
  email: string
  password: string
  metadata: Record<string, string | null>
}

export interface OrganisationSummary {
  id: string
  slug: string
  displayName: string
  metadata: Record<string, string | null>
  status: string
}

export interface Membership {
  organisationId: string
  userId: string
  tenantId: string | null
  isPrimary: boolean
  roleIds: string[]
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface OrganisationDetails extends OrganisationSummary {
  roles: OrganisationRole[]
  members: OrganisationMember[]
}

export interface OrganisationRole {
  id: string
  name: string
  description?: string | null
  isSystemRole: boolean
}

export interface OrganisationRolePermissions {
  effective: string[]
  explicit: string[]
}

export interface OrganisationMember {
  userId: string
  organisationId: string
  isPrimary: boolean
  roleIds: string[]
  createdAtUtc: string
  email?: string | null
  displayName?: string | null
}

export interface InvitationResponse {
  code: string
  email: string
  roleIds: string[]
  expiresAtUtc: string
  organisationName: string
  organisationSlug: string
  isExistingUser: boolean
  registerUrl?: string | null
  claimUrl?: string | null
}

export interface CreateInvitationRequest {
  email: string
  roleIds: string[]
  expiresInHours?: number | null
}

export interface ClaimInvitationRequest {
  code: string
}

export interface InvitationDetailsResponse {
  code: string
  email: string
  organisationId: string
  organisationName: string
  organisationSlug: string
  roleIds: string[]
  expiresAtUtc: string
  isExistingUser: boolean
  claimUrl?: string | null
  registerUrl?: string | null
}
