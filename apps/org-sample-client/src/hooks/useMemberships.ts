import { useEffect, useState, useCallback } from 'react'
import { useAuth } from '@identity-base/react-client'
import type { Membership } from '../api/types'
import { getMemberships } from '../api/organizations'

export function useMemberships() {
  const { isAuthenticated } = useAuth()
  const [memberships, setMemberships] = useState<Membership[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const load = useCallback(async () => {
    if (!isAuthenticated) {
      setMemberships([])
      return
    }

    setIsLoading(true)
    setError(null)
    try {
      const response = await getMemberships()
      setMemberships(response)
    } catch (err) {
      setError(err)
    } finally {
      setIsLoading(false)
    }
  }, [isAuthenticated])

  useEffect(() => {
    if (isAuthenticated) {
      load()
    }
  }, [isAuthenticated, load])

  return {
    memberships,
    isLoading,
    error,
    reload: load,
  }
}

