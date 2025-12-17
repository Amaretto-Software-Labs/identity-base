import React from 'react'
import type { ReactNode } from 'react'
import { useRequireAuth } from '../hooks/useRequireAuth'

interface RequireAuthProps {
  children: ReactNode
  fallback?: ReactNode
  redirectTo?: string
  onUnauthenticated?: () => void
}

export function RequireAuth({
  children,
  fallback,
  redirectTo,
  onUnauthenticated,
}: RequireAuthProps) {
  const user = useRequireAuth({
    redirectTo,
    onUnauthenticated,
  })

  // If user is null, we're either loading or redirecting
  if (!user) {
    return fallback || null
  }

  // User is authenticated, render children
  return <>{children}</>
}