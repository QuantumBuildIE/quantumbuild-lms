# Playwright Step 2 Recon — Test Tenant & Authenticated Infrastructure

**Date:** 2026-06-17  
**Branch:** transval  
**Phase:** Playwright Step 2 recon (investigation only — no code changes)

---

## 1. Verification Verdict — Step 1 Carried-Forward Claims

**Verdict: Partially correct. The constants exist in code; the claim that the users exist in the Dev DB is almost certainly false.**

### What the Step 1 recon and implementation report claimed

> "The seeded admin user already exists at `admin@test.quantumbuild.ie` with password `TestAdmin123!`, under an 'Automated Test Tenant' with tenant ID `AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA`."

### What the source code actually shows

| Claim | File:Line | Status |
|---|---|---|
| Tenant ID `AAAAAAAA-...` | `tests/QuantumBuild.Tests.Common/TestTenant/TestTenantConstants.cs:10` | **Confirmed in code** |
| Email `admin@test.quantumbuild.ie` | `TestTenantConstants.cs:22` | **Confirmed in code** |
| Password `TestAdmin123!` | `TestTenantConstants.cs:23` | **Confirmed in code** |
| These users exist in the Dev DB | — | **Almost certainly false** |

The `TestTenantSeeder` (`tests/QuantumBuild.Tests.Common/TestTenant/TestTenantSeeder.cs`) does create the Automated Test Tenant and its users — but it is **only invoked from `CustomWebApplicationFactory`**, which uses a **Testcontainers PostgreSQL container** (`CustomWebApplicationFactory.cs:40-45`). That container is ephemeral; it spins up and tears down with each integration test run. Nothing from it reaches the shared Dev DB (`rascor_stock` on `localhost:5432`).

The Step 1 recon stated: *"the data persists between runs since the integration tests use Testcontainers rather than the shared Dev DB."* This is self-contradictory — if the tests use Testcontainers, the data explicitly does NOT persist to the Dev DB. The Step 1 recon confused the two databases.

**Practical impact:** The Playwright globalSetup will fail at login unless the test tenant is first seeded into the Dev DB. This seeding does not happen automatically today. Step 2 Chunk A must fix this.

**Process note:** The Step 1 recon stated the test tenant credentials as fact without citing a file. In future recons, every factual claim about the codebase must be backed by a file:line citation. "It says so in the code" is the standard; "I believe it exists" is not.

---

## 2. Seeder Inventory

### 2a. Application Seeder — `DataSeeder.cs` (always runs)

**File:** `src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs`  
**Invoked:** `Program.cs:332` — `await DataSeeder.SeedAsync(app.Services)` — **unconditionally on every startup, in every environment including Production.**

| What it creates | Detail |
|---|---|
| **Tenant** `11111111-1111-1111-1111-111111111111` | Name: "QUANTUMBUILD", Code: "QUANTUMBUILD" |
| **SuperUser** `superuser@certifiediq.ai` | Password: `SuperUser123!`, `IsSuperUser = true`, `TenantId = DefaultTenantId` |
| **Admin user** `admin@quantumbuild.ai` | Password: `Admin123!`, Role: "Admin", `TenantId = DefaultTenantId` |
| **Admin employee** record | Linked to admin user, `EMP-ADMIN` |
| **Roles** | SuperUser, Admin, Supervisor, Operator |
| **Permissions** | All `Permissions.GetAll()` items |
| **Role-permission assignments** | Per `GetPermissionsForRole` switch table |
| **Lookup categories** | TrainingCategory, Department, JobTitle, Language |
| **Language values** | 33 languages (ElevenLabs Multilingual v2+v3) |
| **Training categories** | 15 categories seeded for `DefaultTenantId` |
| **Tenant modules** | Learnings module enabled for every tenant in the DB |

**Idempotency:** Skip-if-exists throughout (`AnyAsync` checks before every insert). Safe to re-run.

**No environment gating exists.** The call at `Program.cs:332` has no `if (app.Environment.IsDevelopment())` wrapper. This seeder runs in Production. See Section 12 (Out of Scope) for the pre-existing security implication.

### 2b. Toolbox Talks Module Seeder (always runs)

**File:** `Program.cs:488-513` — `SeedToolboxTalksDataAsync`  
**Invoked:** `Program.cs:335` — also unconditionally.

