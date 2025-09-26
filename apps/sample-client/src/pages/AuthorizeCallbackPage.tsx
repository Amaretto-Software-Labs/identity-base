import { useEffect } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAuthorization } from '@identity-base/react-client'

export default function AuthorizeCallbackPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const { handleCallback, isLoading, error } = useAuthorization({
    onSuccess: () => {
      navigate('/', { replace: true })
    }
  })

  const receivedError = params.get('error')
  const errorDescription = params.get('error_description')
  const code = params.get('code')
  const state = params.get('state')

  useEffect(() => {
    if (code && state) {
      handleCallback(code, state)
    }
  }, [code, state, handleCallback])

  if (receivedError) {
    return (
      <div className="space-y-3">
        <h1 className="text-2xl font-semibold text-slate-900">Authorization Error</h1>
        <p className="text-sm text-red-600">{receivedError}</p>
        {errorDescription && <p className="text-sm text-slate-600">{errorDescription}</p>}
      </div>
    )
  }

  if (!code) {
    return <p className="text-sm text-slate-600">No authorization code present in the callback URL.</p>
  }

  if (isLoading) {
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-slate-900">Completing Authorization</h1>
        <p className="text-sm text-slate-600">Processing authorization code and exchanging for tokens...</p>
      </div>
    )
  }

  if (error) {
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-slate-900">Authorization Failed</h1>
        <p className="text-sm text-red-600">{error.message}</p>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-slate-900">Authorization Complete</h1>
      <p className="text-sm text-slate-600">Successfully authenticated! Redirecting...</p>
    </div>
  )
}