export function isPasskeySupported(): boolean {
  return typeof window !== 'undefined'
    && window.isSecureContext
    && typeof window.PublicKeyCredential !== 'undefined'
    && typeof navigator !== 'undefined'
    && typeof navigator.credentials?.create === 'function'
    && typeof navigator.credentials?.get === 'function'
}

export async function isConditionalMediationAvailable(): Promise<boolean> {
  if (!isPasskeySupported()) return false
  const credentialType = window.PublicKeyCredential as typeof PublicKeyCredential & {
    isConditionalMediationAvailable?: () => Promise<boolean>
  }
  return typeof credentialType.isConditionalMediationAvailable === 'function'
    && await credentialType.isConditionalMediationAvailable()
}

export function parsePasskeyCreationOptions(json: string): PublicKeyCredentialCreationOptions {
  const credentialType = typeof window === 'undefined'
    ? undefined
    : window.PublicKeyCredential as typeof PublicKeyCredential & {
        parseCreationOptionsFromJSON?: (options: unknown) => PublicKeyCredentialCreationOptions
      }
  const parsed = JSON.parse(json) as Record<string, any>
  if (typeof credentialType?.parseCreationOptionsFromJSON === 'function') {
    return credentialType.parseCreationOptionsFromJSON(parsed)
  }
  const options = parsed
  options.challenge = decodeBase64Url(options.challenge)
  options.user.id = decodeBase64Url(options.user.id)
  if (Array.isArray(options.excludeCredentials)) {
    options.excludeCredentials = options.excludeCredentials.map((credential: Record<string, any>) => ({
      ...credential,
      id: decodeBase64Url(credential.id),
    }))
  }
  return options as PublicKeyCredentialCreationOptions
}

export function parsePasskeyRequestOptions(json: string): PublicKeyCredentialRequestOptions {
  const credentialType = typeof window === 'undefined'
    ? undefined
    : window.PublicKeyCredential as typeof PublicKeyCredential & {
        parseRequestOptionsFromJSON?: (options: unknown) => PublicKeyCredentialRequestOptions
      }
  const parsed = JSON.parse(json) as Record<string, any>
  if (typeof credentialType?.parseRequestOptionsFromJSON === 'function') {
    return credentialType.parseRequestOptionsFromJSON(parsed)
  }
  const options = parsed
  options.challenge = decodeBase64Url(options.challenge)
  if (Array.isArray(options.allowCredentials)) {
    options.allowCredentials = options.allowCredentials.map((credential: Record<string, any>) => ({
      ...credential,
      id: decodeBase64Url(credential.id),
    }))
  }
  return options as PublicKeyCredentialRequestOptions
}

export function serializePasskeyCredential(credential: PublicKeyCredential): Record<string, unknown> {
  const response = credential.response
  const base = {
    id: credential.id,
    rawId: encodeBase64Url(credential.rawId),
    type: credential.type,
    authenticatorAttachment: credential.authenticatorAttachment,
    clientExtensionResults: credential.getClientExtensionResults(),
  }

  if ('attestationObject' in response) {
    const attestation = response as AuthenticatorAttestationResponse
    return {
      ...base,
      response: {
        attestationObject: encodeBase64Url(attestation.attestationObject),
        clientDataJSON: encodeBase64Url(attestation.clientDataJSON),
        transports: typeof attestation.getTransports === 'function' ? attestation.getTransports() : [],
      },
    }
  }

  const assertion = response as AuthenticatorAssertionResponse
  return {
    ...base,
    response: {
      authenticatorData: encodeBase64Url(assertion.authenticatorData),
      clientDataJSON: encodeBase64Url(assertion.clientDataJSON),
      signature: encodeBase64Url(assertion.signature),
      userHandle: assertion.userHandle ? encodeBase64Url(assertion.userHandle) : null,
    },
  }
}

function decodeBase64Url(value: string): ArrayBuffer {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')
  const bytes = Uint8Array.from(atob(padded), character => character.charCodeAt(0))
  return bytes.buffer
}

function encodeBase64Url(value: ArrayBuffer): string {
  const bytes = new Uint8Array(value)
  let binary = ''
  for (const byte of bytes) {
    binary += String.fromCharCode(byte)
  }
  return btoa(binary)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '')
}
