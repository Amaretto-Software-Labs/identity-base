const DEFAULT_API_BASE = 'https://localhost:5000'
const DEFAULT_SAMPLE_API_BASE = 'https://localhost:8199'
const EXTERNAL_PROVIDER_SEPARATOR = ','

function parseExternalProviders(value: string | undefined): string[] {
  if (!value) {
    return []
  }

  const seen = new Set<string>()
  const providers: string[] = []
  for (const candidate of value.split(EXTERNAL_PROVIDER_SEPARATOR)) {
    const normalized = candidate.trim().toLowerCase()
    if (!normalized || seen.has(normalized)) {
      continue
    }

    seen.add(normalized)
    providers.push(normalized)
  }

  return providers
}

export const CONFIG = {
  apiBase: (import.meta.env.VITE_API_BASE as string | undefined) ?? DEFAULT_API_BASE,
  sampleApiBase: (import.meta.env.VITE_SAMPLE_API_BASE as string | undefined) ?? DEFAULT_SAMPLE_API_BASE,
  clientId: (import.meta.env.VITE_CLIENT_ID as string | undefined) ?? 'spa-client',
  authorizeRedirectUri:
    (import.meta.env.VITE_AUTHORIZE_REDIRECT as string | undefined) ?? `${window.location.origin}/auth/callback`,
  authorizeScope:
    (import.meta.env.VITE_AUTHORIZE_SCOPE as string | undefined) ?? 'openid profile email offline_access identity.api',
  externalProviders: parseExternalProviders(import.meta.env.VITE_EXTERNAL_PROVIDERS as string | undefined),
}

export const API_ROUTES = {
  register: '/auth/register',
  confirmEmail: '/auth/confirm-email',
  login: '/auth/login',
  logout: '/auth/logout',
  profileSchema: '/auth/profile-schema',
  mfaChallenge: '/auth/mfa/challenge',
  mfaVerify: '/auth/mfa/verify',
  usersMe: '/users/me',
  updateProfile: '/users/me/profile',
}
