import { defineConfig } from "vitest/config";
export default defineConfig({
  server: { proxy: { "/api": "http://localhost:5080" } },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test-setup.ts"],
    include: ["src/**/*.test.tsx"],
  },
});
