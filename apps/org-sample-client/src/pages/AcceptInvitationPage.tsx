import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '@identity-base/react-client'
import { useOrganizations } from '@identity-base/react-organizations'
import { claimInvitation } from '../api/organizations'
import { renderApiError } from '../api/client'

export default function AcceptInvitationPage() {
  const { refreshUser } = useAuth()
  const { reloadMemberships } = useOrganizations()
  const [searchParams] = useSearchParams()
  const [code, setCode] = useState('')
  const [status, setStatus] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const autoProcessedRef = useRef(false)

  const submitInvitation = async (inviteCode: string) => {
    setStatus(null)
    setError(null)

    if (!inviteCode) {
      setError('Provide an invitation code.')
      return
    }

    setIsSubmitting(true)
    try {
      const response = await claimInvitation({ code: inviteCode })
      if (response.requiresTokenRefresh) {
        await refreshUser()
      }
      await reloadMemberships().catch(() => undefined)

      setStatus(
        `Added to ${response.organizationName} (${response.organizationSlug}). ${
          response.roleIds.length > 0 ? `Roles: ${response.roleIds.join(', ')}` : 'No roles assigned.'
        }`,
      )
      setCode('')
    } catch (err) {
      setError(renderApiError(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  useEffect(() => {
    const codeParam = searchParams.get('code')?.trim()
    if (!codeParam || autoProcessedRef.current) {
      return
    }

    setCode(codeParam)
    autoProcessedRef.current = true
    void submitInvitation(codeParam)
  }, [searchParams])

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    await submitInvitation(code)
  }

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">Claim an invitation</h1>
        <p className="text-sm text-slate-600">
          If an organization admin shared a code with you, submit it here to join their organization. You must already be signed
          in to redeem the invitation.
        </p>
      </header>

      <form onSubmit={handleSubmit} className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div>
          <label htmlFor="invitation-code" className="block text-sm font-medium text-slate-700">
            Invitation code
          </label>
          <input
            id="invitation-code"
            type="text"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 font-mono shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
            placeholder="e.g. 0f5b1234-9c6d-48ff-b2d1-93a3a1484fa4"
          />
        </div>

        {error && <p className="text-sm text-red-600">{error}</p>}
        {status && <p className="text-sm text-green-600">{status}</p>}

        <button
          type="submit"
          disabled={isSubmitting || !code}
          className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-70"
        >
          {isSubmitting ? 'Claiming…' : 'Claim invitation'}
        </button>
      </form>

      <section className="space-y-2 rounded-lg border border-slate-200 bg-white p-4 text-sm text-slate-600 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-800">How it works</h2>
        <ol className="list-decimal space-y-1 pl-5">
          <li>An organization admin issues an invitation from the organization management page.</li>
          <li>
            You sign in (or register) and paste the invitation code here. The sample API validates membership and assigns the
            requested organization roles.
          </li>
          <li>Switch to the dashboard to see the new organization in your membership list.</li>
        </ol>
      </section>
    </div>
  )
}
