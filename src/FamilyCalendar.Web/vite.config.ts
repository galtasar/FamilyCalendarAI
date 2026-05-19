import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const apiUrl = process.env['services__familycalendar-api__http__0']
  || process.env['services__familycalendar-api__https__0']
  || 'http://localhost:5000'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0',
    port: process.env.PORT ? parseInt(process.env.PORT) : 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: apiUrl,
        changeOrigin: true,
      },
    },
  },
})
