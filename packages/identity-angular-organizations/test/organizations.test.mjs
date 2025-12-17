import test from 'node:test'
import assert from 'node:assert/strict'

import { firstValueFrom, of } from 'rxjs'

import {
  ActiveOrganizationService,
  IDENTITY_ORGANIZATIONS_CONFIG,
  OrganizationContextInterceptor,
  OrganizationsService,
  provideIdentityOrganizations,
} from '../dist/index.mjs'

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

test('provideIdentityOrganizations applies defaults', () => {
  const providers = provideIdentityOrganizations({ apiBase: 'https://identity.example.com' })
  assert.ok(Array.isArray(providers))

  const configProvider = providers.find(p => p?.provide === IDENTITY_ORGANIZATIONS_CONFIG)
  assert.ok(configProvider, 'Expected IDENTITY_ORGANIZATIONS_CONFIG provider')
  assert.equal(configProvider.useValue.timeoutMs, 10000)
  assert.equal(configProvider.useValue.organizationHeader.headerName, 'X-Organization-Id')
})

test('ActiveOrganizationService stores active org id', () => {
  const svc = new ActiveOrganizationService()
  assert.equal(svc.organizationId, null)
  svc.setOrganizationId('org-1')
  assert.equal(svc.organizationId, 'org-1')
  svc.clear()
  assert.equal(svc.organizationId, null)
})

test('OrganizationContextInterceptor attaches X-Organization-Id', async () => {
  const active = new ActiveOrganizationService()
  active.setOrganizationId('org-1')

  const interceptor = new OrganizationContextInterceptor(active, {
    apiBase: 'https://identity.example.com',
    organizationHeader: { headerName: 'X-Organization-Id' },
  })

  let headerValue = null
  const req = {
    url: 'https://identity.example.com/users/me/permissions',
    headers: { has: () => false },
    clone: ({ setHeaders }) => {
      headerValue = setHeaders['X-Organization-Id']
      return req
    },
  }

  const next = { handle: () => of('ok') }
  await firstValueFrom(interceptor.intercept(req, next))
  assert.equal(headerValue, 'org-1')
})

