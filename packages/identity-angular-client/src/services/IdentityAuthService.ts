import { Inject, Injectable } from '@angular/core'
import type {
  IdentityAuthManager,
  LoginRequest,
  LoginResponse,
  PasskeyCompletionRequest,
  PasskeyEmailConfirmation,
  PasskeyLoginOptions,
  PasskeyRecoveryRequest,
  PasskeySignupRequest,
  PasskeySummary,
  RegisterRequest,
  UserProfile,
} from '@identity-base/client-core'
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
    return await this.refreshUserInternal(true)
  }

  private async refreshUserInternal(updateLoading: boolean): Promise<UserProfile | null> {
    if (updateLoading) {
      this.isLoadingSubject.next(true)
      this.errorSubject.next(null)
    }
    try {
      const user = await this.authManager.getCurrentUser()
      this.userSubject.next(user)
      return user
    } catch (error) {
      this.errorSubject.next(error)
      throw error
    } finally {
      if (updateLoading) {
        this.isLoadingSubject.next(false)
      }
    }
  }

  private async runWithLoading<T>(action: () => Promise<T>): Promise<T> {
    this.isLoadingSubject.next(true)
    this.errorSubject.next(null)
    try {
      return await action()
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
    await this.runWithLoading(async () => {
      await this.authManager.startAuthorization()
    })
  }

  async handleAuthorizationCallback(code: string, state: string): Promise<UserProfile> {
    if (!this.isBrowser) {
      throw new Error('handleAuthorizationCallback() requires a browser environment.')
    }
    return await this.runWithLoading(async () => {
      const user = await this.authManager.handleAuthorizationCallback(code, state)
      this.userSubject.next(user)
      return user
    })
  }

  async login(request: LoginRequest): Promise<LoginResponse> {
    return await this.runWithLoading(async () => {
      const response = await this.authManager.login(request)
      if (response.message && !response.requiresTwoFactor) {
        await this.refreshUserInternal(false)
      }
      return response
    })
  }

  async logout(): Promise<void> {
    await this.runWithLoading(async () => {
      await this.authManager.logout()
      this.userSubject.next(null)
      this.errorSubject.next(null)
    })
  }

  async register(request: RegisterRequest): Promise<{ correlationId: string }> {
    return await this.runWithLoading(async () => await this.authManager.register(request))
  }

  isPasskeySupported(): boolean {
    return this.isBrowser && this.authManager.isPasskeySupported()
  }

  async isConditionalMediationAvailable(): Promise<boolean> {
    return this.isBrowser && await this.authManager.isConditionalMediationAvailable()
  }

  async loginWithPasskey(options?: PasskeyLoginOptions): Promise<LoginResponse> {
    this.ensureBrowserForPasskeys()
    return await this.runWithLoading(async () => {
      const response = await this.authManager.loginWithPasskey(options)
      await this.refreshUserInternal(false)
      return response
    })
  }

  async beginPasskeySignup(request: PasskeySignupRequest) {
    return await this.runWithLoading(async () => await this.authManager.beginPasskeySignup(request))
  }

  async confirmPasskeySignupEmail(request: PasskeyEmailConfirmation): Promise<void> {
    await this.runWithLoading(async () => await this.authManager.confirmPasskeySignupEmail(request))
  }

  async completePasskeySignup(request: PasskeyCompletionRequest): Promise<LoginResponse> {
    this.ensureBrowserForPasskeys()
    return await this.runWithLoading(async () => {
      const response = await this.authManager.completePasskeySignup(request)
      await this.refreshUserInternal(false)
      return response
    })
  }

  async listPasskeys(): Promise<PasskeySummary[]> {
    return await this.runWithLoading(async () => await this.authManager.listPasskeys())
  }

  async registerPasskey(name: string): Promise<PasskeySummary> {
    this.ensureBrowserForPasskeys()
    return await this.runWithLoading(async () => await this.authManager.registerPasskey(name))
  }

  async renamePasskey(passkey: PasskeySummary, name: string): Promise<PasskeySummary> {
    return await this.runWithLoading(async () =>
      await this.authManager.renamePasskey(passkey.id, name, passkey.concurrencyStamp))
  }

  async removePasskey(id: string): Promise<void> {
    await this.runWithLoading(async () => await this.authManager.removePasskey(id))
  }

  async beginPasskeyRecovery(request: PasskeyRecoveryRequest) {
    return await this.runWithLoading(async () => await this.authManager.beginPasskeyRecovery(request))
  }

  async confirmPasskeyRecoveryEmail(request: PasskeyEmailConfirmation): Promise<void> {
    await this.runWithLoading(async () => await this.authManager.confirmPasskeyRecoveryEmail(request))
  }

  async completePasskeyRecovery(request: Pick<PasskeyCompletionRequest, 'draftId' | 'name'>): Promise<LoginResponse> {
    this.ensureBrowserForPasskeys()
    return await this.runWithLoading(async () => {
      const response = await this.authManager.completePasskeyRecovery(request)
      await this.refreshUserInternal(false)
      return response
    })
  }

  private ensureBrowserForPasskeys(): void {
    if (!this.isBrowser) {
      throw new Error('Passkey ceremonies require a browser environment.')
    }
  }
}
