import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

// The API (src/YueToLogic.Api) serves this app under /ui. During `dotnet run`, SpaProxy starts this dev server
// and sends the browser here; /api requests are forwarded back to the API.
const apiTarget = process.env.YUE_API_URL ?? 'http://localhost:5080'

export default defineConfig({
  base: '/ui/',
  plugins: [vue()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    // SpaProxy expects the dev server at exactly this address (SpaProxyServerUrl in the .csproj).
    strictPort: true,
    proxy: { '/api': apiTarget },
  },
})
