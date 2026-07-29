import '@angular/compiler'

import test from 'node:test'
import assert from 'node:assert/strict'

import { firstValueFrom, of } from 'rxjs'

import {
  IDENTITY_AUTH_MANAGER,
  IDENTITY_CLIENT_CONFIG,
  IdentityAuthInterceptor,
  IdentityAdminService,
  IdentityAuthService,
  IdentityPasskeyService,
  provideIdentityClient,
} from '../dist/fesm2022/identity-base-angular-client.mjs'

test('provideIdentityClient applies defaults', () => {
  const providers = provideIdentityClient({
    apiBase: 'https://identity.example.com',
    clientId: 'spa-client',
    redirectUri: 'https://app.example.com/auth/callback',
  })

  assert.ok(Array.isArray(providers))

  const configProvider = providers.find(p => p?.provide === IDENTITY_CLIENT_CONFIG)
  assert.ok(configProvider, 'Expected IDENTITY_CLIENT_CONFIG provider')
  assert.equal(configProvider.useValue.tokenStorage, 'sessionStorage')
  assert.equal(configProvider.useValue.autoRefresh, true)
})

test('provideIdentityClient factory creates IdentityAuthManager from normalized config', () => {
  const providers = provideIdentityClient({
    apiBase: 'https://identity.example.com',
    clientId: 'spa-client',
    redirectUri: 'https://app.example.com/auth/callback',
    autoRefresh: false,
  })

  const configProvider = providers.find(p => p?.provide === IDENTITY_CLIENT_CONFIG)
  const authManagerProvider = providers.find(p => p?.provide === IDENTITY_AUTH_MANAGER)

  assert.ok(configProvider)
  assert.ok(authManagerProvider)
  assert.equal(typeof authManagerProvider.useFactory, 'function')

  const authManager = authManagerProvider.useFactory(configProvider.useValue)
  assert.equal(typeof authManager.getAccessToken, 'function')
  assert.equal(typeof authManager.getCurrentUser, 'function')
})

test('IdentityAdminService forwards admin namespaces', () => {
  const authManager = {
    admin: {
      users: { list: () => 'u' },
      roles: { list: () => 'r' },
      permissions: { list: () => 'p' },
    },
  }

  const service = new IdentityAdminService(authManager)
  assert.equal(service.users.list(), 'u')
  assert.equal(service.roles.list(), 'r')
  assert.equal(service.permissions.list(), 'p')
})

test('IdentityAuthInterceptor attaches bearer token for apiBase requests', async () => {
  const auth = { getAccessToken: async () => 'token123' }
  const config = { apiBase: 'https://identity.example.com' }
  const interceptor = new IdentityAuthInterceptor(auth, config)

  let seenAuthHeader = null
  const req = {
    url: 'https://identity.example.com/users/me',
    headers: { has: () => false },
    clone: ({ setHeaders }) => {
      seenAuthHeader = setHeaders.Authorization
      return req
    },
  }

  const next = { handle: () => of('ok') }
  await firstValueFrom(interceptor.intercept(req, next))
  assert.equal(seenAuthHeader, 'Bearer token123')
})

test('IdentityAuthInterceptor does not attach token when excluded', async () => {
  const auth = { getAccessToken: async () => 'token123' }
  const config = {
    apiBase: 'https://identity.example.com',
    tokenAttachment: { exclude: ['https://identity.example.com/auth/'] },
  }
  const interceptor = new IdentityAuthInterceptor(auth, config)

  let cloned = false
  const req = {
    url: 'https://identity.example.com/auth/login',
    headers: { has: () => false },
    clone: () => {
      cloned = true
      return req
    },
  }

  const next = { handle: () => of('ok') }
  await firstValueFrom(interceptor.intercept(req, next))
  assert.equal(cloned, false)
})

test('IdentityAuthInterceptor leaves requests with Authorization header untouched', async () => {
  const auth = { getAccessToken: async () => 'token123' }
  const config = { apiBase: 'https://identity.example.com' }
  const interceptor = new IdentityAuthInterceptor(auth, config)

  let cloneCalled = false
  const req = {
    url: 'https://identity.example.com/users/me',
    headers: { has: name => name === 'Authorization' },
    clone: () => {
      cloneCalled = true
      return req
    },
  }

  const next = { handle: () => of('ok') }
  await firstValueFrom(interceptor.intercept(req, next))
  assert.equal(cloneCalled, false)
})

test('IdentityAuthInterceptor respects include rules (RegExp and function)', async () => {
  const auth = { getAccessToken: async () => 'token123' }
  const config = {
    apiBase: 'https://identity.example.com',
    tokenAttachment: {
      include: [
        /\/users\//,
        url => url.endsWith('/special'),
      ],
    },
  }
  const interceptor = new IdentityAuthInterceptor(auth, config)

  let seenAuthHeader = null
  const req = {
    url: 'https://identity.example.com/users/special',
    headers: { has: () => false },
    clone: ({ setHeaders }) => {
      seenAuthHeader = setHeaders.Authorization
      return req
    },
  }

  const next = { handle: () => of('ok') }
  await firstValueFrom(interceptor.intercept(req, next))
  assert.equal(seenAuthHeader, 'Bearer token123')
})

