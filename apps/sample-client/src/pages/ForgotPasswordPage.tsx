import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useForgotPassword } from '@identity-base/react-client'

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const { requestReset, isLoading, isCompleted, error } = useForgotPassword()

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    await requestReset(email)
  }

  return (
    <div className="mx-auto max-w-md space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Forgot password</h1>
        <p className="text-sm text-slate-600">
          Enter your email address and we&apos;ll send you a password reset link. If the email exists and is confirmed,
          the message will arrive shortly.
        </p>
      </header>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="email" className="block text-sm font-medium text-slate-700">
            Email address
          </label>
          <input
            id="email"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            disabled={isLoading || isCompleted}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
          />
        </div>

        {error && <p className="text-sm text-red-600">{error.message ?? 'Something went wrong.'}</p>}
        {isCompleted && (
          <p className="rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
            If an account exists for that email address, a password reset link has been sent.
          </p>
        )}

        <button
          type="submit"
          disabled={isLoading || isCompleted}
          className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-70"
        >
          {isLoading ? 'Sending…' : 'Send reset email'}
        </button>
      </form>

      <div className="text-center text-sm">
        <Link to="/login" className="text-slate-600 hover:text-slate-900">
          Back to sign in
        </Link>
      </div>
    </div>
  )
}
