import { useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'

export default function ExternalResultPage() {
  const [params] = useSearchParams()

  const data = useMemo(
    () => ({
      status: params.get('status'),
      message: params.get('message'),
      requiresTwoFactor: params.get('requiresTwoFactor'),
      methods: params.get('methods'),
    }),
    [params],
  )

  return (
    <div className="space-y-4">
      <header className="space-y-1">
        <h1 className="text-2xl font-semibold text-slate-900">External Authentication Result</h1>
        <p className="text-sm text-slate-600">
          Providers redirect here after completing link or login flows. Review the status below.
        </p>
      </header>

      <dl className="rounded-lg border border-slate-200 bg-white p-4 text-sm text-slate-700">
        <Item label="Status" value={data.status ?? '—'} />
        <Item label="Message" value={data.message ?? '—'} />
        <Item label="Requires two-factor" value={data.requiresTwoFactor ?? '—'} />
        <Item label="Methods" value={data.methods ?? '—'} />
      </dl>
    </div>
  )
}

function Item({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4 border-b border-slate-100 py-2 last:border-0">
      <dt className="font-medium text-slate-800">{label}</dt>
      <dd className="flex-1 text-right font-mono text-xs text-slate-600">{value}</dd>
    </div>
  )
}