test('IdentityAuthInterceptor skips token attachment when include rules do not match', async () => {
  const auth = { getAccessToken: async () => { throw new Error('should not be called') } }
  const config = { apiBase: 'https://identity.example.com', tokenAttachment: { include: ['https://other.example.com/'] } }
  const interceptor = new IdentityAuthInterceptor(auth, config)

  let cloneCalled = false
  const req = {
    url: 'https://identity.example.com/users/me',
    headers: { has: () => false },
    clone: () => {
      cloneCalled = true
      return req
    },
  }

  const next = { handle: () => of('ok') }
  await firstValueFrom(interceptor.intercept(req, next))
  assert.equal(cloneCalled, false)
})

test('IdentityAuthService.snapshot reflects current user state', async () => {
  const authManager = {
    addEventListener: () => () => {},
    isAuthenticated: () => false,
    getCurrentUser: async () => ({ id: '1', email: 'a', displayName: 'b', emailConfirmed: true, metadata: {}, concurrencyStamp: 'c', createdAt: '', updatedAt: '' }),
    getAccessToken: async () => null,
    logout: async () => {},
    login: async () => ({ message: 'ok', clientId: 'spa-client' }),
    register: async () => ({ correlationId: 'x' }),
    startAuthorization: async () => {},
    handleAuthorizationCallback: async () => ({ id: '1', email: 'a', displayName: 'b', emailConfirmed: true, metadata: {}, concurrencyStamp: 'c', createdAt: '', updatedAt: '' }),
  }

  const service = new IdentityAuthService(authManager)
  await service.init()
  assert.equal(service.snapshot.user?.id, '1')
  assert.equal(service.snapshot.isAuthenticated, true)
})

test('IdentityAuthService refreshUser surfaces errors and resets loading state', async () => {
  const boom = new Error('boom')
  const authManager = {
    addEventListener: () => () => {},
    isAuthenticated: () => false,
    getCurrentUser: async () => { throw boom },
    getAccessToken: async () => null,
    logout: async () => {},
    login: async () => ({ message: 'ok', clientId: 'spa-client' }),
    register: async () => ({ correlationId: 'x' }),
    startAuthorization: async () => {},
    handleAuthorizationCallback: async () => ({ id: '1', email: 'a', displayName: 'b', emailConfirmed: true, metadata: {}, concurrencyStamp: 'c', createdAt: '', updatedAt: '' }),
  }

  const service = new IdentityAuthService(authManager)

  await assert.rejects(() => service.refreshUser(), err => err === boom)
  assert.equal(service.snapshot.isLoading, false)
  assert.equal(service.snapshot.error, boom)
})

test('IdentityAuthService requires a browser for auth code redirects', async () => {
  const authManager = {
    addEventListener: () => () => {},
    isAuthenticated: () => false,
    getCurrentUser: async () => null,
    getAccessToken: async () => null,
    logout: async () => {},
    login: async () => ({ message: 'ok', clientId: 'spa-client' }),
    register: async () => ({ correlationId: 'x' }),
    startAuthorization: async () => {},
    handleAuthorizationCallback: async () => ({ id: '1', email: 'a', displayName: 'b', emailConfirmed: true, metadata: {}, concurrencyStamp: 'c', createdAt: '', updatedAt: '' }),
  }

  const service = new IdentityAuthService(authManager)
  await assert.rejects(() => service.startAuthorization(), /requires a browser/)
  await assert.rejects(() => service.handleAuthorizationCallback('code', 'state'), /requires a browser/)
})

test('IdentityAuthService can run auth code flow in a browser environment', async () => {
  const originalWindow = globalThis.window
  try {
    globalThis.window = {}

    let started = false
    const authManager = {
      addEventListener: () => () => {},
      isAuthenticated: () => false,
      getCurrentUser: async () => null,
      getAccessToken: async () => null,
      logout: async () => {},
      login: async () => ({ message: 'ok', clientId: 'spa-client' }),
      register: async () => ({ correlationId: 'x' }),
      startAuthorization: async () => { started = true },
      handleAuthorizationCallback: async () => ({ id: '1', email: 'a', displayName: 'b', emailConfirmed: true, metadata: {}, concurrencyStamp: 'c', createdAt: '', updatedAt: '' }),
    }

    const service = new IdentityAuthService(authManager)
    await service.startAuthorization()
    assert.equal(started, true)

    const user = await service.handleAuthorizationCallback('code', 'state')
    assert.equal(user.id, '1')
    assert.equal(service.snapshot.user?.id, '1')
  } finally {
    if (originalWindow === undefined) {
      delete globalThis.window
    } else {
      globalThis.window = originalWindow
    }
  }
})

