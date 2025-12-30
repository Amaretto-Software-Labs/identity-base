import { Inject, Injectable } from '@angular/core'
import type { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router'
import { Router } from '@angular/router'
import type { IdentityAngularClientConfig } from '../public-types'
import { IDENTITY_CLIENT_CONFIG } from '../tokens'
import { IdentityAuthService } from '../services/IdentityAuthService'

@Injectable({ providedIn: 'root' })
export class IdentityRequireAuthGuard implements CanActivate {
  constructor(
    private readonly auth: IdentityAuthService,
    private readonly router: Router,
    @Inject(IDENTITY_CLIENT_CONFIG) private readonly config: IdentityAngularClientConfig,
  ) {}

  async canActivate(_route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Promise<boolean | UrlTree> {
    if (this.auth.snapshot.isAuthenticated) {
      return true
    }

    try {
      await this.auth.refreshUser()
    } catch {
      // Ignore refresh errors to allow unauthenticated handling below.
    }

    if (this.auth.snapshot.isAuthenticated) {
      return true
    }

    if (this.config.onUnauthenticated) {
      this.config.onUnauthenticated(state.url)
      return false
    }

    const loginPath = this.config.loginPath ?? '/login'
    return this.router.createUrlTree([loginPath], {
      queryParams: { returnUrl: state.url },
    })
  }
}
