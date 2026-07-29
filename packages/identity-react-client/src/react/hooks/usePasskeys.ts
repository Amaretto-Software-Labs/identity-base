import { useCallback, useEffect, useState } from 'react'
import type { PasskeySummary } from '../../core/types'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'

export function usePasskeys() {
  const { authManager } = useIdentityContext()
  const [passkeys, setPasskeys] = useState<PasskeySummary[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<unknown>(null)

  const refresh = useCallback(async () => {
    setIsLoading(true)
    setError(null)
    try {
      const current = await authManager.listPasskeys()
      setPasskeys(current)
      return current
    } catch (reason) {
      const normalized = createError(reason)
      setError(normalized)
      throw normalized
    } finally {
      setIsLoading(false)
    }
  }, [authManager])

  useEffect(() => {
    void refresh().catch(() => undefined)
  }, [refresh])

  const create = useCallback(async (name: string) => {
    const created = await authManager.createPasskey(name)
    await refresh()
    return created
  }, [authManager, refresh])

  const rename = useCallback(async (passkey: PasskeySummary, name: string) => {
    const updated = await authManager.renamePasskey(passkey.id, name, passkey.concurrencyStamp)
    await refresh()
    return updated
  }, [authManager, refresh])

  const remove = useCallback(async (id: string) => {
    await authManager.removePasskey(id)
    await refresh()
  }, [authManager, refresh])

  return {
    passkeys,
    isSupported: authManager.isPasskeySupported(),
    isLoading,
    error,
    refresh,
    create,
    rename,
    remove,
  }
}
