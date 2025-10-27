import type { AuthManager } from '@identity-base/react-client'

let currentManager: AuthManager | null = null

export function setAuthManager(manager: AuthManager) {
  currentManager = manager
}

export function getAuthManager() {
  return currentManager
}
