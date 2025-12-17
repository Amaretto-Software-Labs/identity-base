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

