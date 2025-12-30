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
