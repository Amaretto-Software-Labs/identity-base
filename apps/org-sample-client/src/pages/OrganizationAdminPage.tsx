import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import dayjs from 'dayjs'
import { useAuth } from '@identity-base/react-client'
import { useOrganizationMembers, type OrganizationMember } from '@identity-base/react-organizations'
import { useVirtualizer } from '@tanstack/react-virtual'
import {
  listInvitations,
  createInvitation,
  revokeInvitation,
  getOrganization,
  getOrganizationRoles,
  getOrganizationRolePermissions,
  updateOrganizationRolePermissions,
} from '../api/organizations'
import type { InvitationResponse, OrganizationRole, OrganizationRolePermissions } from '../api/types'
import { renderApiError } from '../api/client'

export default function OrganizationAdminPage() {
  const { organizationId } = useParams<'organizationId'>()
  const navigate = useNavigate()
  const { user } = useAuth()
  const currentUserId = user?.id ?? null

  const {
    members,
    isLoading: isLoadingMembers,
    error: membersError,
    updateMember,
    removeMember,
    query: memberQuery,
    setQuery: setMemberQuery,
    totalCount: memberTotalCount,
    pageCount: memberPageCount,
    page: currentPage,
    pageSize: currentPageSize,
    ensurePage,
    isPageLoaded,
    getMemberAt,
  } = useOrganizationMembers(organizationId, {
    initialQuery: { pageSize: 25, sort: 'createdAt:desc' },
  })

  const [organizationName, setOrganizationName] = useState<string>('')
  const [organizationSlug, setOrganizationSlug] = useState<string>('')
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [roles, setRoles] = useState<OrganizationRole[]>([])
  const [activeRoleId, setActiveRoleId] = useState<string | null>(null)
  const [rolePermissions, setRolePermissions] = useState<OrganizationRolePermissions | null>(null)
  const [editableRolePermissions, setEditableRolePermissions] = useState<string[]>([])
  const [rolePermissionsLoading, setRolePermissionsLoading] = useState(false)
  const [rolePermissionsSaving, setRolePermissionsSaving] = useState(false)
  const [rolePermissionsError, setRolePermissionsError] = useState<string | null>(null)
  const [newRolePermission, setNewRolePermission] = useState('')
  const [invitations, setInvitations] = useState<InvitationResponse[]>([])

  const [searchTerm, setSearchTerm] = useState(memberQuery.search ?? '')
  const [roleFilter, setRoleFilter] = useState(memberQuery.roleId ?? '')
  const [primaryOnly, setPrimaryOnly] = useState(memberQuery.isPrimary ?? false)
  const [pageInput, setPageInput] = useState(currentPage.toString())
  const pageSizeOptions = useMemo(() => [10, 25, 50, 100, 200], [])

  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteRoleIds, setInviteRoleIds] = useState<string[]>([])
  const [inviteExpiry, setInviteExpiry] = useState<number>(48)
  const [inviting, setInviting] = useState(false)
  const [inviteError, setInviteError] = useState<string | null>(null)

  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [memberActionError, setMemberActionError] = useState<string | null>(null)
  const [mutatingMemberId, setMutatingMemberId] = useState<string | null>(null)

  useEffect(() => {
    if (!organizationId) {
      navigate('/dashboard', { replace: true })
      return
    }

    let cancelled = false

    const load = async () => {
      setIsLoading(true)
      setLoadError(null)
      try {
        const [organization, roleList, inviteList] = await Promise.all([
          getOrganization(organizationId),
          getOrganizationRoles(organizationId).catch(() => []),
          listInvitations(organizationId).catch(() => []),
        ])

        if (cancelled) return

        setOrganizationName(organization.displayName)
        setOrganizationSlug(organization.slug)
        setRoles(roleList)
        setInvitations(inviteList)
        setActiveRoleId((previous) => {
          if (roleList.length === 0) {
            return null
          }

          if (previous && roleList.some((role) => role.id === previous)) {
            return previous
          }

          return roleList[0]?.id ?? null
        })
      } catch (err) {
        if (!cancelled) {
          setLoadError(renderApiError(err))
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false)
        }
      }
    }

    load()

    return () => {
      cancelled = true
    }
  }, [organizationId, navigate])

  useEffect(() => {
    setSearchTerm(memberQuery.search ?? '')
  }, [memberQuery.search])

  useEffect(() => {
    setRoleFilter(memberQuery.roleId ?? '')
  }, [memberQuery.roleId])

  useEffect(() => {
    setPrimaryOnly(memberQuery.isPrimary ?? false)
  }, [memberQuery.isPrimary])

  useEffect(() => {
    setPageInput(currentPage.toString())
  }, [currentPage])

  useEffect(() => {
    if (!organizationId || !activeRoleId) {
      setRolePermissions(null)
      setEditableRolePermissions([])
      return
    }

    let cancelled = false
    setRolePermissionsLoading(true)
    setRolePermissionsError(null)

    getOrganizationRolePermissions(organizationId, activeRoleId)
      .then((response) => {
        if (cancelled) return
        setRolePermissions(response)
        setEditableRolePermissions(response.explicit)
      })
      .catch((err) => {
        if (cancelled) return
        setRolePermissionsError(renderApiError(err))
        setRolePermissions(null)
        setEditableRolePermissions([])
      })
      .finally(() => {
        if (!cancelled) {
          setRolePermissionsLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [organizationId, activeRoleId])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      const trimmed = searchTerm.trim()
      setMemberQuery((previous) => {
        const normalized = trimmed === '' ? undefined : trimmed
        if (previous.search === normalized && previous.page === 1) {
          return previous
        }

        return {
          ...previous,
          page: 1,
          search: normalized,
        }
      })
    }, 300)

    return () => window.clearTimeout(handle)
  }, [searchTerm, setMemberQuery])

  const roleNameLookup = useMemo(() => {
    return roles.reduce<Record<string, string>>((acc, role) => {
      acc[role.id] = role.name
      return acc
    }, {})
  }, [roles])

  const handleRoleFilterChange = (value: string) => {
    setRoleFilter(value)
    setMemberQuery((previous) => ({
      ...previous,
      page: 1,
      roleId: value === '' ? undefined : value,
    }))
  }

  const handlePrimaryToggle = (value: boolean) => {
    setPrimaryOnly(value)
    setMemberQuery((previous) => ({
      ...previous,
      page: 1,
      isPrimary: value ? true : undefined,
    }))
  }

  const handlePageSizeChange = (value: number) => {
    setMemberQuery((previous) => ({
      ...previous,
      page: 1,
      pageSize: value,
    }))
  }

  const handlePagePrev = () => {
    if (currentPage <= 1) {
      return
    }

    const target = currentPage - 1
    setMemberQuery((previous) => ({ ...previous, page: target }))
    ensurePage(target).catch(() => undefined)
  }

  const handlePageNext = () => {
    if (currentPage >= memberPageCount) {
      return
    }

    const target = currentPage + 1
    setMemberQuery((previous) => ({ ...previous, page: target }))
    ensurePage(target).catch(() => undefined)
  }

  const handlePageInputChange = (value: string) => {
    setPageInput(value)
  }

  const handlePageInputCommit = () => {
    const parsed = Number.parseInt(pageInput, 10)
    if (Number.isNaN(parsed)) {
      setPageInput(currentPage.toString())
      return
    }

    const target = Math.max(1, Math.min(parsed, memberPageCount))
    setMemberQuery((previous) => ({ ...previous, page: target }))
    ensurePage(target).catch(() => undefined)
  }

  const membersShouldVirtualize = memberTotalCount > currentPageSize
  const membersScrollRef = useRef<HTMLDivElement>(null)
  const virtualizer = useVirtualizer({
    count: memberTotalCount,
    getScrollElement: () => membersScrollRef.current,
    estimateSize: () => 96,
    overscan: 8,
  })

  const virtualItems = virtualizer.getVirtualItems()

  useEffect(() => {
    if (!organizationId || memberTotalCount === 0) {
      return
    }

    ensurePage(currentPage).catch(() => undefined)
  }, [organizationId, currentPage, ensurePage, memberTotalCount])

  useEffect(() => {
    if (memberTotalCount === 0) {
      return
    }

    for (const item of virtualItems) {
      const pageIndex = Math.floor(item.index / currentPageSize) + 1
      if (!isPageLoaded(pageIndex)) {
        ensurePage(pageIndex).catch(() => undefined)
      }
    }
  }, [virtualItems, currentPageSize, ensurePage, isPageLoaded, memberTotalCount])

  useEffect(() => {
    if (memberTotalCount === 0 || !membersShouldVirtualize) {
      return
    }

    virtualizer.scrollToIndex((currentPage - 1) * currentPageSize, { align: 'start', behavior: 'smooth' })
  }, [currentPage, currentPageSize, memberTotalCount, membersShouldVirtualize, virtualizer])

  const hasMembers = memberTotalCount > 0
  const currentStart = hasMembers ? (currentPage - 1) * currentPageSize + 1 : 0
  const expectedPageCount = hasMembers ? Math.min(currentPageSize, Math.max(memberTotalCount - (currentStart - 1), 0)) : 0
  const pageMemberCount = hasMembers ? (members.length > 0 ? members.length : expectedPageCount) : 0
  const currentEnd = hasMembers ? Math.min(currentStart + pageMemberCount - 1, memberTotalCount) : 0
  const disablePrev = currentPage <= 1
  const disableNext = currentPage >= memberPageCount

  const permissionsChanged = useMemo(() => {
    if (!rolePermissions) {
      return editableRolePermissions.length > 0
    }

    const current = new Set(rolePermissions.explicit.map((value) => value.toLowerCase()))
    const next = new Set(
      editableRolePermissions
        .map((value) => value.trim())
        .filter((value) => value.length > 0)
        .map((value) => value.toLowerCase()),
    )

    if (current.size !== next.size) {
      return true
    }

    for (const value of next) {
      if (!current.has(value)) {
        return true
      }
    }

    return false
  }, [editableRolePermissions, rolePermissions])

  const handleAddRolePermission = () => {
    const trimmed = newRolePermission.trim()
    if (trimmed === '') {
      return
    }

    setRolePermissionsError(null)

    setEditableRolePermissions((previous) => {
      if (previous.some((value) => value.toLowerCase() === trimmed.toLowerCase())) {
        return previous
      }

      return [...previous, trimmed]
    })
    setNewRolePermission('')
  }

  const handleRemoveRolePermission = (permission: string) => {
    setRolePermissionsError(null)
    setEditableRolePermissions((previous) => previous.filter((value) => value.toLowerCase() !== permission.toLowerCase()))
  }

  const handleSaveRolePermissions = async () => {
    if (!organizationId || !activeRoleId) {
      return
    }

    const payload = editableRolePermissions
      .map((value) => value.trim())
      .filter((value, index, array) => value.length > 0 && array.findIndex((candidate) => candidate.toLowerCase() === value.toLowerCase()) === index)

    setRolePermissionsSaving(true)
    setRolePermissionsError(null)
    setStatusMessage(null)

    try {
      await updateOrganizationRolePermissions(organizationId, activeRoleId, payload)
      setStatusMessage('Role permissions updated.')
      const updated = await getOrganizationRolePermissions(organizationId, activeRoleId)
      setRolePermissions(updated)
      setEditableRolePermissions(updated.explicit)
    } catch (err) {
      setRolePermissionsError(renderApiError(err))
    } finally {
      setRolePermissionsSaving(false)
    }
  }

  const handleCreateInvitation = async () => {
    if (!organizationId) return
    setInviteError(null)
    setStatusMessage(null)

    try {
      setInviting(true)
      const response = await createInvitation(organizationId, {
        email: inviteEmail.trim(),
        roleIds: inviteRoleIds,
        expiresInHours: inviteExpiry,
      })

      setInvitations((previous) => [response, ...previous])
      setInviteEmail('')
      setInviteRoleIds([])
      setInviteExpiry(48)
      setStatusMessage(`Invitation created for ${response.email}.`)
    } catch (err) {
      setInviteError(renderApiError(err))
    } finally {
      setInviting(false)
    }
  }

  const handleRevoke = async (code: string) => {
    if (!organizationId) return
    setStatusMessage(null)
    setInviteError(null)

    try {
      await revokeInvitation(organizationId, code)
      setInvitations((previous) => previous.filter((invitation) => invitation.code !== code))
      setStatusMessage('Invitation revoked.')
    } catch (err) {
      setInviteError(renderApiError(err))
    }
  }

  const handleUpdateMemberRoles = async (memberId: string, roleIds: string[]) => {
    setMemberActionError(null)
    setStatusMessage(null)
    setMutatingMemberId(memberId)

    if (roleIds.length === 0) {
      setMemberActionError('Members must have at least one role assigned.')
      setMutatingMemberId(null)
      return
    }

    try {
      await updateMember(memberId, { roleIds })
      setStatusMessage('Member roles updated.')
    } catch (err) {
      setMemberActionError(renderApiError(err))
    } finally {
      setMutatingMemberId(null)
    }
  }

  const handleRemoveMember = async (memberId: string) => {
    if (currentUserId === memberId) {
      setMemberActionError('You cannot remove your own membership.')
      return
    }

    if (!window.confirm('Remove this member from the organization?')) {
      return
    }

    setMemberActionError(null)
    setStatusMessage(null)
    setMutatingMemberId(memberId)

    try {
      await removeMember(memberId)
      setStatusMessage('Member removed from organization.')
    } catch (err) {
      setMemberActionError(renderApiError(err))
    } finally {
      setMutatingMemberId(null)
    }
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <p className="text-xs text-slate-500">
          <Link to="/dashboard" className="text-slate-900 underline">
            Dashboard
          </Link>{' '}
          / Organization
        </p>
        <h1 className="text-2xl font-semibold text-slate-900">{organizationName || 'Organization'}</h1>
        <p className="text-sm text-slate-600">
          Manage memberships, inspect organization roles, and create invitation codes. Permissions and scope enforcement are
          handled by the sample API using <code>RequireOrganizationPermission</code>.
        </p>
        <p className="text-xs text-slate-500">
          Slug: <span className="font-mono">{organizationSlug || 'unknown'}</span>
        </p>
      </header>

      {statusMessage && (
        <div className="rounded-md border border-green-200 bg-green-50 p-3 text-sm text-green-700">{statusMessage}</div>
      )}
      {inviteError && (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">{inviteError}</div>
      )}
      {memberActionError && (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">{memberActionError}</div>
      )}
      {loadError && (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          Failed to load organization. {loadError}
        </div>
      )}
      {membersError ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          Failed to load members. {renderApiError(membersError)}
        </div>
      ) : null}

      {isLoading ? (
        <p className="text-sm text-slate-600">Loading organization details…</p>
      ) : (
        <>
          <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold text-slate-900">Roles</h2>
              {roles.length > 0 ? (
                <span className="text-xs font-medium uppercase tracking-wide text-slate-500">
                  {roles.length.toLocaleString()} total
                </span>
              ) : null}
            </div>

            {rolePermissionsError && (
              <div className="rounded-md border border-red-200 bg-red-50 p-3 text-xs text-red-700">
                {rolePermissionsError}
              </div>
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
                ) : rolePermissionsLoading ? (
                  <p className="text-sm text-slate-600">Loading permissions…</p>
                ) : !rolePermissions ? (
                  <p className="text-sm text-slate-600">Unable to load permissions for the selected role.</p>
                ) : (
                  <>
                    <div>
                      <h3 className="text-sm font-semibold text-slate-900">Effective permissions</h3>
                      {rolePermissions.effective.length === 0 ? (
                        <p className="text-xs text-slate-500">This role does not grant any permissions.</p>
                      ) : (
                        <div className="mt-2 flex flex-wrap gap-2">
                          {rolePermissions.effective.map((permission) => (
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

                      {editableRolePermissions.length === 0 ? (
                        <p className="text-xs text-slate-500">No explicit permissions configured.</p>
                      ) : (
                        <div className="flex flex-wrap gap-2">
                          {editableRolePermissions.map((permission) => (
                            <button
                              key={permission}
                              type="button"
                              onClick={() => handleRemoveRolePermission(permission)}
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
                          value={newRolePermission}
                          onChange={(event) => setNewRolePermission(event.target.value)}
                          onKeyDown={(event) => {
                            if (event.key === 'Enter') {
                              event.preventDefault()
                              handleAddRolePermission()
                            }
                          }}
                          placeholder="Enter permission name"
                          className="flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
                        />
                        <button
                          type="button"
                          onClick={handleAddRolePermission}
                          disabled={newRolePermission.trim() === ''}
                          className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                          Add permission
                        </button>
                      </div>

                      <div className="flex items-center gap-3">
                        <button
                          type="button"
                          onClick={handleSaveRolePermissions}
                          disabled={rolePermissionsSaving || !permissionsChanged}
                          className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                          {rolePermissionsSaving ? 'Saving…' : 'Save permissions'}
                        </button>
                        {!permissionsChanged && !rolePermissionsSaving ? (
                          <span className="text-[11px] text-slate-500">No changes to save.</span>
                        ) : null}
                      </div>
                    </div>
                  </>
                )}
              </div>
            </div>
          </section>

          <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold text-slate-900">Members</h2>
              {hasMembers ? (
                <span className="text-xs font-medium uppercase tracking-wide text-slate-500">
                  {memberTotalCount.toLocaleString()} total
                </span>
              ) : null}
            </div>

            <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
              <div className="flex flex-col gap-3 md:flex-row md:items-end md:gap-4">
                <div className="space-y-1">
                  <label htmlFor="member-search" className="block text-xs font-semibold uppercase tracking-wide text-slate-600">
                    Search members
                  </label>
                  <input
                    id="member-search"
                    type="search"
                    value={searchTerm}
                    onChange={(event) => setSearchTerm(event.target.value)}
                    placeholder="Name or email"
                    className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
                  />
                </div>
                <div className="space-y-1">
                  <label htmlFor="member-role-filter" className="block text-xs font-semibold uppercase tracking-wide text-slate-600">
                    Role filter
                  </label>
                  <select
                    id="member-role-filter"
                    value={roleFilter}
                    onChange={(event) => handleRoleFilterChange(event.target.value)}
                    className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
                  >
                    <option value="">All roles</option>
                    {roles.map((role) => (
                      <option key={role.id} value={role.id}>
                        {role.name}
                      </option>
                    ))}
                  </select>
                </div>
                <label className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-slate-600">
                  <input
                    type="checkbox"
                    checked={primaryOnly}
                    onChange={(event) => handlePrimaryToggle(event.target.checked)}
                    className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-500"
                  />
                  Primary only
                </label>
              </div>
              <div className="flex items-end gap-3">
                <div className="space-y-1">
                  <label htmlFor="member-page-size" className="block text-xs font-semibold uppercase tracking-wide text-slate-600">
                    Page size
                  </label>
                  <select
                    id="member-page-size"
                    value={currentPageSize}
                    onChange={(event) => handlePageSizeChange(Number.parseInt(event.target.value, 10))}
                    className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
                  >
                    {pageSizeOptions.map((size) => (
                      <option key={size} value={size}>
                        {size}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            </div>

            {isLoadingMembers && !hasMembers ? (
              <p className="text-sm text-slate-600">Loading members…</p>
            ) : !hasMembers ? (
              <p className="text-sm text-slate-600">No members found. Send an invitation to add teammates.</p>
            ) : (
              <>
                <div className="rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-xs font-semibold uppercase tracking-wide text-slate-600">
                  <div className="grid grid-cols-[2fr,2fr,0.8fr,1fr,1fr] gap-3">
                    <span>Member</span>
                    <span>Roles</span>
                    <span>Primary</span>
                    <span>Joined</span>
                    <span />
                  </div>
                </div>

                {membersShouldVirtualize ? (
                  <div ref={membersScrollRef} className="max-h-[480px] overflow-auto rounded-md border border-slate-200 bg-white">
                    <div className="relative" style={{ height: virtualizer.getTotalSize() }}>
                      {virtualItems.map((virtualRow) => {
                        const member = getMemberAt(virtualRow.index)
                        return (
                          <div
                            key={virtualRow.key}
                            className="absolute left-0 right-0 border-b border-slate-100"
                            style={{ transform: `translateY(${virtualRow.start}px)` }}
                          >
                            {member ? (
                              <OrganizationMemberRow
                                member={member}
                                availableRoles={roles}
                                roleNameLookup={roleNameLookup}
                                onUpdateRoles={(roleIds) => handleUpdateMemberRoles(member.userId, roleIds)}
                                onRemove={() => handleRemoveMember(member.userId)}
                                isCurrentUser={currentUserId === member.userId}
                                isBusy={mutatingMemberId === member.userId}
                              />
                            ) : (
                              <OrganizationMemberPlaceholderRow />
                            )}
                          </div>
                        )
                      })}
                    </div>
                  </div>
                ) : (
                  <div className="divide-y divide-slate-100 rounded-md border border-slate-200 bg-white">
                    {members.map((member) => (
                      <OrganizationMemberRow
                        key={member.userId}
                        member={member}
                        availableRoles={roles}
                        roleNameLookup={roleNameLookup}
                        onUpdateRoles={(roleIds) => handleUpdateMemberRoles(member.userId, roleIds)}
                        onRemove={() => handleRemoveMember(member.userId)}
                        isCurrentUser={currentUserId === member.userId}
                        isBusy={mutatingMemberId === member.userId}
                      />
                    ))}
                  </div>
                )}

                {isLoadingMembers && hasMembers && (
                  <p className="text-xs text-slate-500">Loading members…</p>
                )}

                <div className="flex flex-col gap-3 border-t border-slate-200 pt-3 text-xs text-slate-600 md:flex-row md:items-center md:justify-between">
                  <p className="font-medium">
                    Viewing {currentStart.toLocaleString()}-{currentEnd.toLocaleString()} of {memberTotalCount.toLocaleString()} members
                  </p>
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={handlePagePrev}
                      disabled={disablePrev}
                      className="rounded-md border border-slate-300 px-3 py-1 font-medium text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      Previous
                    </button>
                    <div className="flex items-center gap-2">
                      <span>Page</span>
                      <input
                        type="number"
                        min={1}
                        max={memberPageCount}
                        value={pageInput}
                        onChange={(event) => handlePageInputChange(event.target.value)}
                        onBlur={handlePageInputCommit}
                        onKeyDown={(event) => event.key === 'Enter' && handlePageInputCommit()}
                        className="w-16 rounded-md border border-slate-300 px-2 py-1 text-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
                      />
                      <span>of {memberPageCount}</span>
                    </div>
                    <button
                      type="button"
                      onClick={handlePageNext}
                      disabled={disableNext}
                      className="rounded-md border border-slate-300 px-3 py-1 font-medium text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      Next
                    </button>
                  </div>
                </div>
              </>
            )}
          </section>

          <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">Create invitation</h2>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1">
                <label htmlFor="invite-email" className="block text-sm font-medium text-slate-700">
                  Email
                </label>
                <input
                  id="invite-email"
                  type="email"
                  value={inviteEmail}
                  onChange={(event) => setInviteEmail(event.target.value)}
                  className="w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
                />
              </div>
              <div className="space-y-1">
                <label htmlFor="invite-expiry" className="block text-sm font-medium text-slate-700">
                  Expires in (hours)
                </label>
                <input
                  id="invite-expiry"
                  type="number"
                  min={1}
                  max={720}
                  value={inviteExpiry}
                  onChange={(event) => setInviteExpiry(Number.parseInt(event.target.value, 10) || 1)}
                  className="w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
                />
              </div>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Assign roles</label>
              <p className="mt-1 text-xs text-slate-500">
                Optional. Select any organization roles to attach when the invite is claimed.
              </p>
              <div className="mt-2 flex flex-wrap gap-2">
                {roles.length === 0 ? (
                  <span className="text-xs text-slate-500">No custom organization roles available.</span>
                ) : (
                  roles.map((role) => {
                    const isSelected = inviteRoleIds.includes(role.id)
                    return (
                      <button
                        key={role.id}
                        type="button"
                        onClick={() =>
                          setInviteRoleIds((previous) =>
                            isSelected ? previous.filter((id) => id !== role.id) : [...previous, role.id],
                          )
                        }
                        className={`rounded-full border px-3 py-1 text-xs font-medium transition ${isSelected ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-300 text-slate-700 hover:bg-slate-100'}`}
                      >
                        {role.name}
                      </button>
                    )
                  })
                )}
              </div>
            </div>
            <button
              type="button"
              onClick={handleCreateInvitation}
              disabled={inviting || !inviteEmail}
              className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-70"
            >
              {inviting ? 'Creating…' : 'Create invitation'}
            </button>
          </section>

          <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">Pending invitations</h2>
            {invitations.length === 0 ? (
              <p className="text-sm text-slate-600">No pending invitations.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-slate-200 text-sm">
                  <thead className="bg-slate-50">
                    <tr>
                      <th className="px-3 py-2 text-left font-medium text-slate-700">Code</th>
                      <th className="px-3 py-2 text-left font-medium text-slate-700">Email</th>
                      <th className="px-3 py-2 text-left font-medium text-slate-700">Roles</th>
                      <th className="px-3 py-2 text-left font-medium text-slate-700">Expires</th>
                      <th className="px-3 py-2" />
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {invitations.map((invitation) => (
                      <tr key={invitation.code}>
                        <td className="whitespace-nowrap px-3 py-2 text-xs font-mono text-slate-600">{invitation.code}</td>
                        <td className="whitespace-nowrap px-3 py-2 text-xs text-slate-700">{invitation.email}</td>
                        <td className="px-3 py-2 text-xs text-slate-600">
                          {invitation.roleIds.length === 0
                            ? 'None'
                            : invitation.roleIds
                                .map((roleId) => roleNameLookup[roleId] ?? roleId)
                                .join(', ')}
                        </td>
                        <td className="whitespace-nowrap px-3 py-2 text-xs text-slate-600">
                          {dayjs(invitation.expiresAtUtc).format('YYYY-MM-DD HH:mm')}
                        </td>
                        <td className="whitespace-nowrap px-3 py-2">
                          <button
                            type="button"
                            onClick={() => handleRevoke(invitation.code)}
                            className="rounded-md border border-red-200 px-2 py-1 text-xs font-medium text-red-600 hover:bg-red-50"
                          >
                            Revoke
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}
    </div>
  )
}

interface OrganizationMemberRowProps {
  member: OrganizationMember
  availableRoles: OrganizationRole[]
  roleNameLookup: Record<string, string>
  onUpdateRoles: (roleIds: string[]) => Promise<void>
  onRemove: () => Promise<void>
  isCurrentUser: boolean
  isBusy: boolean
  className?: string
}

function OrganizationMemberRow({
  member,
  availableRoles,
  roleNameLookup,
  onUpdateRoles,
  onRemove,
  isCurrentUser,
  isBusy,
  className,
}: OrganizationMemberRowProps) {
  const [selectedRoles, setSelectedRoles] = useState<string[]>(member.roleIds)

  useEffect(() => {
    setSelectedRoles(member.roleIds)
  }, [member.roleIds])

  const toggleRole = (roleId: string) => {
    setSelectedRoles((previous) =>
      previous.includes(roleId) ? previous.filter((id) => id !== roleId) : [...previous, roleId],
    )
  }

  const isDirty = useMemo(() => {
    if (selectedRoles.length !== member.roleIds.length) {
      return true
    }

    const current = new Set(member.roleIds)
    return selectedRoles.some((roleId) => !current.has(roleId))
  }, [selectedRoles, member.roleIds])

  const readonlyRoles = member.roleIds.filter(
    (roleId) => !availableRoles.some((role) => role.id === roleId),
  )

  const handleSave = async () => {
    await onUpdateRoles(selectedRoles)
  }

  const baseClass = 'grid grid-cols-[2fr,2fr,0.8fr,1fr,1fr] gap-3 px-3 py-3 text-sm'
  const containerClass = className ? `${baseClass} ${className}` : baseClass

  return (
    <div className={containerClass}>
      <div className="space-y-1">
        <span className="font-medium text-slate-800">
          {member.displayName ?? member.email ?? 'Organization member'}
        </span>
        {member.email && <span className="block text-xs text-slate-500">{member.email}</span>}
        <span className="block text-[11px] font-mono text-slate-400">{member.userId}</span>
      </div>
      <div className="flex flex-col gap-1">
        <div className="flex flex-wrap gap-2">
          {availableRoles.length === 0 ? (
            <span className="text-xs text-slate-500">No custom organization roles available.</span>
          ) : (
            availableRoles.map((role) => {
              const isSelected = selectedRoles.includes(role.id)
              return (
                <button
                  key={role.id}
                  type="button"
                  onClick={() => (isCurrentUser || isBusy ? undefined : toggleRole(role.id))}
                  disabled={isCurrentUser || isBusy}
                  className={`rounded-full border px-3 py-1 text-xs font-medium transition ${
                    isSelected ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-300 text-slate-700 hover:bg-slate-100'
                  } ${isCurrentUser || isBusy ? 'cursor-not-allowed opacity-60' : ''}`}
                >
                  {role.name}
                </button>
              )
            })
          )}
        </div>
        {readonlyRoles.length > 0 && (
          <p className="text-[11px] text-slate-500">
            Fixed roles:{' '}
            {readonlyRoles
              .map((roleId) => roleNameLookup[roleId] ?? roleId)
              .join(', ')}
          </p>
        )}
      </div>
      <div className="text-xs text-slate-600">{member.isPrimary ? 'Yes' : 'No'}</div>
      <div className="text-xs text-slate-600">{dayjs(member.createdAtUtc).format('YYYY-MM-DD HH:mm')}</div>
      <div className="flex flex-col gap-2">
        <button
          type="button"
          onClick={handleSave}
          disabled={!isDirty || isBusy || isCurrentUser || selectedRoles.length === 0}
          className="rounded-md border border-slate-300 px-3 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isBusy ? 'Saving…' : 'Save changes'}
        </button>
        <button
          type="button"
          onClick={() => (isBusy || isCurrentUser ? undefined : onRemove())}
          disabled={isBusy || isCurrentUser}
          className="rounded-md border border-red-200 px-3 py-1 text-xs font-medium text-red-600 hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-60"
        >
          Remove
        </button>
        {isCurrentUser && (
          <p className="text-[11px] text-slate-500">You cannot modify or remove your own membership.</p>
        )}
      </div>
    </div>
  )
}

function OrganizationMemberPlaceholderRow() {
  return (
    <div className="grid grid-cols-[2fr,2fr,0.8fr,1fr,1fr] gap-3 px-3 py-3 text-sm">
      <div className="h-4 w-3/4 animate-pulse rounded bg-slate-200" />
      <div className="flex flex-col gap-2">
        <div className="h-4 w-full max-w-[180px] animate-pulse rounded bg-slate-200" />
        <div className="h-4 w-24 animate-pulse rounded bg-slate-200" />
      </div>
      <div className="h-4 w-10 animate-pulse rounded bg-slate-200" />
      <div className="h-4 w-20 animate-pulse rounded bg-slate-200" />
      <div className="flex flex-col gap-2">
        <div className="h-7 w-20 animate-pulse rounded bg-slate-200" />
        <div className="h-7 w-16 animate-pulse rounded bg-slate-200" />
      </div>
    </div>
  )
}
