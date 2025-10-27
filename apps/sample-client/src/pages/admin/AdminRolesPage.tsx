import type { FormEvent } from 'react'
import { useEffect, useMemo, useState } from 'react'
import { useAdminRoles, useAdminPermissions } from '@identity-base/react-client'

function classNames(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(' ')
}

export default function AdminRolesPage() {
  const [roleSearch, setRoleSearch] = useState('')
  const [systemFilter, setSystemFilter] = useState<'all' | 'system' | 'custom'>('all')
  const [sortOrder, setSortOrder] = useState<'name' | 'name:desc' | 'userCount:asc' | 'userCount:desc'>('name')
  const [pageSize, setPageSize] = useState(25)

  const {
    data,
    roles,
    query,
    isLoading,
    isMutating,
    error,
    listRoles,
    refresh,
    createRole,
    updateRole,
    deleteRole,
  } = useAdminRoles({ autoLoad: true, initialQuery: { page: 1, pageSize, sort: 'name' } })

  const {
    permissions: permissionOptions,
    query: permissionQuery,
    isLoading: isLoadingPermissions,
    error: permissionError,
    listPermissions,
  } = useAdminPermissions({ autoLoad: true, initialQuery: { page: 1, pageSize: 200, sort: 'name' } })

  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null)
  const selectedRole = useMemo(() => roles.find(role => role.id === selectedRoleId) ?? null, [roles, selectedRoleId])

  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [isSystemRoleFlag, setIsSystemRoleFlag] = useState(false)
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([])
  const [customPermission, setCustomPermission] = useState('')
  const [permissionSearch, setPermissionSearch] = useState('')
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  const permissionCatalog = useMemo(() => (permissionOptions ?? []).map(permission => permission.name), [permissionOptions])

  const resolveSystemFilter = (value: typeof systemFilter): boolean | undefined => {
    if (value === 'all') {
      return undefined
    }

    return value === 'system'
  }

  const currentPage = data?.page ?? query.page ?? 1
  const currentPageSize = data?.pageSize ?? query.pageSize ?? pageSize
  const totalRoleCount = data?.totalCount ?? 0
  const totalPages = Math.max(1, Math.ceil(totalRoleCount / Math.max(currentPageSize, 1)))
  const hasRoles = totalRoleCount > 0
  const pageStart = hasRoles ? (currentPage - 1) * currentPageSize + 1 : 0
  const pageEnd = hasRoles ? Math.min(pageStart + roles.length - 1, totalRoleCount) : 0

  useEffect(() => {
    if (selectedRole) {
      setName(selectedRole.name)
      setDescription(selectedRole.description ?? '')
      setIsSystemRoleFlag(selectedRole.isSystemRole)
      setSelectedPermissions(selectedRole.permissions)
    } else {
      setName('')
      setDescription('')
      setIsSystemRoleFlag(false)
      setSelectedPermissions([])
    }
  }, [selectedRole])

  useEffect(() => {
    if (data && data.pageSize !== pageSize) {
      setPageSize(data.pageSize)
    }
  }, [data, pageSize])

  useEffect(() => {
    if (selectedRoleId && !roles.some(role => role.id === selectedRoleId)) {
      setSelectedRoleId(null)
    }
  }, [roles, selectedRoleId])

  useEffect(() => {
    if ((query.search ?? '') !== roleSearch) {
      setRoleSearch(query.search ?? '')
    }
  }, [query.search])

  useEffect(() => {
    const expected = query.isSystemRole
    const next: typeof systemFilter = expected === undefined ? 'all' : expected ? 'system' : 'custom'
    if (next !== systemFilter) {
      setSystemFilter(next)
    }
  }, [query.isSystemRole])

  useEffect(() => {
    if (query.sort && query.sort !== sortOrder) {
      setSortOrder(query.sort as typeof sortOrder)
    }
  }, [query.sort])

  const resetForm = () => {
    setSelectedRoleId(null)
    setStatusMessage(null)
    setFormError(null)
    setCustomPermission('')
  }

  useEffect(() => {
    const trimmed = permissionSearch.trim()
    if ((permissionQuery.search ?? '') === trimmed) {
      return
    }

    const handle = window.setTimeout(() => {
      listPermissions({
        page: 1,
        pageSize: permissionQuery.pageSize ?? 200,
        search: trimmed || undefined,
        sort: permissionQuery.sort ?? 'name',
      }).catch(() => undefined)
    }, 300)

    return () => window.clearTimeout(handle)
  }, [permissionSearch, listPermissions, permissionQuery.pageSize, permissionQuery.sort, permissionQuery.search])

  const togglePermission = (permission: string) => {
    setSelectedPermissions(current => {
      if (current.includes(permission)) {
        return current.filter(item => item !== permission)
      }
      return [...current, permission]
    })
  }

  const handleAddPermission = () => {
    const trimmed = customPermission.trim()
    if (!trimmed) {
      return
    }
    if (!selectedPermissions.includes(trimmed)) {
      setSelectedPermissions(current => [...current, trimmed])
    }
    setCustomPermission('')
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    if (!name.trim()) {
      setFormError('Role name is required')
      return
    }

    setFormError(null)
    setStatusMessage(null)

    const permissions = Array.from(new Set(selectedPermissions.map(permission => permission.trim()).filter(Boolean))).sort()

    try {
      if (selectedRole) {
        const detail = await updateRole(selectedRole.id, {
          concurrencyStamp: selectedRole.concurrencyStamp,
          name: name.trim(),
          description: description.trim() || null,
          isSystemRole: isSystemRoleFlag,
          permissions,
        })
        setStatusMessage(`Role “${detail.name}” updated`)
        setSelectedRoleId(detail.id)
      } else {
        const detail = await createRole({
          name: name.trim(),
          description: description.trim() || undefined,
          isSystemRole: isSystemRoleFlag,
          permissions,
        })
        setStatusMessage(`Role “${detail.name}” created`)
        setSelectedRoleId(detail.id)
      }
      await refresh()
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to save role')
    }
  }

  const handleDelete = async () => {
    if (!selectedRole) {
      return
    }

    setFormError(null)
    setStatusMessage(null)

    try {
      await deleteRole(selectedRole.id)
      setStatusMessage(`Role “${selectedRole.name}” deleted`)
      resetForm()
      await refresh()
    } catch (err: any) {
      setFormError(err?.message ?? 'Failed to delete role')
    }
  }

  const canDelete = selectedRole && !selectedRole.isSystemRole && selectedRole.userCount === 0

  const handleApplyFilters = async () => {
    await listRoles({
      page: 1,
      pageSize: currentPageSize,
      search: roleSearch.trim() || undefined,
      isSystemRole: resolveSystemFilter(systemFilter),
      sort: sortOrder,
    })
  }

  const handleRolePageChange = async (direction: 'prev' | 'next') => {
    const nextPage = direction === 'prev' ? Math.max(1, currentPage - 1) : Math.min(totalPages, currentPage + 1)
    if (nextPage === currentPage) {
      return
    }

    await listRoles({
      page: nextPage,
      pageSize: currentPageSize,
      search: roleSearch.trim() || undefined,
      isSystemRole: resolveSystemFilter(systemFilter),
      sort: sortOrder,
    })
  }

  const handleRolePageSizeChange = async (size: number) => {
    setPageSize(size)
    await listRoles({
      page: 1,
      pageSize: size,
      search: roleSearch.trim() || undefined,
      isSystemRole: resolveSystemFilter(systemFilter),
      sort: sortOrder,
    })
  }

  const handleSystemFilterChange = async (value: typeof systemFilter) => {
    setSystemFilter(value)
    await listRoles({
      page: 1,
      pageSize: currentPageSize,
      search: roleSearch.trim() || undefined,
      isSystemRole: resolveSystemFilter(value),
      sort: sortOrder,
    })
  }

  const handleSortOrderChange = async (value: typeof sortOrder) => {
    setSortOrder(value)
    await listRoles({
      page: 1,
      pageSize: currentPageSize,
      search: roleSearch.trim() || undefined,
      isSystemRole: resolveSystemFilter(systemFilter),
      sort: value,
    })
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold text-slate-900">Roles</h2>
          <p className="text-sm text-slate-600">Manage role definitions and assign permissions.</p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => listRoles().catch(() => undefined)}
            className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
          >
            Refresh
          </button>
          <button
            onClick={resetForm}
            className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800"
          >
            New role
          </button>
        </div>
      </div>

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

      {error && (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {error.message}
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[1.4fr_1fr]">
        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex items-center justify-between">
            <h3 className="text-lg font-semibold text-slate-900">Existing roles</h3>
            {isLoading && <span className="text-xs text-slate-500">Loading…</span>}
          </div>

          <div className="mt-4 grid gap-4 md:grid-cols-5">
            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 md:col-span-2">
              Search
              <input
                type="text"
                value={roleSearch}
                onChange={event => setRoleSearch(event.target.value)}
                onKeyDown={event => {
                  if (event.key === 'Enter') {
                    event.preventDefault()
                    handleApplyFilters().catch(() => undefined)
                  }
                }}
                placeholder="Role name or description"
                className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
              Role type
              <select
                value={systemFilter}
                onChange={event => handleSystemFilterChange(event.target.value as typeof systemFilter)}
                className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
              >
                <option value="all">All roles</option>
                <option value="system">System only</option>
                <option value="custom">Custom only</option>
              </select>
            </label>
            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
              Sort by
              <select
                value={sortOrder}
                onChange={event => handleSortOrderChange(event.target.value as typeof sortOrder)}
                className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
              >
                <option value="name">Name A→Z</option>
                <option value="name:desc">Name Z→A</option>
                <option value="userCount:desc">Most users</option>
                <option value="userCount:asc">Fewest users</option>
              </select>
            </label>
            <div className="flex items-end gap-2">
              <button
                type="button"
                onClick={() => handleApplyFilters().catch(() => undefined)}
                className="inline-flex w-full items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800"
              >
                Apply search
              </button>
            </div>
          </div>

          <div className="mt-4 flex flex-col gap-3 border-b border-slate-200 pb-3 text-xs text-slate-600 md:flex-row md:items-center md:justify-between">
            <p className="font-medium">
              {hasRoles
                ? `Viewing ${pageStart.toLocaleString()}-${pageEnd.toLocaleString()} of ${totalRoleCount.toLocaleString()} roles`
                : 'No roles found'}
            </p>
            <div className="flex flex-wrap items-center gap-2">
              <label className="flex items-center gap-2">
                <span>Page size</span>
                <select
                  value={currentPageSize}
                  onChange={event => handleRolePageSizeChange(Number(event.target.value)).catch(() => undefined)}
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
                  type="button"
                  onClick={() => handleRolePageChange('prev').catch(() => undefined)}
                  disabled={currentPage <= 1}
                  className="rounded-md border border-slate-300 px-3 py-1 font-medium text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  Previous
                </button>
                <span>
                  Page {currentPage} of {totalPages}
                </span>
                <button
                  type="button"
                  onClick={() => handleRolePageChange('next').catch(() => undefined)}
                  disabled={currentPage >= totalPages}
                  className="rounded-md border border-slate-300 px-3 py-1 font-medium text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  Next
                </button>
              </div>
            </div>
          </div>

          <div className="mt-4 overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-sm">
              <thead className="bg-slate-50 text-left">
                <tr>
                  <th className="px-4 py-3 font-semibold text-slate-600">Role</th>
                  <th className="px-4 py-3 font-semibold text-slate-600">Permissions</th>
                  <th className="px-4 py-3 font-semibold text-slate-600">Users</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {roles.map(role => (
                  <tr
                    key={role.id}
                    onClick={() => setSelectedRoleId(role.id)}
                    className={classNames(
                      'cursor-pointer hover:bg-slate-50',
                      selectedRoleId === role.id && 'bg-slate-100'
                    )}
                  >
                    <td className="px-4 py-3 align-top">
                      <div className="font-medium text-slate-900">{role.name}</div>
                      <div className="text-xs text-slate-500">
                        {role.description || 'No description'}
                      </div>
                      {role.isSystemRole && (
                        <span className="mt-2 inline-flex rounded-full bg-amber-100 px-2 py-1 text-xs font-semibold text-amber-800">
                          System role
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 align-top text-sm text-slate-600">
                      {role.permissions.length === 0 ? (
                        <span className="text-xs text-slate-400">No permissions</span>
                      ) : (
                        <div className="flex flex-wrap gap-2">
                          {role.permissions.map(permission => (
                            <span key={permission} className="rounded-full bg-slate-100 px-2 py-1 text-xs text-slate-700">
                              {permission}
                            </span>
                          ))}
                        </div>
                      )}
                    </td>
                    <td className="px-4 py-3 align-top text-sm text-slate-600">{role.userCount}</td>
                  </tr>
                ))}

                {roles.length === 0 && !isLoading && (
                  <tr>
                    <td colSpan={3} className="px-4 py-6 text-center text-sm text-slate-500">
                      {hasRoles ? 'No roles match the current filters.' : 'No roles defined yet.'}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>

        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
          <h3 className="text-lg font-semibold text-slate-900">
            {selectedRole ? 'Edit role' : 'Create role'}
          </h3>
          <form onSubmit={handleSubmit} className="mt-4 space-y-4">
            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
              Name
              <input
                type="text"
                value={name}
                onChange={event => setName(event.target.value)}
                placeholder="e.g. administrators"
                className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
                disabled={selectedRole?.isSystemRole}
              />
            </label>

            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
              Description
              <textarea
                value={description}
                onChange={event => setDescription(event.target.value)}
                rows={3}
                className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
              />
            </label>

            <label className="inline-flex items-center gap-2 text-sm text-slate-700">
              <input
                type="checkbox"
                checked={isSystemRoleFlag}
                onChange={event => setIsSystemRoleFlag(event.target.checked)}
                className="h-4 w-4"
                disabled={selectedRole?.isSystemRole}
              />
              System role (immutable name and permissions)
            </label>

            <div className="space-y-3">
              <div className="text-sm font-medium text-slate-700">Permissions</div>
              <div className="flex flex-wrap items-center gap-2">
                <input
                  type="text"
                  value={permissionSearch}
                  onChange={event => setPermissionSearch(event.target.value)}
                  placeholder="Search permissions"
                  className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none md:w-auto"
                />
                {isLoadingPermissions && <span className="text-xs text-slate-500">Loading…</span>}
                {permissionError && (
                  <span className="text-xs text-red-600">{permissionError.message ?? 'Failed to load permissions'}</span>
                )}
              </div>
              {permissionCatalog.length > 0 ? (
                <div className="grid max-h-48 gap-2 overflow-y-auto rounded-md border border-slate-200 bg-slate-50 p-3">
                  {permissionCatalog.map(permission => (
                    <label key={permission} className="flex items-center gap-2 text-sm text-slate-700">
                      <input
                        type="checkbox"
                        checked={selectedPermissions.includes(permission)}
                        onChange={() => togglePermission(permission)}
                        className="h-4 w-4"
                      />
                      {permission}
                    </label>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-slate-500">
                  {permissionSearch.trim()
                    ? 'No permissions match the current search.'
                    : 'No permissions registered yet.'}
                </p>
              )}

              <div className="flex items-center gap-2">
                <input
                  type="text"
                  value={customPermission}
                  onChange={event => setCustomPermission(event.target.value)}
                  placeholder="Add custom permission"
                  className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-600 focus:outline-none"
                />
                <button
                  type="button"
                  onClick={handleAddPermission}
                  className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
                >
                  Add
                </button>
              </div>

              {selectedPermissions.length > 0 && (
                <div className="flex flex-wrap gap-2">
                  {selectedPermissions.map(permission => (
                    <span
                      key={permission}
                      className="inline-flex items-center gap-2 rounded-full bg-slate-100 px-3 py-1 text-xs text-slate-700"
                    >
                      {permission}
                      <button
                        type="button"
                        onClick={() => togglePermission(permission)}
                        className="text-slate-500 hover:text-slate-700"
                      >
                        ×
                      </button>
                    </span>
                  ))}
                </div>
              )}
            </div>

            <div className="flex flex-wrap items-center gap-3">
              <button
                type="submit"
                disabled={isMutating}
                className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:opacity-50"
              >
                {selectedRole ? (isMutating ? 'Updating…' : 'Update role') : isMutating ? 'Creating…' : 'Create role'}
              </button>
              {selectedRole && (
                <button
                  type="button"
                  onClick={handleDelete}
                  disabled={!canDelete || isMutating}
                  className="rounded-md border border-red-300 px-4 py-2 text-sm font-semibold text-red-700 hover:bg-red-50 disabled:opacity-50"
                >
                  Delete role
                </button>
              )}
            </div>

            {selectedRole && !canDelete && (
              <p className="text-xs text-slate-500">
                Roles that are system-managed or assigned to users cannot be deleted.
              </p>
            )}
          </form>
        </section>
      </div>
    </div>
  )
}
