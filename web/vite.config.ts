import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { loadEnv } from 'vite'
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const monorepoRoot = path.resolve(__dirname, '..')

export default defineConfig(({ mode }) => {
  // Fuente canónica: .env raíz. `make sync-env` también escribe web/.env (VITE_*).
  const rootEnv = loadEnv(mode, monorepoRoot, '')
  const webEnv = loadEnv(mode, __dirname, '')
  const isTest = mode === 'test'

  const apiUrl =
    webEnv.VITE_API_URL ||
    rootEnv.API_URL ||
    rootEnv.VITE_API_URL ||
    'http://localhost:8080'

  // En tests forzamos clave vacía para poder assert del aviso "falta Maps".
  const mapsKey = isTest
    ? ''
    : webEnv.VITE_GOOGLE_MAPS_API_KEY ||
      rootEnv.GOOGLE_MAPS_API_KEY ||
      rootEnv.VITE_GOOGLE_MAPS_API_KEY ||
      ''

  return {
    plugins: [react(), tailwindcss()],
    server: {
      port: Number(rootEnv.WEB_PORT || webEnv.WEB_PORT || 5173),
    },
    define: {
      'import.meta.env.VITE_API_URL': JSON.stringify(apiUrl),
      'import.meta.env.VITE_GOOGLE_MAPS_API_KEY': JSON.stringify(mapsKey),
    },
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: './src/test/setup.ts',
    },
  }
})
