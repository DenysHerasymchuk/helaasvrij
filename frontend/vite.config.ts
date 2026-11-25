import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: {
    // Keep the browser's Host (localhost:5174) so the GitHub OAuth redirect URI points back at this dev server.
    proxy: { '/api': { target: process.env.API_TARGET ?? 'http://localhost:5080', changeOrigin: false } },
  },
})
