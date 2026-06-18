# Playwright Step 2 (first slice) — Authenticated SuperUser Login Test

**Date:** 2026-06-18
**Branch:** transval

---

## 1. Test Results

```
Running 3 tests using 1 worker

  ok 1 [setup] › e2e\auth.setup.ts:13:6 › authenticate as superuser (3.9s)
  ok 2 [unauthenticated] › e2e\login-page.spec.ts:3:5 › login page renders (535ms)
  ok 3 [authenticated] › e2e\authenticated\login-flow.spec.ts:6:5 › superuser reaches tenants page via saved auth state (836ms)

  3 passed (16.2s)
```

Reuse path (API already running):

```
  3 passed (4.5s)
```

Prior baseline: `1 passed (4.8s)` — Step 1 unauthenticated smoke is still green.

---

## 2. Summary of What Landed

### Files created (in scope)

| File | Purpose |
|---|---|
| `web/e2e/auth.setup.ts` | Playwright setup project — logs in as SuperUser, saves session to `e2e/.auth/superuser.json` |
| `web/e2e/authenticated/login-flow.spec.ts` | Authenticated smoke test — SuperUser navigates to `/admin/tenants` via saved auth state |

### Files modified (in scope)

| File | Change |
|---|---|
| `web/playwright.config.ts` | `webServer` → array (Next.js + .NET API); added `setup`, `unauthenticated`, `authenticated` projects |
| `web/.gitignore` | Added `/e2e/.auth/` |
| `web/src/app/login/page.tsx` | Added `data-testid` attrs to email input, password input, submit button |
| `CLAUDE.md` | Updated Note 30 to reflect Step 2 state |

### Files changed outside the stated scope

| File | Rationale |
|---|---|
| `src/QuantumBuild.API/appsettings.Development.json` | `Cors:AllowedOrigins` was not configured anywhere. The browser in Playwright makes cross-origin requests from `http://localhost:3000` to `http://localhost:5222`. Without this, the CORS preflight returns 204 with no `Access-Control-Allow-Origin` header and Chromium blocks the request — the login call never completes and the auth setup cannot navigate away from `/login`. Note: `appsettings.Development.json` is gitignored, so this change is local-only. The portable fix is the `env: { Cors__AllowedOrigins__0: "http://localhost:3000" }` entry added to the `webServer` config in `playwright.config.ts` — this injects the CORS setting whenever Playwright spawns the API. Pre-existing gap; explicit go-ahead received from user. |

---

## 3. Notable Decisions

### Auth fixture: setup project, not globalSetup

Used the Playwright "setup project" pattern (a project with `testMatch: /auth\.setup\.ts/` and no `dependencies`) rather than `globalSetup`. This is the recommended approach since Playwright 1.42. The `authenticated` project declares `dependencies: ["setup"]`, so Playwright always runs the auth setup before any authenticated test. This is cleaner than `globalSetup` for multi-project configs — the dependency is explicit and re-running just the authenticated project retries the setup.

### Credential env vars — §5.31 pattern

Auth setup reads credentials from `process.env.SEED_SUPERUSER_EMAIL` and `process.env.SEED_SUPERUSER_PASSWORD`, falling back to `"superuser@certifiediq.ai"` / `"SuperUser123!"`. This mirrors the `SEED_ADMIN_EMAIL`/`SEED_ADMIN_PASSWORD` pattern in `tests/QuantumBuild.Tests.E2E/global-setup.ts`. Dev-only credentials hardcoded as fallbacks per Note 31 (the values are in `appsettings.Development.json` anyway — not secrets).

### Storage state path

`web/e2e/.auth/superuser.json`. The `auth.setup.ts` creates the `.auth/` directory with `mkdirSync({ recursive: true })` before writing. The directory is gitignored via `web/.gitignore` (`/e2e/.auth/`).

### Health endpoint URL

`http://localhost:5222/health`. ASP.NET Core health checks (`MapHealthChecks("/health")`) are already wired in `Program.cs` with a PostgreSQL health check. This is the correct readiness signal — if Postgres is down, the health check returns 503 and Playwright waits (or fails with a clear timeout error rather than mysterious test failures).

### Project `testMatch` for unauthenticated

