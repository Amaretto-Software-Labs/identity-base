import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useLocation, useNavigate, useSearchParams } from '@identity-base/sample-router'
import { useAuth, usePasskeyLogin, usePasskeys } from '@identity-base/react-client'

export default function PasskeysPage() {
  const { isAuthenticated } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const passkeys = usePasskeyLogin()
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [signupMode, setSignupMode] = useState<'passkey-assisted' | 'passwordless'>(() =>
    window.localStorage.getItem('identity-passkey-signup-mode') === 'passkey-assisted'
      ? 'passkey-assisted'
      : 'passwordless')
  const [password, setPassword] = useState('')
  const [passkeyName, setPasskeyName] = useState('My passkey')
  const [confirmed, setConfirmed] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [signupModes, setSignupModes] = useState<Array<'passkey-assisted' | 'passwordless'>>([])
  const draftId = searchParams.get('draftId')
  const token = searchParams.get('token')
  const isRecovery = location.pathname.includes('/recover/')
  const isConfirmation = !!draftId && !!token

  useEffect(() => {
    void passkeys.getConfiguration()
      .then(configuration => {
        setSignupModes(configuration.signupModes)
        if (!configuration.signupModes.includes(signupMode) && configuration.signupModes[0]) {
          setSignupMode(configuration.signupModes[0])
        }
      })
      .catch(() => undefined)
  }, [])

  const beginSignup = async (event: FormEvent) => {
    event.preventDefault()
    window.localStorage.setItem('identity-passkey-signup-mode', signupMode)
    const result = await passkeys.beginSignup({
      mode: signupMode,
      email,
      metadata: { displayName },
    })
    setMessage(`Request accepted (${result.correlationId}). Check your email to continue.`)
  }

  const beginRecovery = async (event: FormEvent) => {
    event.preventDefault()
    const result = await passkeys.beginRecovery({ email })
    setMessage(`Request accepted (${result.correlationId}). Check your email to continue.`)
  }

  const confirmEmail = async () => {
    if (!draftId || !token) return
    if (isRecovery) {
      await passkeys.confirmRecoveryEmail({ draftId, token })
    } else {
      const result = await passkeys.confirmSignupEmail({ draftId, token })
      setSignupMode(result.registrationMode)
    }
    setConfirmed(true)
    setMessage('Email confirmed. Create the passkey to finish.')
  }

  const complete = async () => {
    if (!draftId) return
    if (isRecovery) {
      await passkeys.completeRecovery({ draftId, name: passkeyName })
    } else {
      await passkeys.completeSignup({
        draftId,
        name: passkeyName,
        password: signupMode === 'passkey-assisted' ? password : undefined,
      })
    }
    navigate('/', { replace: true })
  }

  return (
    <div className="space-y-8">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Passkeys</h1>
        <p className="text-sm text-slate-600">
          Try usernameless sign-in, passwordless signup, passkey-assisted signup, recovery, and passkey management.
        </p>
      </header>

      {!passkeys.isSupported && (
        <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          This browser does not expose the WebAuthn APIs required for passkeys.
        </p>
      )}

      {!isConfirmation && (
        <section className="grid gap-6 md:grid-cols-2">
          <div className="space-y-4 rounded-lg border border-slate-200 bg-white p-5">
            <h2 className="text-lg font-semibold">Sign in</h2>
            <p className="text-sm text-slate-600">No email or password is sent; the authenticator selects the account.</p>
            <button
              type="button"
              disabled={!passkeys.isSupported || passkeys.isLoading}
              onClick={() => void passkeys.login().then(() => navigate('/'))}
              className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white disabled:opacity-60"
            >
              Sign in with a passkey
            </button>
          </div>

          {signupModes.length > 0 && (
          <form onSubmit={beginSignup} className="space-y-4 rounded-lg border border-slate-200 bg-white p-5">
            <h2 className="text-lg font-semibold">Create an account</h2>
            <select
              value={signupMode}
              onChange={event => setSignupMode(event.target.value as typeof signupMode)}
              className="w-full rounded-md border border-slate-300 px-3 py-2"
            >
              {signupModes.includes('passwordless') && <option value="passwordless">Passwordless</option>}
              {signupModes.includes('passkey-assisted') && <option value="passkey-assisted">Password + passkey</option>}
            </select>
            <input
              type="email"
              required
              placeholder="Email"
              value={email}
              onChange={event => setEmail(event.target.value)}
              className="w-full rounded-md border border-slate-300 px-3 py-2"
            />
            <input
              required
              placeholder="Display name"
              value={displayName}
              onChange={event => setDisplayName(event.target.value)}
              className="w-full rounded-md border border-slate-300 px-3 py-2"
            />
            <button className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white">
              Email signup link
            </button>
          </form>
          )}

          <form onSubmit={beginRecovery} className="space-y-4 rounded-lg border border-slate-200 bg-white p-5">
            <h2 className="text-lg font-semibold">Recover a passwordless account</h2>
            <input
              type="email"
              required
              placeholder="Email"
              value={email}
              onChange={event => setEmail(event.target.value)}
              className="w-full rounded-md border border-slate-300 px-3 py-2"
            />
            <button className="rounded-md border border-slate-300 px-4 py-2 text-sm font-semibold">
              Email recovery link
            </button>
          </form>
        </section>
      )}

      {isConfirmation && (
        <section className="max-w-lg space-y-4 rounded-lg border border-slate-200 bg-white p-5">
          <h2 className="text-lg font-semibold">{isRecovery ? 'Recover account' : 'Finish signup'}</h2>
          {!confirmed ? (
            <button
              type="button"
              onClick={() => void confirmEmail()}
              className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white"
            >
              Confirm email
            </button>
          ) : (
            <>
              {!isRecovery && (
                <p className="text-sm text-slate-600">
                  {signupMode === 'passkey-assisted'
                    ? 'Complete password + passkey signup.'
                    : 'Complete passwordless signup.'}
                </p>
              )}
              {!isRecovery && signupMode === 'passkey-assisted' && (
                <input
                  type="password"
                  required
                  minLength={12}
                  placeholder="Password"
                  value={password}
                  onChange={event => setPassword(event.target.value)}
                  className="w-full rounded-md border border-slate-300 px-3 py-2"
                />
              )}
              <input
                required
                value={passkeyName}
                onChange={event => setPasskeyName(event.target.value)}
                className="w-full rounded-md border border-slate-300 px-3 py-2"
              />
              <button
                type="button"
                onClick={() => void complete()}
                className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white"
              >
                Create passkey and continue
              </button>
            </>
          )}
        </section>
      )}

      {isAuthenticated && <PasskeyManagement />}
      {message && <p className="text-sm text-emerald-700">{message}</p>}
      {!!passkeys.error && <p className="text-sm text-red-600">{String((passkeys.error as Error).message)}</p>}
    </div>
  )
}

