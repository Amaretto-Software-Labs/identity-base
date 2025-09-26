import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth, useProfile } from '@identity-base/react-client'
import { buildExternalStartUrl, unlinkExternalProvider } from '../api/auth'
import { CONFIG } from '../config'

export default function ProfilePage() {
  const navigate = useNavigate()
  const { user, isAuthenticated, isLoading: authLoading, refreshUser } = useAuth()
  const { schema, isLoadingSchema, updateProfile, isUpdating, error: profileError } = useProfile()
  const [formState, setFormState] = useState<Record<string, string>>({})
  const [status, setStatus] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [linking, setLinking] = useState<string | null>(null)
  const [unlinking, setUnlinking] = useState<string | null>(null)

  useEffect(() => {
    if (user && schema) {
      const next: Record<string, string> = {}
      schema.fields.forEach((field) => {
        next[field.name] = user.metadata[field.name] ?? ''
      })
      setFormState(next)
    }
  }, [user, schema])

  const ready = useMemo(() => !authLoading && !isLoadingSchema, [authLoading, isLoadingSchema])

  useEffect(() => {
    if (ready && !isAuthenticated) {
      navigate('/login', { replace: true })
    }
  }, [ready, isAuthenticated, navigate])

  if (profileError) {
    return <p className="text-sm text-red-600">Unable to load profile schema.</p>
  }

  if (!user) {
    return <p className="text-sm text-slate-600">Sign in to manage your profile.</p>
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setStatus(null)
    setError(null)

    if (!schema || !user) return

    try {
      const metadata = Object.fromEntries(schema.fields.map((field) => [field.name, (formState[field.name] ?? '').trim() || null]))
      await updateProfile({ metadata, concurrencyStamp: user.concurrencyStamp })
      setStatus('Profile saved successfully.')
    } catch (err) {
      setError(renderError(err))
    }
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Profile</h1>
        <p className="text-sm text-slate-600">Update the metadata captured during registration.</p>
      </header>

      <form onSubmit={handleSubmit} className="space-y-4">
        {schema?.fields.map((field) => (
          <div key={field.name}>
            <label htmlFor={`profile-${field.name}`} className="block text-sm font-medium text-slate-700">
              {field.displayName}
            </label>
            <input
              id={`profile-${field.name}`}
              type="text"
              value={formState[field.name] ?? ''}
              onChange={(event) =>
                setFormState((previous) => ({
                  ...previous,
                  [field.name]: event.target.value,
                }))
              }
              maxLength={field.maxLength}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
            />
            <p className="mt-1 text-xs text-slate-500">Max length {field.maxLength}</p>
          </div>
        ))}

       {error && <p className="text-sm text-red-600">{error}</p>}
        {status && <p className="text-sm text-green-600">{status}</p>}

        <button
          type="submit"
          disabled={isUpdating}
          className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:opacity-70"
        >
          {isUpdating ? 'Saving…' : 'Save profile'}
        </button>
      </form>

      <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-800">External providers</h2>
        <p className="text-xs text-slate-600">
          Link a provider to reuse its sign-in flow. For linking, a new tab will redirect you back to the harness with status
          information.
        </p>
        <div className="flex flex-wrap gap-2">
          {(providersList()).map((provider) => (
              <div key={provider} className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => {
                    setLinking(provider)
                    const url = buildExternalStartUrl(provider, 'link', `${window.location.origin}/external-result`, {
                      email: user.email ?? '',
                    })
                    window.location.assign(url)
                  }}
                  className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-800 hover:bg-slate-100"
                >
                  Link {provider}
                </button>
                <button
                  type="button"
                  onClick={async () => {
                    setUnlinking(provider)
                    try {
                      await unlinkExternalProvider(provider)
                      await refreshUser()
                      setStatus(`Unlinked ${provider}.`)
                    } catch (err) {
                      setError(renderError(err))
                    } finally {
                      setUnlinking(null)
                    }
                  }}
                  className="rounded-md border border-red-200 px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50"
                  disabled={unlinking === provider}
                >
                  {unlinking === provider ? 'Unlinking…' : 'Unlink'}
                </button>
              </div>
            ))}
        </div>
        {linking && <p className="text-xs text-slate-500">Link flow started for {linking}. Complete the provider login.</p>}
      </section>
    </div>
  )
}

type ExternalProviderKey = 'google' | 'microsoft' | 'apple'

function providersList(): ExternalProviderKey[] {
  return (['google', 'microsoft', 'apple'] as const).filter(
    (provider): provider is ExternalProviderKey => CONFIG.externalProviders[provider],
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
