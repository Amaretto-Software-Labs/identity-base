import React from 'react'
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

  // Show loading state
  if (isLoading) {
    return fallback || <div>Loading...</div>
  }

  // Handle unauthenticated state
  if (!isAuthenticated) {
    if (onUnauthenticated) {
      onUnauthenticated()
      return null
    }

    if (redirectTo) {
      window.location.href = redirectTo
      return null
    }

    // Default: redirect to login with return URL
    const returnUrl = encodeURIComponent(window.location.href)
    window.location.href = `/login?returnUrl=${returnUrl}`
    return null
  }

  // Render protected content
  return <>{children}</>
}