import { useCallback, useEffect, useRef, useState } from 'react'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'
import type {
  AdminRoleSummary,
  AdminRoleDetail,
  AdminRoleCreateRequest,
  AdminRoleUpdateRequest,
} from '../../core/types'

interface UseAdminRolesOptions {
  autoLoad?: boolean
  onError?: (error: any) => void
}

interface UseAdminRolesResult {
  roles: AdminRoleSummary[]
  isLoading: boolean
  isMutating: boolean
  error: any
  listRoles: () => Promise<AdminRoleSummary[]>
  refresh: () => Promise<AdminRoleSummary[]>
  createRole: (payload: AdminRoleCreateRequest) => Promise<AdminRoleDetail>
  updateRole: (roleId: string, payload: AdminRoleUpdateRequest) => Promise<AdminRoleDetail>
  deleteRole: (roleId: string) => Promise<void>
}

export function useAdminRoles(options: UseAdminRolesOptions = {}): UseAdminRolesResult {
  const { authManager } = useIdentityContext()
  const optionsRef = useRef(options)
  optionsRef.current = options

  const [roles, setRoles] = useState<AdminRoleSummary[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isMutating, setIsMutating] = useState(false)
  const [error, setError] = useState<any>(null)

  const handleError = useCallback((err: any) => {
    const normalized = createError(err)
    setError(normalized)
    optionsRef.current.onError?.(normalized)
    return normalized
  }, [])

  const fetchRoles = useCallback(async (): Promise<AdminRoleSummary[]> => {
    try {
      const result = await authManager.listAdminRoles()
      setRoles(result)
      setError(null)
      return result
    } catch (err) {
      throw handleError(err)
    }
  }, [authManager, handleError])

  const listRoles = useCallback(async (): Promise<AdminRoleSummary[]> => {
    setIsLoading(true)

    try {
      return await fetchRoles()
    } finally {
      setIsLoading(false)
    }
  }, [fetchRoles])

  useEffect(() => {
    if (optionsRef.current.autoLoad) {
      listRoles().catch(() => undefined)
    }
  }, [listRoles])

  const refresh = useCallback(async (): Promise<AdminRoleSummary[]> => {
    return await fetchRoles()
  }, [fetchRoles])

  const createRole = useCallback(async (payload: AdminRoleCreateRequest): Promise<AdminRoleDetail> => {
    setIsMutating(true)
    setError(null)

    try {
      const detail = await authManager.createAdminRole(payload)
      await fetchRoles()
      return detail
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, fetchRoles, handleError])

  const updateRole = useCallback(async (roleId: string, payload: AdminRoleUpdateRequest): Promise<AdminRoleDetail> => {
    setIsMutating(true)
    setError(null)

    try {
      const detail = await authManager.updateAdminRole(roleId, payload)
      await fetchRoles()
      return detail
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, fetchRoles, handleError])

  const deleteRole = useCallback(async (roleId: string): Promise<void> => {
    setIsMutating(true)
    setError(null)

    try {
      await authManager.deleteAdminRole(roleId)
      await fetchRoles()
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, fetchRoles, handleError])

  return {
    roles,
    isLoading,
    isMutating,
    error,
    listRoles,
    refresh,
    createRole,
    updateRole,
    deleteRole,
  }
}
