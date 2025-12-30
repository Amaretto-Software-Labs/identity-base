import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react-swc'

function parsePort(value: string | undefined, fallback: number) {
  const parsed = value ? Number.parseInt(value, 10) : Number.NaN
  return Number.isFinite(parsed) ? parsed : fallback
}

export default defineConfig({
  plugins: [react()],
  server: {
    host: process.env.HOST ?? 'localhost',
    port: parsePort(process.env.PORT ?? process.env.VITE_PORT, 5174),
    proxy: {
      '/users': {
        target: 'https://localhost:5000',
        changeOrigin: true,
        secure: false,
      },
      '/healthz': {
        target: 'https://localhost:5000',
        changeOrigin: true,
        secure: false,
      }
    }
  }
})
