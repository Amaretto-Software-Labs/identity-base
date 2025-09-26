import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useLogin, useIdentityContext } from '@identity-base/react-client'
import { CONFIG } from '../config'

export default function LoginPage() {
  const navigate = useNavigate()
  const { authManager } = useIdentityContext()
  const { login, isLoading, error } = useLogin({
    onSuccess: (response) => {
      if (response.requiresTwoFactor) {
        navigate('/mfa', {
          state: {
            methods: response.methods ?? [],
          },
          replace: true,
        })
      } else {
        navigate('/', { replace: true })
      }
    }
  })
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    await login({ email, password })
  }

  const renderExternalSection = () => {
    const hasExternalProviders = CONFIG.externalProviders.google ||
                                 CONFIG.externalProviders.microsoft ||
                                 CONFIG.externalProviders.apple

    if (!hasExternalProviders) {
      return (
        <p className="text-center text-sm text-slate-500">
          No external providers are configured.
        </p>
      )
    }

    return (
      <div className="space-y-3">
        <div className="text-center">
          <span className="bg-slate-50 px-2 text-sm text-slate-500">or continue with</span>
        </div>
        <div className="flex flex-col gap-2">
          {CONFIG.externalProviders.google && (
            <button
              type="button"
              onClick={() => {
                const url = authManager.buildExternalStartUrl('google', 'login', window.location.origin)
                window.location.href = url
              }}
              className="flex items-center justify-center gap-2 rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Google
            </button>
          )}
          {CONFIG.externalProviders.microsoft && (
            <button
              type="button"
              onClick={() => {
                const url = authManager.buildExternalStartUrl('microsoft', 'login', window.location.origin)
                window.location.href = url
              }}
              className="flex items-center justify-center gap-2 rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Microsoft
            </button>
          )}
          {CONFIG.externalProviders.apple && (
            <button
              type="button"
              onClick={() => {
                const url = authManager.buildExternalStartUrl('apple', 'login', window.location.origin)
                window.location.href = url
              }}
              className="flex items-center justify-center gap-2 rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Apple
            </button>
          )}
        </div>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-md space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Sign in</h1>
        <p className="text-sm text-slate-600">
          Submit your credentials to establish authentication. If your account requires MFA you'll be redirected to the
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
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            disabled={isLoading}
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
            disabled={isLoading}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
          />
        </div>

        {error && <p className="text-sm text-red-600">{error.message}</p>}

        <button
          type="submit"
          disabled={isLoading}
          className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-70"
        >
          {isLoading ? 'Signing in…' : 'Sign in'}
        </button>
      </form>

      {renderExternalSection()}
    </div>
  )
}