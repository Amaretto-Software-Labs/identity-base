import React, { useEffect } from 'react'
import type { ReactNode } from 'react'
import { useAuth } from '../hooks/useAuth'

interface ProtectedRouteProps {
  children: ReactNode
  fallback?: ReactNode
  redirectTo?: string
  onUnauthenticated?: () => void
}

export function ProtectedRoute({
  children,
  fallback,
  redirectTo,
  onUnauthenticated,
}: ProtectedRouteProps) {
  const { isAuthenticated, isLoading } = useAuth()

  useEffect(() => {
    if (isLoading || isAuthenticated) {
      return
    }

    if (onUnauthenticated) {
      onUnauthenticated()
      return
    }

    if (redirectTo) {
      window.location.href = redirectTo
      return
    }

    const returnUrl = encodeURIComponent(window.location.href)
    window.location.href = `/login?returnUrl=${returnUrl}`
  }, [isAuthenticated, isLoading, onUnauthenticated, redirectTo])

  // Show loading state
  if (isLoading) {
    return fallback || <div>Loading...</div>
  }

  // Handle unauthenticated state
  if (!isAuthenticated) {
    return null
  }

  // Render protected content
  return <>{children}</>
}
