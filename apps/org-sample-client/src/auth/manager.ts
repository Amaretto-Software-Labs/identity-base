import type { IdentityAuthManager } from '@identity-base/react-client'

let currentManager: IdentityAuthManager | null = null

export function setAuthManager(manager: IdentityAuthManager) {
  currentManager = manager
}

export function getAuthManager() {
  return currentManager
}
