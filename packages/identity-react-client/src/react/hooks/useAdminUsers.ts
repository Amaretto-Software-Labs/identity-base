import { useCallback, useEffect, useRef, useState } from 'react'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'
import type {
  AdminUserListQuery,
  AdminUserListResponse,
  AdminUserCreateRequest,
  AdminUserCreateResponse,
  AdminUserLockRequest,
  AdminUserSummary,
} from '../../core/types'

interface UseAdminUsersOptions {
  initialQuery?: AdminUserListQuery
  autoLoad?: boolean
  onError?: (error: any) => void
}

interface UseAdminUsersResult {
  data: AdminUserListResponse | null
  users: AdminUserSummary[]
  query: AdminUserListQuery
  isLoading: boolean
  isMutating: boolean
  error: any
  listUsers: (override?: AdminUserListQuery) => Promise<AdminUserListResponse>
  refresh: () => Promise<AdminUserListResponse>
  createUser: (payload: AdminUserCreateRequest) => Promise<AdminUserCreateResponse>
  lockUser: (userId: string, payload?: AdminUserLockRequest) => Promise<void>
  unlockUser: (userId: string) => Promise<void>
  forcePasswordReset: (userId: string) => Promise<void>
  resetMfa: (userId: string) => Promise<void>
  resendConfirmation: (userId: string) => Promise<void>
  softDeleteUser: (userId: string) => Promise<void>
  restoreUser: (userId: string) => Promise<void>
}

export function useAdminUsers(options: UseAdminUsersOptions = {}): UseAdminUsersResult {
  const { authManager } = useIdentityContext()
  const optionsRef = useRef(options)
  optionsRef.current = options

  const initialQueryRef = useRef<AdminUserListQuery>({
    page: options.initialQuery?.page ?? 1,
    pageSize: options.initialQuery?.pageSize ?? 25,
    search: options.initialQuery?.search,
    role: options.initialQuery?.role,
    locked: options.initialQuery?.locked,
    deleted: options.initialQuery?.deleted,
    sort: options.initialQuery?.sort ?? 'createdAt:desc',
  })

  const queryRef = useRef<AdminUserListQuery>(initialQueryRef.current)
  const [query, setQuery] = useState<AdminUserListQuery>(initialQueryRef.current)
  const [data, setData] = useState<AdminUserListResponse | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isMutating, setIsMutating] = useState(false)
  const [error, setError] = useState<any>(null)

  const handleError = useCallback((err: any) => {
    const normalized = createError(err)
    setError(normalized)
    optionsRef.current.onError?.(normalized)
    return normalized
  }, [])

  const listUsers = useCallback(async (override?: AdminUserListQuery): Promise<AdminUserListResponse> => {
    const nextQuery = { ...queryRef.current, ...override }
    queryRef.current = nextQuery
    setQuery(nextQuery)
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.admin.users.list(nextQuery)
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
      listUsers().catch(() => undefined)
    }
  }, [listUsers])

  const refresh = useCallback(async (): Promise<AdminUserListResponse> => {
    return await listUsers()
  }, [listUsers])

  const createUser = useCallback(async (payload: AdminUserCreateRequest): Promise<AdminUserCreateResponse> => {
    setIsMutating(true)
    setError(null)

    try {
      return await authManager.admin.users.create(payload)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, handleError])

  const lockUser = useCallback(async (userId: string, payload?: AdminUserLockRequest): Promise<void> => {
    setIsMutating(true)
    setError(null)

    try {
      await authManager.admin.users.lock(userId, payload)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, handleError])

  const unlockUser = useCallback(async (userId: string): Promise<void> => {
    setIsMutating(true)
    setError(null)

    try {
      await authManager.admin.users.unlock(userId)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, handleError])

  const forcePasswordReset = useCallback(async (userId: string): Promise<void> => {
    setIsMutating(true)
    setError(null)

    try {
      await authManager.admin.users.forcePasswordReset(userId)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, handleError])

  const resetMfa = useCallback(async (userId: string): Promise<void> => {
    setIsMutating(true)
    setError(null)

    try {
      await authManager.admin.users.resetMfa(userId)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, handleError])

  const resendConfirmation = useCallback(async (userId: string): Promise<void> => {
    setIsMutating(true)
    setError(null)

    try {
      await authManager.admin.users.resendConfirmation(userId)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, handleError])

  const softDeleteUser = useCallback(async (userId: string): Promise<void> => {
    setIsMutating(true)
    setError(null)

    try {
      await authManager.admin.users.softDelete(userId)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, handleError])

  const restoreUser = useCallback(async (userId: string): Promise<void> => {
    setIsMutating(true)
    setError(null)

    try {
      await authManager.admin.users.restore(userId)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, handleError])

  const usersList: AdminUserSummary[] = data?.items ?? []

  return {
    data,
    users: usersList,
    query,
    isLoading,
    isMutating,
    error,
    listUsers,
    refresh,
    createUser,
    lockUser,
    unlockUser,
    forcePasswordReset,
    resetMfa,
    resendConfirmation,
    softDeleteUser,
    restoreUser,
  }
}
