function base64UrlEncode(buffer: ArrayBuffer) {
  return btoa(String.fromCharCode(...new Uint8Array(buffer)))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

export async function generatePkce() {
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

export function persistPkce(verifier: string, state: string) {
  sessionStorage.setItem('pkce:verifier', verifier)
  sessionStorage.setItem('pkce:state', state)
}

export function consumePkce(state: string) {
  const storedState = sessionStorage.getItem('pkce:state')
  const verifier = sessionStorage.getItem('pkce:verifier')

  if (!storedState || !verifier || storedState !== state) {
    return null
  }

  sessionStorage.removeItem('pkce:state')
  sessionStorage.removeItem('pkce:verifier')

  return verifier
}

export function randomState() {
  return crypto.randomUUID().replace(/-/g, '')
}
