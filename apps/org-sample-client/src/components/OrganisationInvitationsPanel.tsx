import { useMemo, useState } from 'react'
import dayjs from 'dayjs'
import { createInvitation, revokeInvitation } from '../api/organisations'
import type { InvitationResponse, OrganisationRole } from '../api/types'
import { renderApiError } from '../api/client'

interface OrganisationInvitationsPanelProps {
  organisationId?: string
  roles: OrganisationRole[]
  invitations: InvitationResponse[]
  onInvitationsChange: (invitations: InvitationResponse[]) => void
  onStatusMessage?: (message: string | null) => void
  roleNameLookup: Record<string, string>
}

export function OrganisationInvitationsPanel({
  organisationId,
  roles,
  invitations,
  onInvitationsChange,
  onStatusMessage,
  roleNameLookup,
}: OrganisationInvitationsPanelProps) {
  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteRoleIds, setInviteRoleIds] = useState<string[]>([])
  const [inviteExpiry, setInviteExpiry] = useState<number>(48)
  const [inviting, setInviting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const canSubmit = useMemo(() => inviteEmail.trim().length > 0, [inviteEmail])

  const handleCreateInvitation = async () => {
    if (!organisationId) {
      return
    }

    setError(null)
    onStatusMessage?.(null)

    try {
      setInviting(true)
      const response = await createInvitation(organisationId, {
        email: inviteEmail.trim(),
        roleIds: inviteRoleIds,
        expiresInHours: inviteExpiry,
      })

      onInvitationsChange([response, ...invitations])
      setInviteEmail('')
      setInviteRoleIds([])
      setInviteExpiry(48)
      const message = response.isExistingUser
        ? `Invitation created for ${response.email}. Share the claim link with the recipient.`
        : `Invitation created for ${response.email}. Share the registration link with the recipient.`
      onStatusMessage?.(message)
    } catch (err) {
      setError(renderApiError(err))
    } finally {
      setInviting(false)
    }
  }

  const handleRevokeInvitation = async (code: string) => {
    if (!organisationId) {
      return
    }

    setError(null)
    onStatusMessage?.(null)

    try {
      await revokeInvitation(organisationId, code)
      onInvitationsChange(invitations.filter((invitation) => invitation.code !== code))
      onStatusMessage?.('Invitation revoked.')
    } catch (err) {
      setError(renderApiError(err))
    }
  }

  const toggleInviteRole = (roleId: string) => {
    setInviteRoleIds((previous) =>
      previous.includes(roleId)
        ? previous.filter((id) => id !== roleId)
        : [...previous, roleId],
    )
  }

  return (
    <>
      <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div>
          <h2 className="text-lg font-semibold text-slate-900">Invite users</h2>
          <p className="text-sm text-slate-600">
            Create invitation links that allow new or existing users to join the organisation with predefined roles.
          </p>
        </div>

        {error && (
          <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</div>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <label className="flex flex-col gap-1 text-sm text-slate-700">
            Recipient email
            <input
              type="email"
              value={inviteEmail}
              onChange={(event) => setInviteEmail(event.target.value)}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm text-slate-700">
            Expiration (hours)
            <input
              type="number"
              min={1}
              max={168}
              value={inviteExpiry}
              onChange={(event) => setInviteExpiry(Number.parseInt(event.target.value, 10) || 1)}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
            />
          </label>
        </div>

        <div className="space-y-2">
          <span className="text-xs font-semibold uppercase tracking-wide text-slate-600">
            Assign roles
          </span>
          {roles.length === 0 ? (
            <span className="text-xs text-slate-500">No custom organisation roles available.</span>
          ) : (
            <div className="flex flex-wrap gap-2">
              {roles.map((role) => {
                const isSelected = inviteRoleIds.includes(role.id)
                return (
                  <button
                    key={role.id}
                    type="button"
                    onClick={() => toggleInviteRole(role.id)}
                    className={`rounded-full border px-3 py-1 text-xs font-medium transition ${
                      isSelected ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-300 text-slate-700 hover:bg-slate-100'
                    }`}
                  >
                    {role.name}
                  </button>
                )
              })}
            </div>
          )}
          <p className="text-xs text-slate-500">
            Optional. Select any organisation roles to attach when the invite is claimed.
          </p>
        </div>

        <button
          type="button"
          onClick={handleCreateInvitation}
          disabled={!canSubmit || inviting}
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-70"
        >
          {inviting ? 'Creating…' : 'Create invitation'}
        </button>
      </section>

      <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">Pending invitations</h2>
        {invitations.length === 0 ? (
          <p className="text-sm text-slate-600">No pending invitations.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-sm">
              <thead className="bg-slate-50">
                <tr>
                  <th className="px-3 py-2 text-left font-medium text-slate-700">Code</th>
                  <th className="px-3 py-2 text-left font-medium text-slate-700">Email</th>
                  <th className="px-3 py-2 text-left font-medium text-slate-700">Roles</th>
                  <th className="px-3 py-2 text-left font-medium text-slate-700">Expires</th>
                  <th className="px-3 py-2 text-left font-medium text-slate-700">User</th>
                  <th className="px-3 py-2 text-left font-medium text-slate-700">Links</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {invitations.map((invitation) => (
                  <tr key={invitation.code}>
                    <td className="whitespace-nowrap px-3 py-2 text-xs font-mono text-slate-600">{invitation.code}</td>
                    <td className="whitespace-nowrap px-3 py-2 text-xs text-slate-700">{invitation.email}</td>
                    <td className="px-3 py-2 text-xs text-slate-600">
                      {invitation.roleIds.length === 0
                        ? 'None'
                        : invitation.roleIds
                            .map((roleId) => roleNameLookup[roleId] ?? roleId)
                            .join(', ')}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2 text-xs text-slate-600">
                      {dayjs(invitation.expiresAtUtc).format('YYYY-MM-DD HH:mm')}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2 text-xs text-slate-600">
                      {invitation.isExistingUser ? 'Registered user' : 'New user'}
                    </td>
                    <td className="px-3 py-2 text-xs text-slate-600">
                      <div className="flex flex-col gap-1">
                        {invitation.registerUrl && (
                          <a
                            href={invitation.registerUrl}
                            className="text-slate-700 underline"
                            target="_blank"
                            rel="noreferrer"
                          >
                            Registration link
                          </a>
                        )}
                        {invitation.claimUrl && (
                          <a
                            href={invitation.claimUrl}
                            className="text-slate-700 underline"
                            target="_blank"
                            rel="noreferrer"
                          >
                            Claim link
                          </a>
                        )}
                      </div>
                    </td>
                    <td className="whitespace-nowrap px-3 py-2">
                      <button
                        type="button"
                        onClick={() => handleRevokeInvitation(invitation.code)}
                        className="rounded-md border border-red-200 px-2 py-1 text-xs font-medium text-red-600 hover:bg-red-50"
                      >
                        Revoke
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}
