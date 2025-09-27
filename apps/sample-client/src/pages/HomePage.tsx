import { Link } from 'react-router-dom'

export default function HomePage() {
  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h1 className="text-3xl font-bold text-slate-900">Identity Sample Client</h1>
        <p className="text-slate-600">
          Use this harness to exercise registration, MFA, profile management, and external sign-in flows against the Identity
          Base API.
        </p>
      </header>

      <section className="grid gap-4 md:grid-cols-2">
        <ActionCard
          title="Create an Account"
          description="Register with dynamic metadata fields and receive a confirmation email."
          to="/register"
        />
        <ActionCard
          title="Sign In"
          description="Authenticate with email + password and complete MFA challenges when required."
          to="/login"
        />
        <ActionCard
          title="Forgot Password"
          description="Request and complete the password reset flow using the email templates configured in Identity Base."
          to="/forgot-password"
        />
        <ActionCard
          title="Manage Profile"
          description="View and update profile metadata sourced from the registration schema."
          to="/profile"
        />
        <ActionCard
          title="Run Authorization Flow"
          description="Generate a PKCE challenge and walk through the OpenID Connect authorize/token exchange."
          to="/authorize"
        />
      </section>
    </div>
  )
}

function ActionCard({ title, description, to }: { title: string; description: string; to: string }) {
  return (
    <Link
      to={to}
      className="block rounded-lg border border-slate-200 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-slate-300 hover:shadow-md"
    >
      <h2 className="text-xl font-semibold text-slate-900">{title}</h2>
      <p className="mt-2 text-sm text-slate-600">{description}</p>
    </Link>
  )
}
