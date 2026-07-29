import { useCallback, useEffect, useRef, useState } from 'react'
import type {
  PasskeyCompletionRequest,
  PasskeyEmailConfirmation,
  PasskeyRecoveryRequest,
  PasskeySignupRequest,
  PasskeyLoginOptions,
} from '../../core/types'
import { useIdentityContext } from '../IdentityProvider'
import { createError } from '../../utils/errors'

export function usePasskeyLogin() {
  const { authManager, refreshUser } = useIdentityContext()
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const ceremonyController = useRef<AbortController | null>(null)

  const cancel = useCallback(() => {
    ceremonyController.current?.abort()
    ceremonyController.current = null
  }, [])

  useEffect(() => cancel, [cancel])

  const run = useCallback(async <T,>(action: () => Promise<T>): Promise<T> => {
    setIsLoading(true)
    setError(null)
    try {
      return await action()
    } catch (reason) {
      const normalized = createError(reason)
      setError(normalized)
      throw normalized
    } finally {
      setIsLoading(false)
    }
  }, [])

  const login = useCallback(
    (options: Omit<PasskeyLoginOptions, 'signal'> = {}) => run(async () => {
      cancel()
      const controller = new AbortController()
      ceremonyController.current = controller
      try {
        const response = await authManager.loginWithPasskey({ ...options, signal: controller.signal })
        await refreshUser()
        return response
      } finally {
        if (ceremonyController.current === controller) {
          ceremonyController.current = null
        }
      }
    }),
    [authManager, cancel, refreshUser, run],
  )

  const completeSignup = useCallback(
    (request: PasskeyCompletionRequest) => run(async () => {
      const response = await authManager.completePasskeySignup(request)
      await refreshUser()
      return response
    }),
    [authManager, refreshUser, run],
  )

  const completeRecovery = useCallback(
    (request: Pick<PasskeyCompletionRequest, 'draftId' | 'name'>) => run(async () => {
      const response = await authManager.completePasskeyRecovery(request)
      await refreshUser()
      return response
    }),
    [authManager, refreshUser, run],
  )

  return {
    isSupported: authManager.isPasskeySupported(),
    isLoading,
    error,
    login,
    cancel,
    isConditionalMediationAvailable: () => authManager.isConditionalMediationAvailable(),
    getConfiguration: () => run(() => authManager.getPasskeyConfiguration()),
    beginSignup: (request: PasskeySignupRequest) => run(() => authManager.beginPasskeySignup(request)),
    confirmSignupEmail: (request: PasskeyEmailConfirmation) =>
      run(() => authManager.confirmPasskeySignupEmail(request)),
    completeSignup,
    beginRecovery: (request: PasskeyRecoveryRequest) =>
      run(() => authManager.beginPasskeyRecovery(request)),
    confirmRecoveryEmail: (request: PasskeyEmailConfirmation) =>
      run(() => authManager.confirmPasskeyRecoveryEmail(request)),
    completeRecovery,
  }
}
