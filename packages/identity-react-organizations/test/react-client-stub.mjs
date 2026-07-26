import React, { createContext, useContext, useMemo } from 'react'

const AuthContext = createContext({ isAuthenticated: true })
const IdentityContext = createContext({ authManager: null, refreshUser: async () => {} })

export function IdentityProvider({ children, token = 'testtoken', isAuthenticated = true, authManager, refreshUser }) {
  const resolvedAuthManager = useMemo(() => {
    if (authManager) return authManager
    return { getAccessToken: async () => token }
  }, [authManager, token])

  const resolvedRefreshUser = useMemo(() => refreshUser ?? (async () => {}), [refreshUser])

  return React.createElement(
    AuthContext.Provider,
    { value: { isAuthenticated } },
    React.createElement(
      IdentityContext.Provider,
      { value: { authManager: resolvedAuthManager, refreshUser: resolvedRefreshUser } },
      children,
    ),
  )
}

export function useAuth() {
  return useContext(AuthContext)
}

export function useIdentityContext() {
  return useContext(IdentityContext)
}

export class IdentityError extends Error {
  constructor(error) {
    super(typeof error === 'string' ? error : error.detail || error.title || 'An error occurred')
    this.name = 'IdentityError'
    this.status = typeof error === 'string' ? undefined : error.status
    this.errors = typeof error === 'string' ? undefined : error.errors
  }
}

export function createError(error) {
  return error instanceof IdentityError ? error : new IdentityError(error)
}
