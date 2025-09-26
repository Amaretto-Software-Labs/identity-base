# @identity-base/react-client

Headless React authentication client for Identity.Base. Provides all the heavy lifting for authentication flows while giving you complete control over your UI.

## Features

- 🔐 **Complete OAuth2/PKCE flow** - Authorization code with PKCE
- 🔄 **Automatic token management** - Refresh tokens, storage, expiration
- 🎯 **Headless design** - Zero UI opinions, bring your own components
- 🛡️ **Route protection** - `<ProtectedRoute>` and `useRequireAuth`
- 📱 **MFA support** - Email and SMS multi-factor authentication
- 👤 **Profile management** - User profiles with custom metadata
- 🔗 **External providers** - Google, Microsoft, Apple sign-in
- ⚡ **Cross-tab sync** - Authentication state synced across tabs
- 📦 **TypeScript first** - Complete type definitions
- 🪝 **React hooks** - Clean, composable API

## Installation

```bash
npm install @identity-base/react-client
```

## Quick Start

### 1. Wrap your app with IdentityProvider

```tsx
import { IdentityProvider } from '@identity-base/react-client'

function App() {
  return (
    <IdentityProvider
      config={{
        apiBase: 'https://your-identity-api.com',
        clientId: 'your-spa-client-id',
        redirectUri: 'https://your-app.com/auth/callback',
      }}
    >
      <Router>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/auth/callback" element={<CallbackPage />} />
        </Routes>
      </Router>
    </IdentityProvider>
  )
}
```

### 2. Create a login form

```tsx
import { useLogin } from '@identity-base/react-client'

function LoginPage() {
  const { login, isLoading, error } = useLogin({
    onSuccess: () => navigate('/dashboard')
  })

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    await login({ email, password })
  }

  return (
    <form onSubmit={handleSubmit}>
      <input
        type="email"
        value={email}
        onChange={e => setEmail(e.target.value)}
        disabled={isLoading}
      />
      <input
        type="password"
        value={password}
        onChange={e => setPassword(e.target.value)}
        disabled={isLoading}
      />
      <button type="submit" disabled={isLoading}>
        {isLoading ? 'Signing in...' : 'Sign In'}
      </button>
      {error && <div className="error">{error.message}</div>}
    </form>
  )
}
```

### 3. Protect routes

```tsx
import { ProtectedRoute, useAuth } from '@identity-base/react-client'

function DashboardPage() {
  return (
    <ProtectedRoute fallback={<div>Please sign in...</div>}>
      <Dashboard />
    </ProtectedRoute>
  )
}

function Dashboard() {
  const { user, logout } = useAuth()

  return (
    <div>
      <h1>Welcome {user?.displayName}!</h1>
      <button onClick={logout}>Sign Out</button>
    </div>
  )
}
```

### 4. Handle OAuth callback

```tsx
import { useAuthorization } from '@identity-base/react-client'
import { useSearchParams } from 'react-router-dom'

function CallbackPage() {
  const [params] = useSearchParams()
  const { handleCallback, isLoading, error } = useAuthorization({
    onSuccess: () => navigate('/dashboard')
  })

  useEffect(() => {
    const code = params.get('code')
    const state = params.get('state')

    if (code && state) {
      handleCallback(code, state)
    }
  }, [params, handleCallback])

  if (isLoading) return <div>Completing sign in...</div>
  if (error) return <div>Sign in failed: {error.message}</div>

  return <div>Processing...</div>
}
```

## Configuration

```tsx
interface IdentityConfig {
  // Required
  apiBase: string              // 'https://your-identity-api.com'
  clientId: string             // Your OAuth2 client ID
  redirectUri: string          // Where to redirect after auth

  // Optional
  scope?: string               // OAuth2 scopes (default: 'openid profile email offline_access')
  tokenStorage?: 'localStorage' | 'sessionStorage' | 'memory'
  autoRefresh?: boolean        // Auto refresh tokens (default: true)
  timeout?: number            // API timeout in ms (default: 10000)
}
```

## Hooks API

### useAuth()
```tsx
const {
  user,           // Current user profile or null
  isAuthenticated, // Boolean auth state
  isLoading,      // Initial loading state
  error,          // Any auth errors
  refreshUser,    // Manually refresh user data
} = useAuth()
```

### useLogin()
```tsx
const {
  login,     // (credentials) => Promise<LoginResponse>
  logout,    // () => Promise<void>
  isLoading, // Boolean loading state
  error,     // Login/logout errors
} = useLogin({
  onSuccess: (response) => {}, // Optional success callback
  onError: (error) => {},     // Optional error callback
})
```

### useRegister()
```tsx
const {
  register,  // (userData) => Promise<{correlationId}>
  isLoading, // Boolean loading state
  error,     // Registration errors
} = useRegister({
  onSuccess: (response) => {}, // Optional success callback
})
```

### useAuthorization()
```tsx
const {
  startAuthorization, // () => Promise<void> (redirects to auth)
  handleCallback,     // (code, state) => Promise<User>
  isLoading,
  error,
} = useAuthorization({
  onSuccess: () => {}, // Called after successful auth
})
```

## Route Protection

### ProtectedRoute Component
```tsx
<ProtectedRoute
  fallback={<LoginPrompt />}
  redirectTo="/login"
  onUnauthenticated={() => {}}
>
  <SecretContent />
</ProtectedRoute>
```

### useRequireAuth Hook
```tsx
function SecurePage() {
  const user = useRequireAuth({
    redirectTo: '/login',
    onUnauthenticated: () => notify('Please sign in')
  })

  // user is null during loading/redirecting
  // user is UserProfile once authenticated
  if (!user) return null

  return <div>Hello {user.displayName}</div>
}
```

## Advanced Usage

### Custom Token Storage
```tsx
import { createTokenStorage } from '@identity-base/react-client'

const storage = createTokenStorage('sessionStorage')
// or implement your own TokenStorage interface
```

### Direct Auth Manager Access
```tsx
import { useIdentityContext } from '@identity-base/react-client'

function AdvancedComponent() {
  const { authManager } = useIdentityContext()

  const handleExternalAuth = () => {
    const url = authManager.buildExternalStartUrl('google', 'login', '/dashboard')
    window.location.href = url
  }
}
```

## TypeScript

The package is built with TypeScript and includes complete type definitions:

```tsx
import type {
  UserProfile,
  LoginRequest,
  IdentityConfig,
  ApiError
} from '@identity-base/react-client'
```

## License

MIT