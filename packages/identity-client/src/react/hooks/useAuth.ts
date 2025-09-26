import { useIdentityContext } from '../IdentityProvider'

export function useAuth() {
  const { user, isAuthenticated, isLoading, error, refreshUser } = useIdentityContext()

  return {
    user,
    isAuthenticated,
    isLoading,
    error,
    refreshUser,
  }
}