import { Inject, Injectable } from '@angular/core'
import type { IdentityAuthManager } from '@identity-base/client-core'
import { IDENTITY_AUTH_MANAGER } from '../tokens'

@Injectable()
export class IdentityAdminService {
  constructor(@Inject(IDENTITY_AUTH_MANAGER) private readonly authManager: IdentityAuthManager) {}

  get users() {
    return this.authManager.admin.users
  }

  get roles() {
    return this.authManager.admin.roles
  }

  get permissions() {
    return this.authManager.admin.permissions
  }
}

