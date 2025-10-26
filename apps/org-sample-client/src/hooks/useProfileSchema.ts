import { useEffect, useState } from 'react'
import type { ProfileSchemaField } from '../api/types'
import { fetchProfileSchema } from '../api/auth'

export function useProfileSchema() {
  const [fields, setFields] = useState<ProfileSchemaField[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<unknown>(null)

  useEffect(() => {
    let isCancelled = false

    fetchProfileSchema()
      .then((response) => {
        if (!isCancelled) {
          setFields(response.fields ?? [])
        }
      })
      .catch((err) => {
        if (!isCancelled) {
          setError(err)
        }
      })
      .finally(() => {
        if (!isCancelled) {
          setIsLoading(false)
        }
      })

    return () => {
      isCancelled = true
    }
  }, [])

  return { fields, isLoading, error }
}

