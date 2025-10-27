import { useCallback, useEffect, useRef, useState } from 'react'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'
import type {
  AdminPermissionSummary,
  AdminPermissionListQuery,
  AdminPermissionListResponse,
} from '../../core/types'

interface UseAdminPermissionsOptions {
  autoLoad?: boolean
  initialQuery?: AdminPermissionListQuery
  onError?: (error: any) => void
}

interface UseAdminPermissionsResult {
  data: AdminPermissionListResponse | null
  permissions: AdminPermissionSummary[]
  query: AdminPermissionListQuery
  isLoading: boolean
  error: any
  listPermissions: (override?: AdminPermissionListQuery) => Promise<AdminPermissionListResponse>
  refresh: () => Promise<AdminPermissionListResponse>
}

export function useAdminPermissions(options: UseAdminPermissionsOptions = {}): UseAdminPermissionsResult {
  const { authManager } = useIdentityContext()
  const optionsRef = useRef(options)
  optionsRef.current = options

  const initialQueryRef = useRef<AdminPermissionListQuery>({
    page: options.initialQuery?.page ?? 1,
    pageSize: options.initialQuery?.pageSize ?? 50,
    search: options.initialQuery?.search,
    sort: options.initialQuery?.sort ?? 'name',
  })

  const queryRef = useRef<AdminPermissionListQuery>(initialQueryRef.current)
  const [query, setQuery] = useState<AdminPermissionListQuery>(initialQueryRef.current)
  const [data, setData] = useState<AdminPermissionListResponse | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<any>(null)

  const handleError = useCallback((err: any) => {
    const normalized = createError(err)
    setError(normalized)
    optionsRef.current.onError?.(normalized)
    return normalized
  }, [])

  const listPermissions = useCallback(async (override?: AdminPermissionListQuery): Promise<AdminPermissionListResponse> => {
    const nextQuery = { ...queryRef.current, ...override }
    queryRef.current = nextQuery
    setQuery(nextQuery)
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.listAdminPermissions(nextQuery)
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
      listPermissions().catch(() => undefined)
    }
  }, [listPermissions])

  const refresh = useCallback(async (): Promise<AdminPermissionListResponse> => {
    return await listPermissions()
  }, [listPermissions])

  return {
    data,
    permissions: data?.permissions ?? [],
    query,
    isLoading,
    error,
    listPermissions,
    refresh,
  }
}
