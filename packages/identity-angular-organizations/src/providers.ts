import type { Provider } from '@angular/core'
import type { IdentityAngularOrganizationsConfig } from './public-types'
import { IDENTITY_ORGANIZATIONS_CONFIG } from './tokens'
import { ActiveOrganizationService } from './services/ActiveOrganizationService'
import { OrganizationsService } from './services/OrganizationsService'

function normalizeConfig(config: IdentityAngularOrganizationsConfig): IdentityAngularOrganizationsConfig {
  return {
    timeoutMs: 10000,
    organizationHeader: {
      headerName: 'X-Organization-Id',
      exclude: [
        `${config.apiBase}/connect/`,
        `${config.apiBase}/auth/`,
        `${config.apiBase}/healthz`,
      ],
    },
    ...config,
  }
}

export function provideIdentityOrganizations(config: IdentityAngularOrganizationsConfig): Provider[] {
  return [
    { provide: IDENTITY_ORGANIZATIONS_CONFIG, useValue: normalizeConfig(config) },
    ActiveOrganizationService,
    OrganizationsService,
  ]
}