Creates: system safety glossaries, sector data, regulatory profiles, regulatory requirements. All system-level, no tenant scope. Idempotent.

### 2c. Test Tenant Seeder — `TestTenantSeeder.cs` (integration tests only)

**File:** `tests/QuantumBuild.Tests.Common/TestTenant/TestTenantSeeder.cs`  
**Invoked:** `CustomWebApplicationFactory` → Testcontainers ephemeral DB — **never touches the Dev DB.**

Creates (for `AAAAAAAA-...` tenant): tenant record, 3 users (Admin/Supervisor/Operator) with proper password hashes via UserManager, sites, employees, companies, contacts, toolbox talks with sections/questions, schedules, scheduled talks, validation runs/results, safety glossaries, content creation sessions, DPA acceptance.

Key method: `SeedUsersWithUserManagerAsync()` (`TestTenantSeeder.cs:263`) uses `UserManager.CreateAsync(user, password)` for proper ASP.NET Identity password hashing. The fallback `SeedUsersAsync()` (line 186) inserts users WITHOUT password hashes — those users cannot authenticate via the real login endpoint. **Any Playwright seeder must use the UserManager path.**

---

## 3. Environment Gating

**DataSeeder and SeedToolboxTalksDataAsync run unconditionally in all environments.** There is no `app.Environment.IsDevelopment()` check around either seeder invocation.

**This is the critical constraint for Playwright seeding:** any test data added to `DataSeeder.cs` would run in Production. Playwright test users in Production is unacceptable — they would represent real accounts with known passwords (`TestAdmin123!`) in a production system.

**Required approach:** Playwright seeding must be inside a `if (app.Environment.IsDevelopment())` block in `Program.cs`. A dedicated `PlaywrightSeeder.cs` class (separate from `DataSeeder.cs`) is the cleanest way to enforce this.

Is the gating tested? No. There are no tests that verify the seeder does not run in Production. This is a pre-existing gap — note it but do not address it here.

---

## 4. Coverage Decision — What Users Playwright Needs

**Recommendation: Comprehensive set from the start — Admin, Supervisor, and Operator.**

Rationale:
- `TestTenantConstants.cs` already defines all three with fixed GUIDs and passwords. Copying these constants to `PlaywrightSeeder.cs` costs one line per user.
- Seeding 3 users instead of 1 is trivial; revisiting the seeder later to add them is not.
- Once globalSetup saves three storageState files, any future test can pick up any role without touching backend code.

**SuperUser:** Do NOT seed a Playwright-specific SuperUser. The seeded system SuperUser (`superuser@certifiediq.ai` / `SuperUser123!`) is already in the Dev DB (via `DataSeeder.SeedSuperUserAsync`). Since SuperUser bypasses tenant isolation, Playwright SuperUser tests can use this account against any tenant — including the test tenant. Reuse it. Store its storageState as `web/e2e/.auth/superuser.json` if and when SuperUser tests are written.

**Who to seed in Step 2 Chunk A:**

| User | Email | Password | Role | Fixed GUID |
|---|---|---|---|---|
| Admin | `admin@test.quantumbuild.ie` | `TestAdmin123!` | Admin | `AAAAAAAA-0001-0001-0001-000000000001` |
| Supervisor | `supervisor@test.quantumbuild.ie` | `TestSupervisor123!` | Supervisor | `AAAAAAAA-0001-0001-0001-000000000006` |
| Operator | `operator@test.quantumbuild.ie` | `TestOperator123!` | Operator | `AAAAAAAA-0001-0001-0001-000000000004` |

These match `TestTenantConstants.cs` exactly, so integration tests and Playwright tests reference the same fixed identifiers.

---

## 5. Tenant Configuration

Minimum viable configuration for the Playwright test tenant, with rationale for each:

| Config item | Value | Why |
|---|---|---|
| **Tenant ID** | `AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA` | Matches `TestTenantConstants.TenantId` — shared with integration tests |
| **Tenant name** | "Automated Test Tenant" | Matches `TestTenantConstants.TenantName` |
| **Tenant code** | "TEST" | Short, readable in logs |
| **Sectors** | Construction (`construction`) | Construction is the primary reference sector; sufficient for admin navigation tests; required to avoid empty-sector warnings in some UIs |
| **TenantModule** | Learnings | Required for admin to access `/admin/toolbox-talks/talks`; `DataSeeder.SeedTenantModulesAsync` will already handle this on API startup for any tenant that exists by then — but the module seed runs before Playwright seeding, so the PlaywrightSeeder must call this explicitly or it will be picked up on the next API restart |
| **DPA acceptance** | Seed one | The `TestTenantSeeder` seeds a DPA record — suggests the frontend or some flow requires it. Confirmed: `DpaAcceptance` entity exists. Safer to seed one than to debug a blocking DPA gate at implementation time. |
| **ToolboxTalkSettings** | Default values | Not strictly required for list-page navigation; skip in Step 2 for simplicity. Add if integration smoke test fails. |
| **Sample content (talks, schedules)** | Do NOT seed | Tests should create their own data. Seeded content adds state that can drift. The authenticated smoke test (Step 2) is read-only and needs no content. |
| **`UseNewWizard` setting** | `"false"` (legacy, default) | Do not change the default. The wizard toggle (CLAUDE.md Note 29) is a tenant-settings key. Step 2's smoke test targets the Learnings list page, not the wizard. Wizard-specific E2E tests should flip this explicitly within the test. |

**TenantSector note:** The `TenantSector` entity requires `SectorId` — a FK to the `Sector` entity. `SectorSeedData` (run in `SeedToolboxTalksDataAsync`) seeds sectors. The sector with key `"construction"` must exist before `PlaywrightSeeder` assigns it to the test tenant. Since `SeedToolboxTalksDataAsync` runs before `PlaywrightSeeder`, the sector will be present.

---

## 6. Seeding Mechanism

**Recommendation: Option B — Separate `PlaywrightSeeder.cs`, invoked from `Program.cs` under `IsDevelopment()`.**

```csharp
// Program.cs — after SeedToolboxTalksDataAsync
if (app.Environment.IsDevelopment())
{
    await PlaywrightSeeder.SeedAsync(app.Services);
}
```

**Why not Option A (extend DataSeeder.cs)?**
`DataSeeder.cs` runs in Production. Adding test accounts to it creates known-password accounts in every production environment. Non-negotiable rejection.

**Why not Option D (API-driven globalSetup)?**
Requires a SuperUser login to create a tenant and users via API. The API doesn't have a "create tenant" endpoint accessible from Playwright without a SuperUser JWT. This adds failure modes (globalSetup fails if the SuperUser password changed), makes the test setup harder to debug, and re-runs expensive API calls every `playwright test` invocation. Backend seeding is simpler and more reliable.

**Why not Option C (SQL script)?**
A `.sql` file committed to the repo and run manually is fragile — it would need to be re-run after every Dev DB wipe, and there's no automated check that it's up to date. A C# seeder can use EF's model and evolves with migrations automatically.

**`PlaywrightSeeder.cs` placement:** `src/Core/QuantumBuild.Core.Infrastructure/Persistence/PlaywrightSeeder.cs`. Alongside `DataSeeder.cs`.

**Key implementation constraints:**
- Must use `UserManager.CreateAsync(user, password)` for password hashing (CLAUDE.md Note 22 equivalent: use `IServiceProvider` to resolve UserManager).
- All `TenantEntity` rows must have `TenantId` set explicitly (CLAUDE.md Note 22).
- Full idempotency: every check before insert (`AnyAsync` + skip). Safe to re-run after DB wipe.
- Must seed a `TenantSector` linking the test tenant to the construction sector, by looking up the `Sector` row with `Key == "construction"`.
- Must seed a `TenantModule` for `ModuleNames.Learnings` (or verify that `DataSeeder.SeedTenantModulesAsync` will have run first — it does iterate all tenants including any new ones added before it runs, but the call order in `Program.cs` is: `DataSeeder.SeedAsync` → `SeedToolboxTalksDataAsync` → `PlaywrightSeeder.SeedAsync`. The tenant does not exist when `SeedTenantModulesAsync` runs, so `PlaywrightSeeder` must seed the module explicitly).

---

## 7. Data Isolation Strategy

**Per-run cleanup** (not per-test cleanup) for Step 2. The authenticated smoke test creates no data, so cleanup is moot for Step 2 specifically.

**Long-term strategy:**

