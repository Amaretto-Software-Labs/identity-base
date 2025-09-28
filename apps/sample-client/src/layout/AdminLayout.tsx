import { NavLink, Outlet } from 'react-router-dom'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center justify-between rounded-md px-3 py-2 text-sm font-medium transition-colors ${
    isActive
      ? 'bg-slate-900 text-white'
      : 'text-slate-700 hover:bg-slate-100 hover:text-slate-900'
  }`

export default function AdminLayout() {
  return (
    <div className="space-y-6">
      <header className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold text-slate-900">Admin Console</h1>
            <p className="text-sm text-slate-600">
              Manage users, roles, and administrative operations.
            </p>
          </div>
        </div>
      </header>

      <div className="grid gap-6 lg:grid-cols-[240px_1fr]">
        <aside className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <nav className="flex flex-col gap-2">
            <NavLink to="users" className={linkClass} end>
              <span>Users</span>
            </NavLink>
            <NavLink to="roles" className={linkClass} end>
              <span>Roles</span>
            </NavLink>
          </nav>
        </aside>
        <section className="space-y-6">
          <Outlet />
        </section>
      </div>
    </div>
  )
}