function PasskeyManagement() {
  const { passkeys, create, rename, remove, isLoading, error } = usePasskeys()
  const [name, setName] = useState('Another passkey')
  const [renameValue, setRenameValue] = useState<Record<string, string>>({})
  const [pendingRemovalId, setPendingRemovalId] = useState<string | null>(null)

  const confirmRemoval = async (id: string) => {
    await remove(id)
    setPendingRemovalId(null)
  }

  return (
    <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-5">
      <h2 className="text-lg font-semibold">Your passkeys</h2>
      <div className="flex gap-2">
        <input
          value={name}
          onChange={event => setName(event.target.value)}
          className="rounded-md border border-slate-300 px-3 py-2"
        />
        <button
          type="button"
          disabled={isLoading}
          onClick={() => void create(name)}
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white"
        >
          Add passkey
        </button>
      </div>
      <ul className="space-y-2">
        {passkeys.map(passkey => (
          <li key={passkey.id} className="space-y-2 rounded-md border border-slate-200 p-3">
            <div className="flex items-center justify-between">
              <span>{passkey.name}</span>
              <span className="text-xs text-slate-500">
                {passkey.isBackedUp ? 'Synced/backed up' : 'Not reported as backed up'}
              </span>
            </div>
            {pendingRemovalId === passkey.id ? (
              <div
                role="group"
                aria-label={`Confirm removal of ${passkey.name}`}
                className="flex flex-wrap items-center gap-2 rounded-md bg-red-50 p-2"
              >
                <span className="mr-auto text-sm text-red-800">
                  Remove {passkey.name}? You will not be able to use it to sign in.
                </span>
                <button
                  type="button"
                  onClick={() => setPendingRemovalId(null)}
                  className="rounded-md border border-slate-300 bg-white px-3 py-1 text-sm text-slate-700"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={() => void confirmRemoval(passkey.id)}
                  className="rounded-md bg-red-700 px-3 py-1 text-sm font-semibold text-white"
                >
                  Remove passkey
                </button>
              </div>
            ) : (
              <div className="flex gap-2">
                <input
                  aria-label={`Rename ${passkey.name}`}
                  value={renameValue[passkey.id] ?? passkey.name}
                  onChange={event => setRenameValue(previous => ({
                    ...previous,
                    [passkey.id]: event.target.value,
                  }))}
                  className="min-w-0 flex-1 rounded-md border border-slate-300 px-3 py-1 text-sm"
                />
                <button
                  type="button"
                  onClick={() => void rename(passkey, renameValue[passkey.id] ?? passkey.name)}
                  className="text-sm text-slate-700"
                >
                  Rename
                </button>
                <button
                  type="button"
                  onClick={() => setPendingRemovalId(passkey.id)}
                  className="text-sm text-red-700"
                >
                  Remove
                </button>
              </div>
            )}
          </li>
        ))}
      </ul>
      {passkeys.length < 2 && (
        <p className="text-sm text-amber-700">
          Add another passkey or keep another login method so one lost device does not lock you out.
        </p>
      )}
      {!!error && <p className="text-sm text-red-600">{String((error as Error).message)}</p>}
    </section>
  )
}
