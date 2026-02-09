import { API_ROUTES, CONFIG } from '../config'
import { apiFetch, buildUrl } from './client'
import type {
  LoginRequest,
  LoginResponse,
  MfaChallengeRequest,
  MfaVerifyRequest,
  ProfileSchemaResponse,
  RegisterRequest,
  UserProfile,
} from './types'

export function getProfileSchema() {
  return apiFetch<ProfileSchemaResponse>(API_ROUTES.profileSchema, { method: 'GET' })
}

export function confirmEmail(payload: { userId: string; token: string }) {
  return apiFetch<void>(API_ROUTES.confirmEmail, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function registerUser(payload: RegisterRequest) {
  return apiFetch<{ correlationId: string }>(API_ROUTES.register, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function login(payload: LoginRequest) {
  return apiFetch<LoginResponse>(API_ROUTES.login, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function logout() {
  return apiFetch<void>(API_ROUTES.logout, {
    method: 'POST',
  })
}

export function sendMfaChallenge(request: MfaChallengeRequest) {
  return apiFetch<{ message: string }>(API_ROUTES.mfaChallenge, {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function verifyMfa(request: MfaVerifyRequest) {
  return apiFetch<{ message: string }>(API_ROUTES.mfaVerify, {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export async function getCurrentUser(): Promise<UserProfile | null> {
  try {
    return await apiFetch<UserProfile>(API_ROUTES.usersMe, { method: 'GET' })
  } catch (error) {
    if (typeof error === 'object' && error !== null && 'status' in error && (error as { status?: number }).status === 401) {
      return null
    }
    throw error
  }
}

export function updateProfile(payload: { metadata: Record<string, string | null>; concurrencyStamp: string }) {
  return apiFetch<UserProfile>(API_ROUTES.updateProfile, {
    method: 'PUT',
    body: JSON.stringify(payload),
  })
}

export function buildExternalStartUrl(
  provider: string,
  mode: 'login' | 'link',
  returnUrl: string,
  extras?: Record<string, string>,
) {
  const params: Record<string, string> = {
    mode,
    returnUrl,
    ...extras,
  }

  return buildUrl(`/auth/external/${provider}/start`, params)
}

export function buildAuthorizationUrl({
  codeChallenge,
  state,
}: {
  codeChallenge: string
  state: string
}) {
  const params = new URLSearchParams({
    response_type: 'code',
    client_id: CONFIG.clientId,
    redirect_uri: CONFIG.authorizeRedirectUri,
    scope: CONFIG.authorizeScope,
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
    state,
  })

  return `${CONFIG.apiBase}/connect/authorize?${params.toString()}`
}

export function exchangeAuthorizationCode(payload: { code: string; codeVerifier: string; redirectUri: string }) {
  const form = new URLSearchParams({
    grant_type: 'authorization_code',
    code: payload.code,
    code_verifier: payload.codeVerifier,
    redirect_uri: payload.redirectUri,
    client_id: CONFIG.clientId,
  })

  return apiFetch<{ access_token: string; refresh_token?: string; expires_in: number }>(`/connect/token`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
    },
    body: form.toString(),
  })
}
