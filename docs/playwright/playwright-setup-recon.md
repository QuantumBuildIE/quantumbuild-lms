# Playwright Setup Recon — QuantumBuild LMS

**Date:** 2026-06-17
**Branch:** transval
**Purpose:** Inventory the current Playwright state and produce a concrete implementation plan for the next Claude Code session.

---

## Section 1: Files Read

The following files were read in full during this recon:

1. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\web\package.json`
2. `web/playwright.config.ts` — absent (confirmed no file at this path)
3. `web/playwright.config.js` — absent
4. `web/playwright.config.mjs` — absent
5. `web/e2e/` — directory does not exist
6. `web/tests/` — directory does not exist
7. `web/playwright/` — directory does not exist
8. `web/test/e2e/` — directory does not exist
9. `web/__e2e__/` — directory does not exist
10. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\BACKLOG.md` (full read, §5.15 specifically)
11. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\CLAUDE.md` (Notes 21–29)
12. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\docs\PHASE_5_STANDARDS.md` (§11 Testing)
13. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\tests\QuantumBuild.Tests.Integration\Fixtures\IntegrationTestBase.cs`
14. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\tests\QuantumBuild.Tests.Integration\Fixtures\CustomWebApplicationFactory.cs` (first 80 lines — sufficient for pattern)
15. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\tests\QuantumBuild.Tests.Common\TestTenant\TestTenantSeeder.cs`
16. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\tests\QuantumBuild.Tests.Common\TestTenant\TestTenantConstants.cs`
17. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\web\next.config.ts`
18. `web/.env`, `web/.env.local`, `web/.env.example`, `web/.env.development` — none exist
19. `.github/workflows/` — directory does not exist (no CI configured)
20. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\.gitignore`
21. Root `package.json` — no file at repo root (not a monorepo managed by root npm)
22. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\web\vitest.config.ts`
23. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\web\vitest.setup.ts`
24. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\web\src\__tests__\framework-smoke.test.ts` (existence confirmed via glob)
25. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\web\src\features\toolbox-talks\components\learning-wizard\lib\__tests__\stepOrder.test.ts` (existence confirmed via glob)
26. `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\web\src\features\toolbox-talks\hooks\__tests__\useWizardPreference.test.ts` (existence confirmed via glob)

---

## Section 2: Current Playwright Inventory

### `@playwright/test` in `web/package.json`

`@playwright/test` is **not present** in `web/package.json` — neither in `dependencies` nor `devDependencies`. The package does not exist anywhere in the frontend dependency tree.

The only test framework currently installed is **vitest** (`^4.1.9`) with `@testing-library/react` and `@testing-library/user-event`, which run unit/component tests. These run in jsdom, not a real browser.

### `web/playwright.config.*`

No Playwright config file exists in any variant (`playwright.config.ts`, `.js`, `.mjs`). This is a clean-slate installation.

### Spec files

No Playwright spec files exist anywhere in the repository. The directory scan of `web/e2e/`, `web/tests/`, `web/playwright/`, `web/test/e2e/`, and `web/__e2e__/` all returned no files.

The three test files that do exist are vitest unit tests:
- `web/src/__tests__/framework-smoke.test.ts` — vitest framework smoke test (added in §5.18)
- `web/src/features/toolbox-talks/components/learning-wizard/lib/__tests__/stepOrder.test.ts` — unit tests for step reachability (§5.19, 7 tests)
- `web/src/features/toolbox-talks/hooks/__tests__/useWizardPreference.test.ts` — unit tests for wizard preference hook (§5.27, 6 tests)

None of these are Playwright specs.

### Fixture files

No Playwright fixture files exist. The only fixture infrastructure is in the backend integration tests (`tests/QuantumBuild.Tests.Integration/Fixtures/`), which is .NET/xUnit, not Playwright.

### Has `npx playwright install` ever been run?

No. Evidence:
- No `.playwright-browsers` directory found anywhere.
- No Playwright entries in `.gitignore` (confirmed: the `.gitignore` contains no mention of `playwright`, `test-results`, or browser binaries).
- No Playwright in `web/package.json` — no version was ever installed to produce a lock file entry.
- No Playwright-related directories in `web/`.

