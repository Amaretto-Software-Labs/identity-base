import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '@identity-base/react-client'
import { useOrganizationMembers } from '@identity-base/react-organizations'
import { useVirtualizer } from '@tanstack/react-virtual'
import { listInvitations, getOrganization, getOrganizationRoles } from '../api/organizations'
import type { InvitationResponse, OrganizationRole } from '../api/types'
import { renderApiError } from '../api/client'
import { OrganizationMemberRow } from '../components/OrganizationMemberRow'
import { OrganizationRolePermissionsPanel } from '../components/OrganizationRolePermissionsPanel'
import { OrganizationInvitationsPanel } from '../components/OrganizationInvitationsPanel'

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
  const [invitations, setInvitations] = useState<InvitationResponse[]>([])

  const [searchTerm, setSearchTerm] = useState(memberQuery.search ?? '')
  const [roleFilter, setRoleFilter] = useState(memberQuery.roleId ?? '')
  const [primaryOnly, setPrimaryOnly] = useState(memberQuery.isPrimary ?? false)
  const [pageInput, setPageInput] = useState(currentPage.toString())
  const pageSizeOptions = useMemo(() => [10, 25, 50, 100, 200], [])

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

        if (cancelled) {
          return
        }

        setOrganizationName(organization.displayName)
        setOrganizationSlug(organization.slug)
        setRoles(roleList)
        setInvitations(inviteList)
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

  const handleCreateInvitationStatus = (message: string | null) => {
    setStatusMessage(message)
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
          Manage memberships, inspect organization roles, manage invitations, and configure role permissions. Permissions and scope enforcement are handled by the sample API using <code>RequireOrganizationPermission</code>.
        </p>
        <p className="text-xs text-slate-500">
          Slug: <span className="font-mono">{organizationSlug || 'unknown'}</span>
        </p>
      </header>

      {statusMessage && (
        <div className="rounded-md border border-green-200 bg-green-50 p-3 text-sm text-green-700">{statusMessage}</div>
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
          {organizationId && (
            <OrganizationRolePermissionsPanel
              organizationId={organizationId}
              roles={roles}
              onStatusMessage={setStatusMessage}
            />
          )}

          <OrganizationInvitationsPanel
            organizationId={organizationId ?? undefined}
            roles={roles}
            invitations={invitations}
            onInvitationsChange={setInvitations}
            onStatusMessage={handleCreateInvitationStatus}
            roleNameLookup={roleNameLookup}
          />

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
        </>
      )}
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
