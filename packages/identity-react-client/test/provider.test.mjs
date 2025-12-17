import test from 'node:test'
import assert from 'node:assert/strict'

globalThis.IS_REACT_ACT_ENVIRONMENT = true

import React from 'react'
import TestRenderer, { act } from 'react-test-renderer'

import { IdentityProvider, useIdentityContext } from '../dist/index.mjs'

function createJsonResponse(payload, status = 200) {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function toHeadersObject(headers) {
  const result = {}
  for (const [k, v] of headers.entries()) result[k.toLowerCase()] = v
  return result
}

function createFetchMock(routes) {
  const calls = []

  async function fetchMock(input, init = {}) {
    const url = typeof input === 'string' ? input : input.toString()
    const method = (init.method || 'GET').toUpperCase()
    const headers = new Headers(init.headers || {})
    const body = init.body
    calls.push({ url, method, headers: toHeadersObject(headers), body })

    const pathname = new URL(url).pathname
    const handler = routes[`${method} ${pathname}`] || routes[`* ${pathname}`]
    if (!handler) {
      return new Response('Not Found', { status: 404 })
    }

    return await handler({ url, method, headers, body })
  }

  fetchMock.calls = calls
  return fetchMock
}

async function flush() {
  await new Promise(resolve => setTimeout(resolve, 0))
}

test('IdentityProvider initializes user state from /users/me', async () => {
  const user = {
    id: 'u1',
    email: 'alice@example.com',
    displayName: 'Alice',
    emailConfirmed: true,
    metadata: {},
    concurrencyStamp: 'cs1',
    twoFactorEnabled: false,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  }

  const fetchMock = createFetchMock({
    'GET /users/me': async ({ headers }) => {
      assert.equal(headers.get('authorization'), 'Bearer testtoken')
      return createJsonResponse(user, 200)
    },
    'POST /auth/logout': async () => new Response(null, { status: 200 }),
  })

  const previousFetch = globalThis.fetch
  const previousLocalStorage = globalThis.localStorage
  globalThis.fetch = fetchMock
  globalThis.localStorage = {
    getItem: (k) => (k === 'identity:access_token' ? 'testtoken' : null),
    setItem: () => {},
    removeItem: () => {},
  }

  let snapshot
  function Probe() {
    snapshot = useIdentityContext()
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
          React.createElement(Probe),
        ),
      )
    })

    assert.equal(snapshot.isLoading, true)
    assert.equal(snapshot.user, null)

    await act(async () => {
      await flush()
    })

    assert.equal(snapshot.isLoading, false)
    assert.equal(snapshot.user.email, 'alice@example.com')

    act(() => renderer.unmount())
  } finally {
    globalThis.fetch = previousFetch
    globalThis.localStorage = previousLocalStorage
  }
})

test('IdentityProvider recreates IdentityAuthManager when config changes', async () => {
  const fetchMock = createFetchMock({
    'GET /users/me': async () => createJsonResponse(null, 200),
  })

  const previousFetch = globalThis.fetch
  globalThis.fetch = fetchMock

  let snapshot
  function Probe() {
    snapshot = useIdentityContext()
    return null
  }

  try {
    const configA = {
      apiBase: 'https://identity-a.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'memory',
      autoRefresh: false,
    }

    const configB = { ...configA, apiBase: 'https://identity-b.example.com' }

    let renderer
    act(() => {
      renderer = TestRenderer.create(
        React.createElement(
          IdentityProvider,
          { config: configA },
          React.createElement(Probe),
        ),
      )
    })

    await act(async () => {
      await flush()
    })

    const managerA = snapshot.authManager

    act(() => {
      renderer.update(
        React.createElement(
          IdentityProvider,
          { config: configB },
          React.createElement(Probe),
        ),
      )
    })

    await act(async () => {
      await flush()
    })

    assert.notEqual(snapshot.authManager, managerA)
    act(() => renderer.unmount())
  } finally {
    globalThis.fetch = previousFetch
  }
})