| Entity type | Owner | Cleanup |
|---|---|---|
| Test tenant | PlaywrightSeeder | Never deleted |
| Test users (Admin/Supervisor/Operator) | PlaywrightSeeder | Never deleted |
| TenantModule, TenantSector | PlaywrightSeeder | Never deleted |
| Entities created by tests (talks, employees, schedules, etc.) | Individual test | `afterEach` via API DELETE calls |

**Cleanup mechanism:** Playwright `test.afterEach` with `request.delete('/api/toolbox-talks/{id}')` etc., scoped to entities the test created. Use unique-per-run identifiers (e.g. append a test-run timestamp to talk titles) so cleanup can target only the test's own data even if a previous run left orphans.

**What happens if a test crashes before `afterEach`?** Orphaned data remains in the Dev DB. This is acceptable for now (Dev DB). Re-running `PlaywrightSeeder` won't help since it skips-if-exists. A `ResetAsync` endpoint (SuperUser-only, Development-only) is a future option for recovering from bad state.

**`TestTenantSeeder.CleanupAsync()`** (`TestTenantSeeder.cs:65`) is the reference for FK-safe delete order. The Playwright implementation chunks that write data-creating tests should follow that order.

---

## 8. globalSetup Design

### File layout

```
web/e2e/
├── .auth/                    # gitignored; created by globalSetup
│   ├── admin.json
│   ├── supervisor.json
│   └── operator.json
├── fixtures/
│   └── globalSetup.ts        # NEW — Step 2 Chunk B
└── login-page.spec.ts        # Existing (Step 1)
```

### `.gitignore` addition (Step 2 Chunk A)

`web/.gitignore` currently has `/playwright-report/`, `/test-results/`, `/playwright/.cache/` — but NOT `/e2e/.auth/`. Add:

```
/e2e/.auth/
```

### `globalSetup.ts` behaviour

1. For each test user (Admin, Supervisor, Operator), call `POST /api/auth/login` via the Playwright `request` API.
2. On 401/404, throw a clear error:
   ```
   Error: Playwright test user 'admin@test.quantumbuild.ie' could not be authenticated (HTTP 401).
   Has PlaywrightSeeder run? Ensure the .NET API is running at http://localhost:5222 and the Dev DB
   contains the Playwright test tenant. See docs/playwright/playwright-test-tenant-recon.md §6.
   ```
3. On success, create a browser context from the JWT and save `context.storageState()` to `e2e/.auth/{role}.json`.
4. The globalSetup **always runs** — does not check for existing storageState files. JWT tokens expire in 60 minutes; re-running `playwright test` after an hour would otherwise fail with stale tokens.

**Does globalSetup require the .NET API?** Yes. Login is a real API call. If the API is down, globalSetup fails with a connection error. That is the correct behaviour — it's a cleaner failure than discovering the problem mid-test.

### `playwright.config.ts` updates (Step 2 Chunk B)

```typescript
globalSetup: './e2e/fixtures/globalSetup.ts',
projects: [
  // Unauthenticated tests (login page smoke test etc.)
  {
    name: 'unauthenticated',
    use: { ...devices['Desktop Chrome'] },
    testMatch: ['**/login-page.spec.ts'],  // or a glob for e2e/public/
  },
  // Authenticated — Admin
  {
    name: 'admin',
    use: { ...devices['Desktop Chrome'], storageState: 'e2e/.auth/admin.json' },
    testIgnore: ['**/login-page.spec.ts'],
  },
],
```

For Step 2, ship two projects: `unauthenticated` and `admin`. Add `supervisor` and `operator` projects when tests covering those roles are written. Keeping unused projects out reduces test matrix noise.

### storageState mechanism

The `storageState` file stores localStorage and cookies. The app uses `localStorage` for "keep me logged in" tokens (CLAUDE.md JWT section). The `globalSetup` must explicitly set localStorage after login — not just cookies.

Implementation: after `POST /api/auth/login` returns `accessToken` and `refreshToken`, use a headless browser page to navigate to the frontend and set the tokens via `page.evaluate(() => localStorage.setItem(...))`, then save `page.context().storageState()`. This ensures the storage state file captures what the frontend's auth context reads.

---

## 9. Authenticated Smoke Test for Step 2

**One test, Admin role only.**

Shape:
1. Start with `storageState: 'e2e/.auth/admin.json'` — no login form interaction.
2. Navigate to `/admin/toolbox-talks/talks`.
3. Assert: a heading or landmark confirms the Learnings list rendered — e.g. `getByRole('heading', { name: /learnings/i })` or a "New Learning" button.
4. No entity creation. No cleanup.

