import { useCallback, useEffect, useRef, useState } from 'react'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'
import type {
  AdminUserDetail,
  AdminUserLockRequest,
  AdminUserUpdateRequest,
} from '../../core/types'

interface UseAdminUserOptions {
  autoLoad?: boolean
  onError?: (error: any) => void
}

interface UseAdminUserResult {
  user: AdminUserDetail | null
  isLoading: boolean
  isMutating: boolean
  error: any
  loadUser: (overrideId?: string) => Promise<AdminUserDetail>
  refresh: () => Promise<AdminUserDetail>
  updateUser: (payload: AdminUserUpdateRequest) => Promise<AdminUserDetail>
  lockUser: (payload?: AdminUserLockRequest) => Promise<AdminUserDetail>
  unlockUser: () => Promise<AdminUserDetail>
  forcePasswordReset: () => Promise<void>
  resetMfa: () => Promise<AdminUserDetail>
  resendConfirmation: () => Promise<void>
  softDeleteUser: () => Promise<AdminUserDetail>
  restoreUser: () => Promise<AdminUserDetail>
}

export function useAdminUser(userId?: string | null, options: UseAdminUserOptions = {}): UseAdminUserResult {
  const { authManager } = useIdentityContext()
  const optionsRef = useRef(options)
  optionsRef.current = options

  const idRef = useRef<string | null>(userId ?? null)
  const [user, setUser] = useState<AdminUserDetail | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isMutating, setIsMutating] = useState(false)
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

  const fetchDetail = useCallback(async (id: string): Promise<AdminUserDetail> => {
    try {
      const detail = await authManager.getAdminUser(id)
      setUser(detail)
      setError(null)
      return detail
    } catch (err) {
      throw handleError(err)
    }
  }, [authManager, handleError])

  const loadUser = useCallback(async (overrideId?: string): Promise<AdminUserDetail> => {
    const id = overrideId ?? requireUserId()
    idRef.current = id
    setIsLoading(true)

    try {
      return await fetchDetail(id)
    } finally {
      setIsLoading(false)
    }
  }, [fetchDetail, requireUserId])

  useEffect(() => {
    idRef.current = userId ?? null
    if (optionsRef.current.autoLoad && userId) {
      loadUser(userId).catch(() => undefined)
    }
  }, [userId, loadUser])

  const refresh = useCallback(async (): Promise<AdminUserDetail> => {
    return await loadUser()
  }, [loadUser])

  const updateUser = useCallback(async (payload: AdminUserUpdateRequest): Promise<AdminUserDetail> => {
    const id = requireUserId()
    setIsMutating(true)
    setError(null)

    try {
      const detail = await authManager.updateAdminUser(id, payload)
      setUser(detail)
      return detail
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, handleError, requireUserId])

  const lockUser = useCallback(async (payload?: AdminUserLockRequest): Promise<AdminUserDetail> => {
    const id = requireUserId()
    setIsMutating(true)
    setError(null)

    try {
      await authManager.lockAdminUser(id, payload)
      return await fetchDetail(id)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, fetchDetail, handleError, requireUserId])

  const unlockUser = useCallback(async (): Promise<AdminUserDetail> => {
    const id = requireUserId()
    setIsMutating(true)
    setError(null)

    try {
      await authManager.unlockAdminUser(id)
      return await fetchDetail(id)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, fetchDetail, handleError, requireUserId])

  const forcePasswordReset = useCallback(async (): Promise<void> => {
    const id = requireUserId()
    setIsMutating(true)
    setError(null)

    try {
      await authManager.forceAdminPasswordReset(id)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, handleError, requireUserId])

  const resetMfa = useCallback(async (): Promise<AdminUserDetail> => {
    const id = requireUserId()
    setIsMutating(true)
    setError(null)

    try {
      await authManager.resetAdminUserMfa(id)
      return await fetchDetail(id)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, fetchDetail, handleError, requireUserId])

  const resendConfirmation = useCallback(async (): Promise<void> => {
    const id = requireUserId()
    setIsMutating(true)
    setError(null)

    try {
      await authManager.resendAdminConfirmation(id)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, handleError, requireUserId])

  const softDeleteUser = useCallback(async (): Promise<AdminUserDetail> => {
    const id = requireUserId()
    setIsMutating(true)
    setError(null)

    try {
      await authManager.softDeleteAdminUser(id)
      return await fetchDetail(id)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, fetchDetail, handleError, requireUserId])

  const restoreUser = useCallback(async (): Promise<AdminUserDetail> => {
    const id = requireUserId()
    setIsMutating(true)
    setError(null)

    try {
      await authManager.restoreAdminUser(id)
      return await fetchDetail(id)
    } catch (err) {
      throw handleError(err)
    } finally {
      setIsMutating(false)
    }
  }, [authManager, fetchDetail, handleError, requireUserId])

  return {
    user,
    isLoading,
    isMutating,
    error,
    loadUser,
    refresh,
    updateUser,
    lockUser,
    unlockUser,
    forcePasswordReset,
    resetMfa,
    resendConfirmation,
    softDeleteUser,
    restoreUser,
  }
}
