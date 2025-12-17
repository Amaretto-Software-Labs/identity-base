# Plan: Headless React Authentication Package for Identity.Base

## Package Philosophy
**Headless Authentication Service** - Handle all auth complexity, zero UI opinions. Clients build their own login/register forms but get:
- Automatic token management & refresh
- PKCE OAuth2 flows
- Session persistence
- Error handling
- Type safety

## Core Architecture

### 1. Authentication Manager (Core Engine)
```typescript
class IdentityAuthManager {
  // Token storage & refresh
  // PKCE flow management
  // API communication
  // Event emission for state changes
}
```

### 2. React Integration Layer
```typescript
// Provider
<IdentityProvider config={...}>

// Hooks
const { user, isAuthenticated, isLoading } = useAuth()
const { login, error, isLoading } = useLogin()
const { register, error, isLoading } = useRegister()
const { logout } = useLogout()
const { sendMfaChallenge, verifyMfa } = useMfa()
const { updateProfile } = useProfile()
```

### 3. Route Protection
```typescript
<ProtectedRoute fallback={<LoginPrompt />}>
  <Dashboard />
</ProtectedRoute>

// Or hook-based
const requireAuth = useRequireAuth()
```

### 4. Token & Storage Management
- Automatic access token refresh
- Secure storage (httpOnly cookies preferred, localStorage fallback)
- Cross-tab synchronization
- Memory caching for performance

## Package Structure
```
packages/identity-react-client/
├── src/
│   ├── core/
│   │   ├── IdentityAuthManager.ts    # Core auth engine
│   │   ├── TokenManager.ts           # Token storage/refresh
│   │   ├── ApiClient.ts              # HTTP client
│   │   └── types.ts                  # TypeScript definitions
│   ├── react/
│   │   ├── IdentityProvider.tsx      # Context provider
│   │   ├── hooks/
│   │   │   ├── useAuth.ts           # Auth state
│   │   │   ├── useLogin.ts          # Login flow
│   │   │   ├── useRegister.ts       # Registration
│   │   │   ├── useMfa.ts            # MFA flows
│   │   │   └── useProfile.ts        # Profile management
│   │   └── components/
│   │       ├── ProtectedRoute.tsx   # Route protection
│   │       └── RequireAuth.tsx      # Auth boundary
│   ├── utils/
│   │   ├── pkce.ts                  # PKCE utilities
│   │   ├── storage.ts               # Storage abstraction
│   │   └── errors.ts                # Error handling
│   └── index.ts                     # Public API
```

## Configuration Design
```typescript
interface IdentityConfig {
  // Required
  apiBase: string
  clientId: string

  // OAuth2 settings
  redirectUri: string
  scope?: string

  // Token settings
  tokenStorage?: 'localStorage' | 'sessionStorage' | 'memory'
  autoRefresh?: boolean

  // API settings
  timeout?: number
  retries?: number
}
```

## Usage Examples

### Basic Setup
```typescript
// App.tsx
import { IdentityProvider } from '@identity-base/react-client'

<IdentityProvider config={{
  apiBase: 'https://api.myapp.com',
  clientId: 'my-spa-client',
  redirectUri: 'https://myapp.com/auth/callback'
}}>
  <App />
</IdentityProvider>
```

### Custom Login Form
```typescript
// LoginForm.tsx
import { useLogin, useAuth } from '@identity-base/react-client'

function LoginForm() {
  const { login, error, isLoading } = useLogin()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const handleSubmit = async (e) => {
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

### Route Protection
```typescript
// Dashboard.tsx
import { useRequireAuth } from '@identity-base/react-client'

function Dashboard() {
  const { user } = useRequireAuth() // Redirects if not authenticated

  return <div>Welcome {user.displayName}!</div>
}
```

## Implementation Steps
1. **Create package structure** with TypeScript + build setup
2. **Build core IdentityAuthManager** - token management, API client
3. **Create React hooks** - useAuth, useLogin, etc.
4. **Add route protection utilities**
5. **Extract from sample-client** - migrate existing logic
6. **Add comprehensive error handling & typing**
7. **Write integration tests**
8. **Update sample-client** to use new package
9. **Create documentation & examples**

## Deliverables
- `@identity-base/react-client` npm package
- Complete TypeScript definitions
- Zero UI dependencies (fully headless)
- Drop-in replacement for existing auth logic
- Comprehensive documentation with examples
