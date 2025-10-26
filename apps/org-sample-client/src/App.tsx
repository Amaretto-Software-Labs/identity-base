import { Navigate, Route, Routes } from 'react-router-dom'
import { IdentityProvider } from '@identity-base/react-client'
import AppLayout from './components/AppLayout'
import ProtectedRoute from './components/ProtectedRoute'
import HomePage from './pages/HomePage'
import RegisterPage from './pages/RegisterPage'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import OrganizationAdminPage from './pages/OrganizationAdminPage'
import AcceptInvitationPage from './pages/AcceptInvitationPage'
import AuthorizeCallbackPage from './pages/AuthorizeCallbackPage'
import { CONFIG } from './config'

export default function App() {
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
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </IdentityProvider>
  )
}

