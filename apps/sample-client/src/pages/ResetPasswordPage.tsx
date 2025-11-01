import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate, useSearchParams, Link } from 'react-router-dom'
import { useResetPassword } from '@identity-base/react-client'

export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()

  const token = searchParams.get('token') ?? ''
  const userId = searchParams.get('userId') ?? ''

  const isInvalidLink = useMemo(() => token.length === 0 || userId.length === 0, [token, userId])

  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const { resetPassword, isLoading, isCompleted, error } = useResetPassword({
    onSuccess: () => {
      // redirect back to login after a short delay
      setTimeout(() => navigate('/login', { replace: true }), 2500)
    },
  })

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (password !== confirmPassword) {
      return
    }

    await resetPassword({ userId, token, password })
  }

  if (isInvalidLink) {
    return (
      <div className="mx-auto max-w-md space-y-4 text-center">
        <h1 className="text-2xl font-semibold text-slate-900">Invalid reset link</h1>
        <p className="text-sm text-slate-600">The password reset link is missing required information. Please request a new email.</p>
        <Link to="/forgot-password" className="text-sm font-medium text-slate-700 hover:text-slate-900">
          Request new reset email
        </Link>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-md space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Choose a new password</h1>
        <p className="text-sm text-slate-600">
          Enter a strong password for your account. Once saved you&apos;ll be redirected to the sign in page.
        </p>
      </header>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="password" className="block text-sm font-medium text-slate-700">
            New password
          </label>
          <input
            id="password"
            type="password"
            required
            minLength={12}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            disabled={isLoading || isCompleted}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
          />
        </div>

        <div>
          <label htmlFor="confirmPassword" className="block text-sm font-medium text-slate-700">
            Confirm password
          </label>
          <input
            id="confirmPassword"
            type="password"
            required
            minLength={12}
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            disabled={isLoading || isCompleted}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
          />
          {password !== confirmPassword && confirmPassword.length > 0 && (
            <p className="mt-1 text-sm text-red-600">Passwords must match.</p>
          )}
        </div>

        {error && <p className="text-sm text-red-600">{error.message ?? 'Unable to reset password.'}</p>}
        {isCompleted && (
          <p className="rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
            Password updated. Redirecting you to the sign in page…
          </p>
        )}

        <button
          type="submit"
          disabled={isLoading || isCompleted || password !== confirmPassword}
          className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-70"
        >
          {isLoading ? 'Saving…' : 'Reset password'}
        </button>
      </form>
    </div>
  )
}
