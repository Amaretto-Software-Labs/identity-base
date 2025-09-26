import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useAuth, useProfile, useMfa } from '@identity-base/react-client'
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
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null)
  const [showRecoveryCodes, setShowRecoveryCodes] = useState(false)

  const { disableMfa, regenerateRecoveryCodes, isLoading: mfaLoading } = useMfa({
    onDisableSuccess: () => {
      setStatus('MFA has been disabled for your account.')
    },
    onRecoveryCodesSuccess: (response) => {
      setRecoveryCodes(response.recoveryCodes)
      setShowRecoveryCodes(true)
      setStatus('New recovery codes generated.')
    }
  })

  useEffect(() => {
    if (user && schema) {
      const next: Record<string, string> = {}
      schema.fields.forEach((field) => {
        next[field.name] = user.metadata?.[field.name] ?? ''
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

  // Refresh user data when component mounts to get latest MFA status
  useEffect(() => {
    if (isAuthenticated && !authLoading) {
      refreshUser()
    }
  }, [isAuthenticated, authLoading, refreshUser])

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

  const handleDisableMfa = async () => {
    if (!window.confirm('Are you sure you want to disable two-factor authentication? This will make your account less secure.')) {
      return
    }

    setStatus(null)
    setError(null)

    try {
      await disableMfa()
    } catch (err) {
      setError(renderError(err))
    }
  }

  const handleRegenerateRecoveryCodes = async () => {
    if (!window.confirm('Are you sure you want to generate new recovery codes? Your existing codes will no longer work.')) {
      return
    }

    setStatus(null)
    setError(null)

    try {
      await regenerateRecoveryCodes()
    } catch (err) {
      setError(renderError(err))
    }
  }

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text).then(() => {
      setStatus('Copied to clipboard!')
    })
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

      <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-800">Two-Factor Authentication</h2>
        <p className="text-xs text-slate-600">
          Two-factor authentication adds an extra layer of security to your account by requiring a second form of verification.
        </p>

        {user.twoFactorEnabled ? (
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-sm">
              <span className="inline-block w-2 h-2 bg-green-500 rounded-full"></span>
              <span className="text-slate-900 font-medium">MFA is enabled</span>
            </div>

            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={handleRegenerateRecoveryCodes}
                disabled={mfaLoading}
                className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-800 hover:bg-slate-100 disabled:opacity-70"
              >
                {mfaLoading ? 'Generating...' : 'Generate Recovery Codes'}
              </button>

              <button
                type="button"
                onClick={handleDisableMfa}
                disabled={mfaLoading}
                className="rounded-md border border-red-200 px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-70"
              >
                {mfaLoading ? 'Disabling...' : 'Disable MFA'}
              </button>
            </div>

            {recoveryCodes && showRecoveryCodes && (
              <div className="border rounded-lg p-4 bg-amber-50 border-amber-200">
                <h3 className="text-sm font-medium text-amber-900 mb-2">
                  ⚠️ Recovery Codes Generated
                </h3>
                <p className="text-xs text-amber-800 mb-3">
                  Save these codes in a safe place. You can use them to access your account if you lose your authenticator device.
                </p>
                <div className="grid grid-cols-2 gap-2 text-xs font-mono mb-2">
                  {recoveryCodes.map((code, index) => (
                    <div key={index} className="bg-white px-2 py-1 rounded border">
                      {code}
                    </div>
                  ))}
                </div>
                <button
                  onClick={() => copyToClipboard(recoveryCodes.join('\n'))}
                  className="text-xs text-amber-900 underline hover:no-underline"
                >
                  Copy All Codes
                </button>
              </div>
            )}
          </div>
        ) : (
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-sm">
              <span className="inline-block w-2 h-2 bg-slate-300 rounded-full"></span>
              <span className="text-slate-600">MFA is not enabled</span>
            </div>

            <Link
              to="/mfa-setup"
              className="inline-flex items-center rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800"
            >
              Set Up Two-Factor Authentication
            </Link>
          </div>
        )}
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
