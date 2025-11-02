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

export interface OrganizationSummary {
  id: string
  slug: string
  displayName: string
  metadata: Record<string, string | null>
  status: string
}

export interface Membership {
  organizationId: string
  userId: string
  tenantId: string | null
  isPrimary: boolean
  roleIds: string[]
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface OrganizationDetails extends OrganizationSummary {
  roles: OrganizationRole[]
  members: OrganizationMember[]
}

export interface OrganizationRole {
  id: string
  name: string
  description?: string | null
  isSystemRole: boolean
}

export interface OrganizationRolePermissions {
  effective: string[]
  explicit: string[]
}

export interface OrganizationMember {
  userId: string
  organizationId: string
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
  organizationName: string
  organizationSlug: string
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
  organizationId: string
  organizationName: string
  organizationSlug: string
  roleIds: string[]
  expiresAtUtc: string
  isExistingUser: boolean
  claimUrl?: string | null
  registerUrl?: string | null
}
