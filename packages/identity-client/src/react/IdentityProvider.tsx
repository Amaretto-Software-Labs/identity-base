import React, { createContext, useContext, useEffect, useState, useCallback, useMemo } from 'react'
import type { ReactNode } from 'react'
import type { IdentityConfig, UserProfile, AuthState, AuthEvent } from '../core/types'
import { IdentityAuthManager } from '../core/IdentityAuthManager'

interface IdentityContextValue extends AuthState {
  authManager: IdentityAuthManager
  refreshUser: () => Promise<void>
}

const IdentityContext = createContext<IdentityContextValue | undefined>(undefined)

interface IdentityProviderProps {
  config: IdentityConfig
  children: ReactNode
}

export function IdentityProvider({ config, children }: IdentityProviderProps) {
  const [authManager] = useState(() => new IdentityAuthManager(config))
  const [user, setUser] = useState<UserProfile | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<any>(null)

  const refreshUser = useCallback(async () => {
    try {
      const currentUser = await authManager.getCurrentUser()
      setUser(currentUser)
      setError(null)
    } catch (err) {
      setError(err)
      setUser(null)
    }
  }, [authManager])

  // Initialize user on mount
  useEffect(() => {
    let mounted = true

    const initializeAuth = async () => {
      try {
        const currentUser = await authManager.getCurrentUser()
        if (mounted) {
          setUser(currentUser)
          setError(null)
        }
      } catch (err) {
        if (mounted) {
          setError(err)
          setUser(null)
        }
      } finally {
        if (mounted) {
          setIsLoading(false)
        }
      }
    }

    initializeAuth()

    return () => {
      mounted = false
    }
  }, [authManager])

  // Listen to auth events
  useEffect(() => {
    const handleAuthEvent = (event: AuthEvent) => {
      switch (event.type) {
        case 'login':
          setUser(event.user)
          setError(null)
          break
        case 'logout':
          setUser(null)
          setError(null)
          break
        case 'error':
          setError(event.error)
          break
      }
    }

    const unsubscribe = authManager.addEventListener(handleAuthEvent)
    return unsubscribe
  }, [authManager])

  const value = useMemo<IdentityContextValue>(() => ({
    authManager,
    user,
    isAuthenticated: !!user,
    isLoading,
    error,
    refreshUser,
  }), [authManager, user, isLoading, error, refreshUser])

  return (
    <IdentityContext.Provider value={value}>
      {children}
    </IdentityContext.Provider>
  )
}

export function useIdentityContext(): IdentityContextValue {
  const context = useContext(IdentityContext)
  if (!context) {
    throw new Error('useIdentityContext must be used within an IdentityProvider')
  }
  return context
}