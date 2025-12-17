# @identity-base/client-core

Framework-agnostic TypeScript client core for Identity Base.

## Install

```bash
npm install @identity-base/client-core
```

## Usage

```ts
import { IdentityAuthManager } from '@identity-base/client-core'

const auth = new IdentityAuthManager({
  apiBase: 'https://identity.example.com',
  clientId: 'spa-client',
  redirectUri: 'https://app.example.com/auth/callback',
  scope: 'openid profile email identity.api',
  tokenStorage: 'sessionStorage',
  autoRefresh: true,
})
```

