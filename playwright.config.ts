import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "tests/e2e",
  retries: 1,
  expect: {
    timeout: 10000
  },
  reporter: "list",
  timeout: 60000,
  webServer: {
    command:
      "dotnet run --project src/BlazorApp/BlazorApp.csproj --urls http://127.0.0.1:5075",
    url: "http://127.0.0.1:5075",
    reuseExistingServer: true,
    timeout: 120000
  },
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL || "http://127.0.0.1:5075",
    trace: "on-first-retry"
  }
});
