import { InjectionToken } from '@angular/core'
import type { IdentityAuthManager } from '@identity-base/client-core'
import type { IdentityAngularClientConfig } from './public-types'

export const IDENTITY_CLIENT_CONFIG = new InjectionToken<IdentityAngularClientConfig>('IDENTITY_CLIENT_CONFIG')
export const IDENTITY_AUTH_MANAGER = new InjectionToken<IdentityAuthManager>('IDENTITY_AUTH_MANAGER')

