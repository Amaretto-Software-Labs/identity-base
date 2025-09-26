import { useState } from 'react'
import { useAuthorization, PKCEManager } from '@identity-base/react-client'
import { CONFIG } from '../config'

export default function AuthorizePage() {
  const [status, setStatus] = useState<string | null>(null)
  const { startAuthorization, isLoading, error } = useAuthorization()

  const clearPkce = () => {
    const pkceManager = new PKCEManager()
    pkceManager.clearPkce()
    setStatus('Cleared stored PKCE verifier.')
  }

  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Authorization Code with PKCE</h1>
        <p className="text-sm text-slate-600">
          This workflow redirects the browser to the Identity Base authorize endpoint. After consent the server will redirect
          back to <code className="rounded bg-slate-100 px-1">{CONFIG.authorizeRedirectUri}</code> with an authorization code
          you can exchange for tokens.
        </p>
      </header>

      <div className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <dl className="grid grid-cols-1 gap-3 text-sm text-slate-600">
          <div>
            <dt className="font-medium text-slate-800">Client ID</dt>
            <dd className="font-mono">{CONFIG.clientId}</dd>
          </div>
          <div>
            <dt className="font-medium text-slate-800">Redirect URI</dt>
            <dd className="font-mono break-all">{CONFIG.authorizeRedirectUri}</dd>
          </div>
          <div>
            <dt className="font-medium text-slate-800">Scopes</dt>
            <dd className="font-mono break-all">{CONFIG.authorizeScope}</dd>
          </div>
        </dl>

        <div className="mt-4 flex flex-col gap-3 sm:flex-row">
          <button
            type="button"
            onClick={startAuthorization}
            disabled={isLoading}
            className="inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-70"
          >
            {isLoading ? 'Preparing…' : 'Start authorization'}
          </button>
          <button
            type="button"
            onClick={clearPkce}
            className="inline-flex items-center justify-center rounded-md border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-800 hover:bg-slate-100"
          >
            Clear stored PKCE verifier
          </button>
        </div>

        {status && <p className="mt-3 text-sm text-green-600">{status}</p>}
        {error && <p className="mt-3 text-sm text-red-600">{error.message}</p>}
      </div>
    </div>
  )
}
