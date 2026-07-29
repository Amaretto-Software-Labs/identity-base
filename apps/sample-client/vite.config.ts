import { defineConfig, type ProxyOptions } from 'vite'
import react from '@vitejs/plugin-react-swc'

function parsePort(value: string | undefined, fallback: number) {
  const parsed = value ? Number.parseInt(value, 10) : Number.NaN
  return Number.isFinite(parsed) ? parsed : fallback
}

function identityProxy(): ProxyOptions {
  return {
    target: 'https://localhost:5000',
    changeOrigin: true,
    secure: false,
    configure(proxy) {
      proxy.on('proxyRes', proxyResponse => {
        const cookies = proxyResponse.headers['set-cookie']
        if (!cookies) {
          return
        }

        // The Vite server is HTTP on localhost. Strip Secure only from its
        // proxied development response so cookie-based sample flows remain
        // same-origin; the Identity host's production cookie stays Secure.
        proxyResponse.headers['set-cookie'] = cookies.map(cookie =>
          cookie.replace(/;\s*Secure/gi, ''))
      })
    },
  }
}

export default defineConfig({
  plugins: [react()],
  optimizeDeps: {
    // This local package is rebuilt while the sample is running. Loading its
    // ESM output directly prevents Vite from serving a stale export surface.
    exclude: ['@identity-base/react-client'],
    // The linked package's compiled ESM imports React's CommonJS runtime.
    include: ['react/jsx-runtime'],
  },
  resolve: {
    preserveSymlinks: true,
    // Local package links also expose the React client's development dependencies.
    // Keep hooks on the application's React instance in both dev and production builds.
    dedupe: ['react', 'react-dom'],
  },
  server: {
    host: process.env.HOST ?? 'localhost',
    port: parsePort(process.env.PORT ?? process.env.VITE_PORT, 5174),
    proxy: {
      '/auth': identityProxy(),
      '/users': identityProxy(),
      '/admin': identityProxy(),
      '/connect': identityProxy(),
      '/.well-known': identityProxy(),
      '/healthz': {
        target: 'https://localhost:5000',
        changeOrigin: true,
        secure: false,
      }
    }
  }
})
