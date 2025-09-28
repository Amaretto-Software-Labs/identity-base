import { useCallback, useEffect, useRef, useState } from 'react'
import { useIdentityContext } from '../IdentityProvider'
import { useAuth } from './useAuth'
import { createError } from '../../utils/errors'

interface UsePermissionsOptions {
  autoLoad?: boolean
}

interface UsePermissionsResult {
  permissions: string[]
  isLoading: boolean
  error: any
  refresh: () => Promise<string[]>
  hasAll: (required: string[]) => boolean
  hasAny: (required: string[]) => boolean
}

export function usePermissions(options: UsePermissionsOptions = {}): UsePermissionsResult {
  const { authManager } = useIdentityContext()
  const { isAuthenticated, isLoading: isAuthLoading } = useAuth()
  const optionsRef = useRef(options)
  optionsRef.current = options

  const [permissions, setPermissions] = useState<string[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<any>(null)

  const loadPermissions = useCallback(async (): Promise<string[]> => {
    if (!isAuthenticated) {
      setPermissions([])
      setError(null)
      return []
    }

    setIsLoading(true)
    setError(null)

    try {
      const result = await authManager.getUserPermissions()
      setPermissions(result)
      setError(null)
      return result
    } catch (err) {
      const normalized = createError(err)
      if (normalized.status === 401 || normalized.status === 404) {
        setPermissions([])
        setError(null)
        return []
      }

      setPermissions([])
      setError(normalized)
      throw normalized
    } finally {
      setIsLoading(false)
    }
  }, [authManager, isAuthenticated])

  useEffect(() => {
    if ((optionsRef.current.autoLoad ?? true) === false) {
      return
    }

    if (isAuthLoading) {
      return
    }

    if (!isAuthenticated) {
      setPermissions([])
      setError(null)
      return
    }

    let cancelled = false

    loadPermissions().catch(() => {
      // errors are stored in state already
      if (cancelled) {
        return
      }
    })

    return () => {
      cancelled = true
    }
  }, [isAuthenticated, isAuthLoading, loadPermissions])

  const refresh = useCallback(async (): Promise<string[]> => {
    return await loadPermissions()
  }, [loadPermissions])

  const hasAll = useCallback((required: string[]) => {
    if (!required || required.length === 0) {
      return true
    }

    return required.every(permission => permissions.includes(permission))
  }, [permissions])

  const hasAny = useCallback((required: string[]) => {
    if (!required || required.length === 0) {
      return true
    }

    return required.some(permission => permissions.includes(permission))
  }, [permissions])

  return {
    permissions,
    isLoading,
    error,
    refresh,
    hasAll,
    hasAny,
  }
}
