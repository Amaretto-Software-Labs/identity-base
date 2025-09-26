import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth, useMfa } from '@identity-base/react-client'
import QRCode from 'qrcode'

export default function MfaSetupPage() {
  const navigate = useNavigate()
  const { user, isAuthenticated, isLoading: authLoading } = useAuth()
  const [currentStep, setCurrentStep] = useState<'enroll' | 'verify' | 'complete'>('enroll')
  const [enrollmentData, setEnrollmentData] = useState<{
    sharedKey: string
    authenticatorUri: string
    recoveryCodes?: string[]
  } | null>(null)
  const [qrCodeDataUrl, setQrCodeDataUrl] = useState<string | null>(null)
  const [verificationCode, setVerificationCode] = useState('')
  const [showRecoveryCodes, setShowRecoveryCodes] = useState(false)

  const { enrollMfa, verifyChallenge, isLoading, error } = useMfa({
    onEnrollSuccess: (response) => {
      setEnrollmentData(response)
      setCurrentStep('verify')
      // Generate QR code
      QRCode.toDataURL(response.authenticatorUri)
        .then(url => setQrCodeDataUrl(url))
        .catch(err => console.error('Failed to generate QR code:', err))
    },
    onVerifySuccess: (response) => {
      setCurrentStep('complete')
      // Extract recovery codes from response if available
      if ('recoveryCodes' in response) {
        setEnrollmentData(prev => prev ? { ...prev, recoveryCodes: (response as any).recoveryCodes } : null)
      }
    }
  })

  useEffect(() => {
    if (!authLoading && !isAuthenticated) {
      navigate('/login', { replace: true })
    }
  }, [authLoading, isAuthenticated, navigate])

  if (authLoading) {
    return <div className="mx-auto max-w-md"><p className="text-sm text-slate-600">Loading...</p></div>
  }

  if (!user) {
    return <div className="mx-auto max-w-md"><p className="text-sm text-red-600">Please sign in to set up MFA.</p></div>
  }

  const handleStartEnrollment = async () => {
    try {
      await enrollMfa()
    } catch (err) {
      // Error handled by hook
    }
  }

  const handleVerifyCode = async () => {
    try {
      await verifyChallenge({
        code: verificationCode,
        method: 'authenticator'
      })
    } catch (err) {
      // Error handled by hook
    }
  }

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text).then(() => {
      // Could add a toast notification here
    })
  }

  if (currentStep === 'enroll') {
    return (
      <div className="mx-auto max-w-md space-y-6">
        <header className="space-y-2">
          <h1 className="text-2xl font-semibold text-slate-900">Set Up Two-Factor Authentication</h1>
          <p className="text-sm text-slate-600">
            Two-factor authentication adds an extra layer of security to your account. You'll need an authenticator app like Google Authenticator, Authy, or 1Password.
          </p>
        </header>

        {error && <p className="text-sm text-red-600">{renderError(error)}</p>}

        <button
          onClick={handleStartEnrollment}
          disabled={isLoading}
          className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:opacity-70"
        >
          {isLoading ? 'Setting up...' : 'Start Setup'}
        </button>

        <div className="text-center">
          <button
            onClick={() => navigate('/profile')}
            className="text-sm text-slate-600 hover:text-slate-900"
          >
            Cancel
          </button>
        </div>
      </div>
    )
  }

  if (currentStep === 'verify' && enrollmentData) {
    return (
      <div className="mx-auto max-w-md space-y-6">
        <header className="space-y-2">
          <h1 className="text-2xl font-semibold text-slate-900">Configure Your Authenticator</h1>
          <p className="text-sm text-slate-600">
            Scan the QR code or manually enter the key into your authenticator app, then enter a verification code.
          </p>
        </header>

        <div className="space-y-4">
          {qrCodeDataUrl && (
            <div className="text-center">
              <img
                src={qrCodeDataUrl}
                alt="MFA QR Code"
                className="mx-auto border rounded-lg p-4 bg-white"
              />
            </div>
          )}

          <div className="border rounded-lg p-4 bg-slate-50">
            <h3 className="text-sm font-medium text-slate-900 mb-2">Manual Entry</h3>
            <p className="text-xs text-slate-600 mb-2">If you can't scan the QR code, enter this key manually:</p>
            <div className="flex items-center gap-2">
              <code className="text-sm bg-white px-2 py-1 rounded border flex-1 font-mono">
                {enrollmentData.sharedKey}
              </code>
              <button
                onClick={() => copyToClipboard(enrollmentData.sharedKey)}
                className="text-xs text-slate-600 hover:text-slate-900 px-2 py-1"
              >
                Copy
              </button>
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700" htmlFor="verification-code">
              Verification Code
            </label>
            <input
              id="verification-code"
              type="text"
              inputMode="numeric"
              autoComplete="one-time-code"
              required
              value={verificationCode}
              onChange={(e) => setVerificationCode(e.target.value)}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200"
              placeholder="Enter 6-digit code"
            />
          </div>

          {error && <p className="text-sm text-red-600">{renderError(error)}</p>}

          <button
            onClick={handleVerifyCode}
            disabled={isLoading || verificationCode.length !== 6}
            className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:opacity-70"
          >
            {isLoading ? 'Verifying...' : 'Verify & Enable MFA'}
          </button>
        </div>
      </div>
    )
  }

  if (currentStep === 'complete') {
    return (
      <div className="mx-auto max-w-md space-y-6">
        <header className="space-y-2">
          <h1 className="text-2xl font-semibold text-slate-900">MFA Setup Complete! 🎉</h1>
          <p className="text-sm text-slate-600">
            Two-factor authentication has been successfully enabled for your account.
          </p>
        </header>

        {enrollmentData?.recoveryCodes && (
          <div className="border rounded-lg p-4 bg-amber-50 border-amber-200">
            <h3 className="text-sm font-medium text-amber-900 mb-2">
              ⚠️ Important: Save Your Recovery Codes
            </h3>
            <p className="text-xs text-amber-800 mb-3">
              Store these recovery codes in a safe place. You can use them to access your account if you lose your authenticator device.
            </p>

            {!showRecoveryCodes ? (
              <button
                onClick={() => setShowRecoveryCodes(true)}
                className="text-sm text-amber-900 underline hover:no-underline"
              >
                Show Recovery Codes
              </button>
            ) : (
              <div className="space-y-2">
                <div className="grid grid-cols-2 gap-2 text-xs font-mono">
                  {enrollmentData.recoveryCodes.map((code, index) => (
                    <div key={index} className="bg-white px-2 py-1 rounded border">
                      {code}
                    </div>
                  ))}
                </div>
                <button
                  onClick={() => copyToClipboard(enrollmentData.recoveryCodes!.join('\n'))}
                  className="text-xs text-amber-900 underline hover:no-underline"
                >
                  Copy All Codes
                </button>
              </div>
            )}
          </div>
        )}

        <div className="space-y-3">
          <button
            onClick={() => navigate('/profile')}
            className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800"
          >
            Return to Profile
          </button>

          <button
            onClick={() => navigate('/')}
            className="w-full text-sm text-slate-600 hover:text-slate-900"
          >
            Go to Home
          </button>
        </div>
      </div>
    )
  }

  return null
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