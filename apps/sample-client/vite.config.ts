import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react-swc'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
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
