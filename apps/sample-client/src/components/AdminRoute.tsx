import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth, usePermissions } from '@identity-base/react-client'

const REQUIRED_PERMISSIONS = ['users.read', 'roles.read']

interface AdminRouteProps {
  children: ReactNode
}

export default function AdminRoute({ children }: AdminRouteProps) {
  const location = useLocation()
  const { isAuthenticated, isLoading: isAuthLoading } = useAuth()
  const { permissions, isLoading: isPermissionsLoading, error, refresh } = usePermissions()

  if (isAuthLoading || (isAuthenticated && isPermissionsLoading)) {
    return (
      <div className="rounded-lg border border-slate-200 bg-white p-6 text-sm text-slate-600">
        Checking admin permissions…
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ returnUrl: location.pathname }} replace />
  }

  const hasAccess = REQUIRED_PERMISSIONS.every(permission => permissions.includes(permission))

  if (!hasAccess) {
    return (
      <div className="space-y-4">
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-6 text-sm text-amber-800">
          <h2 className="text-lg font-semibold">Admin access required</h2>
          <p className="mt-2">
            Your account does not have the permissions needed to use the admin console. Contact an administrator to
            assign the required roles (must include {REQUIRED_PERMISSIONS.join(', ')}).
          </p>
        </div>
        {error && error.status && error.status !== 401 && (
          <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            {error.message}
          </div>
        )}
        <button
          onClick={() => refresh().catch(() => undefined)}
          className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
        >
          Retry permission check
        </button>
      </div>
    )
  }

  return <>{children}</>
}
