import { Navigate, Route, Routes } from 'react-router-dom'
import { IdentityProvider } from '@identity-base/react-client'
import Layout from './layout/Layout'
import HomePage from './pages/HomePage'
import RegisterPage from './pages/RegisterPage'
import LoginPage from './pages/LoginPage'
import MfaPage from './pages/MfaPage'
import MfaSetupPage from './pages/MfaSetupPage'
import ForgotPasswordPage from './pages/ForgotPasswordPage'
import ResetPasswordPage from './pages/ResetPasswordPage'
import ProfilePage from './pages/ProfilePage'
import AuthorizePage from './pages/AuthorizePage'
import AuthorizeCallbackPage from './pages/AuthorizeCallbackPage'
import ExternalResultPage from './pages/ExternalResultPage'
import ApiDemoPage from './pages/ApiDemoPage'
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
        <Route element={<Layout />}>
          <Route index element={<HomePage />} />
          <Route path="register" element={<RegisterPage />} />
          <Route path="login" element={<LoginPage />} />
          <Route path="forgot-password" element={<ForgotPasswordPage />} />
          <Route path="reset-password" element={<ResetPasswordPage />} />
          <Route path="mfa" element={<MfaPage />} />
          <Route path="mfa-setup" element={<MfaSetupPage />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route path="authorize" element={<AuthorizePage />} />
          <Route path="api-demo" element={<ApiDemoPage />} />
          <Route path="external-result" element={<ExternalResultPage />} />
        </Route>
        <Route path="auth/callback" element={<AuthorizeCallbackPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </IdentityProvider>
  )
}
