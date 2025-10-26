import { useMemo, useState, type Dispatch, type SetStateAction, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { registerUser } from '../api/auth'
import type { ProfileSchemaField } from '../api/types'
import { useProfileSchema } from '../hooks/useProfileSchema'
import { renderApiError } from '../api/client'

interface FieldState {
  value: string
  error?: string
}

export default function RegisterPage() {
  const navigate = useNavigate()
  const { fields, isLoading, error: schemaError } = useProfileSchema()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [metadataState, setMetadataState] = useState<Record<string, FieldState>>({})
  const [submitting, setSubmitting] = useState(false)
  const [serverError, setServerError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  const mergedState = useMemo(() => mergeMetadataState(fields, metadataState, setMetadataState), [fields, metadataState])

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setServerError(null)
    setSuccessMessage(null)

    const validationErrors = validateFields(fields, mergedState, setMetadataState)
    if (validationErrors) {
      return
    }

    setSubmitting(true)
    try {
      const metadata = Object.fromEntries(
        fields.map((field) => [field.name, mergedState[field.name]?.value?.trim() || null]),
      )

      const response = await registerUser({
        email,
        password,
        metadata,
      })

      setSuccessMessage(`Registration accepted (correlation ${response.correlationId}). Check your inbox to confirm.`)
      setTimeout(() => navigate('/login'), 1500)
    } catch (err) {
      setServerError(renderApiError(err))
    } finally {
      setSubmitting(false)
    }
  }

  if (schemaError) {
    return (
      <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
        Failed to load registration schema. Ensure the sample API is running.
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Create an account</h1>
        <p className="text-sm text-slate-600">
          Provide your email, password, and the requested organization metadata. The bootstrap service will create that
          organization and assign you the OrgOwner role after confirmation.
        </p>
      </header>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="register-email" className="block text-sm font-medium text-slate-700">
              Email
            </label>
            <input
              id="register-email"
              type="email"
              required
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
            />
          </div>

          <div>
            <label htmlFor="register-password" className="block text-sm font-medium text-slate-700">
              Password
            </label>
            <input
              id="register-password"
              type="password"
              required
              autoComplete="new-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
            />
            <p className="mt-1 text-xs text-slate-500">Minimum 12 characters with mixed case and a digit.</p>
          </div>
        </div>

        <fieldset className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <legend className="px-2 text-sm font-semibold text-slate-800">Organization metadata</legend>
          {isLoading ? (
            <p className="text-sm text-slate-600">Loading schema…</p>
          ) : (
            fields.map((field) => (
              <MetadataField
                key={field.name}
                field={field}
                state={mergedState[field.name]!}
                onChange={(value) =>
                  setMetadataState((previous) => ({
                    ...previous,
                    [field.name]: { value },
                  }))
                }
              />
            ))
          )}
        </fieldset>

        {serverError && <p className="text-sm text-red-600">{serverError}</p>}
        {successMessage && (
          <p className="rounded-md border border-green-200 bg-green-50 p-3 text-sm text-green-700">{successMessage}</p>
        )}

        <button
          type="submit"
          disabled={submitting || isLoading}
          className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-70"
        >
          {submitting ? 'Submitting…' : 'Register'}
        </button>
      </form>
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
  const label = `${field.displayName}${field.required ? ' *' : ''}`
  return (
    <div>
      <label htmlFor={`metadata-${field.name}`} className="block text-sm font-medium text-slate-700">
        {label}
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
        <p className="mt-1 text-xs text-slate-500">
          Max {field.maxLength} characters{field.pattern ? ` • Pattern ${field.pattern}` : ''}
        </p>
      )}
    </div>
  )
}

function mergeMetadataState(
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
    const value = state[field.name]?.value?.trim() ?? ''
    let error: string | undefined

    if (field.required && value.length === 0) {
      error = 'Required'
    } else if (value.length > field.maxLength) {
      error = `Limit ${field.maxLength} characters`
    } else if (field.pattern) {
      const regex = new RegExp(field.pattern)
      if (value && !regex.test(value)) {
        error = 'Does not match expected pattern'
      }
    }

    if (error) {
      hasErrors = true
    }

    next[field.name] = { value, error }
  }

  if (hasErrors) {
    setState(next)
  }

  return hasErrors
}
