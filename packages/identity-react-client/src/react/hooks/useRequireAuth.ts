import { useEffect } from 'react'
import { useAuth } from './useAuth'

interface UseRequireAuthOptions {
  redirectTo?: string
  onUnauthenticated?: () => void
}

export function useRequireAuth(options: UseRequireAuthOptions = {}) {
  const { user, isAuthenticated, isLoading } = useAuth()

  useEffect(() => {
    // Don't do anything while loading
    if (isLoading) return

    // If not authenticated, handle the redirect/callback
    if (!isAuthenticated) {
      if (options.onUnauthenticated) {
        options.onUnauthenticated()
      } else if (options.redirectTo) {
        window.location.href = options.redirectTo
      } else {
        // Default behavior: redirect to login with return URL
        const returnUrl = encodeURIComponent(window.location.href)
        window.location.href = `/login?returnUrl=${returnUrl}`
      }
    }
  }, [isAuthenticated, isLoading, options])

  // Return user only if authenticated (will be null during loading or if unauthenticated)
  return isAuthenticated ? user : null
}