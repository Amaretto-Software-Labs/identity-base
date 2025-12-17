import { Inject, Injectable } from '@angular/core'
import type { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http'
import { Observable, from } from 'rxjs'
import { mergeMap } from 'rxjs/operators'
import type { IdentityAngularClientConfig, IdentityTokenAttachmentRule } from '../public-types'
import { IDENTITY_CLIENT_CONFIG } from '../tokens'
import { IdentityAuthService } from '../services/IdentityAuthService'

@Injectable()
export class IdentityAuthInterceptor implements HttpInterceptor {
  constructor(
    private readonly auth: IdentityAuthService,
    @Inject(IDENTITY_CLIENT_CONFIG) private readonly config: IdentityAngularClientConfig,
  ) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (req.headers.has('Authorization')) {
      return next.handle(req)
    }

    if (!shouldAttachToken(req.url, this.config)) {
      return next.handle(req)
    }

    return from(this.auth.getAccessToken()).pipe(
      mergeMap(token => {
        if (!token) {
          return next.handle(req)
        }

        const cloned = req.clone({
          setHeaders: {
            Authorization: `Bearer ${token}`,
          },
        })

        return next.handle(cloned)
      }),
    )
  }
}

function shouldAttachToken(url: string, config: IdentityAngularClientConfig): boolean {
  const include = config.tokenAttachment?.include
  const exclude = config.tokenAttachment?.exclude

  if (exclude?.some(rule => matchesRule(url, rule))) {
    return false
  }

  if (!include || include.length === 0) {
    return typeof config.apiBase === 'string' && url.startsWith(config.apiBase)
  }

  return include.some(rule => matchesRule(url, rule))
}

function matchesRule(url: string, rule: IdentityTokenAttachmentRule): boolean {
  if (typeof rule === 'string') {
    return url.startsWith(rule)
  }
  if (rule instanceof RegExp) {
    return rule.test(url)
  }
  return rule(url)
}
