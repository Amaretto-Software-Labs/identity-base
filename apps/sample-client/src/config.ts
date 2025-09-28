const DEFAULT_API_BASE = 'https://localhost:5001'

export const CONFIG = {
  apiBase: (import.meta.env.VITE_API_BASE as string | undefined) ?? DEFAULT_API_BASE,
  clientId: (import.meta.env.VITE_CLIENT_ID as string | undefined) ?? 'spa-client',
  authorizeRedirectUri:
    (import.meta.env.VITE_AUTHORIZE_REDIRECT as string | undefined) ?? `${window.location.origin}/auth/callback`,
  authorizeScope:
    (import.meta.env.VITE_AUTHORIZE_SCOPE as string | undefined) ?? 'openid profile email offline_access identity.api',
  externalProviders: {
    google: ((import.meta.env.VITE_EXTERNAL_GOOGLE_ENABLED as string | undefined) ?? 'false') === 'true',
    microsoft: ((import.meta.env.VITE_EXTERNAL_MICROSOFT_ENABLED as string | undefined) ?? 'false') === 'true',
    apple: ((import.meta.env.VITE_EXTERNAL_APPLE_ENABLED as string | undefined) ?? 'false') === 'true',
  } as Record<'google' | 'microsoft' | 'apple', boolean>,
}

export const API_ROUTES = {
  register: '/auth/register',
  login: '/auth/login',
  logout: '/auth/logout',
  profileSchema: '/auth/profile-schema',
  mfaChallenge: '/auth/mfa/challenge',
  mfaVerify: '/auth/mfa/verify',
  usersMe: '/users/me',
  updateProfile: '/users/me/profile',
}
