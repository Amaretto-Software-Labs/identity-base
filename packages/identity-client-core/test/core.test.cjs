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

test('createTokenStorage supports localStorage/sessionStorage and falls back on invalid type', async () => {
  const core = require('../dist/index.js')

  const local = core.createTokenStorage('localStorage')
  assert.equal(local.constructor.name, 'LocalStorageTokenStorage')
  assert.equal(local.getToken(), null)
  local.setToken('x')
  local.setRefreshToken('y')
  local.clear()

  const session = core.createTokenStorage('sessionStorage')
  assert.equal(session.constructor.name, 'SessionStorageTokenStorage')
  assert.equal(session.getToken(), null)
  session.setToken('x')
  session.setRefreshToken('y')
  session.clear()

  const fallback = core.createTokenStorage('bogus')
  assert.equal(fallback.constructor.name, 'LocalStorageTokenStorage')
})

test('createError flattens validation errors into message', async () => {
  const { createError } = require('../dist/index.js')
  const err = createError({
    status: 400,
    title: 'Validation failed',
    errors: { email: ['Required'], password: ['Too short', 'Missing symbol'] },
  })

  assert.equal(err.name, 'IdentityError')
  assert.equal(err.status, 400)
  assert.ok(err.message.includes('email: Required'))
  assert.ok(err.message.includes('password: Too short, Missing symbol'))
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

test('ApiClient.buildUrl appends params and omits undefined', async () => {
  const { ApiClient } = require('../dist/index.js')
  const client = new ApiClient({
    apiBase: 'https://identity.example.com',
    clientId: 'spa-client',
    redirectUri: 'https://app.example.com/auth/callback',
  })

  const url = client.buildUrl('/admin/users', {
    page: 2,
    search: ' alice ',
    locked: false,
    ignored: undefined,
  })

  const parsed = new URL(url)
  assert.equal(parsed.pathname, '/admin/users')
  assert.equal(parsed.searchParams.get('page'), '2')
  assert.equal(parsed.searchParams.get('search'), ' alice ')
  assert.equal(parsed.searchParams.get('locked'), 'false')
  assert.equal(parsed.searchParams.get('ignored'), null)
})

test('ApiClient.fetch returns undefined on 204 and supports text parsing', async () => {
  const { ApiClient } = require('../dist/index.js')
  const originalFetch = globalThis.fetch

  globalThis.fetch = async (url) => {
    const parsedUrl = typeof url === 'string' ? url : url.toString()
    if (parsedUrl.endsWith('/empty')) {
      return makeResponse({ status: 204 })
    }
    if (parsedUrl.endsWith('/empty-body')) {
      return makeResponse({ status: 200, text: '' })
    }
    if (parsedUrl.endsWith('/text')) {
      return makeResponse({ status: 200, text: 'hello' })
    }
    return makeResponse({ status: 404, json: { title: 'not found' } })
  }

  try {
    const client = new ApiClient({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      timeout: 250,
    })

    const empty = await client.fetch('/empty', { method: 'GET' })
    assert.equal(empty, undefined)

    const emptyBody = await client.fetch('/empty-body', { method: 'GET' })
    assert.equal(emptyBody, undefined)

    const text = await client.fetch('/text', { method: 'GET', parse: 'text' })
    assert.equal(text, 'hello')
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('ApiClient.fetch throws IdentityError when response JSON is invalid', async () => {
  const { ApiClient, IdentityError } = require('../dist/index.js')
  const originalFetch = globalThis.fetch

  globalThis.fetch = async () => makeResponse({ status: 200, text: 'not-json' })

  try {
    const client = new ApiClient({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      timeout: 250,
    })

    await assert.rejects(
      () => client.fetch('/whatever', { method: 'GET' }),
      (err) => {
        assert.ok(err instanceof IdentityError)
        assert.equal(err.message, 'not-json')
        return true
      },
    )
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('ApiClient.fetch converts error responses into IdentityError with status', async () => {
  const { ApiClient, IdentityError } = require('../dist/index.js')
  const originalFetch = globalThis.fetch

  globalThis.fetch = async () => makeResponse({
    status: 400,
    json: { title: 'Bad Request', detail: 'oops', errors: { email: ['invalid'] } },
  })

  try {
    const client = new ApiClient({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      timeout: 250,
    })

    await assert.rejects(
      () => client.fetch('/fail', { method: 'GET' }),
      (err) => {
        assert.ok(err instanceof IdentityError)
        assert.equal(err.status, 400)
        assert.ok(err.message.includes('email: invalid'))
        return true
      },
    )
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('ApiClient.fetch throws timeout IdentityError when request aborts', async () => {
  const { ApiClient, IdentityError } = require('../dist/index.js')
  const originalFetch = globalThis.fetch

  globalThis.fetch = async (_url, init) => {
    return await new Promise((_, reject) => {
      init.signal.addEventListener('abort', () => {
        const e = new Error('aborted')
        e.name = 'AbortError'
        reject(e)
      })
    })
  }

  try {
    const client = new ApiClient({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      timeout: 10,
    })

    await assert.rejects(
      () => client.fetch('/slow', { method: 'GET' }),
      (err) => {
        assert.ok(err instanceof IdentityError)
        assert.equal(err.message, 'Request timeout')
        return true
      },
    )
  } finally {
    globalThis.fetch = originalFetch
  }
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

test('TokenManager.exchangeAuthorizationCode posts form body and stores tokens', async () => {
  const { TokenManager } = require('../dist/index.js')
  const originalFetch = globalThis.fetch
  let lastBody = null

  globalThis.fetch = async (_url, init) => {
    lastBody = init.body
    return makeResponse({
      status: 200,
      json: { access_token: makeJwt({ exp: Math.floor(Date.now() / 1000) + 3600 }), refresh_token: 'r1', expires_in: 3600 },
    })
  }

  try {
    const tokenManager = new TokenManager({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'memory',
      autoRefresh: false,
    })

    const response = await tokenManager.exchangeAuthorizationCode('code123', 'verifier456')
    assert.equal(response.refresh_token, 'r1')
    assert.equal(tokenManager.getRefreshToken(), 'r1')
    assert.equal(typeof tokenManager.getAccessToken(), 'string')

    const params = new URLSearchParams(lastBody)
    assert.equal(params.get('grant_type'), 'authorization_code')
    assert.equal(params.get('code'), 'code123')
    assert.equal(params.get('code_verifier'), 'verifier456')
    assert.equal(params.get('client_id'), 'spa-client')
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('TokenManager clears expired tokens when autoRefresh is disabled', async () => {
  const { TokenManager } = require('../dist/index.js')
  const tokenManager = new TokenManager({
    apiBase: 'https://identity.example.com',
    clientId: 'spa-client',
    redirectUri: 'https://app.example.com/auth/callback',
    tokenStorage: 'memory',
    autoRefresh: false,
  })

  tokenManager.setTokens({
    access_token: makeJwt({ exp: Math.floor(Date.now() / 1000) - 10 }),
    refresh_token: 'r1',
    expires_in: 1,
  })

  const token = await tokenManager.ensureValidToken()
  assert.equal(token, null)
  assert.equal(tokenManager.getAccessToken(), null)
  assert.equal(tokenManager.getRefreshToken(), null)
})

test('TokenManager returns token when exp is missing', async () => {
  const { TokenManager } = require('../dist/index.js')
  const tokenManager = new TokenManager({
    apiBase: 'https://identity.example.com',
    clientId: 'spa-client',
    redirectUri: 'https://app.example.com/auth/callback',
    tokenStorage: 'memory',
    autoRefresh: true,
  })

  tokenManager.setTokens({ access_token: makeJwt({ sub: 'u1' }), refresh_token: 'r1', expires_in: 3600 })
  const token = await tokenManager.ensureValidToken()
  assert.equal(typeof token, 'string')
  assert.equal(token, tokenManager.getAccessToken())
})

test('TokenManager.refreshAccessToken throws when refresh token missing', async () => {
  const { TokenManager } = require('../dist/index.js')
  const tokenManager = new TokenManager({
    apiBase: 'https://identity.example.com',
    clientId: 'spa-client',
    redirectUri: 'https://app.example.com/auth/callback',
    tokenStorage: 'memory',
    autoRefresh: true,
  })

  await assert.rejects(() => tokenManager.refreshAccessToken(), /No refresh token available/)
})

test('TokenManager clears tokens when autoRefresh refresh fails', async () => {
  const { TokenManager } = require('../dist/index.js')
  const originalFetch = globalThis.fetch

  globalThis.fetch = async (url) => {
    const parsedUrl = typeof url === 'string' ? url : url.toString()
    if (parsedUrl === 'https://identity.example.com/connect/token') {
      return makeResponse({ status: 400, json: { title: 'Bad Request', detail: 'nope' } })
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

    tokenManager.setTokens({
      access_token: makeJwt({ exp: Math.floor(Date.now() / 1000) + 1 }),
      refresh_token: 'r1',
      expires_in: 1,
    })

    await assert.rejects(() => tokenManager.ensureValidToken())
    assert.equal(tokenManager.getAccessToken(), null)
    assert.equal(tokenManager.getRefreshToken(), null)
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('PKCEManager persists and consumes verifier only once', async () => {
  const { PKCEManager } = require('../dist/index.js')
  const originalSessionStorage = globalThis.sessionStorage

  const store = new Map()
  globalThis.sessionStorage = {
    getItem: (k) => (store.has(k) ? store.get(k) : null),
    setItem: (k, v) => store.set(k, String(v)),
    removeItem: (k) => store.delete(k),
  }

  try {
    const pkce = new PKCEManager()
    pkce.persistPkce('verifier', 'state')
    const v1 = pkce.consumePkce('state')
    const v2 = pkce.consumePkce('state')
    assert.equal(v1, 'verifier')
    assert.equal(v2, null)
    pkce.clearPkce()
  } finally {
    globalThis.sessionStorage = originalSessionStorage
  }
})

test('randomState returns a UUID without dashes', async () => {
  const { randomState } = require('../dist/index.js')
  const value = randomState()
  assert.equal(typeof value, 'string')
  assert.equal(value.includes('-'), false)
  assert.equal(value.length, 32)
})

test('enableDebugLogging toggles console logging', async () => {
  const { enableDebugLogging, debugLog } = require('../dist/index.js')

  const originalLog = console.log
  const seen = []
  console.log = (...args) => seen.push(args.join(' '))

  try {
    enableDebugLogging(false)
    debugLog('nope')
    enableDebugLogging(true)
    debugLog('hello', 'world')
    assert.equal(seen.length, 1)
    assert.ok(seen[0].includes('hello'))
  } finally {
    console.log = originalLog
  }
})

test('IdentityAuthManager supports auth code flow and authorized calls', async () => {
  const { IdentityAuthManager } = require('../dist/index.js')

  const originalFetch = globalThis.fetch
  const originalWindow = globalThis.window
  const originalSessionStorage = globalThis.sessionStorage

  const store = new Map()
  globalThis.sessionStorage = {
    getItem: (k) => (store.has(k) ? store.get(k) : null),
    setItem: (k, v) => store.set(k, String(v)),
    removeItem: (k) => store.delete(k),
  }

  const assigned = []
  globalThis.window = {
    location: {
      href: 'https://app.example.com/',
      assign: (url) => assigned.push(url),
    },
  }

  const calls = []
  globalThis.fetch = async (url, init = {}) => {
    const requestUrl = typeof url === 'string' ? url : url.toString()
    const { pathname, searchParams } = new URL(requestUrl)
    const method = (init.method || 'GET').toUpperCase()
    const headers = init.headers || {}
    const body = init.body
    calls.push({ method, pathname, search: searchParams.toString(), headers, body })

    if (pathname === '/connect/token' && method === 'POST') {
      const params = new URLSearchParams(body)
      if (params.get('grant_type') === 'authorization_code') {
        return makeResponse({
          status: 200,
          json: {
            access_token: makeJwt({ exp: Math.floor(Date.now() / 1000) + 3600 }),
            refresh_token: 'r1',
            expires_in: 3600,
          },
        })
      }
      return makeResponse({
        status: 200,
        json: {
          access_token: makeJwt({ exp: Math.floor(Date.now() / 1000) + 3600 }),
          refresh_token: 'r2',
          expires_in: 3600,
        },
      })
    }

    if (pathname === '/users/me' && method === 'GET') {
      return makeResponse({
        status: 200,
        json: { id: 'u1', email: 'alice@example.com', displayName: 'Alice', emailConfirmed: true, metadata: {}, concurrencyStamp: 'cs', createdAt: '', updatedAt: '' },
      })
    }

    if (pathname === '/auth/profile-schema' && method === 'GET') {
      return makeResponse({ status: 200, json: { fields: [] } })
    }

    if (pathname === '/auth/logout' && method === 'POST') {
      return makeResponse({ status: 200, json: { message: 'ok' } })
    }

    if (pathname === '/auth/mfa/enroll' && method === 'POST') {
      return makeResponse({ status: 200, json: { sharedKey: 'k', authenticatorUri: 'otpauth://totp/x' } })
    }

    if (pathname === '/auth/mfa/disable' && method === 'POST') {
      return makeResponse({ status: 200, json: { message: 'ok' } })
    }

    if (pathname === '/auth/mfa/recovery-codes' && method === 'POST') {
      return makeResponse({ status: 200, json: { recoveryCodes: ['c1', 'c2'] } })
    }

    if (pathname === '/users/me/profile' && method === 'PUT') {
      return makeResponse({
        status: 200,
        json: { id: 'u1', email: 'alice@example.com', displayName: 'Alice', emailConfirmed: true, metadata: {}, concurrencyStamp: 'cs2', createdAt: '', updatedAt: '' },
      })
    }

    if (pathname === '/users/me/permissions' && method === 'GET') {
      return makeResponse({ status: 200, json: { permissions: ['users.read', 'users.write'] } })
    }

    if (pathname === '/auth/external/google' && method === 'DELETE') {
      return makeResponse({ status: 200, json: { message: 'ok' } })
    }

    if (pathname === '/admin/users' && method === 'GET') {
      return makeResponse({ status: 200, json: { page: 1, pageSize: 25, totalCount: 0, items: [] } })
    }

    if (pathname === '/admin/roles' && method === 'GET') {
      return makeResponse({ status: 200, json: { page: 1, pageSize: 25, totalCount: 0, items: [] } })
    }

    if (pathname === '/admin/permissions' && method === 'GET') {
      return makeResponse({ status: 200, json: { page: 1, pageSize: 25, totalCount: 0, items: [] } })
    }

    if (pathname.startsWith('/admin/') && (method === 'POST' || method === 'DELETE' || method === 'PUT')) {
      return makeResponse({ status: 204 })
    }

    return makeResponse({ status: 200, json: {} })
  }

  try {
    const auth = new IdentityAuthManager({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'memory',
      autoRefresh: true,
      timeout: 250,
    })

    const events = []
    auth.addEventListener((e) => events.push(e.type))

    await auth.startAuthorization()
    assert.equal(assigned.length, 1)
    assert.ok(assigned[0].includes('/connect/authorize?'))

    const stored = JSON.parse(globalThis.sessionStorage.getItem('identity:pkce'))
    assert.ok(stored.state)
    assert.ok(stored.verifier)

    const user = await auth.handleAuthorizationCallback('code123', stored.state)
    assert.equal(user.id, 'u1')
    assert.ok(events.includes('token-refresh'))
    assert.ok(events.includes('login'))

    const perms = await auth.getUserPermissions()
    assert.deepEqual(perms, ['users.read', 'users.write'])

    const schema = await auth.getProfileSchema()
    assert.deepEqual(schema, { fields: [] })

    const updated = await auth.updateProfile({ metadata: {}, concurrencyStamp: 'cs' })
    assert.equal(updated.concurrencyStamp, 'cs2')

    const enrolled = await auth.enrollMfa()
    assert.equal(enrolled.sharedKey, 'k')

    const disabled = await auth.disableMfa()
    assert.equal(disabled.message, 'ok')

    const codes = await auth.regenerateRecoveryCodes()
    assert.deepEqual(codes.recoveryCodes, ['c1', 'c2'])

    const refreshedUser = await auth.refreshTokens()
    assert.equal(refreshedUser.id, 'u1')

    const external = auth.buildExternalStartUrl('google', 'login', 'https://app.example.com/return', { prompt: 'select_account' })
    const parsed = new URL(external)
    assert.equal(parsed.pathname, '/auth/external/google/start')
    assert.equal(parsed.searchParams.get('mode'), 'login')
    assert.equal(parsed.searchParams.get('returnUrl'), 'https://app.example.com/return')
    assert.equal(parsed.searchParams.get('prompt'), 'select_account')

    await auth.unlinkExternalProvider('google')

    await auth.admin.users.list({ page: 2, search: ' alice ', sort: ['createdAt:desc', 'email:asc'] })
    await auth.admin.roles.list({ page: 1, pageSize: 10 })
    await auth.admin.permissions.list({ page: 1, pageSize: 10 })

    await auth.logout()

    const listCall = calls.find(c => c.pathname === '/admin/users')
    assert.ok(listCall)
    assert.ok(listCall.search.includes('page=2'))
    assert.ok(listCall.search.includes('search=alice'))
    assert.ok(listCall.search.includes('sort=createdAt%3Adesc'))
    assert.ok(listCall.search.includes('sort=email%3Aasc'))

    const unlinkCall = calls.find(c => c.pathname === '/auth/external/google' && c.method === 'DELETE')
    assert.ok(unlinkCall)
    assert.ok(typeof unlinkCall.headers.Authorization === 'string')
    assert.ok(unlinkCall.headers.Authorization.startsWith('Bearer '))
  } finally {
    globalThis.fetch = originalFetch
    globalThis.window = originalWindow
    globalThis.sessionStorage = originalSessionStorage
  }
})

test('IdentityAuthManager login uses cookie flow when no token is present', async () => {
  const { IdentityAuthManager } = require('../dist/index.js')
  const originalFetch = globalThis.fetch

  const calls = []
  globalThis.fetch = async (url, init = {}) => {
    const requestUrl = typeof url === 'string' ? url : url.toString()
    const { pathname } = new URL(requestUrl)
    calls.push({ pathname, method: (init.method || 'GET').toUpperCase(), headers: init.headers })

    if (pathname === '/auth/login') {
      return makeResponse({ status: 200, json: { message: 'ok', requiresTwoFactor: false, clientId: 'spa-client' } })
    }

    if (pathname === '/users/me') {
      const headers = init.headers || {}
      assert.equal(headers.Authorization, undefined)
      return makeResponse({ status: 200, json: { id: 'u1', email: 'alice@example.com', displayName: 'Alice', emailConfirmed: true, metadata: {}, concurrencyStamp: 'cs', createdAt: '', updatedAt: '' } })
    }

    if (pathname === '/auth/external/google' && (init.method || 'GET').toUpperCase() === 'DELETE') {
      const headers = init.headers || {}
      assert.equal(headers.Authorization, undefined)
      return makeResponse({ status: 200, json: { message: 'ok' } })
    }

    return makeResponse({ status: 404, json: { title: 'Not found' } })
  }

  try {
    const auth = new IdentityAuthManager({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'memory',
      autoRefresh: false,
    })

    const events = []
    auth.addEventListener((e) => events.push(e.type))

    const response = await auth.login({ email: 'alice@example.com', password: 'pw' })
    assert.equal(response.message, 'ok')
    assert.ok(events.includes('login'))
    assert.ok(calls.some(c => c.pathname === '/users/me'))
    await auth.unlinkExternalProvider('google')
    assert.ok(calls.some(c => c.pathname === '/auth/external/google' && c.method === 'DELETE'))
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('IdentityAuthManager login does not fetch user when 2FA required', async () => {
  const { IdentityAuthManager } = require('../dist/index.js')
  const originalFetch = globalThis.fetch

  const calls = []
  globalThis.fetch = async (url, init = {}) => {
    const requestUrl = typeof url === 'string' ? url : url.toString()
    const { pathname } = new URL(requestUrl)
    calls.push({ pathname, method: (init.method || 'GET').toUpperCase() })

    if (pathname === '/auth/login') {
      return makeResponse({ status: 200, json: { message: 'ok', requiresTwoFactor: true, clientId: 'spa-client' } })
    }

    if (pathname === '/users/me') {
      return makeResponse({ status: 500, json: { title: 'should not call' } })
    }

    return makeResponse({ status: 404, json: { title: 'Not found' } })
  }

  try {
    const auth = new IdentityAuthManager({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'memory',
      autoRefresh: false,
    })

    const response = await auth.login({ email: 'alice@example.com', password: 'pw' })
    assert.equal(response.requiresTwoFactor, true)
    assert.equal(calls.some(c => c.pathname === '/users/me'), false)
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('IdentityAuthManager returns null for getCurrentUser on 401', async () => {
  const { IdentityAuthManager } = require('../dist/index.js')
  const originalFetch = globalThis.fetch

  globalThis.fetch = async (url) => {
    const requestUrl = typeof url === 'string' ? url : url.toString()
    const { pathname } = new URL(requestUrl)
    if (pathname === '/users/me') {
      return makeResponse({ status: 401, json: { title: 'Unauthorized' } })
    }
    return makeResponse({ status: 404, json: { title: 'Not found' } })
  }

  try {
    const auth = new IdentityAuthManager({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'memory',
      autoRefresh: false,
    })

    const user = await auth.getCurrentUser()
    assert.equal(user, null)
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('IdentityAuthManager exposes registration, password, MFA, and admin helpers', async () => {
  const { IdentityAuthManager } = require('../dist/index.js')
  const originalFetch = globalThis.fetch

  globalThis.fetch = async (url, init = {}) => {
    const requestUrl = typeof url === 'string' ? url : url.toString()
    const { pathname } = new URL(requestUrl)
    const method = (init.method || 'GET').toUpperCase()

    if (pathname === '/auth/register' && method === 'POST') {
      return makeResponse({ status: 200, json: { correlationId: 'c1' } })
    }

    if (pathname === '/auth/forgot-password' && method === 'POST') {
      return makeResponse({ status: 204 })
    }

    if (pathname === '/auth/reset-password' && method === 'POST') {
      return makeResponse({ status: 200, json: { message: 'ok' } })
    }

    if (pathname === '/auth/mfa/challenge' && method === 'POST') {
      return makeResponse({ status: 200, json: { message: 'sent' } })
    }

    if (pathname === '/auth/mfa/verify' && method === 'POST') {
      return makeResponse({ status: 200, json: { message: 'verified' } })
    }

    if (pathname === '/auth/mfa/enroll' && method === 'POST') {
      return makeResponse({ status: 200, json: { sharedKey: 'k', authenticatorUri: 'otpauth://totp/x' } })
    }

    if (pathname === '/auth/mfa/disable' && method === 'POST') {
      return makeResponse({ status: 200, json: { message: 'ok' } })
    }

    if (pathname === '/auth/mfa/recovery-codes' && method === 'POST') {
      return makeResponse({ status: 200, json: { recoveryCodes: ['c1', 'c2'] } })
    }

    if (pathname === '/users/me' && method === 'GET') {
      return makeResponse({ status: 200, json: { id: 'u1', email: 'alice@example.com', displayName: 'Alice', emailConfirmed: true, metadata: {}, concurrencyStamp: 'cs', createdAt: '', updatedAt: '' } })
    }

    if (pathname === '/admin/users' && method === 'POST') {
      return makeResponse({ status: 200, json: { id: 'u2', email: 'bob@example.com', displayName: 'Bob', emailConfirmed: false, metadata: {}, concurrencyStamp: 'cs2', createdAt: '', updatedAt: '' } })
    }

    if (pathname === '/admin/users/u2' && method === 'GET') {
      return makeResponse({ status: 200, json: { id: 'u2', email: 'bob@example.com', displayName: 'Bob', emailConfirmed: false, metadata: {}, concurrencyStamp: 'cs2', createdAt: '', updatedAt: '' } })
    }

    if (pathname === '/admin/users/u2' && method === 'PUT') {
      return makeResponse({ status: 200, json: { id: 'u2', email: 'bob@example.com', displayName: 'Bob2', emailConfirmed: false, metadata: {}, concurrencyStamp: 'cs3', createdAt: '', updatedAt: '' } })
    }

    if (pathname === '/admin/users' && method === 'GET') {
      return makeResponse({ status: 200, json: { page: 1, pageSize: 25, totalCount: 0, items: [] } })
    }

    if (pathname === '/admin/users/u2/lock' && method === 'POST') {
      return makeResponse({ status: 204 })
    }

    if (pathname === '/admin/users/u2/unlock' && method === 'POST') {
      return makeResponse({ status: 204 })
    }

    if (pathname === '/admin/users/u2/force-password-reset' && method === 'POST') {
      return makeResponse({ status: 204 })
    }

    if (pathname === '/admin/users/u2/reset-mfa' && method === 'POST') {
      return makeResponse({ status: 204 })
    }

    if (pathname === '/admin/users/u2/resend-confirmation' && method === 'POST') {
      return makeResponse({ status: 204 })
    }

    if (pathname.startsWith('/admin/users/') && pathname.endsWith('/roles') && method === 'GET') {
      return makeResponse({ status: 200, json: { roleIds: [] } })
    }

    if (pathname === '/admin/users/u2/roles' && method === 'PUT') {
      return makeResponse({ status: 204 })
    }

    if (pathname === '/admin/users/u2' && method === 'DELETE') {
      return makeResponse({ status: 204 })
    }

    if (pathname === '/admin/users/u2/restore' && method === 'POST') {
      return makeResponse({ status: 204 })
    }

    if (pathname.startsWith('/admin/roles') && method === 'GET') {
      return makeResponse({ status: 200, json: { page: 1, pageSize: 25, totalCount: 0, items: [] } })
    }

    if (pathname === '/admin/roles' && method === 'POST') {
      return makeResponse({ status: 200, json: { id: 'r1', name: 'Role', description: null, isSystemRole: false, createdAtUtc: '', updatedAtUtc: null } })
    }

    if (pathname === '/admin/permissions' && method === 'GET') {
      return makeResponse({ status: 200, json: { page: 1, pageSize: 25, totalCount: 0, items: [] } })
    }

    if (pathname === '/admin/roles/r1' && method === 'PUT') {
      return makeResponse({ status: 200, json: { id: 'r1', name: 'Role2', description: null, isSystemRole: false, createdAtUtc: '', updatedAtUtc: null } })
    }

    if (pathname === '/admin/roles/r1' && method === 'DELETE') {
      return makeResponse({ status: 204 })
    }

    if (method === 'DELETE' || method === 'PUT' || method === 'POST') {
      return makeResponse({ status: 204 })
    }

    return makeResponse({ status: 404, json: { title: 'Not found' } })
  }

  try {
    const auth = new IdentityAuthManager({
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'memory',
      autoRefresh: false,
    })

    // Seed a token so authorized calls attach a bearer token.
    auth.tokenManager.setTokens({
      access_token: makeJwt({ exp: Math.floor(Date.now() / 1000) + 3600 }),
      refresh_token: 'r1',
      expires_in: 3600,
    })

    assert.equal(auth.isAuthenticated(), true)
    assert.equal(typeof (await auth.getAccessToken()), 'string')

    const register = await auth.register({ email: 'alice@example.com', password: 'pw', profile: { displayName: 'Alice' } })
    assert.equal(register.correlationId, 'c1')

    await auth.requestPasswordReset('alice@example.com')
    const reset = await auth.resetPassword({ email: 'alice@example.com', token: 't', password: 'pw' })
    assert.equal(reset.message, 'ok')

    const challenge = await auth.sendMfaChallenge({ method: 'email', clientId: 'spa-client' })
    assert.equal(challenge.message, 'sent')
    const verified = await auth.verifyMfa({ method: 'email', code: '123456', clientId: 'spa-client' })
    assert.equal(verified.message, 'verified')

    const enrolled = await auth.enrollMfa()
    assert.equal(enrolled.sharedKey, 'k')
    const disabled = await auth.disableMfa()
    assert.equal(disabled.message, 'ok')
    const codes = await auth.regenerateRecoveryCodes()
    assert.deepEqual(codes.recoveryCodes, ['c1', 'c2'])

    // Exercise admin namespaces.
    await auth.admin.users.list({ page: 1 })
    await auth.admin.users.create({ email: 'bob@example.com', password: 'pw', displayName: 'Bob' })
    await auth.admin.users.get('u2')
    await auth.admin.users.update('u2', { displayName: 'Bob2' })
    await auth.admin.users.lock('u2', { untilUtc: null })
    await auth.admin.users.unlock('u2')
    await auth.admin.users.forcePasswordReset('u2')
    await auth.admin.users.resetMfa('u2')
    await auth.admin.users.resendConfirmation('u2')
    await auth.admin.users.getRoles('u2')
    await auth.admin.users.updateRoles('u2', { roleIds: [] })
    await auth.admin.users.softDelete('u2')
    await auth.admin.users.restore('u2')

    await auth.admin.roles.list({ page: 1 })
    await auth.admin.roles.create({ name: 'Role' })
    await auth.admin.roles.update('r1', { name: 'Role2' })
    await auth.admin.roles.delete('r1')

    await auth.admin.permissions.list({ page: 1 })
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('Logger attaches window debug flags when loaded in browser context', async () => {
  const originalWindow = globalThis.window
  globalThis.window = { location: { href: 'https://app.example.com/' } }

  try {
    const corePath = require.resolve('../dist/index.js')
    delete require.cache[corePath]
    const { enableDebugLogging } = require('../dist/index.js')

    assert.equal(typeof globalThis.window.__enableIdentityDebug, 'function')
    enableDebugLogging(true)
    assert.equal(globalThis.window.__identityDebugEnabled, true)
  } finally {
    globalThis.window = originalWindow
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
