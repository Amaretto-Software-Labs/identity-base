import { Inject, Injectable } from '@angular/core'
import type { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http'
import { Observable } from 'rxjs'
import type { IdentityAngularOrganizationsConfig, OrganizationHeaderRule } from '../public-types'
import { IDENTITY_ORGANIZATIONS_CONFIG } from '../tokens'
import { ActiveOrganizationService } from '../services/ActiveOrganizationService'

@Injectable()
export class OrganizationContextInterceptor implements HttpInterceptor {
  constructor(
    private readonly activeOrg: ActiveOrganizationService,
    @Inject(IDENTITY_ORGANIZATIONS_CONFIG) private readonly config: IdentityAngularOrganizationsConfig,
  ) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const organizationId = this.activeOrg.organizationId
    if (!organizationId) {
      return next.handle(req)
    }

    const headerName = this.config.organizationHeader?.headerName ?? 'X-Organization-Id'
    if (req.headers.has(headerName)) {
      return next.handle(req)
    }

    if (!shouldAttachHeader(req.url, this.config)) {
      return next.handle(req)
    }

    return next.handle(req.clone({ setHeaders: { [headerName]: organizationId } }))
  }
}

function shouldAttachHeader(url: string, config: IdentityAngularOrganizationsConfig): boolean {
  const include = config.organizationHeader?.include
  const exclude = config.organizationHeader?.exclude

  if (exclude?.some(rule => matchesRule(url, rule))) {
    return false
  }

  if (!include || include.length === 0) {
    return typeof config.apiBase === 'string' && url.startsWith(config.apiBase)
  }

  return include.some(rule => matchesRule(url, rule))
}

function matchesRule(url: string, rule: OrganizationHeaderRule): boolean {
  if (typeof rule === 'string') {
    return url.startsWith(rule)
  }
  if (rule instanceof RegExp) {
    return rule.test(url)
  }
  return rule(url)
}

