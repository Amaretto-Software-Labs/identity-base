import type { Provider } from '@angular/core'
import { IdentityAuthManager } from '@identity-base/client-core'
import type { IdentityAngularClientConfig } from './public-types'
import { IDENTITY_AUTH_MANAGER, IDENTITY_CLIENT_CONFIG } from './tokens'
import { IdentityAuthService } from './services/IdentityAuthService'
import { IdentityAdminService } from './services/IdentityAdminService'

function normalizeConfig(config: IdentityAngularClientConfig): IdentityAngularClientConfig {
  return {
    autoRefresh: true,
    tokenStorage: 'sessionStorage',
    loginPath: '/login',
    ...config,
  }
}

function createAuthManager(config: IdentityAngularClientConfig): IdentityAuthManager {
  const normalized = normalizeConfig(config)
  return new IdentityAuthManager(normalized)
}

export function provideIdentityClient(config: IdentityAngularClientConfig): Provider[] {
  return [
    { provide: IDENTITY_CLIENT_CONFIG, useValue: normalizeConfig(config) },
    { provide: IDENTITY_AUTH_MANAGER, useFactory: createAuthManager, deps: [IDENTITY_CLIENT_CONFIG] },
    IdentityAuthService,
    IdentityAdminService,
  ]
}
