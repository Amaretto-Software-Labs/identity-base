import { useEffect, useState, type ChangeEvent } from 'react'
import { Link, NavLink, Outlet } from 'react-router-dom'
import { useAuth, useIdentityContext, useLogin } from '@identity-base/react-client'
import { useOrganizations, useOrganizationSwitcher } from '@identity-base/react-organizations'
import { renderApiError } from '../api/client'

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `rounded-md px-3 py-2 text-sm font-medium ${isActive ? 'bg-slate-900 text-white' : 'text-slate-100 hover:bg-slate-800 hover:text-white'}`

export default function AppLayout() {
  const { user, isAuthenticated } = useAuth()
  const { authManager } = useIdentityContext()
  const { logout } = useLogin()
  const {
    memberships,
    activeOrganizationId,
    isLoadingMemberships,
    isLoadingOrganizations,
    organizations,
    organizationsError,
  } = useOrganizations()
  const { isSwitching, error: switchError, switchOrganization } = useOrganizationSwitcher()

  const [selectedOrganizationId, setSelectedOrganizationId] = useState<string>('')
  const [switchStatus, setSwitchStatus] = useState<string | null>(null)

  useEffect(() => {
    if (!switchStatus) {
      return
    }

    if (typeof window === 'undefined') {
      return
    }

    const timeoutId = window.setTimeout(() => {
      setSwitchStatus(null)
    }, 4000)

    return () => window.clearTimeout(timeoutId)
  }, [switchStatus])

  useEffect(() => {
    if (activeOrganizationId) {
      setSelectedOrganizationId(activeOrganizationId)
    } else if (memberships.length > 0) {
      setSelectedOrganizationId(memberships[0].organizationId)
    } else {
      setSelectedOrganizationId('')
    }
  }, [activeOrganizationId, memberships])

  const handleOrganizationChange = async (event: ChangeEvent<HTMLSelectElement>) => {
    const nextOrganizationId = event.target.value
    setSelectedOrganizationId(nextOrganizationId)
    setSwitchStatus(null)

    if (!nextOrganizationId || nextOrganizationId === activeOrganizationId) {
      return
    }

    try {
      const result = await switchOrganization(nextOrganizationId)

      if (result.requiresTokenRefresh) {
        if (result.tokensRefreshed) {
          setSwitchStatus('Active organization updated. Refreshing session…')
        } else {
          setSwitchStatus('Active organization updated. Completing authorization…')
          if (authManager) {
            authManager.startAuthorization()
          }
        }
      } else {
        setSwitchStatus('Active organization updated.')
      }
    } catch (err) {
      setSelectedOrganizationId(activeOrganizationId ?? '')
      setSwitchStatus(null)
    }
  }

  const organizationOptions = memberships.map((membership) => {
    const summary = organizations[membership.organizationId]
    const label = summary?.displayName ?? summary?.slug ?? membership.organizationId
    return {
      id: membership.organizationId,
      label,
    }
  })

  const activeOrganization = activeOrganizationId ? organizations[activeOrganizationId] : undefined
  const activeOrganizationLabel = activeOrganization?.displayName ?? activeOrganization?.slug ?? (activeOrganizationId ?? 'None')

  const organizationErrorMessage = organizationsError ? renderApiError(organizationsError) : null
  const switchErrorMessage = switchError ? renderApiError(switchError) : null
  const organizationSelectorDisabled = isSwitching || isLoadingMemberships || isLoadingOrganizations

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-slate-900 text-white">
        <div className="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-4 sm:flex-row sm:items-center sm:gap-6">
          <div className="flex items-center justify-between gap-4 sm:w-48">
            <Link to="/" className="text-lg font-semibold">
              Org Sample Client
            </Link>
            {isAuthenticated && (
              <button
                type="button"
                onClick={logout}
                className="rounded-md border border-slate-600 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800 sm:hidden"
              >
                Sign out
              </button>
            )}
          </div>
          <div className="flex flex-1 flex-col gap-3">
            <nav className="flex flex-wrap items-center gap-2">
              <NavLink to="/" className={navLinkClass} end>
                Overview
              </NavLink>
              <NavLink to="/register" className={navLinkClass}>
                Register
              </NavLink>
              <NavLink to="/login" className={navLinkClass}>
                Login
              </NavLink>
              <NavLink to="/dashboard" className={navLinkClass}>
                Dashboard
              </NavLink>
              <NavLink to="/invitations/claim" className={navLinkClass}>
                Claim Invite
              </NavLink>
            </nav>
            {isAuthenticated && organizationOptions.length > 0 && (
              <div className="flex flex-col gap-1 text-xs text-slate-200 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex flex-wrap items-center gap-2">
                  <label htmlFor="active-organization" className="text-xs uppercase tracking-wide text-slate-300">
                    Active org
                  </label>
                  <select
                    id="active-organization"
                    value={selectedOrganizationId}
                    onChange={handleOrganizationChange}
                    disabled={organizationSelectorDisabled}
                    className="rounded-md border border-slate-600 bg-slate-900 px-2 py-1 text-xs font-medium text-white focus:outline-none focus:ring-2 focus:ring-slate-500 disabled:cursor-not-allowed disabled:opacity-70"
                  >
                    {organizationOptions.map((option) => (
                      <option key={option.id} value={option.id}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                  {(isLoadingMemberships || isLoadingOrganizations || isSwitching) && (
                    <span className="text-xs text-slate-300">Updating…</span>
                  )}
                </div>
                {switchErrorMessage ? (
                  <span className="text-xs text-red-200">{switchErrorMessage}</span>
                ) : switchStatus ? (
                  <span className="text-xs text-emerald-200">{switchStatus}</span>
                ) : organizationErrorMessage ? (
                  <span className="text-xs text-amber-200">{organizationErrorMessage}</span>
                ) : null}
              </div>
            )}
          </div>
          <div className="hidden items-center gap-3 sm:flex">
            {user ? (
              <>
                <div className="text-sm text-slate-200">
                  <p className="font-medium">{user.displayName ?? user.email ?? 'Authenticated user'}</p>
                  <p className="text-xs text-slate-300">{user.email ?? 'Email pending verification'}</p>
                  {isAuthenticated && (
                    <p className="text-xs text-slate-300">
                      Active org:{' '}
                      <span className="font-medium text-white">{activeOrganizationLabel}</span>
                    </p>
                  )}
                </div>
                <button
                  type="button"
                  onClick={logout}
                  className="rounded-md border border-slate-600 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800"
                >
                  Sign out
                </button>
              </>
            ) : (
              <span className="text-sm text-slate-200">Guest</span>
            )}
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-6xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  )
}
