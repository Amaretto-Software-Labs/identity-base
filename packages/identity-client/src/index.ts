// Core types
export type * from './core/types'

// Core classes (for advanced usage)
export { IdentityAuthManager } from './core/IdentityAuthManager'
export { TokenManager } from './core/TokenManager'
export { ApiClient } from './core/ApiClient'

// React integration
export { IdentityProvider, useIdentityContext } from './react/IdentityProvider'

// React hooks
export { useAuth } from './react/hooks/useAuth'
export { useLogin } from './react/hooks/useLogin'
export { useRegister } from './react/hooks/useRegister'
export { useForgotPassword } from './react/hooks/useForgotPassword'
export { useResetPassword } from './react/hooks/useResetPassword'
export { useMfa } from './react/hooks/useMfa'
export { useProfile } from './react/hooks/useProfile'
export { useAuthorization } from './react/hooks/useAuthorization'
export { useRequireAuth } from './react/hooks/useRequireAuth'

// React components
export { ProtectedRoute } from './react/components/ProtectedRoute'
export { RequireAuth } from './react/components/RequireAuth'

// Utilities
export { generatePkce, randomState, PKCEManager } from './utils/pkce'
export { createTokenStorage } from './utils/storage'
export { IdentityError, createError } from './utils/errors'
export { enableDebugLogging, debugLog } from './utils/logger'

// Import logger for side effects (sets up __enableIdentityDebug global)
import './utils/logger'
