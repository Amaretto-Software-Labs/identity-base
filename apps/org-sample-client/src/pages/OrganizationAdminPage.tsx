import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import dayjs from 'dayjs'
import { listInvitations, createInvitation, revokeInvitation, getOrganization, getOrganizationMembers, getOrganizationRoles } from '../api/organizations'
import type { OrganizationRole, InvitationResponse } from '../api/types'
import { renderApiError } from '../api/client'

interface MemberViewModel {
  userId: string
  isPrimary: boolean
  roleIds: string[]
  createdAtUtc: string
  updatedAtUtc: string | null
}

export default function OrganizationAdminPage() {
  const { organizationId } = useParams<'organizationId'>()
  const navigate = useNavigate()

  const [organizationName, setOrganizationName] = useState<string>('')
  const [organizationSlug, setOrganizationSlug] = useState<string>('')
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [members, setMembers] = useState<MemberViewModel[]>([])
  const [roles, setRoles] = useState<OrganizationRole[]>([])
  const [invitations, setInvitations] = useState<InvitationResponse[]>([])

  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteRoleIds, setInviteRoleIds] = useState<string[]>([])
  const [inviteExpiry, setInviteExpiry] = useState<number>(48)
  const [inviting, setInviting] = useState(false)
  const [inviteError, setInviteError] = useState<string | null>(null)
  const [statusMessage, setStatusMessage] = useState<string | null>(null)

  useEffect(() => {
    if (!organizationId) {
      navigate('/dashboard', { replace: true })
      return
    }

    let cancelled = false

    const load = async () => {
      setIsLoading(true)
      setLoadError(null)
      try {
        const [organization, memberList, roleList, inviteList] = await Promise.all([
          getOrganization(organizationId),
          getOrganizationMembers(organizationId),
          getOrganizationRoles(organizationId).catch(() => []),
          listInvitations(organizationId).catch(() => []),
        ])

        if (cancelled) return

        setOrganizationName(organization.displayName)
        setOrganizationSlug(organization.slug)
        setMembers(
          memberList.map((member) => ({
            userId: member.userId,
            isPrimary: member.isPrimary,
            roleIds: member.roleIds,
            createdAtUtc: member.createdAtUtc,
            updatedAtUtc: member.updatedAtUtc,
          })),
        )
        setRoles(roleList)
        setInvitations(inviteList)
      } catch (err) {
        if (!cancelled) {
          setLoadError(renderApiError(err))
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false)
        }
      }
    }

    load()

    return () => {
      cancelled = true
    }
  }, [organizationId, navigate])

  const roleNameLookup = useMemo(() => {
    return roles.reduce<Record<string, string>>((acc, role) => {
      acc[role.id] = role.name
      return acc
    }, {})
  }, [roles])

  const handleCreateInvitation = async () => {
    if (!organizationId) return
    setInviteError(null)
    setStatusMessage(null)

    try {
      setInviting(true)
      const response = await createInvitation(organizationId, {
        email: inviteEmail.trim(),
        roleIds: inviteRoleIds,
        expiresInHours: inviteExpiry,
      })

      setInvitations((previous) => [response, ...previous])
      setInviteEmail('')
      setInviteRoleIds([])
      setInviteExpiry(48)
      setStatusMessage(`Invitation created for ${response.email}.`)
    } catch (err) {
      setInviteError(renderApiError(err))
    } finally {
      setInviting(false)
    }
  }

  const handleRevoke = async (code: string) => {
    if (!organizationId) return
    setStatusMessage(null)
    setInviteError(null)

    try {
      await revokeInvitation(organizationId, code)
      setInvitations((previous) => previous.filter((invitation) => invitation.code !== code))
      setStatusMessage('Invitation revoked.')
    } catch (err) {
      setInviteError(renderApiError(err))
    }
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <p className="text-xs text-slate-500">
          <Link to="/dashboard" className="text-slate-900 underline">
            Dashboard
          </Link>{' '}
          / Organization
        </p>
        <h1 className="text-2xl font-semibold text-slate-900">{organizationName || 'Organization'}</h1>
        <p className="text-sm text-slate-600">
          Manage memberships, inspect organization roles, and create invitation codes. Permissions and scope enforcement are
          handled by the sample API using <code>RequireOrganizationPermission</code>.
        </p>
        <p className="text-xs text-slate-500">Slug: <span className="font-mono">{organizationSlug}</span></p>
      </header>

      {statusMessage && (
        <div className="rounded-md border border-green-200 bg-green-50 p-3 text-sm text-green-700">{statusMessage}</div>
      )}
      {inviteError && (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">{inviteError}</div>
      )}
      {loadError && (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">Failed to load organization. {loadError}</div>
      )}

      {isLoading ? (
        <p className="text-sm text-slate-600">Loading organization details…</p>
      ) : (
        <>
          <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">Members</h2>
            {members.length === 0 ? (
              <p className="text-sm text-slate-600">No members found. Send an invitation to add teammates.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-slate-200 text-sm">
                  <thead className="bg-slate-50">
                    <tr>
                      <th className="px-3 py-2 text-left font-medium text-slate-700">User ID</th>
                      <th className="px-3 py-2 text-left font-medium text-slate-700">Roles</th>
                      <th className="px-3 py-2 text-left font-medium text-slate-700">Primary</th>
                      <th className="px-3 py-2 text-left font-medium text-slate-700">Joined</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {members.map((member) => (
                      <tr key={member.userId}>
                        <td className="whitespace-nowrap px-3 py-2 font-mono text-xs text-slate-600">{member.userId}</td>
                        <td className="px-3 py-2">
                          {member.roleIds.length === 0 ? (
                            <span className="text-xs text-slate-500">None</span>
                          ) : (
                            <div className="flex flex-wrap gap-1">
                              {member.roleIds.map((roleId) => (
                                <span
                                  key={roleId}
                                  className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700"
                                >
                                  {roleNameLookup[roleId] ?? roleId}
                                </span>
                              ))}
                            </div>
                          )}
                        </td>
                        <td className="whitespace-nowrap px-3 py-2 text-xs text-slate-600">{member.isPrimary ? 'Yes' : 'No'}</td>
                        <td className="whitespace-nowrap px-3 py-2 text-xs text-slate-600">
                          {dayjs(member.createdAtUtc).format('YYYY-MM-DD HH:mm')}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">Create invitation</h2>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1">
                <label htmlFor="invite-email" className="block text-sm font-medium text-slate-700">
                  Email
                </label>
                <input
                  id="invite-email"
                  type="email"
                  value={inviteEmail}
                  onChange={(event) => setInviteEmail(event.target.value)}
                  className="w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
                />
              </div>
              <div className="space-y-1">
                <label htmlFor="invite-expiry" className="block text-sm font-medium text-slate-700">
                  Expires in (hours)
                </label>
                <input
                  id="invite-expiry"
                  type="number"
                  min={1}
                  max={720}
                  value={inviteExpiry}
                  onChange={(event) => setInviteExpiry(Number.parseInt(event.target.value, 10) || 1)}
                  className="w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
                />
              </div>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Assign roles</label>
              <p className="mt-1 text-xs text-slate-500">
                Optional. Select any organization roles to attach when the invite is claimed.
              </p>
              <div className="mt-2 flex flex-wrap gap-2">
                {roles.length === 0 ? (
                  <span className="text-xs text-slate-500">No custom organization roles available.</span>
                ) : (
                  roles.map((role) => {
                    const isSelected = inviteRoleIds.includes(role.id)
                    return (
                      <button
                        key={role.id}
                        type="button"
                        onClick={() =>
                          setInviteRoleIds((previous) =>
                            isSelected ? previous.filter((id) => id !== role.id) : [...previous, role.id],
                          )
                        }
                        className={`rounded-full border px-3 py-1 text-xs font-medium transition ${isSelected ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-300 text-slate-700 hover:bg-slate-100'}`}
                      >
                        {role.name}
                      </button>
                    )
                  })
                )}
              </div>
            </div>
            <button
              type="button"
              onClick={handleCreateInvitation}
              disabled={inviting || !inviteEmail}
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
                        <td className="whitespace-nowrap px-3 py-2">
                          <button
                            type="button"
                            onClick={() => handleRevoke(invitation.code)}
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
      )}
    </div>
  )
}

