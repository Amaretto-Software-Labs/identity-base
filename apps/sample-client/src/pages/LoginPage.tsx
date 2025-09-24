import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { buildExternalStartUrl, login } from '../api/auth'
import type { LoginResponse } from '../api/types'
import { CONFIG } from '../config'
import { useAuth } from '../context/AuthContext'

export default function LoginPage() {
  const navigate = useNavigate()
  const { refreshUser } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [clientId, setClientId] = useState(CONFIG.clientId)
  const [clientSecret, setClientSecret] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSubmitting(true)
    setError(null)

    try {
      const response: LoginResponse = await login({
        email,
        password,
        clientId,
        clientSecret: clientSecret || undefined,
      })

      if (response.requiresTwoFactor) {
        navigate('/mfa', {
          state: {
            email,
            methods: response.methods ?? [],
          },
          replace: true,
        })
        return
      }

      await refreshUser()
      navigate('/', { replace: true })
    } catch (err) {
      setError(renderError(err))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="mx-auto max-w-md space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Sign in</h1>
        <p className="text-sm text-slate-600">
          Submit your credentials to establish the Identity cookie. If the account requires MFA you’ll be redirected to the
          verification step.
        </p>
      </header>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="email" className="block text-sm font-medium text-slate-700">
            Email
          </label>
          <input
            id="email"
            type="email"
            required
            autoComplete="username"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
          />
        </div>

        <div>
          <label htmlFor="password" className="block text-sm font-medium text-slate-700">
            Password
          </label>
          <input
            id="password"
            type="password"
            required
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
          />
        </div>

        <div>
          <label htmlFor="clientId" className="block text-sm font-medium text-slate-700">
            Client ID
          </label>
          <input
            id="clientId"
            type="text"
            required
            value={clientId}
            onChange={(event) => setClientId(event.target.value)}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
          />
          <p className="mt-1 text-xs text-slate-500">Defaults to the SPA client seeded in the Identity Base API.</p>
        </div>

        <div>
          <label htmlFor="clientSecret" className="block text-sm font-medium text-slate-700">
            Client Secret (optional for public clients)
          </label>
          <input
            id="clientSecret"
            type="text"
            value={clientSecret}
            onChange={(event) => setClientSecret(event.target.value)}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
          />
        </div>

        {error && <p className="text-sm text-red-600">{error}</p>}

        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-70"
        >
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>

      {renderExternalSection()}
    </div>
  )
}

function renderError(error: unknown) {
  if (!error) return 'Unexpected error'
  if (typeof error === 'string') return error
  if (typeof error === 'object' && error !== null) {
    const maybeProblem = error as { detail?: string; title?: string; errors?: Record<string, string[]> }
    if (maybeProblem.errors) {
      return Object.entries(maybeProblem.errors)
        .map(([key, messages]) => `${key}: ${messages.join(', ')}`)
        .join('\n')
    }
    return maybeProblem.detail ?? maybeProblem.title ?? 'Unexpected error'
  }
  return 'Unexpected error'
}

type ExternalProviderKey = 'google' | 'microsoft' | 'apple'

function renderExternalSection() {
  const candidates: ReadonlyArray<{ key: ExternalProviderKey; label: string }> = [
    { key: 'google', label: 'Google' },
    { key: 'microsoft', label: 'Microsoft' },
    { key: 'apple', label: 'Apple' },
  ]

  const providers = candidates.filter((provider): provider is { key: ExternalProviderKey; label: string } =>
    CONFIG.externalProviders[provider.key],
  )

  if (providers.length === 0) {
    return null
  }

  const handleStart = (provider: ExternalProviderKey) => {
    const url = buildExternalStartUrl(provider, 'login', `${window.location.origin}/external-result`)
    window.location.assign(url)
  }

  return (
    <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <h2 className="text-sm font-semibold text-slate-800">Or sign in with an external provider</h2>
      <div className="flex flex-wrap gap-2">
        {providers.map((provider) => (
          <button
            key={provider.key}
            type="button"
            onClick={() => handleStart(provider.key)}
            className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-800 hover:bg-slate-100"
          >
            Continue with {provider.label}
          </button>
        ))}
      </div>
    </section>
  )
}
