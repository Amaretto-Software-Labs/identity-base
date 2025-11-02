import { useEffect, useMemo, useState } from 'react'
import dayjs from 'dayjs'
import type { OrganisationMember, OrganisationRole } from '../api/types'

export interface OrganisationMemberRowProps {
  member: OrganisationMember
  availableRoles: OrganisationRole[]
  roleNameLookup: Record<string, string>
  onUpdateRoles: (roleIds: string[]) => Promise<void>
  onRemove: () => Promise<void>
  isCurrentUser: boolean
  isBusy: boolean
  className?: string
}

export function OrganisationMemberRow({
  member,
  availableRoles,
  roleNameLookup,
  onUpdateRoles,
  onRemove,
  isCurrentUser,
  isBusy,
  className,
}: OrganisationMemberRowProps) {
  const [selectedRoles, setSelectedRoles] = useState<string[]>(member.roleIds)

  useEffect(() => {
    setSelectedRoles(member.roleIds)
  }, [member.roleIds])

  const toggleRole = (roleId: string) => {
    setSelectedRoles((previous) =>
      previous.includes(roleId) ? previous.filter((id) => id !== roleId) : [...previous, roleId],
    )
  }

  const isDirty = useMemo(() => {
    if (selectedRoles.length !== member.roleIds.length) {
      return true
    }

    const current = new Set(member.roleIds)
    return selectedRoles.some((roleId) => !current.has(roleId))
  }, [selectedRoles, member.roleIds])

  const readonlyRoles = member.roleIds.filter(
    (roleId) => !availableRoles.some((role) => role.id === roleId),
  )

  const handleSave = async () => {
    await onUpdateRoles(selectedRoles)
  }

  const baseClass = 'grid grid-cols-[2fr,2fr,0.8fr,1fr,1fr] gap-3 px-3 py-3 text-sm'
  const containerClass = className ? `${baseClass} ${className}` : baseClass

  return (
    <div className={containerClass}>
      <div className="space-y-1">
        <span className="font-medium text-slate-800">
          {member.displayName ?? member.email ?? 'Organisation member'}
        </span>
        {member.email && <span className="block text-xs text-slate-500">{member.email}</span>}
        <span className="block text-[11px] font-mono text-slate-400">{member.userId}</span>
      </div>
      <div className="flex flex-col gap-1">
        <div className="flex flex-wrap gap-2">
          {availableRoles.length === 0 ? (
            <span className="text-xs text-slate-500">No custom organisation roles available.</span>
          ) : (
            availableRoles.map((role) => {
              const isSelected = selectedRoles.includes(role.id)
              return (
                <button
                  key={role.id}
                  type="button"
                  onClick={() => (isCurrentUser || isBusy ? undefined : toggleRole(role.id))}
                  disabled={isCurrentUser || isBusy}
                  className={`rounded-full border px-3 py-1 text-xs font-medium transition ${
                    isSelected ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-300 text-slate-700 hover:bg-slate-100'
                  } ${isCurrentUser || isBusy ? 'cursor-not-allowed opacity-60' : ''}`}
                >
                  {role.name}
                </button>
              )
            })
          )}
        </div>
        {readonlyRoles.length > 0 && (
          <p className="text-[11px] text-slate-500">
            Fixed roles:{' '}
            {readonlyRoles
              .map((roleId) => roleNameLookup[roleId] ?? roleId)
              .join(', ')}
          </p>
        )}
      </div>
      <div className="text-xs text-slate-600">{member.isPrimary ? 'Yes' : 'No'}</div>
      <div className="text-xs text-slate-600">{dayjs(member.createdAtUtc).format('YYYY-MM-DD HH:mm')}</div>
      <div className="flex flex-col gap-2">
        <button
          type="button"
          onClick={handleSave}
          disabled={!isDirty || isBusy || isCurrentUser || selectedRoles.length === 0}
          className="rounded-md border border-slate-300 px-3 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isBusy ? 'Saving…' : 'Save changes'}
        </button>
        <button
          type="button"
          onClick={() => (isBusy || isCurrentUser ? undefined : onRemove())}
          disabled={isBusy || isCurrentUser}
          className="rounded-md border border-red-200 px-3 py-1 text-xs font-medium text-red-600 hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-60"
        >
          Remove
        </button>
      </div>
    </div>
  )
}
