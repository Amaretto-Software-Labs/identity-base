import { useCallback, useEffect, useRef, useState } from 'react'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'
import type { AdminUserRolesUpdateRequest } from '../../core/types'

interface UseAdminUserRolesOptions {
  autoLoad?: boolean
  onError?: (error: any) => void
}

interface UseAdminUserRolesResult {
  roles: string[]
  isLoading: boolean
  isUpdating: boolean
  error: any
  loadRoles: (overrideId?: string) => Promise<string[]>
  refresh: () => Promise<string[]>
  updateRoles: (payload: AdminUserRolesUpdateRequest) => Promise<string[]>
}

export function useAdminUserRoles(userId?: string | null, options: UseAdminUserRolesOptions = {}): UseAdminUserRolesResult {
  const { authManager } = useIdentityContext()
  const optionsRef = useRef(options)
  optionsRef.current = options

  const idRef = useRef<string | null>(userId ?? null)
  const [roles, setRoles] = useState<string[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isUpdating, setIsUpdating] = useState(false)
  const [error, setError] = useState<any>(null)

  const handleError = useCallback((err: any) => {
    const normalized = createError(err)
    setError(normalized)
    optionsRef.current.onError?.(normalized)
    return normalized
  }, [])

  const requireUserId = useCallback((): string => {
    const id = idRef.current
    if (!id) {
      throw createError('User id is required')
    }
    return id
  }, [])

  const loadRoles = useCallback(async (overrideId?: string): Promise<string[]> => {
    const id = overrideId ?? requireUserId()
    idRef.current = id
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.getAdminUserRoles(id)
      setRoles(response.roles)
      return response.roles
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsLoading(false)
    }
  }, [authManager, handleError, requireUserId])

  useEffect(() => {
    idRef.current = userId ?? null
    if (optionsRef.current.autoLoad && userId) {
      loadRoles(userId).catch(() => undefined)
    }
  }, [userId, loadRoles])

  const refresh = useCallback(async (): Promise<string[]> => {
    return await loadRoles()
  }, [loadRoles])

  const updateRoles = useCallback(async (payload: AdminUserRolesUpdateRequest): Promise<string[]> => {
    const id = requireUserId()
    setIsUpdating(true)
    setError(null)

    try {
      await authManager.updateAdminUserRoles(id, payload)
      const nextRoles = payload.roles ?? []
      setRoles(nextRoles)
      return nextRoles
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsUpdating(false)
    }
  }, [authManager, handleError, requireUserId])

  return {
    roles,
    isLoading,
    isUpdating,
    error,
    loadRoles,
    refresh,
    updateRoles,
  }
}
