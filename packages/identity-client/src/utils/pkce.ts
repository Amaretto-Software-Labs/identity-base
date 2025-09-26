import type { PKCEPair } from '../core/types'

function base64UrlEncode(buffer: ArrayBuffer): string {
  return btoa(String.fromCharCode(...new Uint8Array(buffer)))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

export async function generatePkce(): Promise<PKCEPair> {
  const array = crypto.getRandomValues(new Uint8Array(32))
  const verifier = Array.from(array, (byte) => ('0' + byte.toString(16)).slice(-2)).join('')

  const encoder = new TextEncoder()
  const data = encoder.encode(verifier)
  const digest = await crypto.subtle.digest('SHA-256', data)
  const challenge = base64UrlEncode(digest)

  return {
    verifier,
    challenge,
  }
}

export function randomState(): string {
  return crypto.randomUUID().replace(/-/g, '')
}

export class PKCEManager {
  private storageKey = 'identity:pkce'

  persistPkce(verifier: string, state: string): void {
    try {
      sessionStorage.setItem(this.storageKey, JSON.stringify({ verifier, state }))
    } catch {
      // Silently fail if sessionStorage is not available
    }
  }

  consumePkce(state: string): string | null {
    try {
      const stored = sessionStorage.getItem(this.storageKey)
      if (!stored) return null

      const { verifier, state: storedState } = JSON.parse(stored)

      if (!storedState || !verifier || storedState !== state) {
        return null
      }

      sessionStorage.removeItem(this.storageKey)
      return verifier
    } catch {
      return null
    }
  }

  clearPkce(): void {
    try {
      sessionStorage.removeItem(this.storageKey)
    } catch {
      // Silently fail if sessionStorage is not available
    }
  }
}