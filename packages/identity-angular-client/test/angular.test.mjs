import test from 'node:test'
import assert from 'node:assert/strict'

import { firstValueFrom, of } from 'rxjs'

import {
  IDENTITY_CLIENT_CONFIG,
  IdentityAuthInterceptor,
  IdentityAdminService,
  IdentityAuthService,
  provideIdentityClient,
} from '../dist/index.mjs'

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
