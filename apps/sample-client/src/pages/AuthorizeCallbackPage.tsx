import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { exchangeAuthorizationCode } from '../api/auth'
import { CONFIG } from '../config'
import { consumePkce } from '../utils/pkce'

type TokenResponse = {
  access_token: string
  refresh_token?: string
  expires_in: number
  [key: string]: unknown
}

export default function AuthorizeCallbackPage() {
  const [params] = useSearchParams()
  const receivedError = params.get('error')
  const errorDescription = params.get('error_description')
  const code = params.get('code')
  const state = params.get('state')

  const [tokenResponse, setTokenResponse] = useState<TokenResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [verifier, setVerifier] = useState<string | null>(null)
  const [exchanging, setExchanging] = useState(false)

  useEffect(() => {
    if (state) {
      const stored = consumePkce(state)
      setVerifier(stored)
    }
  }, [state])

  const canExchange = useMemo(() => Boolean(code && verifier), [code, verifier])

  const exchangeCode = async () => {
    if (!code || !verifier) {
      setError('Missing authorization code or PKCE verifier.')
      return
    }

    setExchanging(true)
    setError(null)

    try {
      const payload = await exchangeAuthorizationCode({
        code,
        codeVerifier: verifier,
        redirectUri: CONFIG.authorizeRedirectUri,
      })
      setTokenResponse(payload)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Token exchange failed')
    } finally {
      setExchanging(false)
    }
  }

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

  return (
    <div className="space-y-4">
      <header className="space-y-1">
        <h1 className="text-2xl font-semibold text-slate-900">Authorization Callback</h1>
        <p className="text-sm text-slate-600">Authorization code received. Exchange it for tokens using the button below.</p>
      </header>

      <dl className="rounded-lg border border-slate-200 bg-white p-4 text-sm">
        <div className="mb-3">
          <dt className="font-medium text-slate-800">Authorization code</dt>
          <dd className="font-mono break-all text-slate-700">{code}</dd>
        </div>
        <div>
          <dt className="font-medium text-slate-800">State</dt>
          <dd className="font-mono break-all text-slate-700">{state ?? '—'}</dd>
        </div>
      </dl>

      <button
        type="button"
        onClick={exchangeCode}
        disabled={!canExchange || exchanging}
        className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-70"
      >
        {exchanging ? 'Exchanging…' : 'Exchange for tokens'}
      </button>

      {verifier === null && state && (
        <p className="text-sm text-amber-600">
          PKCE verifier not found in session storage. Was the authorization started from this browser tab?
        </p>
      )}

      {error && <p className="text-sm text-red-600">{error}</p>}

      {tokenResponse && (
        <pre className="overflow-x-auto rounded-md bg-slate-900 p-4 text-xs text-slate-100">
          {JSON.stringify(tokenResponse, null, 2)}
        </pre>
      )}
    </div>
  )
}
