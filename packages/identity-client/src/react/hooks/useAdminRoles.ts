import { useCallback, useEffect, useRef, useState } from 'react'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'
import type {
  AdminRoleSummary,
  AdminRoleDetail,
  AdminRoleCreateRequest,
  AdminRoleUpdateRequest,
  AdminRoleListQuery,
  AdminRoleListResponse,
} from '../../core/types'

interface UseAdminRolesOptions {
  autoLoad?: boolean
  onError?: (error: any) => void
  initialQuery?: AdminRoleListQuery
}

interface UseAdminRolesResult {
  data: AdminRoleListResponse | null
  roles: AdminRoleSummary[]
  query: AdminRoleListQuery
  isLoading: boolean
  isMutating: boolean
  error: any
  listRoles: (override?: AdminRoleListQuery) => Promise<AdminRoleListResponse>
  refresh: () => Promise<AdminRoleListResponse>
  createRole: (payload: AdminRoleCreateRequest) => Promise<AdminRoleDetail>
  updateRole: (roleId: string, payload: AdminRoleUpdateRequest) => Promise<AdminRoleDetail>
  deleteRole: (roleId: string) => Promise<void>
}

export function useAdminRoles(options: UseAdminRolesOptions = {}): UseAdminRolesResult {
  const { authManager } = useIdentityContext()
  const optionsRef = useRef(options)
  optionsRef.current = options

  const initialQueryRef = useRef<AdminRoleListQuery>({
    page: options.initialQuery?.page ?? 1,
    pageSize: options.initialQuery?.pageSize ?? 25,
    search: options.initialQuery?.search,
    isSystemRole: options.initialQuery?.isSystemRole,
    sort: options.initialQuery?.sort ?? 'name',
  })

  const queryRef = useRef<AdminRoleListQuery>(initialQueryRef.current)
  const [query, setQuery] = useState<AdminRoleListQuery>(initialQueryRef.current)
  const [data, setData] = useState<AdminRoleListResponse | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isMutating, setIsMutating] = useState(false)
  const [error, setError] = useState<any>(null)

  const handleError = useCallback((err: any) => {
    const normalized = createError(err)
    setError(normalized)
    optionsRef.current.onError?.(normalized)
    return normalized
  }, [])

  const listRoles = useCallback(async (override?: AdminRoleListQuery): Promise<AdminRoleListResponse> => {
    const nextQuery = { ...queryRef.current, ...override }
    queryRef.current = nextQuery
    setQuery(nextQuery)
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.listAdminRoles(nextQuery)
      setData(response)
      return response
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsLoading(false)
    }
  }, [authManager, handleError])

  useEffect(() => {
    if (optionsRef.current.autoLoad) {
      listRoles().catch(() => undefined)
    }
  }, [listRoles])

  const refresh = useCallback(async (): Promise<AdminRoleListResponse> => {
    return await listRoles()
  }, [listRoles])

  const createRole = useCallback(async (payload: AdminRoleCreateRequest): Promise<AdminRoleDetail> => {
    setIsMutating(true)
    setError(null)

    try {
      const detail = await authManager.createAdminRole(payload)
      await listRoles()
      return detail
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, listRoles, handleError])

  const updateRole = useCallback(async (roleId: string, payload: AdminRoleUpdateRequest): Promise<AdminRoleDetail> => {
    setIsMutating(true)
    setError(null)

    try {
      const detail = await authManager.updateAdminRole(roleId, payload)
      await listRoles()
      return detail
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, listRoles, handleError])

  const deleteRole = useCallback(async (roleId: string): Promise<void> => {
    setIsMutating(true)
    setError(null)

    try {
      await authManager.deleteAdminRole(roleId)
      await listRoles()
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, listRoles, handleError])

  return {
    data,
    roles: data?.items ?? [],
    query,
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
