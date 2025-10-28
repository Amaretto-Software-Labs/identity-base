import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useLogin, useIdentityContext } from '@identity-base/react-client'
import { useOrganizations } from '@identity-base/react-organizations'

export default function LoginPage() {
  const navigate = useNavigate()
  const { authManager } = useIdentityContext()
  const { reloadMemberships } = useOrganizations()
  const { login, isLoading, error } = useLogin({
    onSuccess: async (response) => {
      if (response.requiresTwoFactor) {
        await reloadMemberships().catch(() => undefined)
        navigate('/dashboard', { replace: true })
      } else {
        authManager.startAuthorization()
      }
    },
  })

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    await login({ email, password })
  }

  return (
    <div className="mx-auto max-w-md space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Sign in</h1>
        <p className="text-sm text-slate-600">
          Use the credentials created during registration. After sign-in the dashboard will display your active organization.
        </p>
      </header>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="login-email" className="block text-sm font-medium text-slate-700">
            Email
          </label>
          <input
            id="login-email"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
            disabled={isLoading}
          />
        </div>

        <div>
          <label htmlFor="login-password" className="block text-sm font-medium text-slate-700">
            Password
          </label>
          <input
            id="login-password"
            type="password"
            required
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
            disabled={isLoading}
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
    </div>
  )
}
