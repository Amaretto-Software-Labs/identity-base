const test = require('node:test')
const assert = require('node:assert/strict')

function base64UrlEncodeJson(value) {
  const json = JSON.stringify(value)
  return Buffer.from(json, 'utf8')
    .toString('base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
}

function makeJwt(payload) {
  const header = base64UrlEncodeJson({ alg: 'none', typ: 'JWT' })
  const body = base64UrlEncodeJson(payload)
  return `${header}.${body}.`
}

function makeResponse({ status = 200, json = null, text = null } = {}) {
  const ok = status >= 200 && status < 300
  return {
    ok,
    status,
    async json() {
      if (json === null) throw new Error('no json')
      return json
    },
    async text() {
      if (text !== null) return text
      if (json === null) return ''
      return JSON.stringify(json)
    },
  }
}

test('createTokenStorage defaults to sessionStorage implementation', async () => {
  const core = require('../dist/index.js')
  const storage = core.createTokenStorage()
  assert.equal(storage.constructor.name, 'SessionStorageTokenStorage')
  assert.equal(storage.getToken(), null)
})

test('MemoryTokenStorage stores access and refresh tokens', async () => {
  const core = require('../dist/index.js')
  const storage = core.createTokenStorage('memory')
  assert.equal(storage.getToken(), null)
  assert.equal(storage.getRefreshToken(), null)
  storage.setToken('a')
  storage.setRefreshToken('r')
  assert.equal(storage.getToken(), 'a')
  assert.equal(storage.getRefreshToken(), 'r')
  storage.clear()
  assert.equal(storage.getToken(), null)
  assert.equal(storage.getRefreshToken(), null)
})

test('ApiClient.buildAuthorizationUrl includes PKCE and state', async () => {
  const { ApiClient } = require('../dist/index.js')
  const client = new ApiClient({
    apiBase: 'https://identity.example.com',
    clientId: 'spa-client',
    redirectUri: 'https://app.example.com/auth/callback',
    scope: 'openid profile',
  })

  const url = client.buildAuthorizationUrl('challenge123', 'state456')
  const parsed = new URL(url)
  assert.equal(parsed.origin, 'https://identity.example.com')
  assert.equal(parsed.pathname, '/connect/authorize')
  assert.equal(parsed.searchParams.get('client_id'), 'spa-client')
  assert.equal(parsed.searchParams.get('redirect_uri'), 'https://app.example.com/auth/callback')
  assert.equal(parsed.searchParams.get('scope'), 'openid profile')
  assert.equal(parsed.searchParams.get('code_challenge'), 'challenge123')
  assert.equal(parsed.searchParams.get('code_challenge_method'), 'S256')
  assert.equal(parsed.searchParams.get('state'), 'state456')
})

test('TokenManager coalesces concurrent refreshes', async () => {
  const { TokenManager } = require('../dist/index.js')
  const originalFetch = globalThis.fetch
  let refreshCalls = 0

  globalThis.fetch = async (url, init) => {
    const parsedUrl = typeof url === 'string' ? url : url.toString()
    if (parsedUrl === 'https://identity.example.com/connect/token') {
      refreshCalls += 1
      assert.equal(init.method, 'POST')
      const body = init.body
      assert.equal(typeof body, 'string')
      const params = new URLSearchParams(body)
      assert.equal(params.get('grant_type'), 'refresh_token')
      return makeResponse({
        status: 200,
        json: { access_token: makeJwt({ exp: Math.floor(Date.now() / 1000) + 3600 }), refresh_token: 'r2', expires_in: 3600 },
      })
    }

    return makeResponse({ status: 404, json: { title: 'Not found' } })
  }

  try {
    const tokenManager = new TokenManager({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'memory',
      autoRefresh: true,
    })

    const soonExpiring = makeJwt({ exp: Math.floor(Date.now() / 1000) + 1 })
    tokenManager.setTokens({ access_token: soonExpiring, refresh_token: 'r1', expires_in: 1 })

    const [t1, t2] = await Promise.all([tokenManager.ensureValidToken(), tokenManager.ensureValidToken()])
    assert.equal(typeof t1, 'string')
    assert.equal(t1, t2)
    assert.equal(refreshCalls, 1)
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('IdentityAuthManager.getUserPermissions returns [] on 404', async () => {
  const { IdentityAuthManager } = require('../dist/index.js')
  const originalFetch = globalThis.fetch

  globalThis.fetch = async (url) => {
    const parsedUrl = typeof url === 'string' ? url : url.toString()
    if (parsedUrl === 'https://identity.example.com/users/me/permissions') {
      return makeResponse({ status: 404, json: { title: 'Not found' } })
    }
    return makeResponse({ status: 404, json: { title: 'Not found' } })
  }

  try {
    const auth = new IdentityAuthManager({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'memory',
      autoRefresh: true,
    })

    const perms = await auth.getUserPermissions()
    assert.deepEqual(perms, [])
  } finally {
    globalThis.fetch = originalFetch
  }
})

