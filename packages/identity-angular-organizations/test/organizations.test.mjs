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
