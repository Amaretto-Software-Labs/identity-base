import { useState, useCallback } from 'react'
import type { MfaChallengeRequest, MfaVerifyRequest } from '../../core/types'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'
import { debugLog } from '../../utils/logger'

interface UseMfaOptions {
  onChallengeSuccess?: (response: { message: string }) => void
  onVerifySuccess?: (response: { message: string }) => void
  onEnrollSuccess?: (response: { sharedKey: string; authenticatorUri: string }) => void
  onDisableSuccess?: (response: { message: string }) => void
  onRecoveryCodesSuccess?: (response: { recoveryCodes: string[] }) => void
  onError?: (error: any) => void
}

export function useMfa(options: UseMfaOptions = {}) {
  const { authManager, refreshUser } = useIdentityContext()
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<any>(null)

  const sendChallenge = useCallback(async (request: Omit<MfaChallengeRequest, 'clientId'>) => {
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.sendMfaChallenge({
        ...request,
        clientId: '',
      })

      options.onChallengeSuccess?.(response)
      return response
    } catch (err) {
      const error = createError(err)
      setError(error)
      options.onError?.(error)
      throw error
    } finally {
      setIsLoading(false)
    }
  }, [authManager, options])

  const verifyChallenge = useCallback(async (request: Omit<MfaVerifyRequest, 'clientId'>) => {
    debugLog('useMfa.verifyChallenge: Starting MFA verification', request)
    setIsLoading(true)
    setError(null)

    try {
      debugLog('useMfa.verifyChallenge: Calling authManager.verifyMfa')
      const response = await authManager.verifyMfa({
        ...request,
        clientId: '',
      })
      debugLog('useMfa.verifyChallenge: MFA verification successful', response)

      // After successful MFA verification, refresh user state
      debugLog('useMfa.verifyChallenge: About to call refreshUser after successful MFA verification')
      await refreshUser()
      debugLog('useMfa.verifyChallenge: Called refreshUser after successful MFA verification')

      options.onVerifySuccess?.(response)
      return response
    } catch (err) {
      debugLog('useMfa.verifyChallenge: MFA verification failed', err)
      const error = createError(err)
      setError(error)
      options.onError?.(error)
      throw error
    } finally {
      setIsLoading(false)
    }
  }, [authManager, refreshUser, options])

  const enrollMfa = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.enrollMfa()
      options.onEnrollSuccess?.(response)
      return response
    } catch (err) {
      const error = createError(err)
      setError(error)
      options.onError?.(error)
      throw error
    } finally {
      setIsLoading(false)
    }
  }, [authManager, options])

  const disableMfa = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.disableMfa()

      // After disabling MFA, refresh user state
      await refreshUser()

      options.onDisableSuccess?.(response)
      return response
    } catch (err) {
      const error = createError(err)
      setError(error)
      options.onError?.(error)
      throw error
    } finally {
      setIsLoading(false)
    }
  }, [authManager, refreshUser, options])

  const regenerateRecoveryCodes = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      const response = await authManager.regenerateRecoveryCodes()
      options.onRecoveryCodesSuccess?.(response)
      return response
    } catch (err) {
      const error = createError(err)
      setError(error)
      options.onError?.(error)
      throw error
    } finally {
      setIsLoading(false)
    }
  }, [authManager, options])

  return {
    sendChallenge,
    verifyChallenge,
    enrollMfa,
    disableMfa,
    regenerateRecoveryCodes,
    isLoading,
    error,
  }
}