### GitHub Actions workflows referencing Playwright

No `.github/workflows/` directory exists. There is no CI pipeline of any kind configured in this repository. Railway auto-deployment is the only automated process (triggered by git push to specific branches), and it only builds and deploys — it does not run tests.

---

## Section 3: Revive vs Restart vs Hybrid

**Recommendation: Restart (clean slate)**

There is nothing to revive or hybridise. No Playwright config, no spec files, no fixture files, no browser installation. This is a fully greenfield Playwright setup.

The only relevant test infrastructure is the backend integration test stack (`CustomWebApplicationFactory`, `TestTenantSeeder`, `TestTenantConstants`), which uses Testcontainers for PostgreSQL. This pattern is instructive for Playwright test data strategy but the code does not transfer — Playwright hits a real running app, not an in-process test server.

**Per-file recommendations (existing test files):**

| File | Recommendation | Reason |
|---|---|---|
| `web/src/__tests__/framework-smoke.test.ts` | Keep as-is | vitest unit test, not Playwright |
| `web/src/.../stepOrder.test.ts` | Keep as-is | vitest unit test, not Playwright |
| `web/src/.../useWizardPreference.test.ts` | Keep as-is | vitest unit test, not Playwright |

None of these are Playwright tests; they coexist cleanly. The vitest `include` glob is `src/**/*.{test,spec}.{ts,tsx}`, so any Playwright specs placed in `web/e2e/` are outside that glob and will not conflict.

---

## Section 4: Playwright Version and Browser Coverage

### Recommended version

`@playwright/test` **1.49.x** (current stable as of August 2025). Pin to a specific minor, not `latest`, to prevent silent upgrades changing test behaviour. Use `^1.49.0` to allow patch updates.

The gap between knowledge cutoff (August 2025) and today (June 2026) means the actual current stable may be higher (possibly 1.50.x or 1.51.x). The implementation chunk should check `npm show @playwright/test version` before installing and note the installed version in the CLAUDE.md entry.

### Browser coverage for the setup chunk

**Chromium only.** Reasons:
- The app is desktop-admin-first. The admin population uses Chrome-based browsers predominantly.
- Firefox and WebKit testing adds installation time (~500MB+ each) and test surface without changing what's covered in the smoke test.
- Multi-browser flakiness will obscure real failures in the early test suite.

Cross-browser testing is explicitly in scope for Step 2 or later (see Section 13).

### What the setup chunk installs vs defers

- **Setup chunk (Step 1):** `npx playwright install chromium` only.
- **Deferred to Step 2:** `firefox` and `webkit` installation and project definitions.

---

## Section 5: Config Shape

Concrete recommendation for `web/playwright.config.ts`:

```typescript
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,       // serialise tests — multi-tenant DB state is shared
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,                 // single worker for setup chunk (see §5: workers)
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
  ],
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  // webServer: see note below
});
```

### `testDir`

`./e2e` — relative to `web/`. Full path: `web/e2e/`. This is the recommended location (see Section 6).

### `baseURL`

`http://localhost:3000` is correct. The Next.js dev server runs on port 3000 (`npm run dev`). The backend API runs on port 5222 (`dotnet run`). The frontend makes API calls to the backend via its configured `NEXT_PUBLIC_API_URL` env var, so Playwright only needs to know the frontend URL.

### `webServer`

**Recommendation for setup chunk: assume it's running. Do not configure `webServer`.** Rationale:

- The `webServer` option spawns a process and waits for it to be ready. For Next.js dev this takes 10–30 seconds. It can be added in Step 2 once the smoke test is proven.
- A more important constraint: Playwright cannot spawn the .NET backend via `webServer` (it's a separate process). Configuring `webServer` for the frontend only gives a false sense of automation — the developer still has to start the backend and PostgreSQL manually. Documenting the manual startup is clearer and more honest than half-automating it.
- **Step 2 option:** Once the smoke suite is stable, add `webServer` with `command: 'npm run dev'`, `url: 'http://localhost:3000'`, and `reuseExistingServer: !process.env.CI`. This lets local runs reuse an already-running server while CI always starts fresh.

### `use` block

- `trace: 'on-first-retry'` — traces are expensive to generate on every run; limiting to retries catches flaky tests.
- `screenshot: 'only-on-failure'` — avoids screenshot sprawl on passing runs.
- `video: 'on-first-retry'` — videos for debugging flaky failures.
- Timeouts: use Playwright defaults for the setup chunk (30s action timeout, 30s navigation timeout). Override per-test only if a specific operation is known-slow (e.g. a Hangfire job completing).

### `reporter`

Both `list` (for terminal feedback) and `html` (for after-run review). `open: 'never'` prevents the HTML report auto-opening on every run. Developers open it manually: `npx playwright show-report`.

### `workers`

**1 worker** for the setup chunk. The reasons are significant:

1. The database used by Playwright is the shared Development PostgreSQL instance (see Section 7). Parallel tests that write data will step on each other.
2. Multi-tenant isolation requires that one test's writes don't contaminate another test's reads. With 1 worker this is guaranteed by serialisation.
3. The smoke test suite planned for Step 1 is small enough (1–5 tests) that parallelism provides no meaningful speed benefit.

Step 2 can revisit this if a separate Playwright database is provisioned with per-test tenant isolation.

### Project definitions for browsers

Setup chunk: Chromium only (as above). Add Firefox and WebKit project blocks in Step 2.

---

## Section 6: Where Tests Live

**Recommendation: `web/e2e/`**

Rationale:
- Clean separation from vitest unit tests, which live in `web/src/**/__tests__/`. The vitest `include` glob (`src/**/*.{test,spec}.{ts,tsx}`) does not reach `e2e/`, so there is no risk of vitest picking up Playwright specs.
- The `web/playwright.config.ts` uses `testDir: './e2e'`, so relative paths work naturally.
- Conventional for Next.js projects. The Next.js documentation and most community examples use `e2e/` at the project root (alongside `src/`).

Suggested initial structure:
```
web/e2e/
├── auth/
│   └── login.spec.ts          # Login smoke test (Step 1 deliverable)
├── fixtures/
│   └── auth.ts                # storageState helpers + page object stubs
└── .auth/
    └── admin.json             # storageState for admin session (gitignored)
```

The `.auth/` directory must be added to `.gitignore` — it contains JWT tokens and cookies that expire and should never be committed.

---

## Section 7: Test Data and Database Strategy

This is the load-bearing decision for Playwright. The backend integration tests use Testcontainers (an ephemeral PostgreSQL container per test run). Playwright cannot use this pattern — it hits a real running API server connected to a real database. There is no in-process interception point.

### Primary strategy for the setup chunk smoke test

**Recommendation: Strategy A — Shared seeded test tenant + scoped cleanup**

Specifically: the existing "Automated Test Tenant" seeded by `TestTenantSeeder` (TenantId `AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA`) with its pre-seeded Admin user (`admin@test.quantumbuild.ie` / `TestAdmin123!`) is the Playwright test user. The smoke test logs in with this account and verifies admin navigation. No new data is created by the smoke test.

Why this works for the setup chunk:
- The smoke test (login + navigate to Learnings list) is **read-only**. It does not create, edit, or delete data. No cleanup is needed.
- The Admin user exists in the Dev database because `TestTenantSeeder.SeedAllAsync()` was run during integration test development (and the data persists between runs since the integration tests use Testcontainers rather than the shared Dev DB).
- If the test tenant data does not exist in Dev DB, a one-time manual seeder call is needed (see Section 9 — backend/frontend running requirements).

**For Step 2 (write tests):** Strategy C (API-driven setup in `test.beforeEach`) is the right evolution. For tests that create data, use the API directly (`request` fixture in Playwright) to POST talks, assignments, etc. before each test, and DELETE them in `afterEach`. This keeps the database clean without requiring a full DB reset between runs.

### Which database does Playwright target

**The Development database** (localhost:5432, `rascor_stock`, username `postgres`). There is no separate Playwright database — creating one would require spinning up a second PostgreSQL instance, configuring the API to point to it, and managing schema migrations separately.

Trade-offs of using the Dev database:
- **Risk:** Playwright tests run against the same database that manual dev testing uses. A crashing test that leaves partial state could interfere with manual testing.
- **Mitigation:** The smoke test is read-only. For write tests (Step 2), use API-based setup/teardown scoped to a Playwright-specific tenant (a new tenant not used by any other test or manual session).
- **Alternative rejected:** A separate Playwright database requires environment variable switching on the API, migration management, and coordination between two PostgreSQL instances. This is disproportionate overhead for the setup chunk.

### Starting state before a test run

The backend must have:
1. The Development database with all migrations applied.
2. The system seed data (Seeder creates roles, permissions, system glossaries, etc. on API startup).
3. The "Automated Test Tenant" (`AAAAAAAA-...`) with its Admin user seeded. This was done for integration testing; verify it exists in Dev DB before running Playwright.

If the test tenant does not exist in Dev DB, the simplest fix is to run the integration tests once against Dev DB (temporarily changing the connection string) so the seeder fires — but this is the wrong approach. Instead, write a small Playwright global setup script that calls `POST /api/auth/login` to confirm the test user exists, and if not, provides a clear error message instructing how to seed the data. A direct DB seeder script (SQL file or `dotnet ef` seed command) is the correct long-term answer.

### What happens if a test crashes mid-run leaving partial state

For the setup chunk (read-only smoke test): no partial state is possible — the test reads data, nothing more.

For Step 2 write tests: use `afterEach` with API cleanup calls. If the test crashes before `afterEach` runs, orphaned data remains in the Dev DB. Design write tests to be idempotent (check for existence before creating, or use unique-per-run identifiers like `${Date.now()}`). A database reset script runnable before a test session solves the worst case.

### SuperUser role constraint (CLAUDE.md Note 20)

SuperUser cannot be assigned through the application — only seeded or DB-direct. This means Playwright tests cannot create a SuperUser via the admin UI. The smoke test does not need SuperUser; it uses the Admin role.

For any future test that requires SuperUser behaviour: use a pre-seeded SuperUser account (created directly in the Dev DB or via a one-time seeder script). Store the storageState separately from the Admin storageState (e.g. `web/e2e/.auth/superuser.json`). Do not attempt to create SuperUser via the app in `globalSetup`.

---

## Section 8: Authentication Strategy

### Does the setup chunk exercise the login form or use `storageState`?

**The smoke test exercises the login form.** Rationale: the login path is the most critical E2E path in the product — if authentication is broken, nothing else works. A smoke test that bypasses login is not a smoke test.

The smoke test should:
1. Navigate to `http://localhost:3000/login`
2. Fill in email (`admin@test.quantumbuild.ie`) and password (`TestAdmin123!`)
3. Submit the form
4. Assert redirect to `/dashboard` or `/admin/toolbox-talks`
5. Navigate to `/admin/toolbox-talks/talks`
6. Assert the Learnings list page heading or "New" button is visible

### How is initial login captured? Global setup script?

Yes. Use Playwright's `globalSetup` to run a one-time login and save the storageState:

```typescript
// web/e2e/fixtures/globalSetup.ts
import { chromium } from '@playwright/test';

async function globalSetup() {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.goto('http://localhost:3000/login');
  await page.getByLabel('Email').fill('admin@test.quantumbuild.ie');
  await page.getByLabel('Password').fill('TestAdmin123!');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.waitForURL('**/dashboard');
  await page.context().storageState({ path: 'e2e/.auth/admin.json' });
  await browser.close();
}

export default globalSetup;
```

Then in `playwright.config.ts`:
```typescript
globalSetup: './e2e/fixtures/globalSetup.ts',
```

And tests that need a pre-authenticated session use:
```typescript
use: { storageState: 'e2e/.auth/admin.json' }
```

The smoke test itself should NOT use the saved storageState — it should login fresh to test the auth path.

### Per-role storage states or one admin state to start

**One admin state to start.** The smoke test needs only Admin. Add Operator and Supervisor states in Step 2 when tests require them.

### Where does storage state live on disk? Gitignored?

`web/e2e/.auth/` directory. Must be added to `.gitignore`:
```
# Playwright auth state
web/e2e/.auth/
```

The `.gitignore` currently has no Playwright entries; this will be the first.

### JWT token expiry

Access tokens expire in 60 minutes. Refresh tokens expire in 7 days. For short local test runs (under 60 minutes) this is not a problem — the stored storageState from `globalSetup` remains valid for the entire run.

For CI runs that might exceed 60 minutes, the globalSetup pattern handles this naturally: setup always runs first, capturing a fresh token immediately before the tests. By the time the longest test finishes, the token may have refreshed automatically via the Axios interceptor (the frontend handles token refresh transparently).

This does not require special handling in the setup chunk.

---

## Section 9: Backend/Frontend Running Requirements

What must be running for `npx playwright test` to succeed:

| Process | Required | Who starts it | Config needed |
|---|---|---|---|
| PostgreSQL | Yes | Developer (or always running locally) | Default localhost:5432 |
| Backend API (`dotnet run`) | Yes | Developer manually | `cd src/QuantumBuild.API && dotnet run` |
| Frontend dev server (`npm run dev`) | Yes | Developer manually | `cd web && npm run dev` |
| Hangfire workers | Yes (embedded in API) | Starts with API | None extra |

No other processes are required for the smoke test. ElevenLabs, Claude API, DeepL, and other external services are not called by the login → Learnings list navigation path.

### Environment configuration required

The frontend dev server requires `NEXT_PUBLIC_API_URL` pointing to the backend. Check `web/.env.local` — it does not exist in the repository (gitignored). The developer must create it:

```
NEXT_PUBLIC_API_URL=http://localhost:5222
```

This is not Playwright-specific; it is required for any frontend development. Playwright just inherits this from the running dev server.

No Playwright-specific environment variables are required for the setup chunk. For CI (Step 2), `CI=true` is the only Playwright-relevant env var (it enables `forbidOnly` and one retry).

### Documenting the startup sequence

The implementation chunk should add a short "Running Playwright Tests" section to the existing project README or CLAUDE.md (Note 30). The sequence:
1. Start PostgreSQL (assumed always running in dev)
2. `cd src/QuantumBuild.API && dotnet run` (leave running)
3. `cd web && npm run dev` (leave running)
4. `cd web && npm run e2e` (in a new terminal)

---

## Section 10: CI Integration

### Does the repo currently have CI?

No. There is no `.github/workflows/` directory. There are no CI pipeline files of any kind. Deployment is handled by Railway's git-push auto-deploy hook.

### Recommend deferring CI integration to Step 2

**Defer CI to Step 2.** Reasons:

1. **Backend startup complexity.** CI for Playwright requires starting the .NET backend (which needs PostgreSQL, migrations, and seed data), the Next.js frontend, and then running Playwright. This is a multi-service setup that takes significant CI configuration effort. The `.net` + `node` + `postgres` combination on GitHub Actions requires a matrix of service containers and startup scripts.

2. **No existing CI baseline.** Adding CI for the first time is a meaningful infrastructure decision. Coupling it to the Playwright setup chunk creates two units of work in one session, each of which can fail independently.

3. **The database question is unresolved for CI.** CI should use an ephemeral database (Testcontainers or a GitHub Actions PostgreSQL service). The Dev database strategy used for local testing does not apply to CI. Designing the CI database isolation strategy deserves its own focused session.

4. **What Step 2 CI should look like** (documented here for planning):
   - GitHub Actions workflow `.github/workflows/e2e.yml`
   - `services.postgres` to spin up PostgreSQL
   - Steps: checkout → dotnet restore → dotnet ef database update → dotnet run (background) → npm install → npx playwright install chromium → npm run dev (background) → wait-for-url → npx playwright test
   - Upload `playwright-report/` as artifact on failure
   - Run only on PRs targeting `main` (not every `transval` push — too slow)

---

## Section 11: Local Dev Workflow

### Recommended npm script name

`e2e` — short and conventional. Add to `web/package.json` `scripts`:

```json
"e2e": "playwright test",
"e2e:ui": "playwright test --ui",
"e2e:headed": "playwright test --headed",
"e2e:report": "playwright show-report"
```

The existing `test` script (`vitest run`) is for unit tests and should not be renamed. Both coexist.

### Headed vs headless default

**Headless default.** `playwright test` runs headless. Use `npm run e2e:headed` or `npm run e2e:ui` when debugging a specific test.

### `playwright test --ui`

Yes — include `e2e:ui` as a script. The Playwright UI mode (a browser-based interactive test runner) is the recommended debugging workflow. It is far more productive than headed mode for diagnosing failing tests.

### VS Code Playwright extension

Recommend installation but do not configure it in the session. The extension (`ms-playwright.playwright`) provides test discovery, run, and debug UI directly in VS Code. The developer can install it independently; it requires no project configuration beyond having `playwright.config.ts` present.

### What needs to land in CLAUDE.md

A new **Note 30** covering Playwright conventions. Content:

- Playwright test directory: `web/e2e/`
- Config: `web/playwright.config.ts`
- Auth state: `web/e2e/.auth/` (gitignored — never commit)
- Run command: `npm run e2e` from `web/`
- Workers: 1 (shared Dev DB — do not increase without a dedicated Playwright DB)
- Backend must be running before `npm run e2e` — `dotnet run` from `src/QuantumBuild.API`
- Frontend must be running — `npm run dev` from `web/`
- Test data: uses "Automated Test Tenant" (`admin@test.quantumbuild.ie` / `TestAdmin123!`)
- Adding new tests: read-only tests need no setup/teardown; write tests must use API-based setup and cleanup in `beforeEach`/`afterEach`

---

## Section 12: Sized Implementation Chunk for Step 1

This is the concrete specification for the next Claude Code session. It is a focused single session.

### Scope statement

Install Playwright, write one smoke test that verifies the authenticated admin path end-to-end, add it to `npm run e2e`. Nothing else.

### Deliverables (concrete list)

1. **Install `@playwright/test`** at the latest 1.x stable (check `npm show @playwright/test version` first). Add to `devDependencies` in `web/package.json`. Do not `npm install` anything else.

2. **Run `npx playwright install chromium`** to install the Chromium browser binary. Note: this modifies nothing in the repo; browser binaries live in the system playwright cache, not in `web/`.

3. **Write `web/playwright.config.ts`** with the exact config shown in Section 5. Key settings: `testDir: './e2e'`, `workers: 1`, `baseURL: 'http://localhost:3000'`, `fullyParallel: false`, Chromium project only, `globalSetup: './e2e/fixtures/globalSetup.ts'`.

4. **Create `web/e2e/.auth/` directory** and add it to `.gitignore` (append `web/e2e/.auth/` to the Node.js section).

5. **Write `web/e2e/fixtures/globalSetup.ts`** — the global setup script that logs in as admin and saves storageState to `web/e2e/.auth/admin.json`. Use the selectors that actually exist in the login page (investigate `web/src/app/login/` before writing selectors — the implementation chunk must read the login page component to find the correct `data-testid`, `aria-label`, or placeholder text for email/password fields and submit button).

6. **Write `web/e2e/auth/login.spec.ts`** — one smoke test. Journey (specific):
   - Navigate to `http://localhost:3000/login`
   - Fill email field with `admin@test.quantumbuild.ie`
   - Fill password field with `TestAdmin123!`
   - Click the sign-in/login button
   - Assert: URL contains `/dashboard` or `/admin`
   - Navigate to `http://localhost:3000/admin/toolbox-talks/talks`
   - Assert: page heading or a landmark element confirms the Learnings list rendered (recommend: `await expect(page.getByRole('heading', { name: /learnings/i })).toBeVisible()` — but verify the actual heading text in the talks list page component before writing this assertion)

7. **Add npm scripts** to `web/package.json`:
   ```json
   "e2e": "playwright test",
   "e2e:ui": "playwright test --ui",
   "e2e:headed": "playwright test --headed",
   "e2e:report": "playwright show-report"
   ```

8. **Add CLAUDE.md Note 30** covering the Playwright conventions described in Section 11.

9. **Update `.gitignore`** to add `web/e2e/.auth/` (Playwright auth tokens) and optionally `web/playwright-report/` and `web/test-results/` (generated artifacts).

### Scope constraints the implementation chunk must respect

- Do not install any package other than `@playwright/test`.
- Do not modify any existing files in `web/src/` or `src/`.
- Do not modify `vitest.config.ts` or any existing test file.
- If the login page uses non-obvious selectors (no visible labels, only placeholder text), note it but do NOT redesign the login page. Use the selectors that exist.
- If the "Automated Test Tenant" admin user does not exist in the local Dev DB, stop and report — do not attempt to seed data during the implementation chunk.
- Do not write more than one spec file. Breadth is Step 2.
- Do not configure CI. That is explicitly deferred.

### Smoke test journey — specific

The journey is: login with seeded admin account → navigate to Learnings list → verify the list page rendered.

**Before writing selectors,** the implementation chunk MUST read:
- `web/src/app/login/page.tsx` (or the login page component, wherever it lives) to find the form field names and button text
- `web/src/app/(authenticated)/admin/toolbox-talks/talks/page.tsx` (or the Learnings list page) to find the heading text or a stable landmark

The specific assertions should be derived from what the actual components render, not guessed. The above journey description is the intent; the exact Playwright selectors are implementation details resolved by reading the page source.

---

## Section 13: Out of Scope Items

These surfaced during recon and warrant future BACKLOG entries but are not part of Playwright Step 1 or Step 2:

**Visual regression testing** — Percy, Chromatic, or Playwright's built-in `toHaveScreenshot()`. Not in scope until the product's visual design is stable. The wizard is actively changing (§24 edit workflow not yet built). Adding snapshots now would generate constant noise.

**Cross-browser testing** — Firefox and WebKit. The setup chunk is Chromium-only by design. Cross-browser is a Step 2 concern, even then only if the admin team uses non-Chromium browsers.

**Mobile viewport testing** — The product-wide mobile audit (BACKLOG §7.2) is its own effort. Playwright can test viewport sizes but the audit should define what to test first.

**Accessibility automation** — `@axe-core/playwright` for automated WCAG scanning. This is high-value but separate from the smoke test. Add in Step 2 or Step 3 as a dedicated accessibility spec.

**Performance budgets** — Playwright can capture metrics, but no budgets have been defined. Not applicable until the product is in a more stable state.

**Playwright Component Testing** — Playwright has a component test runner for React. This would overlap with vitest's existing coverage. The two frameworks should not coexist for component tests; keep vitest for component/unit tests and Playwright for browser E2E only.

**`login.spec.ts` skipped block (backend test suite)** — BACKLOG §5.15 notes that 3 tests remain in a `test.describe.skip` block in the backend integration test suite. This is unrelated to Playwright but flagged in the recon. It should be addressed in the post-Phase-5 test suite review (§5.15).

**Seeder/JWT divergence (BACKLOG §12)** — The backend integration tests have a structural issue where `TestTenantSeeder` and `CustomWebApplicationFactory.GenerateTestToken` can produce different user representations. This is a backend test infrastructure concern and does not affect Playwright, which authenticates through the real login endpoint.

**`web/e2e/` storageState for external review pages** — The product has public-facing reviewer pages (`/qr/[codeToken]`, external review links). These do not require authentication and are candidates for separate unauthenticated Playwright tests. Out of scope for Step 1.

**Playwright for Hangfire job verification** — Some features (translation validation, content generation, subtitle processing) depend on Hangfire background jobs completing before the UI updates. Testing these flows in Playwright requires waiting strategies (SignalR event receipt or polling). This is complex and out of scope until the smoke suite is stable.

---

## Summary

**The Playwright state is a clean slate.** No config, no specs, no browser installation, no CI. This is low-risk: there is nothing to undo, nothing to migrate, no drift to reconcile.

**The decision that matters most before Step 1:** confirm the "Automated Test Tenant" admin user (`admin@test.quantumbuild.ie` / `TestAdmin123!`) exists in the Development database. If it does not, the smoke test will fail at login. Check this before starting the implementation chunk.

**The decision that matters most before Step 2:** decide whether Playwright should target the Dev database or a separate Playwright database. Using the Dev database is fine for read-only smoke tests but fragile for write tests. A dedicated Playwright database (provisioned as a second PostgreSQL DB, pointed to by `PLAYWRIGHT_DATABASE_URL` env var, with migrations applied separately) is the correct long-term architecture. This decision should be made at Step 2 planning time, not deferred further.
