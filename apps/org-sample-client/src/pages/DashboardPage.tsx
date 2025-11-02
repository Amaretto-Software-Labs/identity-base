import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useOrganisations, useOrganisationSwitcher } from '@identity-base/react-organisations'
import { getOrganisationRoles } from '../api/organisations'
import type { OrganisationRole } from '../api/types'
import { renderApiError } from '../api/client'

export default function DashboardPage() {
  const {
    memberships,
    activeOrganisationId,
    isLoadingMemberships,
    membershipError,
    organisations,
    isLoadingOrganisations: isLoadingOrganisationSummaries,
    organisationsError,
  } = useOrganisations()
  const { isSwitching, switchOrganisation } = useOrganisationSwitcher()

  const [rolesLookup, setRolesLookup] = useState<Record<string, Record<string, OrganisationRole>>>({})
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

      const uniqueOrgIds = Array.from(new Set(memberships.map((membership) => membership.organisationId)))
      const nextRoles: Record<string, Record<string, OrganisationRole>> = {}

      await Promise.all(
        uniqueOrgIds.map(async (organisationId) => {
          try {
            const roles = await getOrganisationRoles(organisationId)
            nextRoles[organisationId] = roles.reduce<Record<string, OrganisationRole>>((acc, role) => {
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

  const handleSetActive = async (organisationId: string) => {
    setStatusMessage(null)
    setActionError(null)

    try {
      const result = await switchOrganisation(organisationId)
      setStatusMessage(result.requiresTokenRefresh
        ? result.tokensRefreshed
          ? 'Active organisation updated. Refreshing session…'
          : 'Active organisation updated. Completing authorization…'
        : 'Active organisation updated.')
    } catch (err) {
      setActionError(renderApiError(err))
    }
  }

  const activeOrganisation = activeOrganisationId ? organisations[activeOrganisationId] : undefined
  const activeOrganisationLabel = activeOrganisation?.displayName ?? activeOrganisation?.slug ?? (activeOrganisationId ?? 'None')
  const organisationsErrorMessage = organisationsError ? renderApiError(organisationsError) : null

  const isLoading = isLoadingMemberships || isLoadingOrganisationSummaries || isLoadingRoles

  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Organisation dashboard</h1>
        <p className="text-sm text-slate-600">
          Review your organisation memberships, switch the active organisation (impacting tokens/claims), and jump into
          the organisation management view to invite new users.
        </p>
        <p className="text-xs text-slate-500">
          Active organisation:{' '}
          <span className="font-medium text-slate-800">
            {activeOrganisationLabel}
          </span>
        </p>
      </header>

      {membershipError ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Failed to load memberships. {renderApiError(membershipError)}
        </div>
      ) : null}

      {organisationsErrorMessage ? (
        <div className="rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
          Organisation details may be out of date. {organisationsErrorMessage}
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
          You are not a member of any organisations yet. Register or ask for an invitation to get started.
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {memberships.map((membership) => {
            const organisation = organisations[membership.organisationId]
            const organisationName = organisation?.displayName ?? organisation?.slug ?? membership.organisationId
            const organisationSlug = organisation?.slug ?? 'unknown'
            const organisationStatus = organisation?.status ?? 'unknown'
            const roles = rolesLookup[membership.organisationId] ?? {}
            const isActive = activeOrganisationId === membership.organisationId

            return (
              <div
                key={`${membership.organisationId}-${membership.userId}`}
                className="flex h-full flex-col gap-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm"
              >
                <div className="space-y-1">
                  <div className="flex items-center justify-between">
                    <h2 className="text-lg font-semibold text-slate-900">
                      {organisationName}
                    </h2>
                    {isActive && (
                      <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-medium text-emerald-700">
                        Active
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-slate-500">
                    Slug: <span className="font-mono">{organisationSlug}</span>
                  </p>
                  <p className="text-xs text-slate-500">Status: {organisationStatus}</p>
                </div>

                <div>
                  <h3 className="text-sm font-semibold text-slate-800">Role assignments</h3>
                  {membership.roleIds.length === 0 ? (
                    <p className="text-xs text-slate-500">No organisation roles assigned.</p>
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
                    onClick={() => handleSetActive(membership.organisationId)}
                    className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-70"
                    disabled={isActive || isSwitching}
                  >
                    {isActive ? 'Current organisation' : isSwitching ? 'Switching…' : 'Set active'}
                  </button>
                  <Link
                    to={`/organisations/${membership.organisationId}`}
                    className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800"
                  >
                    Manage organisation
                  </Link>
                </div>
              </div>
            )
          })}
        </div>
      )}

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-800">Need to join another organisation?</h2>
        <p className="mt-1 text-xs text-slate-600">
          Ask an organisation admin to generate an invitation code and redeem it on the{' '}
          <Link to="/invitations/claim" className="text-slate-900 underline">
            claim invitation
          </Link>{' '}
          page.
        </p>
      </section>
    </div>
  )
}
