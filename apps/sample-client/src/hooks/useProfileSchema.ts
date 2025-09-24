import { useEffect, useState } from 'react'
import { getProfileSchema } from '../api/auth'
import type { ProfileSchemaField } from '../api/types'

interface ProfileSchemaState {
  fields: ProfileSchemaField[]
  isLoading: boolean
  error: unknown
  refresh: () => void
}

export function useProfileSchema(): ProfileSchemaState {
  const [fields, setFields] = useState<ProfileSchemaField[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<unknown>(null)
  const [version, setVersion] = useState(0)

  useEffect(() => {
    let mounted = true
    setIsLoading(true)
    ;(async () => {
      try {
        const response = await getProfileSchema()
        if (mounted) {
          setFields(response.fields)
          setError(null)
        }
      } catch (err) {
        if (mounted) {
          setError(err)
        }
      } finally {
        if (mounted) {
          setIsLoading(false)
        }
      }
    })()

    return () => {
      mounted = false
    }
  }, [version])

  const refresh = () => setVersion((value) => value + 1)

  return { fields, isLoading, error, refresh }
}
