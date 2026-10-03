import { defineConfig } from "vitest/config";
import { loadEnv } from "vite";
export default defineConfig(({ mode }) => ({
  base: loadEnv(mode, ".", "VITE_").VITE_BASE_PATH || "/",
  build: { target: "es2022" },
  server: { proxy: { "/api": "http://localhost:5080" } },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test-setup.ts"],
    include: ["src/**/*.test.tsx"],
  },
}));