Pattern `/e2e[/\\][^/\\]+\.spec\.ts$/` — matches only files directly in `e2e/` (not in subdirectories). This prevents the `unauthenticated` project from picking up files in `e2e/authenticated/`. The `authenticated` project uses an explicit `testDir: './e2e/authenticated'` override so its files don't overlap with the unauthenticated project.

### data-testid selectors on login form

Added `data-testid="login-email-input"`, `data-testid="login-password-input"`, `data-testid="login-submit-button"` to `web/src/app/login/page.tsx`. Both `Input` and `Button` shadcn components spread `{...props}` onto their underlying DOM elements, so the attributes reach the actual `<input>` and `<button>` nodes. The auth setup uses `page.getByTestId()` for reliability. The existing Step 1 smoke continues to use `getByLabel()` and `getByRole()` (unchanged).

### CORS discovery

The first test run failed — the browser was blocked by CORS (no `Access-Control-Allow-Origin` header on the preflight). `Cors:AllowedOrigins` was not configured anywhere (no `appsettings.Development.json` entry, no user secrets, no env vars). Added `"Cors": { "AllowedOrigins": ["http://localhost:3000"] }` to `appsettings.Development.json`. Stopped and reported before applying the fix; explicit go-ahead received.

---

## 4. Build Output

```
npx tsc --noEmit          : clean (no errors)
npm run test (vitest)     : 3 files, 15 tests, all passed
dotnet build (API)        : 0 errors, 0 warnings
npm run e2e (Playwright)  : 3 passed
```

---

## 5. Foundation for Future Authenticated Tests

Every future authenticated test inherits from this work:

1. **Add a file under `web/e2e/authenticated/`** — it's automatically assigned to the `authenticated` Playwright project
2. **SuperUser session is pre-loaded** — `page.context()` starts with the SuperUser's cookies + localStorage from `e2e/.auth/superuser.json`
3. **No login boilerplate** — tests go straight to `await page.goto("/some/admin/page")` and assert

The auth fixture will stay fresh for the full test run (access tokens are 60 min; the setup project runs at the start of each `npm run e2e` invocation).

For non-SuperUser roles (Admin, Supervisor, Operator): create additional setup files (e.g., `admin.setup.ts`) following the same pattern as `auth.setup.ts`, register a second setup project in `playwright.config.ts`, and add a corresponding authenticated project with its own `storageState` path.

---

## Addendum — SuperUser identity verification (2026-06-18)

### Option chosen: Option B (API call from inside the test)

**Why not Option A (add `data-testid` to TopNav email element):** The email is rendered inside `DropdownMenuContent` — a Radix UI portal that is not in the DOM until the user clicks the avatar. Testing it would require an extra click step and relies on UI rendering details. Option B is faster and more direct.

**What the assertion now verifies:**

The test now does two things, not one:

1. `page.getByRole("heading", { name: "Tenants", level: 1 })` — proves SOME authenticated session reached the correct page.
2. The `/api/auth/me` assertion — proves the token in `localStorage` belongs to the SuperUser account specifically, by asserting `body.email === "superuser@certifiediq.ai"` and `body.isSuperUser === true`.

A wrong-user auth bug (e.g., auth setup accidentally logged in as the tenant Admin) would pass assertion 1 but fail assertion 2. The test now catches that class of silent failure.

**How it works:**

```typescript
const token = await page.evaluate(() => localStorage.getItem("accessToken"));
// JWT is stored under "accessToken" key in localStorage (see auth-context.tsx getStoredToken)

const response = await page.request.get(
  "http://localhost:5222/api/auth/me",
  { headers: { Authorization: `Bearer ${token!}` } }
);
// /api/auth/me returns a direct DTO (no Result<T> envelope) — see Note 18 in CLAUDE.md

expect(body.email).toBe(SUPERUSER_EMAIL);     // "superuser@certifiediq.ai"
expect(body.isSuperUser).toBe(true);           // JWT claim is_super_user === "true"
```

**Credential constant:** `SUPERUSER_EMAIL` uses the same `process.env.SEED_SUPERUSER_EMAIL || "superuser@certifiediq.ai"` pattern as `auth.setup.ts` — both sides stay in sync if the env var is set.

**No selectors added:** Option B required no changes to any production source file. Only `login-flow.spec.ts` was modified.
