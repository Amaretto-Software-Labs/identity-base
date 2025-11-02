import { Link } from 'react-router-dom'
import { useAuth } from '@identity-base/react-client'

const nextSteps = [
  {
    title: 'Register with organisation metadata',
    description: 'The registration flow asks for organisation slug/name and seeds you as the OrgOwner.',
    to: '/register',
  },
  {
    title: 'Sign in and explore the dashboard',
    description: 'Switch active organisations, inspect memberships, and refresh claims.',
    to: '/dashboard',
  },
  {
    title: 'Invite collaborators',
    description: 'Create invitation codes for additional users, assign org roles, and track pending invites.',
    to: '/organisations/current',
  },
  {
    title: 'Claim an invitation',
    description: 'Redeem a code after signing in to join another organisation with pre-assigned roles.',
    to: '/invitations/claim',
  },
]

export default function HomePage() {
  const { isAuthenticated, user } = useAuth()

  return (
    <div className="space-y-8">
      <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <header className="space-y-2">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Identity Base sample</p>
          <h1 className="text-3xl font-bold text-slate-900">Multi-organisation SaaS walkthrough</h1>
        </header>
        <p className="text-slate-700">
          This frontend pairs with <span className="font-mono">apps/org-sample-api</span> to demonstrate the full organisation
          scenario: users register with organisation metadata, automatically become organisation admins, invite teammates, and
          switch active organisations. The UI leans on <code>@identity-base/react-client</code> for authentication state and the
          sample REST endpoints for organisation workflows.
        </p>

        <div className="rounded-md border border-slate-100 bg-slate-50 p-4">
          <p className="text-sm text-slate-600">
            {isAuthenticated ? (
              <>
                Signed in as <span className="font-medium">{user?.displayName ?? user?.email ?? 'authenticated user'}</span>. Go
                straight to the <Link to="/dashboard" className="text-slate-900 underline">dashboard</Link>.
              </>
            ) : (
              <>
                You are not signed in. Start by <Link to="/register" className="text-slate-900 underline">registering</Link> or{' '}
                <Link to="/login" className="text-slate-900 underline">logging in</Link>.
              </>
            )}
          </p>
        </div>
      </section>

      <section className="space-y-4">
        <h2 className="text-xl font-semibold text-slate-900">Guided tour</h2>
        <div className="grid gap-4 lg:grid-cols-2">
          {nextSteps.map((step) => (
            <Link
              key={step.title}
              to={step.to === '/organisations/current' ? '/dashboard' : step.to}
              className="block rounded-lg border border-slate-200 bg-white p-4 shadow-sm transition hover:-translate-y-0.5 hover:border-slate-300 hover:shadow-md"
            >
              <h3 className="text-lg font-semibold text-slate-900">{step.title}</h3>
              <p className="mt-1 text-sm text-slate-600">{step.description}</p>
            </Link>
          ))}
        </div>
      </section>

      <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-xl font-semibold text-slate-900">API endpoints in play</h2>
        <ul className="list-disc space-y-2 pl-6 text-sm text-slate-600">
          <li>
            <code>/auth/register</code> – extends registration with organisation metadata and seeds OrgOwner role via hosted
            callbacks.
          </li>
          <li>
            <code>/users/me/organisations</code> + <code>/users/me/organisations/active</code> – list and switch memberships.
          </li>
          <li>
            <code>{'/organisations/{id}'}</code>, <code>{'/organisations/{id}/members'}</code>, <code>{'/organisations/{id}/roles'}</code>
            {' '}– manage organisation surface secured via permission scopes.
          </li>
          <li>
            <code>{'/sample/organisations/{id}/invitations'}</code> – sample API overlay storing invitations in Postgres.
          </li>
          <li>
            <code>/sample/invitations/claim</code> – attach accepted invitations to the signed-in user.
          </li>
        </ul>
      </section>
    </div>
  )
}
