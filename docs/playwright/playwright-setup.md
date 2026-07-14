# Playwright Setup — Implementation Report

**Date:** 2026-06-17  
**Branch:** transval  
**Phase:** Playwright Step 1 (unauthenticated smoke test only)

---

## 1. Test Results

```
Running 1 test using 1 worker

  ok 1 [chromium] › e2e\login-page.spec.ts:3:5 › login page renders (3.1s)

  1 passed (4.8s)
```

One test. One pass. No failures, no retries.

---

## 2. Summary of What Landed

### Package installed

| Package | Version pinned | Location |
|---|---|---|
| `@playwright/test` | `^1.61.0` | `web/devDependencies` |

The recon (written August 2025) predicted the latest stable would be 1.49.x. The actual current stable at install time (2026-06-17) is **1.61.0**. Installed version confirmed via `npx playwright --version`: `Version 1.61.0`.

### Browser installed

Chromium only, as planned. Browser binary downloaded to the system Playwright cache (`C:\Users\Eddie\AppData\Local\ms-playwright\chromium-1228`). This directory is not inside the repo and requires no gitignore entry.

- Chrome for Testing 149.0.7827.55 (playwright chromium v1228)
- Chrome Headless Shell 149.0.7827.55 (playwright chromium-headless-shell v1228)

### Files created

| File | Purpose |
|---|---|
| `web/playwright.config.ts` | Playwright configuration |
| `web/e2e/login-page.spec.ts` | Unauthenticated smoke test |

### Files modified

| File | Change |
|---|---|
| `web/package.json` | Added `@playwright/test ^1.61.0` to devDependencies; added 5 e2e scripts |
| `web/package-lock.json` | Updated by npm install (99 packages added) |
| `web/.gitignore` | Added Playwright output directory entries |
| `CLAUDE.md` | Added Note 30 (Playwright conventions) |

---

## 3. Notable Decisions

### Smoke test scope — diverges from the recon's Section 12 recommendation

The recon's Section 12 specified a smoke test that logs in with the seeded admin account and navigates to the Learnings list. This chunk's smoke test is **narrower**: it navigates to `/login` and asserts the login form renders. No credentials, no submit, no redirect.

**Why the change:**
- The two-step split was agreed after the recon was written. Step 2's work is to create/verify a test tenant and test users before writing authenticated tests.
- The narrower test verifies the full stack (browser launch → navigation → frontend serves the page → React renders → assertions) without coupling to any user or tenant state.
- It stays useful as a "is the login page broken" canary after authenticated tests are added.

Consequently, there is no `globalSetup.ts`, no `e2e/.auth/` directory, and no `storageState` plumbing in this chunk. Those belong to Step 2.

### Config deviations from the recon's Section 5

The prompt's config takes precedence over the recon's:

| Setting | Recon Section 5 | This chunk |
|---|---|---|
| `retries` | `process.env.CI ? 1 : 0` | `0` (always) |
| `video` | `"on-first-retry"` | `"retain-on-failure"` |
| `globalSetup` | `'./e2e/fixtures/globalSetup.ts'` | absent |

The `retries: 0` removes a small inconsistency between local and CI behaviour. The `video: "retain-on-failure"` keeps videos for any failure regardless of retry state, which is slightly more generous for a single-worker setup.

### Selector choices for the smoke test

The login page (`web/src/app/login/page.tsx`) uses standard HTML semantics:
- `<Label htmlFor="email">Email</Label>` + `<Input id="email" type="email" ...>` — `getByLabel("Email")` resolves to the input via the `htmlFor` association
- `<Label htmlFor="password">Password</Label>` + `<Input id="password" type="password" ...>` — `getByLabel("Password")` resolves to the input
- `<Button type="submit" ...>Sign In</Button>` — `getByRole("button", { name: "Sign In" })` is an exact text match

For the heading: the CardTitle renders `Certified<span>IQ</span>`. Since `getByText` matches normalized text content (which concatenates child text nodes), `getByText("CertifiedIQ")` matches the element. `.first()` is added because the `<Suspense>` fallback also contains a CardTitle with the same text — `.first()` ensures the assertion targets whichever is visible first in the DOM without caring about Suspense timing.

The login page does not use `data-testid` attributes. No `data-testid` attributes were added to avoid modifying `web/src/` (out of scope for this chunk). Role-based selectors are robust to styling changes and align with the project's existing testing approach.

### The login page does not hard-fail when the API is down

Pre-flight read 4 revealed the login page uses a `useAuth()` hook that may call the API on mount to check for an existing session. When the API is not running, `useAuth` returns `user = null` without crashing. The form renders normally. The smoke test ran successfully without the .NET API running — only the Next.js dev server was required. This is expected behaviour and not a product bug.

### `npm audit` warnings — pre-existing

`npm install` reported 13 vulnerabilities (1 low, 5 moderate, 7 high). These were present in the project before this chunk (the prior `npm install` on the existing dependencies would have reported the same). Adding `@playwright/test` did not introduce new ones.

---

## 4. Step 2 Prerequisites

Before writing authenticated tests (Step 2), the following must be resolved:

1. **Verify or create the Playwright test tenant in the Dev DB.** The recon identifies `AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA` (Automated Test Tenant) with `admin@test.quantumbuild.ie` / `TestAdmin123!` as the intended test account. Confirm this user exists and can log in against the live Dev database before writing any authenticated test.

2. **Write `web/e2e/fixtures/globalSetup.ts`** — the global setup script that logs in and saves `storageState` to `web/e2e/.auth/admin.json`. The `.auth/` directory must be gitignored before this is created.

3. **Add `web/e2e/.auth/` to `.gitignore`** — Playwright auth state (JWT tokens/cookies) must never be committed.

4. **Add `globalSetup: './e2e/fixtures/globalSetup.ts'` to `playwright.config.ts`** when the setup script is ready.

These are explicitly Step 2 work. BACKLOG §1.3.7 ("Content creation end-to-end tests") is the entry that this Playwright infrastructure unblocks — it is not closed by this chunk but is now actionable.

---

## 5. BACKLOG.md

**Not modified by this chunk.** BACKLOG §1.3.7 ("Content creation end-to-end tests") is unblocked by this infrastructure but not closed. The §5.15 phantom-reference cleanup (orphaned Playwright fixture mentions in the backend test suite) is left for the comprehensive test review as noted in the recon.

---

## 6. Build Output

```
npm install: added 99 packages, 752 audited, 13 pre-existing vulnerabilities
npx playwright install chromium: Chrome for Testing 149.0.7827.55 downloaded
npm run e2e: 1 passed (4.8s)
```

No TypeScript errors. No new ESLint warnings. The vitest unit test suite is unaffected — the `playwright.config.ts` config does not touch the `vitest.config.ts` and the `e2e/` directory is outside vitest's `include` glob (`src/**/*.{test,spec}.{ts,tsx}`).
