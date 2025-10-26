import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '@identity-base/react-client'
import type { ReactNode } from 'react'

export default function ProtectedRoute({ children }: { children: ReactNode }) {
  const location = useLocation()
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="rounded-md border border-slate-200 bg-white p-4 text-sm text-slate-600 shadow-sm">
        Checking authentication…
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <>{children}</>
}

