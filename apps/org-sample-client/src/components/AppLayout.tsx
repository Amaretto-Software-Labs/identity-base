import { Link, NavLink, Outlet } from 'react-router-dom'
import { useAuth, useLogin } from '@identity-base/react-client'

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `rounded-md px-3 py-2 text-sm font-medium ${isActive ? 'bg-slate-900 text-white' : 'text-slate-100 hover:bg-slate-800 hover:text-white'}`

export default function AppLayout() {
  const { user, isAuthenticated } = useAuth()
  const { logout } = useLogin()

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-slate-900 text-white">
        <div className="mx-auto flex max-w-6xl flex-col gap-3 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center justify-between gap-4">
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
          <div className="hidden items-center gap-3 sm:flex">
            {user ? (
              <>
                <div className="text-sm text-slate-200">
                  <p className="font-medium">{user.displayName ?? user.email ?? 'Authenticated user'}</p>
                  <p className="text-xs text-slate-300">{user.email ?? 'Email pending verification'}</p>
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
