import test from 'node:test'
import assert from 'node:assert/strict'

globalThis.IS_REACT_ACT_ENVIRONMENT = true

import React from 'react'
import TestRenderer, { act } from 'react-test-renderer'

import { IdentityProvider } from '@identity-base/react-client'
import { OrganizationsProvider, useOrganizations } from '../dist/index.mjs'

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
      assert.equal(searchParams.get('pageSize'), '200')
      return createJsonResponse({
        page: 1,
        pageSize: 200,
        totalCount: 1,
        items: [
          {
            organizationId: 'org1',
            userId: 'u1',
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
