import type { FormEvent } from 'react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAdminRoles, useAdminUsers } from '@identity-base/react-client'

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

function classNames(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(' ')
}

export default function AdminUsersPage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [roleFilter, setRoleFilter] = useState('')
  const [lockedFilter, setLockedFilter] = useState<'all' | 'locked' | 'unlocked'>('all')
  const [pageSize, setPageSize] = useState(25)
  const [sortOrder, setSortOrder] = useState<'createdAt:desc' | 'createdAt:asc' | 'email:asc' | 'email:desc'>('createdAt:desc')
  const [deletedFilter, setDeletedFilter] = useState<'all' | 'active' | 'deleted'>('all')
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [showCreateForm, setShowCreateForm] = useState(false)

  const {
    data,
    users,
    query,
    isLoading,
    isMutating,
    error,
    listUsers,
    refresh,
    createUser,
    lockUser,
    unlockUser,
    forcePasswordReset,
    resetMfa,
    resendConfirmation,
    softDeleteUser,
    restoreUser,
  } = useAdminUsers({ autoLoad: true, initialQuery: { page: 1, pageSize, sort: 'createdAt:desc' } })

  const {
    roles: availableRoles,
    error: rolesError,
  } = useAdminRoles({ autoLoad: true })

  const [newUserEmail, setNewUserEmail] = useState('')
  const [newUserDisplayName, setNewUserDisplayName] = useState('')
  const [newUserRoles, setNewUserRoles] = useState<string[]>([])
  const [sendConfirmationEmail, setSendConfirmationEmail] = useState(false)
  const [sendPasswordResetEmail, setSendPasswordResetEmail] = useState(true)

  const totalPages = useMemo(() => {
    if (!data) {
      return 1
    }
    if (data.pageSize <= 0) {
      return 1
    }
    return Math.max(1, Math.ceil(data.totalCount / data.pageSize))
  }, [data])

  const currentPage = data?.page ?? query.page ?? 1
  const currentPageSize = data?.pageSize ?? query.pageSize ?? pageSize
  const totalUsers = data?.totalCount ?? 0
  const pageStart = totalUsers > 0 ? (currentPage - 1) * currentPageSize + 1 : 0
  const pageEnd = totalUsers > 0 ? Math.min(pageStart + Math.max(users.length, 1) - 1, totalUsers) : 0

  const hasForbiddenError = error?.status === 403

  const applyFilters = async () => {
    const locked = lockedFilter === 'all' ? undefined : lockedFilter === 'locked'

    await listUsers({
      page: 1,
      search: searchTerm.trim() || undefined,
      role: roleFilter || undefined,
      locked,
      deleted: deletedFilter === 'all' ? undefined : deletedFilter === 'deleted',
      sort: sortOrder,
      pageSize: currentPageSize,
    })
  }

  const handlePageChange = async (direction: 'prev' | 'next') => {
    const locked = lockedFilter === 'all' ? undefined : lockedFilter === 'locked'
    const nextPage = direction === 'prev' ? Math.max(1, currentPage - 1) : Math.min(totalPages, currentPage + 1)
    if (nextPage === currentPage) {
      return
    }

    await listUsers({
      page: nextPage,
      search: searchTerm.trim() || undefined,
      role: roleFilter || undefined,
      locked,
      deleted: deletedFilter === 'all' ? undefined : deletedFilter === 'deleted',
      sort: sortOrder,
      pageSize: currentPageSize,
    })
  }

  const handlePageSizeChange = async (size: number) => {
    setPageSize(size)
    const locked = lockedFilter === 'all' ? undefined : lockedFilter === 'locked'

    await listUsers({
      page: 1,
      search: searchTerm.trim() || undefined,
      role: roleFilter || undefined,
      locked,
      deleted: deletedFilter === 'all' ? undefined : deletedFilter === 'deleted',
      sort: sortOrder,
      pageSize: size,
    })
  }

  const handleSortChange = async (value: typeof sortOrder) => {
    setSortOrder(value)
    const locked = lockedFilter === 'all' ? undefined : lockedFilter === 'locked'

    await listUsers({
      page: 1,
      search: searchTerm.trim() || undefined,
      role: roleFilter || undefined,
      locked,
      deleted: deletedFilter === 'all' ? undefined : deletedFilter === 'deleted',
      sort: value,
      pageSize: currentPageSize,
    })
  }

  const handleDeletedFilterChange = async (value: typeof deletedFilter) => {
    setDeletedFilter(value)
    const locked = lockedFilter === 'all' ? undefined : lockedFilter === 'locked'

    await listUsers({
      page: 1,
      search: searchTerm.trim() || undefined,
      role: roleFilter || undefined,
      locked,
      deleted: value === 'all' ? undefined : value === 'deleted',
      sort: sortOrder,
      pageSize: currentPageSize,
    })
  }

  const toggleRoleSelection = (roleName: string) => {
    setNewUserRoles(current => {
      if (current.includes(roleName)) {
        return current.filter(role => role !== roleName)
      }
      return [...current, roleName]
    })
  }

  const handleCreateUser = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setFormError(null)
    setStatusMessage(null)

    if (!newUserEmail.trim()) {
      setFormError('Email is required')
      return
    }

    try {
      const created = await createUser({
        email: newUserEmail.trim(),
        displayName: newUserDisplayName.trim() || undefined,
        roles: newUserRoles,
        sendConfirmationEmail,
        sendPasswordResetEmail,
      })

      setStatusMessage(`Created user ${created.email ?? created.id}`)
      setShowCreateForm(false)
      setNewUserEmail('')
      setNewUserDisplayName('')
      setNewUserRoles([])
      setSendConfirmationEmail(false)
      setSendPasswordResetEmail(true)
      await refresh()
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to create user')
    }
  }

  const handleLockToggle = async (userId: string, shouldLock: boolean) => {
    setStatusMessage(null)
    try {
      if (shouldLock) {
        await lockUser(userId)
        setStatusMessage('User locked')
      } else {
        await unlockUser(userId)
        setStatusMessage('User unlocked')
      }
      await refresh()
    } catch (err: any) {
      setFormError(err?.message ?? 'Operation failed')
    }
  }

  const handleDeleteToggle = async (userId: string, shouldRestore: boolean) => {
    setStatusMessage(null)
    try {
      if (shouldRestore) {
        await restoreUser(userId)
        setStatusMessage('User restored')
      } else {
        await softDeleteUser(userId)
        setStatusMessage('User deleted')
      }
      await refresh()
    } catch (err: any) {
      setFormError(err?.message ?? 'Operation failed')
    }
  }

  const handleForcePasswordReset = async (userId: string) => {
    setStatusMessage(null)
    try {
      await forcePasswordReset(userId)
      setStatusMessage('Password reset email sent')
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to send password reset email')
    }
  }

  const handleResetMfa = async (userId: string) => {
    setStatusMessage(null)
    try {
      await resetMfa(userId)
      setStatusMessage('MFA reset for user')
      await refresh()
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to reset MFA')
    }
  }

  const handleResendConfirmation = async (userId: string) => {
    setStatusMessage(null)
    try {
      await resendConfirmation(userId)
      setStatusMessage('Confirmation email sent')
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to resend confirmation email')
    }
  }

  if (hasForbiddenError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6">
        <h2 className="text-lg font-semibold text-red-800">Access denied</h2>
        <p className="mt-2 text-sm text-red-700">
          You do not have permission to view admin users. Ensure your account has the required admin roles.
        </p>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div className="space-y-2">
            <h2 className="text-xl font-semibold text-slate-900">Users</h2>
            <p className="text-sm text-slate-600">
              Search by email or display name, filter by role or lock status, and manage individual accounts.
            </p>
          </div>
          <button
            onClick={() => setShowCreateForm(value => !value)}
            className="inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800"
          >
            {showCreateForm ? 'Cancel' : 'Create user'}
          </button>
        </div>

        {showCreateForm && (
          <form onSubmit={handleCreateUser} className="mt-6 space-y-4 rounded-md border border-slate-200 p-4">
            <div className="grid gap-4 md:grid-cols-2">
              <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
                Email
                <input
                  type="email"
                  value={newUserEmail}
                  onChange={event => setNewUserEmail(event.target.value)}
                  required
                  className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
                />
              </label>
              <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
                Display name
                <input
                  type="text"
                  value={newUserDisplayName}
                  onChange={event => setNewUserDisplayName(event.target.value)}
                  className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
                />
              </label>
            </div>

            <div className="flex flex-wrap gap-4 text-sm text-slate-700">
              <label className="inline-flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={sendConfirmationEmail}
                  onChange={event => setSendConfirmationEmail(event.target.checked)}
                  className="h-4 w-4"
                />
                Send confirmation email
              </label>
              <label className="inline-flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={sendPasswordResetEmail}
                  onChange={event => setSendPasswordResetEmail(event.target.checked)}
                  className="h-4 w-4"
                />
                Send password reset email
              </label>
            </div>

            <div className="space-y-2">
              <p className="text-sm font-medium text-slate-700">Assign roles</p>
              {availableRoles.length === 0 && (
                <p className="text-sm text-slate-500">No roles available yet. Create roles first.</p>
              )}
              <div className="grid gap-2 sm:grid-cols-2">
                {availableRoles.map(role => (
                  <label key={role.id} className="flex items-center gap-2 rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-sm">
                    <input
                      type="checkbox"
                      checked={newUserRoles.includes(role.name)}
                      onChange={() => toggleRoleSelection(role.name)}
                      className="h-4 w-4"
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
            </div>

            {formError && (
              <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
                {formError}
              </div>
            )}

            <div className="flex items-center gap-3">
              <button
                type="submit"
                disabled={isMutating}
                className="inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:opacity-60"
              >
                {isMutating ? 'Creating…' : 'Create user'}
              </button>
              <button
                type="button"
                onClick={() => setShowCreateForm(false)}
                className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
              >
                Cancel
              </button>
            </div>
          </form>
        )}
      </section>

      <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <form
          onSubmit={event => {
            event.preventDefault()
            applyFilters().catch(() => undefined)
          }}
          className="grid gap-4 md:grid-cols-5"
        >
          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 md:col-span-2">
            Search
            <input
              type="text"
              value={searchTerm}
              onChange={event => setSearchTerm(event.target.value)}
              placeholder="Email or display name"
              className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
            Role
            <select
              value={roleFilter}
              onChange={event => setRoleFilter(event.target.value)}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
            >
              <option value="">All roles</option>
              {availableRoles.map(role => (
                <option key={role.id} value={role.name}>
                  {role.name}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
            Status
            <select
              value={lockedFilter}
              onChange={event => setLockedFilter(event.target.value as typeof lockedFilter)}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
            >
              <option value="all">All users</option>
              <option value="locked">Locked only</option>
              <option value="unlocked">Unlocked only</option>
            </select>
          </label>
          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
            Account state
            <select
              value={deletedFilter}
              onChange={event => handleDeletedFilterChange(event.target.value as typeof deletedFilter)}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
            >
              <option value="all">All</option>
              <option value="active">Active only</option>
              <option value="deleted">Soft-deleted only</option>
            </select>
          </label>
          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
            Sort by
            <select
              value={sortOrder}
              onChange={event => handleSortChange(event.target.value as typeof sortOrder)}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
            >
              <option value="createdAt:desc">Newest first</option>
              <option value="createdAt:asc">Oldest first</option>
              <option value="email:asc">Email A→Z</option>
              <option value="email:desc">Email Z→A</option>
            </select>
          </label>
          <div className="flex items-end gap-3">
            <button
              type="submit"
              className="inline-flex w-full items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800"
            >
              Apply filters
            </button>
          </div>
        </form>

        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 pb-3">
          <div className="text-sm text-slate-600">
            {totalUsers > 0
              ? `Viewing ${pageStart.toLocaleString()}-${pageEnd.toLocaleString()} of ${totalUsers.toLocaleString()} users`
              : 'No users match the current filters.'}
          </div>
          <div className="flex items-center gap-3">
            <label className="flex items-center gap-2 text-sm text-slate-600">
              Page size
              <select
                value={currentPageSize}
                onChange={event => handlePageSizeChange(Number(event.target.value))}
                className="rounded-md border border-slate-300 px-2 py-1 text-sm focus:border-slate-600 focus:outline-none"
              >
                {[10, 25, 50, 100].map(size => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </label>
            <div className="flex items-center gap-2">
              <button
                onClick={() => handlePageChange('prev')}
                disabled={isLoading || currentPage <= 1}
                className="rounded-md border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-100 disabled:opacity-50"
              >
                Previous
              </button>
              <button
                onClick={() => handlePageChange('next')}
                disabled={isLoading || currentPage >= totalPages}
                className="rounded-md border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-100 disabled:opacity-50"
              >
                Next
              </button>
            </div>
          </div>
        </div>

        {statusMessage && (
          <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">
            {statusMessage}
          </div>
        )}

        {(error && !hasForbiddenError) && (
          <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
            {error.message}
          </div>
        )}

        {rolesError?.status === 403 && (
          <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-700">
            You do not have permission to view roles. Role filters and assignment will be limited.
          </div>
        )}

        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-3 font-semibold text-slate-600">User</th>
                <th className="px-4 py-3 font-semibold text-slate-600">Roles</th>
                <th className="px-4 py-3 font-semibold text-slate-600">Status</th>
                <th className="px-4 py-3 font-semibold text-slate-600">Created</th>
                <th className="px-4 py-3 font-semibold text-slate-600">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200">
              {isLoading && (
                <tr>
                  <td colSpan={5} className="px-4 py-6 text-center text-sm text-slate-500">
                    Loading users…
                  </td>
                </tr>
              )}

              {!isLoading && users.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-6 text-center text-sm text-slate-500">
                    No users match the current filters.
                  </td>
                </tr>
              )}

              {users.map(user => {
                const isLocked = user.isLockedOut
                return (
                  <tr key={user.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3">
                      <div className="flex flex-col">
                        <Link
                          to={`./${user.id}`}
                          className="font-medium text-slate-900 hover:underline"
                        >
                          {user.email ?? 'Unnamed user'}
                        </Link>
                        {user.displayName && (
                          <span className="text-xs text-slate-500">{user.displayName}</span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-2">
                        {user.roles.length === 0 && (
                          <span className="rounded-full bg-slate-100 px-2 py-1 text-xs text-slate-500">No roles</span>
                        )}
                        {user.roles.map(role => (
                          <span
                            key={role}
                            className="rounded-full bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700"
                          >
                            {role}
                          </span>
                        ))}
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex flex-col gap-1">
                        <span
                          className={classNames(
                            'inline-flex items-center justify-center rounded-full px-2 py-1 text-xs font-semibold',
                            user.isDeleted
                              ? 'bg-red-100 text-red-700'
                              : isLocked
                                ? 'bg-amber-100 text-amber-800'
                                : 'bg-emerald-100 text-emerald-700',
                          )}
                        >
                          {user.isDeleted ? 'Deleted' : isLocked ? 'Locked' : 'Active'}
                        </span>
                        <span className="text-xs text-slate-500">
                          MFA: {user.mfaEnabled ? 'Enabled' : 'Disabled'}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-slate-600">{formatDate(user.createdAt)}</td>
                    <td className="px-4 py-3 text-sm">
                      <div className="flex flex-wrap gap-2">
                        <Link
                          to={`./${user.id}`}
                          className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100"
                        >
                          View
                        </Link>
                        <button
                          onClick={() => handleLockToggle(user.id, !isLocked)}
                          disabled={isMutating}
                          className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-50"
                        >
                          {isLocked ? 'Unlock' : 'Lock'}
                        </button>
                        <button
                          onClick={() => handleForcePasswordReset(user.id)}
                          disabled={isMutating}
                          className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-50"
                        >
                          Reset password
                        </button>
                        <button
                          onClick={() => handleResetMfa(user.id)}
                          disabled={isMutating}
                          className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-50"
                        >
                          Reset MFA
                        </button>
                        {!user.emailConfirmed && (
                          <button
                            onClick={() => handleResendConfirmation(user.id)}
                            disabled={isMutating}
                            className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-50"
                          >
                            Resend confirmation
                          </button>
                        )}
                        <button
                          onClick={() => handleDeleteToggle(user.id, user.isDeleted)}
                          disabled={isMutating}
                          className={classNames(
                            'rounded-md border px-2 py-1 text-xs font-medium disabled:opacity-50',
                            user.isDeleted
                              ? 'border-emerald-200 text-emerald-700 hover:bg-emerald-100'
                              : 'border-red-300 text-red-700 hover:bg-red-50',
                          )}
                        >
                          {user.isDeleted ? 'Restore' : 'Delete'}
                        </button>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
