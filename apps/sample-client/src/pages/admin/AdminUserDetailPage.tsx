import type { FormEvent } from 'react'
import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  useAdminRoles,
  useAdminUser,
  useAdminUserRoles,
} from '@identity-base/react-client'

function classNames(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(' ')
}

function formatDate(value?: string | null) {
  if (!value) {
    return '—'
  }
  try {
    return new Date(value).toLocaleString()
  } catch {
    return value
  }
}

export default function AdminUserDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  const {
    user,
    isLoading,
    isMutating,
    error,
    updateUser,
    lockUser,
    unlockUser,
    forcePasswordReset,
    resetMfa,
    resendConfirmation,
    softDeleteUser,
    restoreUser,
  } = useAdminUser(id, { autoLoad: true })

  const {
    roles: assignedRoles,
    isLoading: isLoadingRoles,
    isUpdating: isUpdatingRoles,
    updateRoles,
  } = useAdminUserRoles(id, { autoLoad: true })

  const {
    roles: availableRoles,
    error: rolesError,
  } = useAdminRoles({ autoLoad: true })

  const [displayName, setDisplayName] = useState('')
  const [emailConfirmed, setEmailConfirmed] = useState(false)
  const [lockoutEnabled, setLockoutEnabled] = useState(false)
  const [twoFactorEnabled, setTwoFactorEnabled] = useState(false)
  const [phoneNumber, setPhoneNumber] = useState('')
  const [phoneNumberConfirmed, setPhoneNumberConfirmed] = useState(false)
  const [selectedRoles, setSelectedRoles] = useState<string[]>([])

  useEffect(() => {
    if (!user) {
      return
    }

    setDisplayName(user.displayName ?? '')
    setEmailConfirmed(user.emailConfirmed)
    setLockoutEnabled(user.lockoutEnabled)
    setTwoFactorEnabled(user.twoFactorEnabled)
    setPhoneNumber(user.phoneNumber ?? '')
    setPhoneNumberConfirmed(user.phoneNumberConfirmed)
  }, [user])

  useEffect(() => {
    setSelectedRoles(assignedRoles)
  }, [assignedRoles])

  const permissions = useMemo(() => {
    if (!availableRoles.length) {
      return [] as string[]
    }
    return Array.from(new Set(availableRoles.flatMap(role => role.permissions))).sort()
  }, [availableRoles])

  const metadataEntries = useMemo(() => {
    if (!user) {
      return [] as Array<[string, string | null]>
    }
    return Object.entries(user.metadata) as Array<[string, string | null]>
  }, [user])

  const externalLogins = user?.externalLogins ?? []

  if (error?.status === 403) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6">
        <h2 className="text-lg font-semibold text-red-800">Access denied</h2>
        <p className="mt-2 text-sm text-red-700">
          You do not have permission to view this user. Please verify your admin scope and permissions.
        </p>
      </div>
    )
  }

  if (!isLoading && error?.status === 404) {
    return (
      <div className="space-y-4">
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-6 text-sm text-amber-800">
          The requested user could not be found. It may have been deleted.
        </div>
        <button
          onClick={() => navigate('../')}
          className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
        >
          Back to users
        </button>
      </div>
    )
  }

  const handleUpdateUser = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!user) {
      return
    }

    setFormError(null)
    setStatusMessage(null)

    try {
      await updateUser({
        concurrencyStamp: user.concurrencyStamp,
        displayName: displayName.trim() || undefined,
        emailConfirmed,
        lockoutEnabled,
        twoFactorEnabled,
        phoneNumber: phoneNumber.trim() ? phoneNumber.trim() : null,
        phoneNumberConfirmed,
      })
      setStatusMessage('User profile updated')
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to update user')
    }
  }

  const toggleRole = (roleName: string) => {
    setSelectedRoles(current => {
      if (current.includes(roleName)) {
        return current.filter(role => role !== roleName)
      }
      return [...current, roleName]
    })
  }

  const handleSaveRoles = async () => {
    if (!user) {
      return
    }

    setFormError(null)
    setStatusMessage(null)

    try {
      await updateRoles({ roles: selectedRoles })
      setStatusMessage('Roles updated')
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to update roles')
    }
  }

  const handleLockToggle = async () => {
    if (!user) {
      return
    }

    setFormError(null)
    setStatusMessage(null)

    try {
      if (user.isLockedOut) {
        await unlockUser()
        setStatusMessage('User unlocked')
      } else {
        await lockUser()
        setStatusMessage('User locked')
      }
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to toggle lock state')
    }
  }

  const handleSoftDeleteToggle = async () => {
    if (!user) {
      return
    }

    setFormError(null)
    setStatusMessage(null)

    try {
      if (user.isDeleted) {
        await restoreUser()
        setStatusMessage('User restored')
      } else {
        await softDeleteUser()
        setStatusMessage('User deleted')
      }
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to change delete state')
    }
  }

  const handleForcePasswordReset = async () => {
    if (!user) {
      return
    }

    setFormError(null)
    setStatusMessage(null)

    try {
      await forcePasswordReset()
      setStatusMessage('Password reset email sent')
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to send password reset email')
    }
  }

  const handleResetMfa = async () => {
    if (!user) {
      return
    }

    setFormError(null)
    setStatusMessage(null)

    try {
      await resetMfa()
      setStatusMessage('MFA reset for user')
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to reset MFA')
    }
  }

  const handleResendConfirmation = async () => {
    if (!user) {
      return
    }

    setFormError(null)
    setStatusMessage(null)

    try {
      await resendConfirmation()
      setStatusMessage('Confirmation email sent')
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to resend confirmation email')
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold text-slate-900">User detail</h2>
          {user && (
            <p className="text-sm text-slate-600">{user.email ?? 'No email on record'}</p>
          )}
        </div>
        <Link
          to="../"
          className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
        >
          Back to users
        </Link>
      </div>

      {(isLoading || !user) && !error && (
        <div className="rounded-lg border border-slate-200 bg-white p-6 text-sm text-slate-600">
          Loading user details…
        </div>
      )}

      {user && (
        <div className="space-y-6">
          {statusMessage && (
            <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">
              {statusMessage}
            </div>
          )}

          {formError && (
            <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
              {formError}
            </div>
          )}

          <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
            <h3 className="text-lg font-semibold text-slate-900">Profile</h3>
            <form onSubmit={handleUpdateUser} className="mt-4 space-y-4">
              <div className="grid gap-4 md:grid-cols-2">
                <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
                  Display name
                  <input
                    type="text"
                    value={displayName}
                    onChange={event => setDisplayName(event.target.value)}
                    className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
                  />
                </label>
                <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
                  Phone number
                  <input
                    type="tel"
                    value={phoneNumber}
                    onChange={event => setPhoneNumber(event.target.value)}
                    className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
                  />
                </label>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <label className="flex items-center gap-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={emailConfirmed}
                    onChange={event => setEmailConfirmed(event.target.checked)}
                    className="h-4 w-4"
                  />
                  Email confirmed
                </label>
                <label className="flex items-center gap-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={phoneNumberConfirmed}
                    onChange={event => setPhoneNumberConfirmed(event.target.checked)}
                    className="h-4 w-4"
                  />
                  Phone confirmed
                </label>
                <label className="flex items-center gap-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={lockoutEnabled}
                    onChange={event => setLockoutEnabled(event.target.checked)}
                    className="h-4 w-4"
                  />
                  Lockout enabled
                </label>
                <label className="flex items-center gap-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={twoFactorEnabled}
                    onChange={event => setTwoFactorEnabled(event.target.checked)}
                    className="h-4 w-4"
                  />
                  Two-factor enabled
                </label>
              </div>

              <div className="flex flex-wrap gap-3">
                <button
                  type="submit"
                  disabled={isMutating}
                  className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:opacity-50"
                >
                  {isMutating ? 'Saving…' : 'Save changes'}
                </button>
                <button
                  type="button"
                  onClick={handleLockToggle}
                  className={classNames(
                    'rounded-md px-4 py-2 text-sm font-semibold shadow-sm',
                    user.isLockedOut
                      ? 'border border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                      : 'border border-amber-200 bg-amber-50 text-amber-800 hover:bg-amber-100',
                  )}
                >
                  {user.isLockedOut ? 'Unlock user' : 'Lock user'}
                </button>
                <button
                  type="button"
                  onClick={handleSoftDeleteToggle}
                  className={classNames(
                    'rounded-md px-4 py-2 text-sm font-semibold shadow-sm',
                    user.isDeleted
                      ? 'border border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                      : 'border border-red-200 bg-red-50 text-red-700 hover:bg-red-100',
                  )}
                >
                  {user.isDeleted ? 'Restore user' : 'Delete user'}
                </button>
              </div>
            </form>
          </section>

          <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
            <h3 className="text-lg font-semibold text-slate-900">Roles</h3>
            {rolesError?.status === 403 && (
              <p className="mt-2 text-sm text-amber-600">
                You do not have permission to view or edit roles.
              </p>
            )}

            <div className="mt-4 space-y-3">
              <div className="grid gap-2 sm:grid-cols-2">
                {availableRoles.map(role => (
                  <label key={role.id} className="flex items-center gap-2 rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-sm">
                    <input
                      type="checkbox"
                      checked={selectedRoles.includes(role.name)}
                      onChange={() => toggleRole(role.name)}
                      className="h-4 w-4"
                      disabled={rolesError?.status === 403}
                    />
                    <span>
                      <span className="font-medium text-slate-800">{role.name}</span>
                      {role.description && (
                        <span className="block text-xs text-slate-500">{role.description}</span>
                      )}
                    </span>
                  </label>
                ))}
              </div>

              {permissions.length > 0 && (
                <div className="rounded-md border border-slate-200 bg-slate-50 p-3">
                  <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Available permissions</p>
                  <div className="mt-2 flex flex-wrap gap-2">
                    {permissions.map(permission => (
                      <span key={permission} className="rounded-full bg-white px-2 py-1 text-xs text-slate-600">
                        {permission}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              <div className="flex items-center gap-3">
                <button
                  onClick={handleSaveRoles}
                  disabled={isUpdatingRoles || rolesError?.status === 403}
                  className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:opacity-50"
                >
                  {isUpdatingRoles ? 'Saving…' : 'Save roles'}
                </button>
                {isLoadingRoles && <span className="text-xs text-slate-500">Loading assigned roles…</span>}
              </div>
            </div>
          </section>

          <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
            <h3 className="text-lg font-semibold text-slate-900">Security actions</h3>
            <div className="mt-4 flex flex-wrap gap-3 text-sm">
              <button
                onClick={handleForcePasswordReset}
                className="rounded-md border border-slate-300 px-4 py-2 font-medium text-slate-700 hover:bg-slate-100"
              >
                Send password reset
              </button>
              <button
                onClick={handleResetMfa}
                className="rounded-md border border-slate-300 px-4 py-2 font-medium text-slate-700 hover:bg-slate-100"
              >
                Reset MFA configuration
              </button>
              {!user.emailConfirmed && (
                <button
                  onClick={handleResendConfirmation}
                  className="rounded-md border border-slate-300 px-4 py-2 font-medium text-slate-700 hover:bg-slate-100"
                >
                  Resend confirmation email
                </button>
              )}
            </div>
          </section>

          <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
            <h3 className="text-lg font-semibold text-slate-900">Metadata</h3>
            <dl className="mt-4 grid gap-4 md:grid-cols-2">
              <div className="rounded-md border border-slate-200 bg-slate-50 p-3">
                <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">User ID</dt>
                <dd className="mt-1 text-sm text-slate-700">{user.id}</dd>
              </div>
              <div className="rounded-md border border-slate-200 bg-slate-50 p-3">
                <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Created</dt>
                <dd className="mt-1 text-sm text-slate-700">{formatDate(user.createdAt)}</dd>
              </div>
              <div className="rounded-md border border-slate-200 bg-slate-50 p-3">
                <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Lockout end</dt>
                <dd className="mt-1 text-sm text-slate-700">{formatDate(user.lockoutEnd)}</dd>
              </div>
              <div className="rounded-md border border-slate-200 bg-slate-50 p-3">
                <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">Authenticator configured</dt>
                <dd className="mt-1 text-sm text-slate-700">{user.authenticatorConfigured ? 'Yes' : 'No'}</dd>
              </div>
            </dl>

            {metadataEntries.length > 0 ? (
              <div className="mt-4">
                <h4 className="text-sm font-medium text-slate-800">Profile metadata</h4>
                <div className="mt-2 grid gap-2 md:grid-cols-2">
                  {metadataEntries.map(([key, value]) => (
                    <div key={key} className="rounded-md border border-slate-200 bg-slate-50 p-3 text-sm">
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">{key}</p>
                      <p className="mt-1 text-slate-700">{value ?? '—'}</p>
                    </div>
                  ))}
                </div>
              </div>
            ) : (
              <p className="mt-4 text-sm text-slate-500">No additional metadata stored for this user.</p>
            )}

            <div className="mt-4">
              <h4 className="text-sm font-medium text-slate-800">External logins</h4>
              {externalLogins.length === 0 ? (
                <p className="mt-1 text-sm text-slate-500">No linked external providers.</p>
              ) : (
                <ul className="mt-2 space-y-2 text-sm text-slate-700">
                  {externalLogins.map(login => (
                    <li key={`${login.provider}:${login.key}`} className="rounded-md border border-slate-200 bg-slate-50 p-3">
                      <span className="font-semibold text-slate-800">{login.provider}</span>
                      <span className="ml-2 text-slate-600">{login.displayName}</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </section>
        </div>
      )}
    </div>
  )
}
