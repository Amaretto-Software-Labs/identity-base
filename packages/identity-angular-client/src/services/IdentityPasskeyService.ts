import { Inject, Injectable } from '@angular/core'
import type {
  IdentityAuthManager,
  PasskeyCompletionRequest,
  PasskeyEmailConfirmation,
  PasskeyRecoveryRequest,
  PasskeySignupRequest,
  PasskeySummary,
} from '@identity-base/client-core'
import { IDENTITY_AUTH_MANAGER } from '../tokens'

@Injectable()
export class IdentityPasskeyService {
  constructor(@Inject(IDENTITY_AUTH_MANAGER) private readonly authManager: IdentityAuthManager) {}

  isSupported(): boolean {
    return this.authManager.isPasskeySupported()
  }

  getConfiguration() {
    return this.authManager.getPasskeyConfiguration()
  }

  login() {
    return this.authManager.loginWithPasskey()
  }

  beginSignup(request: PasskeySignupRequest) {
    return this.authManager.beginPasskeySignup(request)
  }

  confirmSignupEmail(request: PasskeyEmailConfirmation) {
    return this.authManager.confirmPasskeySignupEmail(request)
  }

  completeSignup(request: PasskeyCompletionRequest) {
    return this.authManager.completePasskeySignup(request)
  }

  list(): Promise<PasskeySummary[]> {
    return this.authManager.listPasskeys()
  }

  create(name: string): Promise<PasskeySummary> {
    return this.authManager.createPasskey(name)
  }

  rename(passkey: PasskeySummary, name: string): Promise<PasskeySummary> {
    return this.authManager.renamePasskey(passkey.id, name, passkey.concurrencyStamp)
  }

  remove(id: string): Promise<void> {
    return this.authManager.removePasskey(id)
  }

  beginRecovery(request: PasskeyRecoveryRequest) {
    return this.authManager.beginPasskeyRecovery(request)
  }

  confirmRecoveryEmail(request: PasskeyEmailConfirmation) {
    return this.authManager.confirmPasskeyRecoveryEmail(request)
  }

  completeRecovery(request: Pick<PasskeyCompletionRequest, 'draftId' | 'name'>) {
    return this.authManager.completePasskeyRecovery(request)
  }
}
