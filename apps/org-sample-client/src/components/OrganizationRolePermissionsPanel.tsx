import { useEffect, useMemo, useState } from 'react'
import { getOrganizationRolePermissions, updateOrganizationRolePermissions } from '../api/organizations'
import type { OrganizationRole, OrganizationRolePermissions } from '../api/types'
import { renderApiError } from '../api/client'

interface RolePermissionsPanelProps {
  organizationId: string
  roles: OrganizationRole[]
  onStatusMessage?: (message: string | null) => void
}

export function OrganizationRolePermissionsPanel({ organizationId, roles, onStatusMessage }: RolePermissionsPanelProps) {
  const [activeRoleId, setActiveRoleId] = useState<string | null>(null)
  const [permissions, setPermissions] = useState<OrganizationRolePermissions | null>(null)
  const [editablePermissions, setEditablePermissions] = useState<string[]>([])
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [newPermission, setNewPermission] = useState('')

  useEffect(() => {
    if (roles.length === 0) {
      setActiveRoleId(null)
      setPermissions(null)
      setEditablePermissions([])
      return
    }

    setActiveRoleId((previous) => {
      if (previous && roles.some((role) => role.id === previous)) {
        return previous
      }

      return roles[0]!.id
    })
  }, [roles])

  useEffect(() => {
    if (!organizationId || !activeRoleId) {
      setPermissions(null)
      setEditablePermissions([])
      return
    }

    let cancelled = false
    setLoading(true)
    setError(null)

    getOrganizationRolePermissions(organizationId, activeRoleId)
      .then((response) => {
        if (cancelled) return
        setPermissions(response)
        setEditablePermissions(response.explicit)
      })
      .catch((err) => {
        if (cancelled) return
        setError(renderApiError(err))
        setPermissions(null)
        setEditablePermissions([])
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [organizationId, activeRoleId])

  const permissionsChanged = useMemo(() => {
    if (!permissions) {
      return editablePermissions.length > 0
    }

    const original = new Set(permissions.explicit.map((value) => value.toLowerCase()))
    const current = editablePermissions
      .map((value) => value.trim())
      .filter((value) => value.length > 0)
      .map((value) => value.toLowerCase())

    if (original.size !== current.length) {
      return true
    }

    return current.some((value) => !original.has(value))
  }, [permissions, editablePermissions])

  const handleAddPermission = () => {
    const trimmed = newPermission.trim()
    if (trimmed.length === 0) {
      return
    }

    setEditablePermissions((previous) => {
      if (previous.some((value) => value.toLowerCase() === trimmed.toLowerCase())) {
        return previous
      }

      return [...previous, trimmed]
    })
    setNewPermission('')
    setError(null)
  }

  const handleRemovePermission = (permission: string) => {
    setEditablePermissions((previous) => previous.filter((value) => value.toLowerCase() !== permission.toLowerCase()))
    setError(null)
  }

  const handleSave = async () => {
    if (!activeRoleId) {
      return
    }

    const payload = editablePermissions
      .map((value) => value.trim())
      .filter((value, index, array) => value.length > 0 && array.findIndex((candidate) => candidate.toLowerCase() === value.toLowerCase()) === index)

    setSaving(true)
    setError(null)
    onStatusMessage?.(null)

    try {
      await updateOrganizationRolePermissions(organizationId, activeRoleId, payload)
      const updated = await getOrganizationRolePermissions(organizationId, activeRoleId)
      setPermissions(updated)
      setEditablePermissions(updated.explicit)
      onStatusMessage?.('Role permissions updated.')
    } catch (err) {
      setError(renderApiError(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-slate-900">Roles</h2>
        {roles.length > 0 ? (
          <span className="text-xs font-medium uppercase tracking-wide text-slate-500">{roles.length.toLocaleString()} total</span>
        ) : null}
      </div>

      {error && (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-xs text-red-700">{error}</div>
      )}

      <div className="grid gap-4 lg:grid-cols-[1.5fr,2fr]">
        <div className="space-y-2">
          {roles.length === 0 ? (
            <p className="text-sm text-slate-600">No organization roles have been defined.</p>
          ) : (
            roles.map((role) => {
              const isActive = role.id === activeRoleId
              return (
                <button
                  key={role.id}
                  type="button"
                  onClick={() => setActiveRoleId(role.id)}
                  className={`w-full rounded-md border px-3 py-2 text-left text-sm shadow-sm transition ${
                    isActive ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-300 bg-white text-slate-800 hover:bg-slate-100'
                  }`}
                >
                  <span className="font-semibold">{role.name}</span>
                  {role.description ? (
                    <span className={`mt-1 block text-xs ${isActive ? 'text-slate-100' : 'text-slate-500'}`}>
                      {role.description}
                    </span>
                  ) : null}
                  {role.isSystemRole ? (
                    <span
                      className={`mt-2 inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium ${
                        isActive ? 'bg-slate-800 text-slate-100' : 'bg-slate-200 text-slate-700'
                      }`}
                    >
                      System role
                    </span>
                  ) : null}
                </button>
              )
            })
          )}
        </div>

        <div className="space-y-3">
          {!activeRoleId ? (
            <p className="text-sm text-slate-600">Select a role to view and edit its permissions.</p>
          ) : loading ? (
            <p className="text-sm text-slate-600">Loading permissions…</p>
          ) : !permissions ? (
            <p className="text-sm text-slate-600">Unable to load permissions for the selected role.</p>
          ) : (
            <>
              <div>
                <h3 className="text-sm font-semibold text-slate-900">Effective permissions</h3>
                {permissions.effective.length === 0 ? (
                  <p className="text-xs text-slate-500">This role does not grant any permissions.</p>
                ) : (
                  <div className="mt-2 flex flex-wrap gap-2">
                    {permissions.effective.map((permission) => (
                      <span
                        key={permission}
                        className="rounded-full border border-slate-300 bg-slate-100 px-3 py-1 text-[11px] font-medium text-slate-700"
                      >
                        {permission}
                      </span>
                    ))}
                  </div>
                )}
              </div>

              <div className="space-y-3">
                <div>
                  <h3 className="text-sm font-semibold text-slate-900">Explicit permissions</h3>
                  <p className="text-xs text-slate-500">
                    These permissions are applied specifically to this organization. Permissions inherited from default role
                    definitions remain read-only.
                  </p>
                </div>

                {editablePermissions.length === 0 ? (
                  <p className="text-xs text-slate-500">No explicit permissions configured.</p>
                ) : (
                  <div className="flex flex-wrap gap-2">
                    {editablePermissions.map((permission) => (
                      <button
                        key={permission}
                        type="button"
                        onClick={() => handleRemovePermission(permission)}
                        className="inline-flex items-center gap-1 rounded-full border border-slate-300 bg-white px-3 py-1 text-[11px] font-medium text-slate-700 hover:bg-red-50 hover:text-red-600"
                      >
                        {permission}
                        <span aria-hidden="true">×</span>
                      </button>
                    ))}
                  </div>
                )}

                <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
                  <input
                    type="text"
                    value={newPermission}
                    onChange={(event) => setNewPermission(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter') {
                        event.preventDefault()
                        handleAddPermission()
                      }
                    }}
                    placeholder="Enter permission name"
                    className="flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
                  />
                  <button
                    type="button"
                    onClick={handleAddPermission}
                    disabled={newPermission.trim() === ''}
                    className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    Add permission
                  </button>
                </div>

                <div className="flex items-center gap-3">
                  <button
                    type="button"
                    onClick={handleSave}
                    disabled={saving || !permissionsChanged}
                    className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {saving ? 'Saving…' : 'Save permissions'}
                  </button>
                  {!permissionsChanged && !saving ? (
                    <span className="text-[11px] text-slate-500">No changes to save.</span>
                  ) : null}
                </div>
              </div>
            </>
          )}
        </div>
      </div>
    </section>
  )
}