**Why the Learnings list?** It is the most commonly used admin page and least coupled to data state — the page renders even with zero talks in the tenant. It verifies the auth plumbing (storageState → valid JWT → API returns 200 → list page renders) without requiring seeded content.

**Do not** ship one smoke test per role in Step 2. Supervisor and Operator smoke tests can follow when those paths are fleshed out. The goal of Step 2 is to prove the globalSetup/storageState plumbing works.

---

## 10. Integration Test Patterns

### What to lift for Playwright

| Pattern | File | Playwright applicability |
|---|---|---|
| Fixed GUIDs from `TestTenantConstants` | `TestTenantConstants.cs` | Reuse the same GUIDs in `PlaywrightSeeder` — same entity, two code paths, one identity |
| Skip-if-exists idempotency | `TestTenantSeeder.cs:137` | Copy pattern exactly — `if (await ctx.AnyAsync(...)) return;` |
| UserManager path for password hashing | `TestTenantSeeder.cs:264` | **Critical** — must use this in PlaywrightSeeder |
| FK-safe delete order in `CleanupAsync` | `TestTenantSeeder.cs:65` | Reference when writing test `afterEach` cleanup |

### Where to diverge

The integration tests inject fake services (FakeEmailSender, FakeR2StorageService, etc.) via `CustomWebApplicationFactory`. Playwright hits the real running API with real services. This means:
- Playwright tests must not trigger email sends (or must accept that emails go to the configured stub provider)
- Playwright tests must not trigger file uploads to R2 (or the app's `StubEmailProvider` / real R2 will be called)
- Tests that exercise AI-generated content, subtitle processing, or translation will call live external APIs — avoid these in the smoke test suite until a test-mode flag exists

The integration suite has `FakeContentParserService`, `FakePdfExtractionService` etc. Playwright has no equivalent — tests must avoid those code paths or accept the external API cost.

---

## 11. Recommended Chunk Split

**Two chunks. Backend first.**

### Chunk 2A — Backend: PlaywrightSeeder + gitignore

**Scope:**
- Create `src/Core/QuantumBuild.Core.Infrastructure/Persistence/PlaywrightSeeder.cs` with `SeedAsync(IServiceProvider)`:
  - Idempotent upsert of Automated Test Tenant (`AAAAAAAA-...`)
  - 3 users via UserManager (Admin / Supervisor / Operator) with roles
  - TenantSector: join to `construction` sector
  - TenantModule: Learnings
  - DPA acceptance for Admin user
- Wire into `Program.cs` under `if (app.Environment.IsDevelopment())` block, after `SeedToolboxTalksDataAsync`
- Add `web/e2e/.auth/` to `web/.gitignore`
- Update `CLAUDE.md` Note 30 to document PlaywrightSeeder and the test credentials

**Deliverable:** After Chunk 2A is deployed to Dev, the tester can manually verify that `admin@test.quantumbuild.ie` / `TestAdmin123!` logs in successfully against the running Dev API. This verification gates Chunk 2B.

**No frontend changes in Chunk 2A.**

### Chunk 2B — Frontend: globalSetup + storageState + authenticated smoke test

**Prerequisite:** Chunk 2A must be deployed and the test login verified manually before starting Chunk 2B.

**Scope:**
- Create `web/e2e/fixtures/globalSetup.ts`: login as Admin/Supervisor/Operator, save three storageState files
- Update `web/playwright.config.ts`: add `globalSetup`, restructure `projects` (unauthenticated + admin)
- Create `web/e2e/admin/learnings-list.spec.ts`: one authenticated smoke test (admin storageState → navigate to `/admin/toolbox-talks/talks` → assert list page renders)
- Update CLAUDE.md Note 30 with the final auth state file locations

**Before writing selectors in Chunk 2B:** Read the current `web/src/app/(authenticated)/admin/toolbox-talks/talks/page.tsx` to find the heading or stable landmark. Do not guess selector text.

---

## 12. Out of Scope Concerns

Items surfaced during this recon that are not part of Step 2:

### Production security: DataSeeder runs without environment gating

`Program.cs:332` calls `DataSeeder.SeedAsync(app.Services)` unconditionally. This means `admin@quantumbuild.ai` / `Admin123!` and `superuser@certifiediq.ai` / `SuperUser123!` are seeded into every environment including Production. Combined with the fact that these credentials are in the committed codebase (`DataSeeder.cs:309`, `DataSeeder.cs:256`), this is a latent security risk. Not introduced by Playwright work. Candidate for a future security hardening pass.

### DPA acceptance gate — verify at implementation time

`TestTenantSeeder` seeds a `DpaAcceptance` record for the test tenant. This suggests some flow requires DPA acceptance before proceeding. The Playwright smoke test should verify at implementation time whether the Admin user can reach `/admin/toolbox-talks/talks` without DPA acceptance, or whether the DPA gate blocks the navigation. `PlaywrightSeeder` should seed a DPA record to avoid discovering this as a surprise mid-test.

### CLAUDE.md InputMode enum is out of date

CLAUDE.md lists `InputMode — Video, Pdf`. The actual enum (`QuantumBuild.Modules.ToolboxTalks.Domain.Enums.InputMode`, file confirmed) is:
- `Text = 1`
- `Pdf = 2`
- `Video = 3`

Three values, not two. The CLAUDE.md entry omits `Text`. Low-urgency doc correction.

### No environment gating is tested

There are no tests that assert the PlaywrightSeeder (or DataSeeder) does not run in a non-Development environment. This gap means a future regression could re-introduce ungated seeding to Production. Consider adding an integration test or startup validation that checks for this.

### CI integration (deferred from Step 1)

Still deferred. CI for Playwright requires: PostgreSQL service, `dotnet run` for the API, `npm run dev` for the frontend, migration, seeding, then Playwright. This is a multi-service GitHub Actions setup. Separate effort.

### Step 1 recon process gap: cite-or-stop

The Step 1 recon asserted the test tenant's existence in the Dev DB without citing a file. The correct standard: every factual claim about the codebase must be backed by `file:line`. If a claim cannot be backed by source code, it must be flagged as "requires runtime verification" rather than stated as fact. This recon documents the gap so future recons apply the higher standard.

---

## Files Read

1. `docs/playwright/playwright-setup-recon.md`
2. `docs/playwright/playwright-setup.md`
3. `web/playwright.config.ts`
4. `web/e2e/login-page.spec.ts`
5. `src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs`
6. `src/QuantumBuild.API/Program.cs`
7. `web/.gitignore`
8. `tests/QuantumBuild.Tests.Common/TestTenant/TestTenantSeeder.cs`
9. `tests/QuantumBuild.Tests.Common/TestTenant/TestTenantConstants.cs`
10. `tests/QuantumBuild.Tests.Integration/Fixtures/CustomWebApplicationFactory.cs` (first 100 lines)
11. `src/QuantumBuild.API/appsettings.json`
12. `src/QuantumBuild.API/appsettings.Development.json`
13. `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Enums/InputMode.cs`
14. Grep: `DpaAcceptance` in `src/Core/QuantumBuild.Core.Infrastructure/Identity/AuthService.cs` (no matches — confirms no DPA gate in login flow itself)

---

## Report Written

`docs/playwright/playwright-test-tenant-recon.md`

---

## Recommended Chunk Split

**Two chunks.** See Section 11 for full scope of each.

- **Chunk 2A (backend):** `PlaywrightSeeder.cs` + `Program.cs` wiring + gitignore update. Backend-only. Verifiable independently before frontend work starts.
- **Chunk 2B (frontend):** `globalSetup.ts` + updated `playwright.config.ts` + one authenticated smoke test. Depends on Chunk 2A being deployed and the test login verified manually.

---

## Out of Scope Items Flagged

| Item | Reason |
|---|---|
| DataSeeder runs ungated in Production | Pre-existing security concern; not introduced here |
| DPA acceptance gate | Needs runtime verification at Chunk 2B implementation time |
| CLAUDE.md `InputMode` enum incomplete | Doc accuracy; low urgency |
| No test for "seeder doesn't run in Production" | Testing gap; future hardening |
| CI integration | Deferred from Step 1; still deferred |
| Step 1 recon cite-or-stop gap | Process lesson; documented here |
| CI database isolation strategy | Deferred |
| Multi-browser Playwright coverage | Deferred |
| Playwright for Hangfire/SignalR-dependent flows | Complex; deferred until smoke suite is stable |
