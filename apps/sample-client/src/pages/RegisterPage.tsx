import { useState } from 'react'
import type { Dispatch, FormEvent, SetStateAction } from 'react'
import { useNavigate } from 'react-router-dom'
import { registerUser } from '../api/auth'
import type { ProfileSchemaField } from '../api/types'
import { useProfileSchema } from '../hooks/useProfileSchema'
import { CONFIG } from '../config'

interface FieldState {
  value: string
  error?: string
}

export default function RegisterPage() {
  const navigate = useNavigate()
  const { fields, isLoading, error: schemaError } = useProfileSchema()
  const [formState, setFormState] = useState<Record<string, FieldState>>({})
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [serverError, setServerError] = useState<string | null>(null)
  const [completed, setCompleted] = useState<string | null>(null)

  const mergedState = ensureFieldState(fields, formState, setFormState)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setServerError(null)

    const hasErrors = validateFields(fields, mergedState, setFormState)
    if (hasErrors) {
      return
    }

    setSubmitting(true)
    try {
      const metadata = Object.fromEntries(
        fields.map((field) => [field.name, mergedState[field.name]?.value?.trim() ?? null]),
      )

      const response = await registerUser({
        email,
        password,
        metadata,
      })

      setCompleted(response.correlationId)
      setTimeout(() => navigate('/login'), 1500)
    } catch (err) {
      setServerError(renderError(err))
    } finally {
      setSubmitting(false)
    }
  }

  if (schemaError) {
    return (
      <div className="rounded-md border border-red-200 bg-red-50 p-4 text-red-700">
        Failed to load registration schema.
      </div>
    )
  }

  return (
    <div className="max-w-xl space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Create an Account</h1>
        <p className="text-slate-600">
          Supply your email, a strong password, and the required profile fields. A confirmation email will be sent via MailJet.
        </p>
      </header>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="email" className="block text-sm font-medium text-slate-700">
            Email
          </label>
          <input
            id="email"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
          />
        </div>

        <div>
          <label htmlFor="password" className="block text-sm font-medium text-slate-700">
            Password
          </label>
          <input
            id="password"
            type="password"
            required
            autoComplete="new-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
          />
          <p className="mt-1 text-xs text-slate-500">Minimum 12 characters, with upper/lowercase and a digit.</p>
        </div>

        <fieldset className="space-y-4">
          <legend className="text-sm font-semibold text-slate-800">Profile metadata</legend>
          {isLoading ? (
            <p className="text-sm text-slate-600">Loading schema…</p>
          ) : (
            fields.map((field) => (
              <MetadataField
                key={field.name}
                field={field}
                state={mergedState[field.name]!}
                onChange={(value) =>
                  setFormState((previous) => ({
                    ...previous,
                    [field.name]: { value },
                  }))
                }
              />
            ))
          )}
        </fieldset>

        {serverError && <p className="text-sm text-red-600">{serverError}</p>}
        {completed && (
          <p className="rounded-md border border-green-200 bg-green-50 p-3 text-sm text-green-700">
            Registration accepted (correlation ID {completed}). Check your inbox to confirm the account.
          </p>
        )}

        <button
          type="submit"
          disabled={submitting || isLoading}
          className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-70"
        >
          {submitting ? 'Submitting…' : 'Register'}
        </button>
      </form>

      <p className="text-xs text-slate-500">
        Client ID: <span className="font-mono">{CONFIG.clientId}</span>
      </p>
    </div>
  )
}

function MetadataField({
  field,
  state,
  onChange,
}: {
  field: ProfileSchemaField
  state: FieldState
  onChange: (value: string) => void
}) {
  return (
    <div>
      <label htmlFor={`metadata-${field.name}`} className="block text-sm font-medium text-slate-700">
        {field.displayName}
        {field.required && <span className="text-red-500"> *</span>}
      </label>
      <input
        id={`metadata-${field.name}`}
        type="text"
        required={field.required}
        maxLength={field.maxLength}
        value={state.value}
        onChange={(event) => onChange(event.target.value)}
        className={`mt-1 w-full rounded-md border px-3 py-2 shadow-sm focus:outline-none focus:ring-2 focus:ring-slate-200 ${state.error ? 'border-red-500' : 'border-slate-300 focus:border-slate-500'}`}
      />
      {state.error ? (
        <p className="mt-1 text-xs text-red-600">{state.error}</p>
      ) : (
        <p className="mt-1 text-xs text-slate-500">Max length {field.maxLength}{field.pattern ? ` • Pattern ${field.pattern}` : ''}</p>
      )}
    </div>
  )
}

function ensureFieldState(
  fields: ProfileSchemaField[],
  state: Record<string, FieldState>,
  setState: Dispatch<SetStateAction<Record<string, FieldState>>>,
) {
  if (fields.length === 0) {
    return state
  }

  let changed = false
  const next: Record<string, FieldState> = { ...state }

  for (const field of fields) {
    if (!next[field.name]) {
      next[field.name] = { value: '' }
      changed = true
    }
  }

  if (changed) {
    setState(next)
  }

  return next
}

function validateFields(
  fields: ProfileSchemaField[],
  state: Record<string, FieldState>,
  setState: Dispatch<SetStateAction<Record<string, FieldState>>>,
) {
  let hasErrors = false
  const next: Record<string, FieldState> = {}

  for (const field of fields) {
    const current = state[field.name]?.value ?? ''
    let error: string | undefined

    if (field.required && current.trim().length === 0) {
      error = 'This field is required.'
    } else if (current.length > field.maxLength) {
      error = `Maximum length is ${field.maxLength} characters.`
    } else if (field.pattern) {
      const regex = new RegExp(field.pattern)
      if (current && !regex.test(current)) {
        error = 'Value does not match the required pattern.'
      }
    }

    next[field.name] = { value: current, error }
    if (error) {
      hasErrors = true
    }
  }

  setState(next)
  return hasErrors
}

function renderError(error: unknown) {
  if (!error) return 'Unexpected error'
  if (typeof error === 'string') return error
  if (typeof error === 'object' && error !== null) {
    const maybeProblem = error as { detail?: string; title?: string; errors?: Record<string, string[]> }
    if (maybeProblem.errors) {
      return Object.entries(maybeProblem.errors)
        .map(([key, messages]) => `${key}: ${messages.join(', ')}`)
        .join('\n')
    }
    return maybeProblem.detail ?? maybeProblem.title ?? 'Unexpected error'
  }
  return 'Unexpected error'
}
