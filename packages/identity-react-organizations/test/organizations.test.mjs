import test from 'node:test'
import assert from 'node:assert/strict'

globalThis.IS_REACT_ACT_ENVIRONMENT = true

import React from 'react'
import TestRenderer, { act } from 'react-test-renderer'

import { IdentityProvider } from '@identity-base/react-client'
import { OrganizationsProvider, useOrganizationMembers, useOrganizations } from '../dist/index.mjs'

function createJsonResponse(payload, status = 200) {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function createStorage() {
  const store = new Map()
  return {
    getItem: (k) => (store.has(k) ? store.get(k) : null),
    setItem: (k, v) => store.set(k, String(v)),
    removeItem: (k) => store.delete(k),
    _dump: () => Object.fromEntries(store.entries()),
  }
}

function toHeadersObject(headers) {
  const result = {}
  for (const [k, v] of headers.entries()) result[k.toLowerCase()] = v
  return result
}

function createFetchMock() {
  const calls = []

  async function fetchMock(input, init = {}) {
    const url = typeof input === 'string' ? input : input.toString()
    const method = (init.method || 'GET').toUpperCase()
    const headers = new Headers(init.headers || {})
    const body = init.body
    calls.push({ url, method, headers: toHeadersObject(headers), body })

    const { pathname, searchParams } = new URL(url)

    if (method === 'GET' && pathname === '/users/me') {
      return createJsonResponse({
        id: 'u1',
        email: 'alice@example.com',
        displayName: 'Alice',
        emailConfirmed: true,
        metadata: {},
        concurrencyStamp: 'cs1',
        twoFactorEnabled: false,
      })
    }

    if (method === 'GET' && pathname === '/users/me/organizations') {
      assert.equal(searchParams.get('page'), '1')
      assert.equal(searchParams.get('pageSize'), '200')
      return createJsonResponse({
        page: 1,
        pageSize: 200,
        totalCount: 1,
        items: [
          {
            organizationId: 'org1',
            slug: 'org1',
            displayName: 'Org One',
            status: 'active',
            roleIds: ['r1'],
            createdAtUtc: new Date().toISOString(),
            updatedAtUtc: null,
            tenantId: null,
          },
        ],
      })
    }

    if (method === 'GET' && pathname === '/users/me/organizations/org1') {
      return createJsonResponse({
        id: 'org1',
        slug: 'org1',
        displayName: 'Org One',
        status: 'active',
        metadata: {},
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: null,
        archivedAtUtc: null,
        tenantId: null,
      })
    }

    if (method === 'GET' && pathname === '/admin/organizations/org1/members') {
      return createJsonResponse({
        page: 1,
        pageSize: 25,
        totalCount: 0,
        items: [],
      })
    }

    return new Response('Not Found', { status: 404 })
  }

  fetchMock.calls = calls
  return fetchMock
}

async function flush() {
  await new Promise(resolve => setTimeout(resolve, 0))
}

function deferred() {
  let resolve
  let reject
  const promise = new Promise((resolvePromise, rejectPromise) => {
    resolve = resolvePromise
    reject = rejectPromise
  })
  return { promise, resolve, reject }
}

test('OrganizationsProvider attaches bearer token and avoids org header on /users/me/organizations', async () => {
  const fetchMock = createFetchMock()

  const previousFetch = globalThis.fetch
  const previousLocalStorage = globalThis.localStorage
  const previousSessionStorage = globalThis.sessionStorage
  const previousWindow = globalThis.window

  const localStorage = createStorage()
  localStorage.setItem('identity:access_token', 'testtoken')

  const window = { localStorage, location: { origin: 'https://identity.example.com' } }

  globalThis.fetch = fetchMock
  globalThis.localStorage = localStorage
  globalThis.sessionStorage = createStorage()
  globalThis.window = window

  let orgSnapshot
  function Probe() {
    orgSnapshot = useOrganizations()
    return null
  }

  try {
    const config = {
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'localStorage',
      autoRefresh: false,
    }

    let renderer
    act(() => {
      renderer = TestRenderer.create(
        React.createElement(
          IdentityProvider,
          { config },
          React.createElement(
            OrganizationsProvider,
            { apiBase: 'https://identity.example.com' },
            React.createElement(Probe),
          ),
        ),
      )
    })

    await act(async () => {
      await flush()
      await flush()
    })

    assert.equal(orgSnapshot.activeOrganizationId, 'org1')
    assert.equal(localStorage.getItem('identity-base:active-organization-id'), 'org1')

    const membershipCalls = fetchMock.calls.filter(c => new URL(c.url).pathname === '/users/me/organizations')
    assert.ok(membershipCalls.length >= 1)
    for (const call of membershipCalls) {
      assert.equal(call.headers.authorization, 'Bearer testtoken')
      assert.ok(!('x-organization-id' in call.headers))
    }

    act(() => renderer.unmount())
  } finally {
    globalThis.fetch = previousFetch
    globalThis.localStorage = previousLocalStorage
    globalThis.sessionStorage = previousSessionStorage
    globalThis.window = previousWindow
  }
})

test('OrganizationsProvider attaches X-Organization-Id on admin routes once active org is set', async () => {
  const fetchMock = createFetchMock()

  const previousFetch = globalThis.fetch
  const previousLocalStorage = globalThis.localStorage
  const previousSessionStorage = globalThis.sessionStorage
  const previousWindow = globalThis.window

  const localStorage = createStorage()
  localStorage.setItem('identity:access_token', 'testtoken')

  const window = { localStorage, location: { origin: 'https://identity.example.com' } }

  globalThis.fetch = fetchMock
  globalThis.localStorage = localStorage
  globalThis.sessionStorage = createStorage()
  globalThis.window = window

  let orgSnapshot
  function Probe() {
    orgSnapshot = useOrganizations()
    return null
  }

  try {
    const config = {
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'localStorage',
      autoRefresh: false,
    }

    let renderer
    act(() => {
      renderer = TestRenderer.create(
        React.createElement(
          IdentityProvider,
          { config },
          React.createElement(
            OrganizationsProvider,
            { apiBase: 'https://identity.example.com' },
            React.createElement(Probe),
          ),
        ),
      )
    })

    await act(async () => {
      await flush()
      await flush()
    })

    await act(async () => {
      await orgSnapshot.client.admin.listMembers('org1')
    })

    const adminCalls = fetchMock.calls.filter(c => new URL(c.url).pathname === '/admin/organizations/org1/members')
    assert.ok(adminCalls.length >= 1)
    const last = adminCalls[adminCalls.length - 1]
    assert.equal(last.headers.authorization, 'Bearer testtoken')
    assert.equal(last.headers['x-organization-id'], 'org1')

    act(() => renderer.unmount())
  } finally {
    globalThis.fetch = previousFetch
    globalThis.localStorage = previousLocalStorage
    globalThis.sessionStorage = previousSessionStorage
    globalThis.window = previousWindow
  }
})

test('OrganizationsProvider rejects an invalid organization switch without changing active state', async () => {
  const fetchMock = createFetchMock()
  const previousFetch = globalThis.fetch
  const previousLocalStorage = globalThis.localStorage
  const previousSessionStorage = globalThis.sessionStorage
  const previousWindow = globalThis.window
  const localStorage = createStorage()
  localStorage.setItem('identity:access_token', 'testtoken')
  globalThis.fetch = fetchMock
  globalThis.localStorage = localStorage
  globalThis.sessionStorage = createStorage()
  globalThis.window = { localStorage, location: { origin: 'https://identity.example.com' } }

  let snapshot
  function Probe() {
    snapshot = useOrganizations()
    return null
  }

  try {
    let renderer
    act(() => {
      renderer = TestRenderer.create(
        React.createElement(
          IdentityProvider,
          {
            config: {
              apiBase: 'https://identity.example.com',
              clientId: 'spa-client',
              redirectUri: 'https://app.example.com/auth/callback',
              tokenStorage: 'localStorage',
              autoRefresh: false,
            },
          },
          React.createElement(
            OrganizationsProvider,
            { apiBase: 'https://identity.example.com' },
            React.createElement(Probe),
          ),
        ),
      )
    })

    await act(async () => {
      await flush()
      await flush()
    })

    await assert.rejects(
      () => snapshot.switchActiveOrganization('org2'),
      (error) => error.name === 'IdentityError' && error.status === 403,
    )
    assert.equal(snapshot.activeOrganizationId, 'org1')
    assert.equal(
      fetchMock.calls.some((call) => new URL(call.url).pathname === '/users/me/organizations/org2'),
      false,
    )

    act(() => renderer.unmount())
  } finally {
    globalThis.fetch = previousFetch
    globalThis.localStorage = previousLocalStorage
    globalThis.sessionStorage = previousSessionStorage
    globalThis.window = previousWindow
  }
})

test('OrganizationsProvider reads all membership pages and maps the server DTO', async () => {
  const previousFetch = globalThis.fetch
  const previousLocalStorage = globalThis.localStorage
  const previousSessionStorage = globalThis.sessionStorage
  const previousWindow = globalThis.window
  const localStorage = createStorage()
  localStorage.setItem('identity:access_token', 'testtoken')
  const membershipPages = []

  globalThis.localStorage = localStorage
  globalThis.sessionStorage = createStorage()
  globalThis.window = { localStorage, location: { origin: 'https://identity.example.com' } }
  globalThis.fetch = async (input, init = {}) => {
    const url = new URL(typeof input === 'string' ? input : input.toString())
    const method = (init.method || 'GET').toUpperCase()
    if (method === 'GET' && url.pathname === '/users/me') {
      return createJsonResponse({
        id: 'u1',
        email: 'alice@example.com',
        displayName: 'Alice',
        emailConfirmed: true,
        metadata: {},
        concurrencyStamp: 'cs1',
      })
    }
    if (method === 'GET' && url.pathname === '/users/me/organizations') {
      const page = Number(url.searchParams.get('page'))
      membershipPages.push(page)
      const organizationId = page === 1 ? 'org1' : 'org2'
      return createJsonResponse({
        page,
        pageSize: 200,
        totalCount: 2,
        items: [{
          organizationId,
          tenantId: null,
          slug: organizationId,
          displayName: page === 1 ? 'Org One' : 'Org Two',
          status: 'Active',
          roleIds: [`role${page}`],
          createdAtUtc: '2026-01-01T00:00:00Z',
          updatedAtUtc: null,
        }],
      })
    }
    if (method === 'GET' && url.pathname.startsWith('/users/me/organizations/')) {
      const id = url.pathname.split('/').at(-1)
      return createJsonResponse({
        id,
        slug: id,
        displayName: id === 'org1' ? 'Org One' : 'Org Two',
        status: 'Active',
        metadata: {},
        createdAtUtc: '2026-01-01T00:00:00Z',
      })
    }
    return new Response('Not Found', { status: 404 })
  }

  let snapshot
  function Probe() {
    snapshot = useOrganizations()
    return null
  }

  try {
    let renderer
    act(() => {
      renderer = TestRenderer.create(
        React.createElement(
          IdentityProvider,
          {
            config: {
              apiBase: 'https://identity.example.com',
              clientId: 'spa-client',
              redirectUri: 'https://app.example.com/auth/callback',
              tokenStorage: 'localStorage',
              autoRefresh: false,
            },
          },
          React.createElement(
            OrganizationsProvider,
            { apiBase: 'https://identity.example.com' },
            React.createElement(Probe),
          ),
        ),
      )
    })

    await act(async () => {
      await flush()
      await flush()
    })

    assert.ok(membershipPages.length >= 2)
    assert.equal(membershipPages.length % 2, 0)
    for (let index = 0; index < membershipPages.length; index += 2) {
      assert.deepEqual(membershipPages.slice(index, index + 2), [1, 2])
    }
    assert.equal(snapshot.memberships.length, 2)
    assert.deepEqual(
      snapshot.memberships.map(({ organizationId, slug, displayName, status }) => ({
        organizationId,
        slug,
        displayName,
        status,
      })),
      [
        { organizationId: 'org1', slug: 'org1', displayName: 'Org One', status: 'Active' },
        { organizationId: 'org2', slug: 'org2', displayName: 'Org Two', status: 'Active' },
      ],
    )

    act(() => renderer.unmount())
  } finally {
    globalThis.fetch = previousFetch
    globalThis.localStorage = previousLocalStorage
    globalThis.sessionStorage = previousSessionStorage
    globalThis.window = previousWindow
  }
})

test('OrganizationsProvider surfaces a plain-text failure as IdentityError', async () => {
  const baseFetch = createFetchMock()
  const previousFetch = globalThis.fetch
  const previousLocalStorage = globalThis.localStorage
  const previousSessionStorage = globalThis.sessionStorage
  const previousWindow = globalThis.window
  const localStorage = createStorage()
  localStorage.setItem('identity:access_token', 'testtoken')
  globalThis.localStorage = localStorage
  globalThis.sessionStorage = createStorage()
  globalThis.window = { localStorage, location: { origin: 'https://identity.example.com' } }
  globalThis.fetch = async (input, init) => {
    const url = new URL(typeof input === 'string' ? input : input.toString())
    if (url.pathname === '/admin/organizations/org1/members') {
      return new Response('server blew up', { status: 500 })
    }
    return await baseFetch(input, init)
  }

  let snapshot
  function Probe() {
    snapshot = useOrganizations()
    return null
  }

  try {
    let renderer
    act(() => {
      renderer = TestRenderer.create(
        React.createElement(
          IdentityProvider,
          {
            config: {
              apiBase: 'https://identity.example.com',
              clientId: 'spa-client',
              redirectUri: 'https://app.example.com/auth/callback',
              tokenStorage: 'localStorage',
              autoRefresh: false,
            },
          },
          React.createElement(
            OrganizationsProvider,
            { apiBase: 'https://identity.example.com' },
            React.createElement(Probe),
          ),
        ),
      )
    })
    await act(async () => {
      await flush()
      await flush()
    })

    await assert.rejects(
      () => snapshot.client.admin.listMembers('org1'),
      (error) => {
        assert.equal(error.name, 'IdentityError')
        assert.equal(error.status, 500)
        assert.equal(error.message, 'server blew up')
        return true
      },
    )

    act(() => renderer.unmount())
  } finally {
    globalThis.fetch = previousFetch
    globalThis.localStorage = previousLocalStorage
    globalThis.sessionStorage = previousSessionStorage
    globalThis.window = previousWindow
  }
})

test('OrganizationsProvider exposes the supported organization mutation surface', async () => {
  const calls = []
  const requestBodies = []
  const organization = {
    id: 'org1',
    slug: 'org1',
    displayName: 'Org One',
    status: 'Active',
    metadata: {},
    createdAtUtc: '2026-01-01T00:00:00Z',
  }
  const member = {
    organizationId: 'org1',
    userId: 'user1',
    roleIds: ['role1'],
    createdAtUtc: '2026-01-01T00:00:00Z',
  }
  const role = {
    id: 'role1',
    organizationId: 'org1',
    name: 'Owner',
    isSystemRole: false,
    createdAtUtc: '2026-01-01T00:00:00Z',
  }
  const invitation = {
    code: 'invite1',
    organizationId: 'org1',
    organizationSlug: 'org1',
    organizationName: 'Org One',
    email: 'bob@example.com',
    roleIds: ['role1'],
    createdAtUtc: '2026-01-01T00:00:00Z',
    expiresAtUtc: '2026-01-02T00:00:00Z',
  }

  const fetcher = async (input, init = {}) => {
    const url = new URL(typeof input === 'string' ? input : input.toString())
    const method = (init.method || 'GET').toUpperCase()
    calls.push(`${method} ${url.pathname}${url.search}`)
    if (init.body) {
      requestBodies.push({ method, pathname: url.pathname, body: JSON.parse(init.body) })
    }

    if (method === 'GET' && url.pathname === '/users/me/organizations') {
      return createJsonResponse({ page: 1, pageSize: 200, totalCount: 0, items: [] })
    }
    if (method === 'GET' && url.pathname === '/invitations/invite1') {
      return createJsonResponse({
        code: 'invite1',
        organizationSlug: 'org1',
        organizationName: 'Org One',
        expiresAtUtc: invitation.expiresAtUtc,
      })
    }
    if (method === 'POST' && url.pathname === '/invitations/claim') {
      return createJsonResponse({
        organizationId: 'org1',
        organizationSlug: 'org1',
        organizationName: 'Org One',
        roleIds: ['role1'],
        wasExistingMember: false,
        wasExistingUser: true,
        requiresTokenRefresh: true,
      })
    }
    if (method === 'GET' && url.pathname === '/admin/organizations') {
      return createJsonResponse({ page: 2, pageSize: 10, totalCount: 1, items: [organization] })
    }
    if ((method === 'POST' || method === 'PATCH') && (
      url.pathname === '/users/me/organizations'
      || url.pathname === '/users/me/organizations/org1'
      || url.pathname === '/admin/organizations'
      || url.pathname === '/admin/organizations/org1'
    )) {
      return createJsonResponse(organization)
    }
    if (method === 'POST' && url.pathname.endsWith('/members')) {
      return createJsonResponse(member)
    }
    if (method === 'POST' && url.pathname.endsWith('/roles')) {
      return createJsonResponse(role)
    }
    if (method === 'POST' && url.pathname.endsWith('/invitations')) {
      return createJsonResponse(invitation)
    }
    if (method === 'DELETE') {
      return new Response(null, { status: 204 })
    }

    return new Response('Not Found', { status: 404 })
  }

  let snapshot
  function Probe() {
    snapshot = useOrganizations()
    return null
  }

  let renderer
  act(() => {
    renderer = TestRenderer.create(
      React.createElement(
        IdentityProvider,
        null,
        React.createElement(
          OrganizationsProvider,
          { apiBase: 'https://identity.example.com', fetcher },
          React.createElement(Probe),
        ),
      ),
    )
  })
  await act(async () => {
    await flush()
  })

  await snapshot.client.invitations.preview('invite1')
  await snapshot.client.invitations.claim('invite1')
  await snapshot.client.user.createOrganization({
    slug: 'org1',
    displayName: 'Org One',
    metadata: { plan: 'pro' },
  })
  await snapshot.client.user.updateOrganization('org1', {
    displayName: 'Org One',
    metadata: { plan: 'enterprise' },
  })
  await snapshot.client.user.addMember('org1', { userId: 'user1', roleIds: ['role1'] })
  await snapshot.client.user.createRole('org1', { name: 'Owner' })
  await snapshot.client.user.deleteRole('org1', 'role1')
  await snapshot.client.user.createInvitation('org1', { email: 'bob@example.com' })
  await snapshot.client.user.revokeInvitation('org1', 'invite1')
  const page = await snapshot.client.admin.listOrganizations({
    page: 2,
    pageSize: 10,
    sort: ['displayName:asc', 'createdAt:desc'],
  })
  await snapshot.client.admin.createOrganization({
    slug: 'org1',
    displayName: 'Org One',
    metadata: { region: 'eu' },
  })
  await snapshot.client.admin.updateOrganization('org1', {
    displayName: 'Org One',
    metadata: { region: 'us' },
  })
  await snapshot.client.admin.archiveOrganization('org1')
  await snapshot.client.admin.addMember('org1', { userId: 'user1', roleIds: ['role1'] })
  await snapshot.client.admin.createRole('org1', { name: 'Owner' })
  await snapshot.client.admin.deleteRole('org1', 'role1')
  await snapshot.client.admin.createInvitation('org1', { email: 'bob@example.com' })
  await snapshot.client.admin.revokeInvitation('org1', 'invite1')

  assert.equal(page.organizations[0].displayName, 'Org One')
  assert.ok(calls.includes('GET /invitations/invite1'))
  assert.ok(calls.includes('POST /invitations/claim'))
  assert.ok(calls.includes('POST /users/me/organizations/org1/members'))
  assert.ok(calls.includes('POST /users/me/organizations/org1/roles'))
  assert.ok(calls.includes('DELETE /users/me/organizations/org1/roles/role1'))
  assert.ok(calls.includes('POST /admin/organizations/org1/invitations'))
  assert.ok(calls.includes('DELETE /admin/organizations/org1'))
  assert.ok(calls.some((call) =>
    call === 'GET /admin/organizations?page=2&pageSize=10&sort=displayName%3Aasc&sort=createdAt%3Adesc'))
  assert.deepEqual(
    requestBodies.find(({ method, pathname }) =>
      method === 'POST' && pathname === '/users/me/organizations').body.metadata,
    { values: { plan: 'pro' } },
  )
  assert.deepEqual(
    requestBodies.find(({ method, pathname }) =>
      method === 'PATCH' && pathname === '/users/me/organizations/org1').body.metadata,
    { values: { plan: 'enterprise' } },
  )
  assert.deepEqual(
    requestBodies.find(({ method, pathname }) =>
      method === 'POST' && pathname === '/admin/organizations').body.metadata,
    { values: { region: 'eu' } },
  )
  assert.deepEqual(
    requestBodies.find(({ method, pathname }) =>
      method === 'PATCH' && pathname === '/admin/organizations/org1').body.metadata,
    { values: { region: 'us' } },
  )

  act(() => renderer.unmount())
})

test('invitation preview remains available when token retrieval fails', async () => {
  let snapshot
  let previewCalls = 0
  const authManager = {
    getAccessToken: async () => {
      throw new Error('stale refresh token')
    },
  }
  const fetcher = async (input, init = {}) => {
    const url = new URL(typeof input === 'string' ? input : input.toString())
    if (url.pathname === '/invitations/invite1') {
      previewCalls += 1
      assert.equal(new Headers(init.headers).has('Authorization'), false)
      return createJsonResponse({
        code: 'invite1',
        organizationSlug: 'org1',
        organizationName: 'Org One',
        expiresAtUtc: '2026-01-02T00:00:00Z',
      })
    }
    return new Response('Not Found', { status: 404 })
  }

  function Probe() {
    snapshot = useOrganizations()
    return null
  }

  let renderer
  act(() => {
    renderer = TestRenderer.create(
      React.createElement(
        IdentityProvider,
        { authManager, isAuthenticated: false },
        React.createElement(
          OrganizationsProvider,
          { apiBase: 'https://identity.example.com', fetcher },
          React.createElement(Probe),
        ),
      ),
    )
  })

  const preview = await snapshot.client.invitations.preview('invite1')
  assert.equal(preview.organizationName, 'Org One')
  assert.equal(previewCalls, 1)
  act(() => renderer.unmount())
})

test('OrganizationsProvider clears membership loading when logout invalidates a request', async () => {
  const membershipsResponse = deferred()
  const authManager = { getAccessToken: async () => 'testtoken' }
  const fetcher = async (input) => {
    const url = new URL(typeof input === 'string' ? input : input.toString())
    if (url.pathname === '/users/me/organizations') {
      return await membershipsResponse.promise
    }
    return new Response('Not Found', { status: 404 })
  }

  let snapshot
  function Probe() {
    snapshot = useOrganizations()
    return null
  }
  const renderTree = (isAuthenticated) => React.createElement(
    IdentityProvider,
    { authManager, isAuthenticated },
    React.createElement(
      OrganizationsProvider,
      { apiBase: 'https://identity.example.com', fetcher },
      React.createElement(Probe),
    ),
  )

  let renderer
  act(() => {
    renderer = TestRenderer.create(renderTree(true))
  })
  await act(async () => {
    await flush()
  })
  assert.equal(snapshot.isLoadingMemberships, true)

  act(() => {
    renderer.update(renderTree(false))
  })
  assert.equal(snapshot.isLoadingMemberships, false)

  await act(async () => {
    membershipsResponse.resolve(createJsonResponse({
      page: 1,
      pageSize: 200,
      totalCount: 0,
      items: [],
    }))
    await flush()
  })
  assert.equal(snapshot.isLoadingMemberships, false)
  act(() => renderer.unmount())
})

test('useOrganizationMembers clears loading when removal invalidates the last pending page', async () => {
  const membersResponse = deferred()
  const fetcher = async (input, init = {}) => {
    const url = new URL(typeof input === 'string' ? input : input.toString())
    const method = (init.method || 'GET').toUpperCase()
    if (method === 'GET' && url.pathname === '/users/me/organizations') {
      return createJsonResponse({ page: 1, pageSize: 200, totalCount: 0, items: [] })
    }
    if (method === 'GET' && url.pathname === '/users/me/organizations/org1/members') {
      return await membersResponse.promise
    }
    if (method === 'DELETE' && url.pathname === '/users/me/organizations/org1/members/user1') {
      return new Response(null, { status: 204 })
    }
    return new Response('Not Found', { status: 404 })
  }

  let snapshot
  function Probe() {
    snapshot = useOrganizationMembers('org1')
    return null
  }

  let renderer
  act(() => {
    renderer = TestRenderer.create(
      React.createElement(
        IdentityProvider,
        null,
        React.createElement(
          OrganizationsProvider,
          { apiBase: 'https://identity.example.com', fetcher },
          React.createElement(Probe),
        ),
      ),
    )
  })
  await act(async () => {
    await flush()
  })
  assert.equal(snapshot.isLoading, true)

  await act(async () => {
    await snapshot.removeMember('user1')
  })
  assert.equal(snapshot.isLoading, false)

  await act(async () => {
    membersResponse.resolve(createJsonResponse({
      page: 1,
      pageSize: 25,
      totalCount: 1,
      items: [{
        organizationId: 'org1',
        userId: 'user1',
        roleIds: [],
        createdAtUtc: '2026-01-01T00:00:00Z',
      }],
    }))
    await flush()
  })
  assert.equal(snapshot.isLoading, false)
  act(() => renderer.unmount())
})

test('useOrganizationMembers ignores a response from the previously selected organization', async () => {
  const previousFetch = globalThis.fetch
  const previousLocalStorage = globalThis.localStorage
  const previousSessionStorage = globalThis.sessionStorage
  const previousWindow = globalThis.window
  const localStorage = createStorage()
  localStorage.setItem('identity:access_token', 'testtoken')
  const org1Members = deferred()
  const org2Members = deferred()

  globalThis.localStorage = localStorage
  globalThis.sessionStorage = createStorage()
  globalThis.window = { localStorage, location: { origin: 'https://identity.example.com' } }
  globalThis.fetch = async (input, init = {}) => {
    const url = new URL(typeof input === 'string' ? input : input.toString())
    const method = (init.method || 'GET').toUpperCase()
    if (method === 'GET' && url.pathname === '/users/me') {
      return createJsonResponse({
        id: 'u1',
        email: 'alice@example.com',
        displayName: 'Alice',
        emailConfirmed: true,
        metadata: {},
        concurrencyStamp: 'cs1',
      })
    }
    if (method === 'GET' && url.pathname === '/users/me/organizations') {
      return createJsonResponse({
        page: 1,
        pageSize: 200,
        totalCount: 2,
        items: ['org1', 'org2'].map((organizationId) => ({
          organizationId,
          tenantId: null,
          slug: organizationId,
          displayName: organizationId,
          status: 'Active',
          roleIds: [],
          createdAtUtc: '2026-01-01T00:00:00Z',
        })),
      })
    }
    if (method === 'GET' && url.pathname === '/users/me/organizations/org1') {
      return createJsonResponse({
        id: 'org1',
        slug: 'org1',
        displayName: 'Org One',
        status: 'Active',
        createdAtUtc: '2026-01-01T00:00:00Z',
      })
    }
    if (method === 'GET' && url.pathname === '/users/me/organizations/org2') {
      return createJsonResponse({
        id: 'org2',
        slug: 'org2',
        displayName: 'Org Two',
        status: 'Active',
        createdAtUtc: '2026-01-01T00:00:00Z',
      })
    }
    if (method === 'GET' && url.pathname === '/users/me/organizations/org1/members') {
      return await org1Members.promise
    }
    if (method === 'GET' && url.pathname === '/users/me/organizations/org2/members') {
      return await org2Members.promise
    }
    return new Response('Not Found', { status: 404 })
  }

  let membersSnapshot
  function MembersProbe({ organizationId }) {
    membersSnapshot = useOrganizationMembers(organizationId)
    return null
  }

  const renderTree = (organizationId) => React.createElement(
    IdentityProvider,
    {
      config: {
        apiBase: 'https://identity.example.com',
        clientId: 'spa-client',
        redirectUri: 'https://app.example.com/auth/callback',
        tokenStorage: 'localStorage',
        autoRefresh: false,
      },
    },
    React.createElement(
      OrganizationsProvider,
      { apiBase: 'https://identity.example.com' },
      React.createElement(MembersProbe, { organizationId }),
    ),
  )

  try {
    let renderer
    act(() => {
      renderer = TestRenderer.create(renderTree('org1'))
    })
    await act(async () => {
      await flush()
    })

    act(() => {
      renderer.update(renderTree('org2'))
    })
    await act(async () => {
      org2Members.resolve(createJsonResponse({
        page: 1,
        pageSize: 25,
        totalCount: 1,
        items: [{
          organizationId: 'org2',
          userId: 'user2',
          roleIds: [],
          createdAtUtc: '2026-01-01T00:00:00Z',
        }],
      }))
      await flush()
    })
    await act(async () => {
      org1Members.resolve(createJsonResponse({
        page: 1,
        pageSize: 25,
        totalCount: 1,
        items: [{
          organizationId: 'org1',
          userId: 'user1',
          roleIds: [],
          createdAtUtc: '2026-01-01T00:00:00Z',
        }],
      }))
      await flush()
    })

    assert.deepEqual(membersSnapshot.members.map((member) => member.userId), ['user2'])
    act(() => renderer.unmount())
  } finally {
    globalThis.fetch = previousFetch
    globalThis.localStorage = previousLocalStorage
    globalThis.sessionStorage = previousSessionStorage
    globalThis.window = previousWindow
  }
})
