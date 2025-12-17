import { useState, useCallback, useEffect } from 'react'
import type { ProfileSchemaResponse, UserProfile } from '../../core/types'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'

interface UseProfileOptions {
  onUpdateSuccess?: (user: UserProfile) => void
  onError?: (error: any) => void
}

export function useProfile(options: UseProfileOptions = {}) {
  const { authManager, refreshUser } = useIdentityContext()
  const [schema, setSchema] = useState<ProfileSchemaResponse | null>(null)
  const [isLoadingSchema, setIsLoadingSchema] = useState(true)
  const [isUpdating, setIsUpdating] = useState(false)
  const [error, setError] = useState<any>(null)

  // Load profile schema on mount
  useEffect(() => {
    let mounted = true

    const loadSchema = async () => {
      try {
        const profileSchema = await authManager.getProfileSchema()
        if (mounted) {
          setSchema(profileSchema)
          setError(null)
        }
      } catch (err) {
        if (mounted) {
          setError(createError(err))
        }
      } finally {
        if (mounted) {
          setIsLoadingSchema(false)
        }
      }
    }

    loadSchema()

    return () => {
      mounted = false
    }
  }, [authManager])

  const updateProfile = useCallback(async (payload: {
    metadata: Record<string, string | null>
    concurrencyStamp: string
  }) => {
    setIsUpdating(true)
    setError(null)

    try {
      const updatedUser = await authManager.updateProfile(payload)
      await refreshUser() // Refresh the user state in context
      options.onUpdateSuccess?.(updatedUser)
      return updatedUser
    } catch (err) {
      const error = createError(err)
      setError(error)
      options.onError?.(error)
      throw error
    } finally {
      setIsUpdating(false)
    }
  }, [authManager, refreshUser, options])

  const refreshSchema = useCallback(async () => {
    setIsLoadingSchema(true)
    setError(null)

    try {
      const profileSchema = await authManager.getProfileSchema()
      setSchema(profileSchema)
      setError(null)
    } catch (err) {
      setError(createError(err))
    } finally {
      setIsLoadingSchema(false)
    }
  }, [authManager])

  return {
    schema,
    isLoadingSchema,
    updateProfile,
    isUpdating,
    refreshSchema,
    error,
  }
}