test('Angular passkey services forward every supported passkey operation', async () => {
  const originalWindow = globalThis.window
  const calls = []
  const user = {
    id: '1',
    email: 'alice@example.com',
    displayName: 'Alice',
    emailConfirmed: true,
    metadata: {},
    concurrencyStamp: 'user-stamp',
    createdAt: '',
    updatedAt: '',
  }
  const passkey = {
    id: 'credential',
    name: 'Laptop',
    createdAt: '',
    transports: ['internal'],
    isBackupEligible: true,
    isBackedUp: true,
    concurrencyStamp: 'passkey-stamp',
  }
  const authManager = {
    addEventListener: () => () => {},
    isAuthenticated: () => false,
    getCurrentUser: async () => user,
    isPasskeySupported: () => true,
    isConditionalMediationAvailable: async () => true,
    getPasskeyConfiguration: async () => ({ enabled: true }),
    loginWithPasskey: async options => {
      calls.push(['login', options])
      return { message: 'ok', clientId: 'spa-client' }
    },
    beginPasskeySignup: async request => {
      calls.push(['begin-signup', request])
      return { correlationId: 'signup' }
    },
    confirmPasskeySignupEmail: async request => {
      calls.push(['confirm-signup', request])
      return { registrationMode: 'passwordless' }
    },
    completePasskeySignup: async request => {
      calls.push(['complete-signup', request])
      return { message: 'registered', clientId: 'spa-client' }
    },
    listPasskeys: async () => [passkey],
    registerPasskey: async name => {
      calls.push(['register', name])
      return passkey
    },
    createPasskey: async name => {
      calls.push(['create', name])
      return passkey
    },
    renamePasskey: async (id, name, stamp) => {
      calls.push(['rename', id, name, stamp])
      return { ...passkey, name }
    },
    removePasskey: async id => {
      calls.push(['remove', id])
    },
    beginPasskeyRecovery: async request => {
      calls.push(['begin-recovery', request])
      return { correlationId: 'recovery' }
    },
    confirmPasskeyRecoveryEmail: async request => {
      calls.push(['confirm-recovery', request])
    },
    completePasskeyRecovery: async request => {
      calls.push(['complete-recovery', request])
      return { message: 'recovered', clientId: 'spa-client', recovered: true }
    },
  }

  try {
    globalThis.window = {}
    const authService = new IdentityAuthService(authManager)
    assert.equal(authService.isPasskeySupported(), true)
    assert.equal(await authService.isConditionalMediationAvailable(), true)
    assert.equal((await authService.loginWithPasskey({ mediation: 'conditional' })).message, 'ok')
    assert.equal((await authService.beginPasskeySignup({
      mode: 'passwordless',
      email: 'alice@example.com',
    })).correlationId, 'signup')
    assert.equal((await authService.confirmPasskeySignupEmail({
      draftId: 'draft',
      token: 'token',
    })).registrationMode, 'passwordless')
    assert.equal((await authService.completePasskeySignup({
      draftId: 'draft',
      name: 'Laptop',
    })).message, 'registered')
    assert.equal((await authService.listPasskeys()).length, 1)
    assert.equal((await authService.registerPasskey('Laptop')).id, 'credential')
    assert.equal((await authService.renamePasskey(passkey, 'Renamed')).name, 'Renamed')
    await authService.removePasskey('credential')
    assert.equal((await authService.beginPasskeyRecovery({
      email: 'alice@example.com',
    })).correlationId, 'recovery')
    await authService.confirmPasskeyRecoveryEmail({ draftId: 'recovery', token: 'token' })
    assert.equal((await authService.completePasskeyRecovery({
      draftId: 'recovery',
      name: 'Replacement',
    })).recovered, true)

    const passkeyService = new IdentityPasskeyService(authManager)
    assert.equal(passkeyService.isSupported(), true)
    assert.equal((await passkeyService.getConfiguration()).enabled, true)
    assert.equal((await passkeyService.login()).message, 'ok')
    assert.equal((await passkeyService.beginSignup({
      mode: 'passwordless',
      email: 'alice@example.com',
    })).correlationId, 'signup')
    assert.equal((await passkeyService.confirmSignupEmail({
      draftId: 'draft',
      token: 'token',
    })).registrationMode, 'passwordless')
    assert.equal((await passkeyService.completeSignup({
      draftId: 'draft',
      name: 'Laptop',
    })).message, 'registered')
    assert.equal((await passkeyService.list()).length, 1)
    assert.equal((await passkeyService.create('Laptop')).id, 'credential')
    assert.equal((await passkeyService.rename(passkey, 'Renamed')).name, 'Renamed')
    await passkeyService.remove('credential')
    assert.equal((await passkeyService.beginRecovery({
      email: 'alice@example.com',
    })).correlationId, 'recovery')
    await passkeyService.confirmRecoveryEmail({ draftId: 'recovery', token: 'token' })
    assert.equal((await passkeyService.completeRecovery({
      draftId: 'recovery',
      name: 'Replacement',
    })).recovered, true)
    assert.ok(calls.length >= 20)
  } finally {
    if (originalWindow === undefined) {
      delete globalThis.window
    } else {
      globalThis.window = originalWindow
    }
  }
})
