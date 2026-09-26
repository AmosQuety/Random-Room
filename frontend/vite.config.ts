import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const api = "http://localhost:5184";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: { outDir: "../backend/RandomRoom.Api/wwwroot", emptyOutDir: true },
  server: {
    proxy: {
      "/api": api,
      "/hubs": { target: api, ws: true },
    },
  },
  test: { environment: "jsdom", setupFiles: "./src/test-setup.ts" },
});
