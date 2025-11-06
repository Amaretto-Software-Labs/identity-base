const DEFAULT_API_BASE = 'https://localhost:8182'

export const CONFIG = {
  apiBase: (import.meta.env.VITE_API_BASE as string | undefined) ?? DEFAULT_API_BASE,
  clientId: (import.meta.env.VITE_CLIENT_ID as string | undefined) ?? 'org-sample-client',
  authorizeRedirectUri:
    (import.meta.env.VITE_AUTHORIZE_REDIRECT as string | undefined) ?? `${window.location.origin}/auth/callback`,
  authorizeScope:
    (import.meta.env.VITE_AUTHORIZE_SCOPE as string | undefined)
    ?? 'openid profile email offline_access identity.api',
}

export const API_ROUTES = {
  register: '/auth/register',
  registerWithInvitation: '/sample/invitations/register',
  confirmEmail: '/auth/confirm-email',
  profileSchema: '/sample/registration/profile-fields',
  profile: '/users/me',
  organizationsMe: '/users/me/organizations',
  organization: (organizationId: string) => `/admin/organizations/${organizationId}`,
  organizationMembers: (organizationId: string) => `/admin/organizations/${organizationId}/members`,
  organizationMember: (organizationId: string, userId: string) => `/admin/organizations/${organizationId}/members/${userId}`,
  organizationRoles: (organizationId: string) => `/admin/organizations/${organizationId}/roles`,
  organizationRolePermissions: (organizationId: string, roleId: string) =>
    `/admin/organizations/${organizationId}/roles/${roleId}/permissions`,
  invitations: (organizationId: string) => `/sample/organizations/${organizationId}/invitations`,
  invitationInfo: (code: string) => `/sample/invitations/${code}`,
  claimInvitation: '/sample/invitations/claim',
}
