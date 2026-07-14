import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: "http://localhost:3000",
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  projects: [
    // Phase 1: login as SuperUser, write storage state to e2e/.auth/superuser.json
    {
      name: "setup",
      testMatch: /auth\.setup\.ts/,
    },
    // Phase 2a: unauthenticated tests — files directly in e2e/ (not in subdirs)
    {
      name: "unauthenticated",
      testMatch: /e2e[/\\][^/\\]+\.spec\.ts$/,
      use: { ...devices["Desktop Chrome"] },
    },
    // Phase 2b: authenticated tests — each test gets SuperUser session pre-loaded
    {
      name: "authenticated",
      testDir: "./e2e/authenticated",
      use: {
        ...devices["Desktop Chrome"],
        storageState: "e2e/.auth/superuser.json",
      },
      dependencies: ["setup"],
    },
  ],
  webServer: [
    {
      command: "npm run dev",
      url: "http://localhost:3000",
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
    {
      command:
        "dotnet run --project ../src/QuantumBuild.API --launch-profile http",
      url: "http://localhost:5222/health",
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      // Ensure CORS allows the Next.js dev server when Playwright spawns the API.
      // appsettings.Development.json is gitignored, so this env var is the portable fix.
      // If you run the API manually (reuseExistingServer path), add this to your own
      // appsettings.Development.json: { "Cors": { "AllowedOrigins": ["http://localhost:3000"] } }
      env: {
        Cors__AllowedOrigins__0: "http://localhost:3000",
      },
    },
  ],
});
