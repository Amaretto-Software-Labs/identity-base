import { Inject, Injectable } from '@angular/core'
import type { IdentityAuthManager, LoginRequest, LoginResponse, RegisterRequest, UserProfile } from '@identity-base/client-core'
import { BehaviorSubject } from 'rxjs'
import { IDENTITY_AUTH_MANAGER } from '../tokens'

export interface IdentityAuthState {
  user: UserProfile | null
  isAuthenticated: boolean
  isLoading: boolean
  error: unknown
}

@Injectable()
export class IdentityAuthService {
  private readonly isBrowser: boolean

  private readonly userSubject = new BehaviorSubject<UserProfile | null>(null)
  private readonly isLoadingSubject = new BehaviorSubject<boolean>(false)
  private readonly errorSubject = new BehaviorSubject<unknown>(null)

  readonly user$ = this.userSubject.asObservable()
  readonly isLoading$ = this.isLoadingSubject.asObservable()
  readonly error$ = this.errorSubject.asObservable()

  constructor(
    @Inject(IDENTITY_AUTH_MANAGER) private readonly authManager: IdentityAuthManager,
  ) {
    this.isBrowser = typeof window !== 'undefined'

    this.authManager.addEventListener(event => {
      if (event.type === 'login') {
        this.userSubject.next(event.user)
      }
      if (event.type === 'logout') {
        this.userSubject.next(null)
      }
    })
  }

  get snapshot(): IdentityAuthState {
    const user = this.userSubject.getValue()
    return {
      user,
      isAuthenticated: !!user || this.authManager.isAuthenticated(),
      isLoading: this.isLoadingSubject.getValue(),
      error: this.errorSubject.getValue(),
    }
  }

  async init(): Promise<void> {
    await this.refreshUser()
  }

  async refreshUser(): Promise<UserProfile | null> {
    this.isLoadingSubject.next(true)
    this.errorSubject.next(null)
    try {
      const user = await this.authManager.getCurrentUser()
      this.userSubject.next(user)
      return user
    } catch (error) {
      this.errorSubject.next(error)
      throw error
    } finally {
      this.isLoadingSubject.next(false)
    }
  }

  async getAccessToken(): Promise<string | null> {
    return await this.authManager.getAccessToken()
  }

  async startAuthorization(): Promise<void> {
    if (!this.isBrowser) {
      throw new Error('startAuthorization() requires a browser environment.')
    }
    await this.authManager.startAuthorization()
  }

  async handleAuthorizationCallback(code: string, state: string): Promise<UserProfile> {
    if (!this.isBrowser) {
      throw new Error('handleAuthorizationCallback() requires a browser environment.')
    }
    const user = await this.authManager.handleAuthorizationCallback(code, state)
    this.userSubject.next(user)
    return user
  }

  async login(request: LoginRequest): Promise<LoginResponse> {
    const response = await this.authManager.login(request)
    await this.refreshUser()
    return response
  }

  async logout(): Promise<void> {
    await this.authManager.logout()
    this.userSubject.next(null)
  }

  async register(request: RegisterRequest): Promise<{ correlationId: string }> {
    return await this.authManager.register(request)
  }
}
