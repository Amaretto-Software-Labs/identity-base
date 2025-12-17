import test from 'node:test'
import assert from 'node:assert/strict'

globalThis.IS_REACT_ACT_ENVIRONMENT = true

import React from 'react'
import TestRenderer, { act } from 'react-test-renderer'

import {
  IdentityProvider,
  ProtectedRoute,
  useLogin,
  usePermissions,
  useProfile,
} from '../dist/index.mjs'

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
  }
}

async function flush() {
  await new Promise(resolve => setTimeout(resolve, 0))
}

test('ProtectedRoute redirects to /login when unauthenticated', async () => {
  const previousFetch = globalThis.fetch
  const previousWindow = globalThis.window
  const previousLocalStorage = globalThis.localStorage
  const previousSessionStorage = globalThis.sessionStorage

  const localStorage = createStorage()
  const sessionStorage = createStorage()
  const window = { location: { href: 'https://app.example.com/protected' }, localStorage, sessionStorage }

  globalThis.window = window
  globalThis.localStorage = localStorage
  globalThis.sessionStorage = sessionStorage

  globalThis.fetch = async (input) => {
    const url = typeof input === 'string' ? input : input.toString()
    const pathname = new URL(url).pathname
    if (pathname === '/users/me') {
      return createJsonResponse({ title: 'Unauthorized' }, 401)
    }
    return new Response('Not Found', { status: 404 })
  }

  try {
    const config = {
      apiBase: 'https://identity.example.com',
      clientId: 'spa-client',
      redirectUri: 'https://app.example.com/auth/callback',
      tokenStorage: 'memory',
      autoRefresh: false,
    }

    let renderer
    await act(async () => {
      renderer = TestRenderer.create(
        React.createElement(
          IdentityProvider,
          { config },
          React.createElement(ProtectedRoute, null, React.createElement('div', null, 'ok')),
        ),
      )
      await flush()
      await flush()
    })

    assert.ok(window.location.href.startsWith('/login?returnUrl='))
    act(() => renderer.unmount())
  } finally {
    globalThis.fetch = previousFetch
    globalThis.window = previousWindow
    globalThis.localStorage = previousLocalStorage
    globalThis.sessionStorage = previousSessionStorage
  }
})

test('useLogin/useProfile/usePermissions exercise common client flows', async () => {
  const previousFetch = globalThis.fetch
  const previousWindow = globalThis.window
  const previousLocalStorage = globalThis.localStorage
  const previousSessionStorage = globalThis.sessionStorage

  const localStorage = createStorage()
  const sessionStorage = createStorage()

  localStorage.setItem('identity:access_token', 'testtoken')

  const window = { location: { href: 'https://app.example.com/' }, localStorage, sessionStorage }
  globalThis.window = window
  globalThis.localStorage = localStorage
  globalThis.sessionStorage = sessionStorage

  const calls = []
  globalThis.fetch = async (input, init = {}) => {
    const url = typeof input === 'string' ? input : input.toString()
    const { pathname } = new URL(url)
    calls.push({ pathname, method: (init.method || 'GET').toUpperCase() })

    if (pathname === '/users/me') {
      return createJsonResponse({
        id: 'u1',
        email: 'alice@example.com',
        displayName: 'Alice',
        emailConfirmed: true,
        metadata: {},
        concurrencyStamp: 'cs1',
        createdAt: '',
        updatedAt: '',
      })
    }

    if (pathname === '/auth/login') {
      return createJsonResponse({ message: 'ok', requiresTwoFactor: false, clientId: 'spa-client' })
    }

    if (pathname === '/users/me/permissions') {
      return createJsonResponse({ permissions: ['users.read'] })
    }

    if (pathname === '/auth/profile-schema') {
      return createJsonResponse({ fields: [] })
    }

    if (pathname === '/users/me/profile') {
      return createJsonResponse({
        id: 'u1',
        email: 'alice@example.com',
        displayName: 'Alice',
        emailConfirmed: true,
        metadata: { displayName: 'Alice' },
        concurrencyStamp: 'cs2',
        createdAt: '',
        updatedAt: '',
      })
    }

    if (pathname === '/auth/logout') {
      return createJsonResponse({ message: 'ok' })
    }

    return new Response('Not Found', { status: 404 })
  }

  let probe
  function Probe() {
    const login = useLogin()
    const permissions = usePermissions({ autoLoad: false })
    const profile = useProfile()
    probe = { login, permissions, profile }
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
    await act(async () => {
      renderer = TestRenderer.create(
        React.createElement(
          IdentityProvider,
          { config },
          React.createElement(Probe),
        ),
      )
      await flush()
      await flush()
    })

    await act(async () => {
      const response = await probe.login.login({ email: 'alice@example.com', password: 'pw' })
      assert.equal(response.message, 'ok')
    })

    await act(async () => {
      const perms = await probe.permissions.refresh()
      assert.deepEqual(perms, ['users.read'])
      await flush()
    })

    await act(async () => {
      assert.equal(probe.permissions.hasAll(['users.read']), true)
      assert.equal(probe.permissions.hasAny(['missing', 'users.read']), true)
    })

    await act(async () => {
      const updated = await probe.profile.updateProfile({ metadata: { displayName: 'Alice' }, concurrencyStamp: 'cs1' })
      assert.equal(updated.concurrencyStamp, 'cs2')
    })

    assert.ok(calls.some(c => c.pathname === '/auth/login'))
    assert.ok(calls.some(c => c.pathname === '/users/me/permissions'))
    assert.ok(calls.some(c => c.pathname === '/auth/profile-schema'))
    assert.ok(calls.some(c => c.pathname === '/users/me/profile'))

    act(() => renderer.unmount())
  } finally {
    globalThis.fetch = previousFetch
    globalThis.window = previousWindow
    globalThis.localStorage = previousLocalStorage
    globalThis.sessionStorage = previousSessionStorage
  }
})
