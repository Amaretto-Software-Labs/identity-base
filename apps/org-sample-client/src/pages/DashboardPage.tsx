import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '@identity-base/react-client'
import { useMemberships } from '../hooks/useMemberships'
import {
  getOrganization,
  getOrganizationRoles,
  setActiveOrganization,
} from '../api/organizations'
import type { OrganizationRole } from '../api/types'
import { renderApiError } from '../api/client'

interface OrganizationSummary {
  id: string
  slug: string
  displayName: string
  status: string
  metadata: Record<string, string | null>
}

export default function DashboardPage() {
  const { refreshUser } = useAuth()
  const { memberships, isLoading: isLoadingMemberships, error: membershipError, reload } = useMemberships()
  const [organizations, setOrganizations] = useState<Record<string, OrganizationSummary>>({})
  const [rolesLookup, setRolesLookup] = useState<Record<string, Record<string, OrganizationRole>>>({})
  const [loadingOrganizations, setLoadingOrganizations] = useState(false)
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [activeOrganizationId, setActiveOrganizationId] = useState<string | null>(null)

  useEffect(() => {
    if (activeOrganizationId === null && memberships.length === 1) {
      setActiveOrganizationId(memberships[0].organizationId)
    }
  }, [memberships, activeOrganizationId])

  useEffect(() => {
    let cancelled = false

    const load = async () => {
      if (memberships.length === 0) {
        setOrganizations({})
        setRolesLookup({})
        return
      }

      setLoadingOrganizations(true)
      setActionError(null)

      const uniqueOrgIds = Array.from(new Set(memberships.map((membership) => membership.organizationId)))
      const nextOrganizations: Record<string, OrganizationSummary> = {}
      const nextRoles: Record<string, Record<string, OrganizationRole>> = {}

      await Promise.all(
        uniqueOrgIds.map(async (organizationId) => {
          try {
            const organization = await getOrganization(organizationId)
            nextOrganizations[organizationId] = {
              id: organization.id,
              slug: organization.slug,
              displayName: organization.displayName,
              status: organization.status,
              metadata: organization.metadata ?? {},
            }

            try {
              const roles = await getOrganizationRoles(organizationId)
              nextRoles[organizationId] = roles.reduce<Record<string, OrganizationRole>>((acc, role) => {
                acc[role.id] = role
                return acc
              }, {})
            } catch {
              nextRoles[organizationId] = {}
            }
          } catch (err) {
            if (!cancelled) {
              setActionError(renderApiError(err))
            }
          }
        }),
      )

      if (!cancelled) {
        setOrganizations(nextOrganizations)
        setRolesLookup(nextRoles)
        setLoadingOrganizations(false)
      }
    }

    load()
    return () => {
      cancelled = true
    }
  }, [memberships])

  const handleSetActive = async (organizationId: string) => {
    setStatusMessage(null)
    setActionError(null)

    try {
      const response = await setActiveOrganization({ organizationId })
      if (response.requiresTokenRefresh) {
        await refreshUser()
        setStatusMessage('Active organization updated. Claims refreshed.')
      } else {
        setStatusMessage('Active organization updated.')
      }
      setActiveOrganizationId(organizationId)
    } catch (err) {
      setActionError(renderApiError(err))
    } finally {
      await reload()
    }
  }

  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Organization dashboard</h1>
        <p className="text-sm text-slate-600">
          Review your organization memberships, switch the active organization (impacting tokens/claims), and jump into
          the organization management view to invite new users.
        </p>
      </header>

      {membershipError ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Failed to load memberships. {renderApiError(membershipError)}
        </div>
      ) : null}

      {actionError && (
        <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">{actionError}</div>
      )}

      {statusMessage && (
        <div className="rounded-md border border-green-200 bg-green-50 p-4 text-sm text-green-700">{statusMessage}</div>
      )}

      {isLoadingMemberships || loadingOrganizations ? (
        <p className="text-sm text-slate-600">Loading memberships…</p>
      ) : memberships.length === 0 ? (
        <div className="rounded-md border border-slate-200 bg-white p-4 text-sm text-slate-600 shadow-sm">
          You are not a member of any organizations yet. Register or ask for an invitation to get started.
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {memberships.map((membership) => {
            const organization = organizations[membership.organizationId]
            const roles = rolesLookup[membership.organizationId] ?? {}
            const isActive = activeOrganizationId === membership.organizationId

            return (
              <div
                key={`${membership.organizationId}-${membership.userId}`}
                className="flex h-full flex-col gap-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm"
              >
                <div className="space-y-1">
                  <div className="flex items-center justify-between">
                    <h2 className="text-lg font-semibold text-slate-900">
                      {organization?.displayName ?? membership.organizationId}
                    </h2>
                    {isActive && (
                      <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-medium text-emerald-700">
                        Active
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-slate-500">
                    Slug: <span className="font-mono">{organization?.slug ?? 'unknown'}</span>
                  </p>
                  <p className="text-xs text-slate-500">Status: {organization?.status ?? 'unknown'}</p>
                </div>

                <div>
                  <h3 className="text-sm font-semibold text-slate-800">Role assignments</h3>
                  {membership.roleIds.length === 0 ? (
                    <p className="text-xs text-slate-500">No organization roles assigned.</p>
                  ) : (
                    <ul className="mt-1 space-y-1 text-xs text-slate-600">
                      {membership.roleIds.map((roleId) => (
                        <li key={roleId} className="rounded bg-slate-100 px-2 py-1 font-mono">
                          {roles[roleId]?.name ?? roleId}
                        </li>
                      ))}
                    </ul>
                  )}
                </div>

                <div className="mt-auto flex flex-wrap gap-2">
                  <button
                    type="button"
                    onClick={() => handleSetActive(membership.organizationId)}
                    className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
                    disabled={isActive}
                  >
                    {isActive ? 'Current organization' : 'Set active'}
                  </button>
                  <Link
                    to={`/organizations/${membership.organizationId}`}
                    className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800"
                  >
                    Manage organization
                  </Link>
                </div>
              </div>
            )
          })}
        </div>
      )}

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-800">Need to join another organization?</h2>
        <p className="mt-1 text-xs text-slate-600">
          Ask an organization admin to generate an invitation code and redeem it on the{' '}
          <Link to="/invitations/claim" className="text-slate-900 underline">
            claim invitation
          </Link>{' '}
          page.
        </p>
      </section>
    </div>
  )
}
