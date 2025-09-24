import { Link, NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `rounded-md px-3 py-2 text-sm font-medium ${isActive ? 'bg-slate-900 text-white' : 'text-slate-100 hover:bg-slate-800 hover:text-white'}`

export default function Layout() {
  const { user, logout } = useAuth()

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-slate-900 text-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
          <Link to="/" className="text-lg font-semibold">
            Identity Sample Client
          </Link>
          <nav className="flex items-center gap-2">
            <NavLink to="/register" className={navLinkClass}>
              Register
            </NavLink>
            <NavLink to="/login" className={navLinkClass}>
              Login
            </NavLink>
            <NavLink to="/mfa" className={navLinkClass}>
              MFA
            </NavLink>
            <NavLink to="/profile" className={navLinkClass}>
              Profile
            </NavLink>
            <NavLink to="/authorize" className={navLinkClass}>
              Authorize
            </NavLink>
          </nav>
          <div className="flex items-center gap-3">
            {user ? (
              <>
                <div className="text-sm text-slate-200">
                  <p className="font-medium">{user.displayName ?? user.email ?? 'Signed in'}</p>
                  <p className="text-xs text-slate-300">{user.email ?? 'Email pending'}</p>
                </div>
                <button
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
      <main className="mx-auto max-w-4xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  )
}
