import { API_ROUTES } from '../config'
import { apiFetch } from './client'
import type { RegisterRequest, RegisterResponse, ProfileSchemaResponse } from './types'

export function fetchProfileSchema() {
  return apiFetch<ProfileSchemaResponse>(API_ROUTES.profileSchema)
}

export function registerUser(payload: RegisterRequest) {
  return apiFetch<RegisterResponse>(API_ROUTES.register, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function confirmEmail(payload: { userId: string, token: string }) {
  return apiFetch<void>(API_ROUTES.confirmEmail, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}
