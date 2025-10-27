import { useEffect, useMemo } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { IdentityProvider, useIdentityContext } from '@identity-base/react-client'
import { OrganizationsProvider } from '@identity-base/react-organizations'
import AppLayout from './components/AppLayout'
import ProtectedRoute from './components/ProtectedRoute'
import HomePage from './pages/HomePage'
import RegisterPage from './pages/RegisterPage'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import OrganizationAdminPage from './pages/OrganizationAdminPage'
import AcceptInvitationPage from './pages/AcceptInvitationPage'
import AuthorizeCallbackPage from './pages/AuthorizeCallbackPage'
import ConfirmEmailPage from './pages/ConfirmEmailPage'
import { setAuthManager } from './auth/manager'
import { CONFIG } from './config'

function AuthManagerBridge() {
  const { authManager } = useIdentityContext()

  useEffect(() => {
    if (authManager) {
      setAuthManager(authManager)
    }
  }, [authManager])

  return null
}

export default function App() {
  const organizationsFetcher = useMemo(() => {
    const rewriteMembersPath = (url: string) => {
      try {
        const parsed = new URL(url, typeof window !== 'undefined' ? window.location.origin : undefined)
        if (
          parsed.pathname.startsWith('/organizations/') &&
          parsed.pathname.endsWith('/members')
        ) {
          parsed.pathname = `/sample${parsed.pathname}`
          return parsed.toString()
        }
      } catch {
        // Ignore parsing issues; fall back to original request.
      }
      return null
    }

    return async (input: RequestInfo | URL, init?: RequestInit) => {
      const method = (init?.method ?? 'GET').toUpperCase()
      const requestUrl = typeof input === 'string'
        ? input
        : input instanceof URL
          ? input.toString()
          : (input as Request).url

      if (method === 'GET') {
        const rewritten = rewriteMembersPath(requestUrl)
        if (rewritten) {
          return fetch(rewritten, init)
        }
      }

      return fetch(input as RequestInfo, init)
    }
  }, [])

  return (
    <IdentityProvider
      config={{
        apiBase: CONFIG.apiBase,
        clientId: CONFIG.clientId,
        redirectUri: CONFIG.authorizeRedirectUri,
        scope: CONFIG.authorizeScope,
        tokenStorage: 'localStorage',
      }}
    >
      <OrganizationsProvider apiBase={CONFIG.apiBase} fetcher={organizationsFetcher}>
        <Routes>
          <Route element={<AppLayout />}>
            <Route index element={<HomePage />} />
            <Route path="register" element={<RegisterPage />} />
            <Route path="login" element={<LoginPage />} />
            <Route
              path="dashboard"
              element={(
                <ProtectedRoute>
                  <DashboardPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="organizations/:organizationId"
              element={(
                <ProtectedRoute>
                  <OrganizationAdminPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="invitations/claim"
              element={(
                <ProtectedRoute>
                  <AcceptInvitationPage />
                </ProtectedRoute>
              )}
            />
          </Route>
          <Route path="auth/callback" element={<AuthorizeCallbackPage />} />
          <Route path="auth/confirm" element={<ConfirmEmailPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
        <AuthManagerBridge />
      </OrganizationsProvider>
    </IdentityProvider>
  )
}
