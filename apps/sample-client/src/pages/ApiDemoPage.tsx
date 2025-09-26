import { useState } from 'react'
import { useAuth, useIdentityContext } from '@identity-base/react-client'

interface ApiResponse {
  status: string
  message: string
  timestamp: string
}

interface WeatherResponse {
  data: Array<{
    date: string
    temperatureC: number
    temperatureF: number
    summary: string
  }>
  user: any
}

export default function ApiDemoPage() {
  const { isAuthenticated, user } = useAuth()
  const { authManager } = useIdentityContext()
  const [publicResponse, setPublicResponse] = useState<ApiResponse | null>(null)
  const [protectedResponse, setProtectedResponse] = useState<WeatherResponse | null>(null)
  const [profileResponse, setProfileResponse] = useState<any>(null)
  const [adminResponse, setAdminResponse] = useState<any>(null)
  const [loading, setLoading] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const apiCall = async (endpoint: string, requiresAuth = false) => {
    setError(null)
    setLoading(endpoint)

    try {
      const headers: HeadersInit = {
        'Content-Type': 'application/json',
      }

      if (requiresAuth) {
        const token = await authManager.getAccessToken()
        if (token) {
          headers['Authorization'] = `Bearer ${token}`
        }
      }

      const response = await fetch(`https://localhost:5001${endpoint}`, {
        method: 'GET',
        headers,
      })

      if (!response.ok) {
        throw new Error(`API call failed: ${response.status} ${response.statusText}`)
      }

      const data = await response.json()
      return data
    } catch (err: any) {
      setError(err.message)
      throw err
    } finally {
      setLoading(null)
    }
  }

  const callPublicApi = async () => {
    try {
      const data = await apiCall('/api/public/status')
      setPublicResponse(data)
    } catch (err) {
      console.error('Public API call failed:', err)
    }
  }

  const callProtectedWeather = async () => {
    try {
      const data = await apiCall('/api/protected/weather', true)
      setProtectedResponse(data)
    } catch (err) {
      console.error('Protected weather API call failed:', err)
    }
  }

  const callProtectedProfile = async () => {
    try {
      const data = await apiCall('/api/protected/profile', true)
      setProfileResponse(data)
    } catch (err) {
      console.error('Protected profile API call failed:', err)
    }
  }

  const callAdminApi = async () => {
    try {
      const data = await apiCall('/api/protected/admin', true)
      setAdminResponse(data)
    } catch (err) {
      console.error('Admin API call failed:', err)
    }
  }

  return (
    <div className="space-y-8">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">API Integration Demo</h1>
        <p className="text-sm text-slate-600">
          Test integration with the Sample API running on https://localhost:5001
        </p>

        {user && (
          <div className="rounded-md bg-green-50 border border-green-200 p-3">
            <p className="text-sm font-medium text-green-800">
              Authenticated as: {user.displayName ?? user.email}
            </p>
          </div>
        )}

        {!isAuthenticated && (
          <div className="rounded-md bg-orange-50 border border-orange-200 p-3">
            <p className="text-sm font-medium text-orange-800">
              You are not authenticated. Some API calls will fail.
            </p>
          </div>
        )}
      </header>

      {/* Public API Test */}
      <section className="space-y-4">
        <h2 className="text-lg font-medium text-slate-900">Public API</h2>
        <div className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h3 className="font-medium text-slate-800">GET /api/public/status</h3>
              <p className="text-sm text-slate-600">No authentication required</p>
            </div>
            <button
              onClick={callPublicApi}
              disabled={loading === '/api/public/status'}
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {loading === '/api/public/status' ? 'Loading...' : 'Test'}
            </button>
          </div>
          {publicResponse && (
            <pre className="mt-4 rounded-md bg-slate-100 p-3 text-xs overflow-auto">
              {JSON.stringify(publicResponse, null, 2)}
            </pre>
          )}
        </div>
      </section>

      {/* Protected API Tests */}
      <section className="space-y-4">
        <h2 className="text-lg font-medium text-slate-900">Protected APIs</h2>

        {/* Weather API */}
        <div className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h3 className="font-medium text-slate-800">GET /api/protected/weather</h3>
              <p className="text-sm text-slate-600">Requires authentication</p>
            </div>
            <button
              onClick={callProtectedWeather}
              disabled={loading === '/api/protected/weather' || !isAuthenticated}
              className="rounded-md bg-green-600 px-4 py-2 text-sm font-semibold text-white hover:bg-green-700 disabled:opacity-50"
            >
              {loading === '/api/protected/weather' ? 'Loading...' : 'Test'}
            </button>
          </div>
          {protectedResponse && (
            <pre className="mt-4 rounded-md bg-slate-100 p-3 text-xs overflow-auto">
              {JSON.stringify(protectedResponse, null, 2)}
            </pre>
          )}
        </div>

        {/* Profile API */}
        <div className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h3 className="font-medium text-slate-800">GET /api/protected/profile</h3>
              <p className="text-sm text-slate-600">Returns user claims and profile info</p>
            </div>
            <button
              onClick={callProtectedProfile}
              disabled={loading === '/api/protected/profile' || !isAuthenticated}
              className="rounded-md bg-green-600 px-4 py-2 text-sm font-semibold text-white hover:bg-green-700 disabled:opacity-50"
            >
              {loading === '/api/protected/profile' ? 'Loading...' : 'Test'}
            </button>
          </div>
          {profileResponse && (
            <pre className="mt-4 rounded-md bg-slate-100 p-3 text-xs overflow-auto">
              {JSON.stringify(profileResponse, null, 2)}
            </pre>
          )}
        </div>

        {/* Admin API */}
        <div className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h3 className="font-medium text-slate-800">GET /api/protected/admin</h3>
              <p className="text-sm text-slate-600">Requires 'identity.api' scope</p>
            </div>
            <button
              onClick={callAdminApi}
              disabled={loading === '/api/protected/admin' || !isAuthenticated}
              className="rounded-md bg-purple-600 px-4 py-2 text-sm font-semibold text-white hover:bg-purple-700 disabled:opacity-50"
            >
              {loading === '/api/protected/admin' ? 'Loading...' : 'Test'}
            </button>
          </div>
          {adminResponse && (
            <pre className="mt-4 rounded-md bg-slate-100 p-3 text-xs overflow-auto">
              {JSON.stringify(adminResponse, null, 2)}
            </pre>
          )}
        </div>
      </section>

      {error && (
        <div className="rounded-md bg-red-50 border border-red-200 p-4">
          <h3 className="font-medium text-red-800">Error</h3>
          <p className="text-sm text-red-600 mt-1">{error}</p>
        </div>
      )}
    </div>
  )
}