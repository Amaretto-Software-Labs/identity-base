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
  organisationsMe: '/users/me/organisations',
  setActiveOrganisation: '/users/me/organisations/active',
  organisation: (organisationId: string) => `/organisations/${organisationId}`,
  organisationMembers: (organisationId: string) => `/organisations/${organisationId}/members`,
  organisationMember: (organisationId: string, userId: string) => `/organisations/${organisationId}/members/${userId}`,
  organisationRoles: (organisationId: string) => `/organisations/${organisationId}/roles`,
  organisationRolePermissions: (organisationId: string, roleId: string) =>
    `/organisations/${organisationId}/roles/${roleId}/permissions`,
  invitations: (organisationId: string) => `/sample/organisations/${organisationId}/invitations`,
  invitationInfo: (code: string) => `/sample/invitations/${code}`,
  claimInvitation: '/sample/invitations/claim',
}
