import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useOrganizations, useOrganizationSwitcher } from '@identity-base/react-organizations'
import { getOrganizationRoles } from '../api/organizations'
import type { OrganizationRole } from '../api/types'
import { renderApiError } from '../api/client'

export default function DashboardPage() {
  const {
    memberships,
    activeOrganizationId,
    isLoadingMemberships,
    membershipError,
    organizations,
    isLoadingOrganizations: isLoadingOrganizationSummaries,
    organizationsError,
  } = useOrganizations()
  const { isSwitching, switchOrganization } = useOrganizationSwitcher()

  const [rolesLookup, setRolesLookup] = useState<Record<string, Record<string, OrganizationRole>>>({})
  const [isLoadingRoles, setIsLoadingRoles] = useState(false)
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [rolesError, setRolesError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    const loadRoles = async () => {
      if (memberships.length === 0) {
        setRolesLookup({})
        setRolesError(null)
        return
      }

      setIsLoadingRoles(true)
      setRolesError(null)

      const uniqueOrgIds = Array.from(new Set(memberships.map((membership) => membership.organizationId)))
      const nextRoles: Record<string, Record<string, OrganizationRole>> = {}

      await Promise.all(
        uniqueOrgIds.map(async (organizationId) => {
          try {
            const roles = await getOrganizationRoles(organizationId)
            nextRoles[organizationId] = roles.reduce<Record<string, OrganizationRole>>((acc, role) => {
              acc[role.id] = role
              return acc
            }, {})
          } catch (err) {
            if (!cancelled) {
              setRolesError((previous) => previous ?? renderApiError(err))
            }
          }
        }),
      )

      if (!cancelled) {
        setRolesLookup(nextRoles)
        setIsLoadingRoles(false)
      }
    }

    loadRoles().catch((err) => {
      if (!cancelled) {
        setRolesError(renderApiError(err))
        setIsLoadingRoles(false)
      }
    })

    return () => {
      cancelled = true
    }
  }, [memberships])

  const handleSetActive = async (organizationId: string) => {
    setStatusMessage(null)
    setActionError(null)

    try {
      const result = await switchOrganization(organizationId)
      setStatusMessage(result.requiresTokenRefresh
        ? result.tokensRefreshed
          ? 'Active organization updated. Refreshing session…'
          : 'Active organization updated. Completing authorization…'
        : 'Active organization updated.')
    } catch (err) {
      setActionError(renderApiError(err))
    }
  }

  const activeOrganization = activeOrganizationId ? organizations[activeOrganizationId] : undefined
  const activeOrganizationLabel = activeOrganization?.displayName ?? activeOrganization?.slug ?? (activeOrganizationId ?? 'None')
  const organizationsErrorMessage = organizationsError ? renderApiError(organizationsError) : null

  const isLoading = isLoadingMemberships || isLoadingOrganizationSummaries || isLoadingRoles

  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Organization dashboard</h1>
        <p className="text-sm text-slate-600">
          Review your organization memberships, switch the active organization (impacting tokens/claims), and jump into
          the organization management view to invite new users.
        </p>
        <p className="text-xs text-slate-500">
          Active organization:{' '}
          <span className="font-medium text-slate-800">
            {activeOrganizationLabel}
          </span>
        </p>
      </header>

      {membershipError ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Failed to load memberships. {renderApiError(membershipError)}
        </div>
      ) : null}

      {organizationsErrorMessage ? (
        <div className="rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
          Organization details may be out of date. {organizationsErrorMessage}
        </div>
      ) : null}

      {rolesError ? (
        <div className="rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
          {rolesError}
        </div>
      ) : null}

      {actionError && (
        <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          {actionError}
        </div>
      )}

      {statusMessage && (
        <div className="rounded-md border border-green-200 bg-green-50 p-4 text-sm text-green-700">
          {statusMessage}
        </div>
      )}

      {isLoading ? (
        <p className="text-sm text-slate-600">Loading memberships…</p>
      ) : memberships.length === 0 ? (
        <div className="rounded-md border border-slate-200 bg-white p-4 text-sm text-slate-600 shadow-sm">
          You are not a member of any organizations yet. Register or ask for an invitation to get started.
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {memberships.map((membership) => {
            const organization = organizations[membership.organizationId]
            const organizationName = organization?.displayName ?? organization?.slug ?? membership.organizationId
            const organizationSlug = organization?.slug ?? 'unknown'
            const organizationStatus = organization?.status ?? 'unknown'
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
                      {organizationName}
                    </h2>
                    {isActive && (
                      <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-medium text-emerald-700">
                        Active
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-slate-500">
                    Slug: <span className="font-mono">{organizationSlug}</span>
                  </p>
                  <p className="text-xs text-slate-500">Status: {organizationStatus}</p>
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
                    className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-70"
                    disabled={isActive || isSwitching}
                  >
                    {isActive ? 'Current organization' : isSwitching ? 'Switching…' : 'Set active'}
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
