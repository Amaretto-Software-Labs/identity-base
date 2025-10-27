import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { confirmEmail } from '../api/auth'

export default function ConfirmEmailPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const [status, setStatus] = useState<'pending' | 'success' | 'error'>('pending')
  const [message, setMessage] = useState('')

  useEffect(() => {
    const userId = searchParams.get('userId')?.trim()
    const token = searchParams.get('token')?.trim()

    if (!userId || !token) {
      setStatus('error')
      setMessage('Missing confirmation parameters. Please use the link from your email.')
      return
    }

    confirmEmail({ userId, token })
      .then(() => {
        setStatus('success')
        setMessage('Email confirmed. Redirecting to login…')
        setTimeout(() => navigate('/login', { replace: true }), 1500)
      })
      .catch(() => {
        setStatus('error')
        setMessage('Unable to confirm your email. The link may be invalid or expired.')
      })
  }, [navigate, searchParams])

  return (
    <div className="mx-auto max-w-md space-y-4 rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
      <h1 className="text-xl font-semibold text-slate-900">Confirming your email…</h1>
      {status === 'pending' && <p className="text-sm text-slate-600">Validating the confirmation link.</p>}
      {status === 'success' && <p className="text-sm text-green-700">{message}</p>}
      {status === 'error' && <p className="text-sm text-red-600">{message}</p>}
    </div>
  )
}
