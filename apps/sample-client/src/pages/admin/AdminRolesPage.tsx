import type { FormEvent } from 'react'
import { useEffect, useMemo, useState } from 'react'
import { useAdminRoles } from '@identity-base/react-client'

function classNames(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(' ')
}

export default function AdminRolesPage() {
  const {
    roles,
    isLoading,
    isMutating,
    error,
    listRoles,
    refresh,
    createRole,
    updateRole,
    deleteRole,
  } = useAdminRoles({ autoLoad: true })

  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null)
  const selectedRole = useMemo(() => roles.find(role => role.id === selectedRoleId) ?? null, [roles, selectedRoleId])

  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [isSystemRoleFlag, setIsSystemRoleFlag] = useState(false)
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([])
  const [customPermission, setCustomPermission] = useState('')
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  const permissionCatalog = useMemo(() => {
    return Array.from(new Set(roles.flatMap(role => role.permissions))).sort()
  }, [roles])

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

  const resetForm = () => {
    setSelectedRoleId(null)
    setStatusMessage(null)
    setFormError(null)
    setCustomPermission('')
  }

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
                      No roles defined yet.
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
              {permissionCatalog.length > 0 && (
                <div className="grid gap-2 max-h-48 overflow-y-auto rounded-md border border-slate-200 bg-slate-50 p-3">
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