test('OrganizationsService attaches bearer token and org header on requests', async () => {
  const originalFetch = globalThis.fetch
  try {
    globalThis.fetch = async (url, init) => {
      const parsed = new URL(url)
      assert.equal(parsed.origin, 'https://identity.example.com')
      assert.equal(parsed.pathname, '/users/me/organizations')
      assert.equal(parsed.searchParams.get('page'), '1')
      assert.equal(parsed.searchParams.get('pageSize'), '10')
      assert.equal(init.method, 'GET')
      assert.equal(init.credentials, 'include')
      const auth = init.headers.get('Authorization')
      assert.equal(auth, 'Bearer token123')
      assert.equal(init.headers.get('X-Organization-Id'), 'org-1')
      return makeResponse({ status: 200, json: { page: 1, pageSize: 10, totalCount: 0, items: [] } })
    }

    const active = new ActiveOrganizationService()
    active.setOrganizationId('org-1')

    const auth = { getAccessToken: async () => 'token123' }

    const service = new OrganizationsService(auth, active, {
      apiBase: 'https://identity.example.com',
      organizationHeader: { headerName: 'X-Organization-Id' },
    })

    const result = await service.user.organizations.list({ page: 1, pageSize: 10 })
    assert.equal(result.page, 1)
    assert.equal(result.totalCount, 0)
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('OrganizationsService.invitations.preview does not require auth', async () => {
  const originalFetch = globalThis.fetch
  try {
    globalThis.fetch = async (url, init) => {
      assert.equal(url, 'https://identity.example.com/invitations/abc')
      assert.equal(init.method, 'GET')
      assert.equal(init.headers.get('Authorization'), null)
      return makeResponse({ status: 200, json: { code: 'abc', organizationSlug: 'o', organizationName: 'Org', expiresAtUtc: '2025-01-01T00:00:00Z' } })
    }

    const active = new ActiveOrganizationService()
    const auth = { getAccessToken: async () => 'token123' }
    const service = new OrganizationsService(auth, active, { apiBase: 'https://identity.example.com' })
    const preview = await service.invitations.preview('abc')
    assert.equal(preview.code, 'abc')
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('OrganizationContextInterceptor respects exclude rules', async () => {
  const active = new ActiveOrganizationService()
  active.setOrganizationId('org-1')

  const interceptor = new OrganizationContextInterceptor(active, {
    apiBase: 'https://identity.example.com',
    organizationHeader: { headerName: 'X-Organization-Id', exclude: ['https://identity.example.com/connect/'] },
  })

  let cloned = false
  const req = {
    url: 'https://identity.example.com/connect/authorize',
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

test('OrganizationsService methods encode ids, build query strings, and honor org-header include/exclude', async () => {
  const originalFetch = globalThis.fetch

  const calls = []

  function json(status, payload) {
    return makeResponse({ status, json: payload })
  }

  try {
    globalThis.fetch = async (url, init) => {
      const parsed = new URL(url)
      calls.push({
        pathname: parsed.pathname,
        search: parsed.search,
        method: init.method,
        auth: init.headers.get('Authorization'),
        org: init.headers.get('X-Organization-Id'),
        body: init.body,
      })

      if (parsed.pathname === '/admin/organizations') {
        return json(200, { page: 1, pageSize: 25, totalCount: 0, items: [] })
      }

      if (parsed.pathname === '/users/me/organizations/org%201') {
        return json(200, { id: 'org 1', slug: 'org1', displayName: 'Org', status: 'active', metadata: {}, createdAtUtc: '', updatedAtUtc: null, archivedAtUtc: null, tenantId: null })
      }

      if (parsed.pathname === '/admin/organizations/org%201/members' && init.method === 'GET') {
        return json(200, { page: 1, pageSize: 25, totalCount: 0, items: [] })
      }

      if (init.method === 'DELETE') {
        return makeResponse({ status: 204 })
      }

      return json(200, {})
    }

    const active = new ActiveOrganizationService()
    active.setOrganizationId('org-1')

    const auth = { getAccessToken: async () => 'token123' }

    const service = new OrganizationsService(auth, active, {
      apiBase: 'https://identity.example.com',
      organizationHeader: {
        headerName: 'X-Organization-Id',
        exclude: ['https://identity.example.com/connect/', 'https://identity.example.com/auth/'],
        include: ['https://identity.example.com/admin/', 'https://identity.example.com/users/'],
      },
      timeoutMs: 250,
    })

    await service.admin.organizations.list({ page: 1, pageSize: 25, search: ' org ' })
    await service.user.organizations.get('org 1')
    await service.admin.members.list('org 1', { page: 1, pageSize: 25, sort: ['createdAt:desc', 'email:asc'], search: '  alice  ' })
    await service.admin.invitations.revoke('org 1', 'code 1')

    const list = calls.find(c => c.pathname === '/admin/organizations')
    assert.ok(list)
    assert.equal(list.method, 'GET')
    assert.equal(list.auth, 'Bearer token123')
    assert.equal(list.org, 'org-1')
    assert.ok(list.search.includes('search=org'))

    const getOrg = calls.find(c => c.pathname === '/users/me/organizations/org%201')
    assert.ok(getOrg)
    assert.equal(getOrg.method, 'GET')
    assert.equal(getOrg.org, 'org-1')

    const members = calls.find(c => c.pathname === '/admin/organizations/org%201/members')
    assert.ok(members)
    assert.ok(members.search.includes('sort=createdAt%3Adesc'))
    assert.ok(members.search.includes('sort=email%3Aasc'))
    assert.ok(members.search.includes('search=alice'))

    const revoke = calls.find(c => c.pathname === '/admin/organizations/org%201/invitations/code%201')
    assert.ok(revoke)
    assert.equal(revoke.method, 'DELETE')
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('OrganizationsService covers user/admin namespaces and optional auth/token paths', async () => {
  const originalFetch = globalThis.fetch
  const calls = []

  try {
    globalThis.fetch = async (url, init) => {
      const parsed = new URL(url)
      calls.push({
        pathname: parsed.pathname,
        method: init.method,
        auth: init.headers.get('Authorization'),
        org: init.headers.get('X-Organization-Id'),
      })

      if (init.method === 'DELETE') return makeResponse({ status: 204 })
      return makeResponse({ status: 200, json: {} })
    }

    const active = new ActiveOrganizationService()
    active.setOrganizationId('org-1')

    const auth = { getAccessToken: async () => null }

    const service = new OrganizationsService(auth, active, {
      apiBase: 'https://identity.example.com',
      organizationHeader: { headerName: 'X-Organization-Id' },
    })

    await service.invitations.claim({ code: 'abc' })

    await service.user.organizations.create({ slug: 's', displayName: 'Org' })
    await service.user.organizations.patch('org 1', { displayName: 'New' })

    await service.user.members.list('org 1', { page: 1, pageSize: 25 })
    await service.user.members.add('org 1', { userId: 'u1', roleIds: ['r1'] })
    await service.user.members.update('org 1', 'u1', { roleIds: ['r2'] })
    await service.user.members.remove('org 1', 'u1')

    await service.user.roles.list('org 1', { page: 1, pageSize: 25 })
    await service.user.roles.create('org 1', { name: 'admin', displayName: 'Admin', description: null })
    await service.user.roles.update('org 1', 'role 1', { displayName: 'Admin', description: '' })
    await service.user.roles.delete('org 1', 'role 1')
    await service.user.roles.getPermissions('org 1', 'role 1')
    await service.user.roles.updatePermissions('org 1', 'role 1', { permissions: ['p1', 'p2'] })

    await service.user.invitations.list('org 1', { page: 1, pageSize: 25 })
    await service.user.invitations.create('org 1', { email: 'a@example.com', roleIds: ['r1'] })
    await service.user.invitations.revoke('org 1', 'code 1')

    await service.admin.organizations.create({ slug: 's', displayName: 'Org' })
    await service.admin.organizations.patch('org 1', { displayName: 'New' })
    await service.admin.organizations.delete('org 1')

    await service.admin.members.add('org 1', { userId: 'u1', roleIds: ['r1'] })
    await service.admin.members.update('org 1', 'u1', { roleIds: ['r2'] })
    await service.admin.members.remove('org 1', 'u1')

    await service.admin.roles.create('org 1', { name: 'admin', displayName: 'Admin', description: null })
    await service.admin.roles.update('org 1', 'role 1', { displayName: 'Admin', description: '' })
    await service.admin.roles.delete('org 1', 'role 1')
    await service.admin.roles.getPermissions('org 1', 'role 1')
    await service.admin.roles.updatePermissions('org 1', 'role 1', { permissions: ['p1', 'p2'] })

    await service.admin.invitations.list('org 1', { page: 1, pageSize: 25 })
    await service.admin.invitations.create('org 1', { email: 'a@example.com', roleIds: ['r1'] })
    await service.admin.invitations.revoke('org 1', 'code 1')

    assert.ok(calls.length > 20, 'Expected many request wrappers to be executed')
    assert.ok(calls.every(c => c.org === 'org-1'), 'Expected org header to be attached by default')
    assert.ok(calls.every(c => c.auth === null), 'Expected null token to omit Authorization header')
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('OrganizationsService surfaces API errors, invalid JSON, and timeouts as IdentityError', async () => {
  const originalFetch = globalThis.fetch

  try {
    const active = new ActiveOrganizationService()
    const auth = { getAccessToken: async () => 'token123' }

    // 1) Non-OK JSON error body
    globalThis.fetch = async () => makeResponse({ status: 400, json: { detail: 'bad request' } })
    const service1 = new OrganizationsService(auth, active, { apiBase: 'https://identity.example.com' })
    await assert.rejects(
      () => service1.user.organizations.list(),
      err => err?.name === 'IdentityError' && err?.status === 400 && /bad request/.test(err.message),
    )

    // 2) Non-OK with JSON parsing failure, text fallback
    globalThis.fetch = async () => makeResponse({ status: 500, json: null, text: 'server blew up' })
    const service2 = new OrganizationsService(auth, active, { apiBase: 'https://identity.example.com' })
    await assert.rejects(
      () => service2.user.organizations.list(),
      err => err?.name === 'IdentityError' && err?.status === 500 && /server blew up/.test(err.message),
    )

    // 3) OK response with invalid JSON text
    globalThis.fetch = async () => makeResponse({ status: 200, json: null, text: 'not-json' })
    const service3 = new OrganizationsService(auth, active, { apiBase: 'https://identity.example.com' })
    await assert.rejects(
      () => service3.user.organizations.list(),
      err => err?.name === 'IdentityError' && /not-json/.test(err.message),
    )

    // 4) Abort / timeout path
    globalThis.fetch = async () => { throw { name: 'AbortError' } }
    const service4 = new OrganizationsService(auth, active, { apiBase: 'https://identity.example.com', timeoutMs: 1 })
    await assert.rejects(
      () => service4.user.organizations.list(),
      err => err?.name === 'IdentityError' && /Request timeout/.test(err.message),
    )
  } finally {
    globalThis.fetch = originalFetch
  }
})
