import { Navigate, Route, Routes } from 'react-router-dom'
import Layout from './layout/Layout'
import HomePage from './pages/HomePage'
import RegisterPage from './pages/RegisterPage'
import LoginPage from './pages/LoginPage'
import MfaPage from './pages/MfaPage'
import ProfilePage from './pages/ProfilePage'
import AuthorizePage from './pages/AuthorizePage'
import AuthorizeCallbackPage from './pages/AuthorizeCallbackPage'
import ExternalResultPage from './pages/ExternalResultPage'
import { AuthProvider } from './context/AuthContext'

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<HomePage />} />
          <Route path="register" element={<RegisterPage />} />
          <Route path="login" element={<LoginPage />} />
          <Route path="mfa" element={<MfaPage />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route path="authorize" element={<AuthorizePage />} />
          <Route path="external-result" element={<ExternalResultPage />} />
        </Route>
        <Route path="auth/callback" element={<AuthorizeCallbackPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AuthProvider>
  )
}
