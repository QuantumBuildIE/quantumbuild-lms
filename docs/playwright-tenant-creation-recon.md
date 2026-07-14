# Playwright Tenant-Creation Recon

**One-line summary:** SuperUser creates a tenant via a single-page form at `/admin/tenants/new`; the POST call atomically creates the tenant + admin user + linked employee + welcome email (when contact details provided); sectors are assigned in separate sequential calls; no delete endpoint exists so tests must use unique tenant names per run.

---

## Q1 — What does the SuperUser tenant-creation UI flow actually look like today?

**Key files:**
- `web/src/app/(authenticated)/admin/tenants/new/page.tsx`
- `web/src/components/admin/tenant-form.tsx`

### Form fields (Zod schema)

| Field | Type | Required | Constraint |
|---|---|---|---|
| `name` | text | **Yes** | min 1, max 200 |
| `code` | text | No | max 50 — optional unique identifier |
| `companyName` | text | No | max 200 |
| `contactName` | text | No | max 200 |
| `contactEmail` | email | No | max 200, must be valid email if provided |
| `sectorIds` | checkbox array | **Yes (≥1)** | at least one sector must be checked |

UI note: "If provided with an email, an admin user account will be created and a welcome email sent" appears beneath the contact fields.

### Step-by-step flow a user executes

1. Navigate to `/admin/tenants/new`
2. Enter **Tenant Name** (required)
3. Optionally enter Code, Company Name, Contact Name, Contact Email
4. Check at least one **Sector** checkbox (the sector list is loaded from `GET /api/toolbox-talks/sectors`)
5. Click **"Create Tenant"**
6. Form submits; button becomes disabled and reads "Creating…"
7. If contact details provided: POST creates tenant + admin user + employee + email in one call
8. Sequential POST calls assign each selected sector
9. `toast.success("Tenant created successfully")` fires
10. `router.push("/admin/tenants")` — redirect to the tenant list page

### §3.11 double-submit guard

The submit button's exact JSX:

```tsx
<Button
  type="submit"
  disabled={isPending || form.formState.isSubmitting}
>
  {isPending ? "Creating..." : "Create Tenant"}
</Button>
```

Where `isPending = createTenant.isPending || updateTenant.isPending`.

Both guards must be false for a submit to go through. A Playwright test clicking the button once and awaiting navigation handles this correctly without needing an explicit `waitFor` on the disabled state.

### Success affordance

1. `toast.success("Tenant created successfully")` — appears immediately on mutation success
2. `onSuccess()` called synchronously after → `router.push("/admin/tenants")`

Both happen in the same microtask tick; Playwright's `waitForURL` waiting for `/admin/tenants` is the correct assertion point.

### Error display

| Error type | How displayed |
|---|---|
| Zod validation failure | Inline `<FormMessage />` under each field (client-side, before submit) |
| API error (4xx) | `toast.error("Failed to create tenant")` |
| Partial success (HTTP 207) — tenant OK, onboarding failed | `toast.warning("Tenant created but sector assignment failed — please configure sectors...")` + redirect still fires |
| Sector assignment failure (individual sector call fails) | Warning toast; redirect still fires |

---

## Q2 — What backend endpoints does the form hit?

**Key files:**
- `web/src/lib/api/admin/tenants.ts`
- `web/src/lib/api/admin/tenant-sectors.ts`
- `src/QuantumBuild.API/Controllers/TenantsController.cs`

### Call 1: Create tenant — `POST /api/tenants`

**Request body (`CreateTenantCommand`):**

```json
{
  "name": "...",
  "code": "...",
  "companyName": "...",
  "contactEmail": "...",
  "contactName": "..."
}
```

**Response envelope:** `Result<TenantDetail>` — frontend reads `response.data.data` (Note 18 pattern).

**What happens server-side in this single call:**

```
TenantService.CreateAsync()         → tenant row persisted
  ↳ (if contactEmail + contactName)
    TenantOnboardingService.ProvisionTenantAsync()
      ↳ EnsureDefaultRolesExistAsync() → verifies Admin/Supervisor/Operator roles exist
      ↳ CreateAdminUserAsync()         → User entity, email confirmed = false
      ↳ AssignRoleAsync()              → user added to "Admin" role
      ↳ CreateLinkedEmployeeAsync()    → Employee entity, JobTitle="Administrator", linked UserId/EmployeeId
      ↳ SendWelcomeEmailAsync()        → generates password reset token, calls EmailService
```

**Partial failure path:** If `ProvisionTenantAsync` fails, the controller returns HTTP 207 with `{ success, data (the tenant), onboardingErrors, message }`. The tenant row already exists. The frontend shows a warning toast and still redirects.

### Calls 2–N: Assign sectors — `POST /api/tenants/{id}/sectors`

One call per selected sector, executed **sequentially** (not parallel) in the form's submit handler:

```json
{ "sectorId": "<uuid>", "isDefault": true }   // first sector only
{ "sectorId": "<uuid>", "isDefault": false }  // subsequent sectors
```

Response: `TenantSectorDto` — returned directly (no `Result<T>` wrapper — Note 18 pattern, newer controller).

---

## Q3 — What constitutes "all follow-up tasks" after tenant creation?

### In-flow (same submit — POST /api/tenants)

| Task | Condition | Outcome if fails |
|---|---|---|
| Tenant row created | Always | 400; nothing else happens |
| Admin user created | Contact email + name provided | 207; tenant exists, user not created |
| Admin role assigned | Same | 207 |
| Linked Employee created | Same | 207 |
| Welcome email attempted | Same | Logged warning only; onboarding does NOT fail |

The welcome email failure is swallowed by `TenantOnboardingService.SendWelcomeEmailAsync` ("Don't fail the entire onboarding if email fails"). StubEmailProvider always returns success, so in the test environment this path always succeeds.

### In-flow (separate calls — POST /api/tenants/{id}/sectors)

Sector assignment is front-end-driven, separate from the tenant POST. The form loops through `sectorIds` and fires one call per sector. Failure shows a warning toast but the redirect still happens.

### Out-of-flow (separate manual actions — NOT exercised by the create test)

- Assigning additional sectors after creation
- Configuring module settings
- Assigning additional users

**Test scope:** The tenant-creation test should exercise steps 1 (tenant creation) + the sector assignments. It should assert the tenant appears in the list post-redirect. Verifying the admin user was created is a secondary assertion (API call via `request` fixture), worth including.

---

## Q4 — Does a usable tenant-delete endpoint exist?

**Key files:**
- `src/QuantumBuild.API/Controllers/TenantsController.cs`
- `src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/TenantConfiguration.cs`

**Answer: No DELETE endpoint exists.** TenantsController exposes only GET, POST, and PUT.

**Unique index situation (CRITICAL for test design):**

```csharp
builder.HasIndex(e => e.Name)
    .IsUnique()
    .HasDatabaseName("IX_Tenants_Name");
```

This index has **no partial filter for `IsDeleted = false`**. Even if a soft-delete were possible, a soft-deleted "PlaywrightTestTenant" would block creating a new "PlaywrightTestTenant" at the database constraint level.

`TenantService.CreateAsync` has an application-level pre-check that queries `!t.IsDeleted`, but the DB constraint would still fire in the same transaction if EF tried to insert with a duplicate name.

**Cleanup strategy conclusion: Outcome B — tests must use unique names per run.**

There is no practical cleanup path short of direct DB intervention. The test must generate a unique tenant name (timestamp or random suffix) so each run creates a fresh, non-conflicting row.

---

## Q5 — What is the email provider configuration in the test environment?

**Key files:**
- `src/QuantumBuild.API/appsettings.Development.json`
- `src/QuantumBuild.API/Program.cs` (lines ~121–134)
- `src/Core/QuantumBuild.Core.Infrastructure/Services/Email/StubEmailProvider.cs`
- `web/playwright.config.ts`

### Provider selection logic (Program.cs)

```csharp
var emailProvider = builder.Configuration.GetValue<string>("EmailProvider:Provider");
if (string.Equals(emailProvider, "MailerSend", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmailProvider, MailerSendEmailProvider>(...);
}
else
{
    builder.Services.AddSingleton<IEmailProvider, StubEmailProvider>();
}
```

`appsettings.Development.json` has **no `EmailProvider` section at all**. The else branch fires, registering `StubEmailProvider`.

### StubEmailProvider behaviour

```csharp
public bool IsConfigured => true;

public Task<EmailSendResult> SendAsync(EmailMessage message, ...)
{
    _logger.LogInformation("[StubEmailProvider] Email logged (not sent) — To: {To}, Subject: {Subject}");
    return Task.FromResult(EmailSendResult.Succeeded("stub-" + Guid.NewGuid().ToString("N")[..8]));
}
```

- Always reports `IsConfigured = true`
- Logs to ILogger only — no HTTP call, no delivery
- Returns a fake message ID (`stub-xxxxxxxx`)
- Never throws

**Conclusion: No `playwright.config.ts` env-block changes are needed.** The stub provider is active by default in the Development environment. Tests will not send real emails and will not hit MailerSend's trial limit.

---

## Q6 — Race conditions and timing concerns

### Submit button disabled state

The button has **dual guard**:

```tsx
disabled={isPending || form.formState.isSubmitting}
```

Both independently prevent re-submission. Playwright's standard `click()` + `waitForURL()` pattern is sufficient — no explicit `waitFor` on the disabled state required.

### Success timing sequence

```
click "Create Tenant"
  → isPending = true, button disabled
  → POST /api/tenants (awaited)
  → POST /api/tenants/{id}/sectors × N (sequential, awaited)
  → toast.success(...)          // synchronous
  → onSuccess() → router.push("/admin/tenants")   // synchronous
  → URL changes to /admin/tenants
```

The entire success flow from toast to redirect is synchronous (same JS microtask tick). `waitForURL("/admin/tenants")` is the correct Playwright assertion after clicking submit.

### Sector assignment timing

Sectors are assigned sequentially. If 3 sectors are selected, the form fires 3 sequential POSTs before the success toast fires. This adds latency but no new race condition — everything is await-chained.

### Tenant list freshness after redirect

The `/admin/tenants` page fetches the list via TanStack Query on mount. The newly created tenant should appear immediately since the mutation completed before the redirect. No polling or extra wait needed.

### No optimistic UI

The form shows no optimistic state. There are no spinner overlays that need to clear. The only animated element is the button label change ("Create Tenant" → "Creating…" → redirect).

---

## Recommended Test Shape

### File path

```
web/e2e/authenticated/tenant-creation.spec.ts
```

Follows the existing `authenticated/` convention; inherits SuperUser session state automatically from `e2e/.auth/superuser.json`.

### Test data

Generate a unique name per run using `Date.now()`:

```typescript
const tenantName = `E2E Tenant ${Date.now()}`;
const contactEmail = `e2e+${Date.now()}@example.com`;
const contactName = "E2E Admin";
```

The email domain `example.com` is an RFC 2606 reserved domain; no real mail is deliverable there. The StubEmailProvider handles it without any real network call.

### Cleanup strategy

**No delete endpoint → no afterEach cleanup.** Each run leaves one soft-stored test tenant in the DB. This is acceptable for a Dev environment with periodic DB resets. The unique name-per-run strategy prevents any run-to-run interference.

If the test DB accumulates too many test tenants over time, a one-time SQL cleanup (`DELETE FROM "Tenants" WHERE "Name" LIKE 'E2E Tenant %'`) handles it.

### beforeEach

No setup required. The unique name guarantees no pre-existing conflict. The SuperUser session is loaded automatically by the `authenticated` project.

### afterEach

None required (unique name, no cleanup path available anyway).

### Playwright action sequence

```typescript
test("SuperUser creates a tenant with contact and sector", async ({ page, request }) => {
  const ts = Date.now();
  const tenantName = `E2E Tenant ${ts}`;
  const contactEmail = `e2e+${ts}@example.com`;
  const contactName = "E2E Admin";

  await page.goto("/admin/tenants/new");

  // Fill required fields
  await page.getByLabel("Tenant Name").fill(tenantName);
  await page.getByLabel("Contact Name").fill(contactName);
  await page.getByLabel("Contact Email").fill(contactEmail);

  // Select the first available sector checkbox
  await page.locator('input[type="checkbox"]').first().check();

  // Submit
  await page.getByRole("button", { name: "Create Tenant" }).click();

  // Wait for redirect to tenant list
  await page.waitForURL("**/admin/tenants");

  // Assert success toast appeared (may have already dismissed — check optionally)
  // Playwright can race the toast; asserting URL is the reliable signal

  // Assert tenant appears in the list
  await expect(page.getByText(tenantName)).toBeVisible();

  // Verify admin user was created via API
  const meResponse = await request.get("/api/users?search=" + encodeURIComponent(contactEmail));
  expect(meResponse.ok()).toBeTruthy();
  const body = await meResponse.json();
  expect(body.data.items.length).toBeGreaterThan(0);
  expect(body.data.items[0].email).toBe(contactEmail);
});
```

### Additional test cases to cover in same file

1. **Create tenant without contact details** — tenant created, no admin user, no welcome email path exercised, redirect succeeds, tenant appears in list
2. **Validation — missing name** — submit with empty name, expect inline error message, no navigation
3. **Validation — missing sector** — submit with no sector checked, expect inline/toast error, no navigation

### Env config changes needed

**None.** StubEmailProvider is active by default in Development. `playwright.config.ts` requires no changes.

### Estimated test runtime

| Test | Estimate |
|---|---|
| Happy path (contact + 1 sector) | ~4–6 s |
| Happy path (no contact) | ~2–3 s |
| Validation failure tests | ~1 s each |

---

## Risks and Open Questions

### Risk 1 — Sector list must not be empty

The form requires at least one sector to be checked. The sector list comes from `GET /api/toolbox-talks/sectors` (active, system-wide sectors). These are seeded unconditionally in all environments (per CLAUDE.md Note 31). If the seed data is missing, the test would find no checkboxes and fail misleadingly. **Mitigation:** Assert sector checkboxes exist before attempting to check one; add a guard `expect(page.locator('input[type="checkbox"]').first()).toBeVisible()`.

### Risk 2 — Tenant name uniqueness across parallel workers

Workers are currently locked to 1 (`fullyParallel: false` in `playwright.config.ts`). No parallel risk today. If workers are ever increased, the `Date.now()` suffix strategy has a sub-millisecond collision window. Switch to `crypto.randomUUID()` or `nanoid()` if workers > 1.

### Risk 3 — 207 partial success path untested by happy-path test

If the onboarding service fails (e.g., admin user creation fails due to a duplicate email at the application-level), the form shows a warning toast and redirects. The test would pass even though the admin user wasn't created. The secondary `request` API assertion on user existence catches this.

### Risk 4 — Toast timing vs redirect

`toast.success()` and `router.push()` fire in the same JS tick. Playwright may not see the toast before the page unmounts. Do not write assertions on toast visibility for the success case; assert `waitForURL` instead. Toast assertions are reliable for error cases where the page does NOT navigate.

### Open question — SuperUser tenant scope on the user-list API

The secondary assertion (`GET /api/users?search=...`) may need an `X-Tenant-Id` header to scope to the newly created tenant, since SuperUser bypasses tenant filters. Confirm whether the users list endpoint requires the header or returns cross-tenant results for SuperUser. If the former, the test needs to capture the new tenant ID from the creation response (not directly available post-redirect). Alternative: assert the user list returns a record with the email across all tenants.

---

## Pre-existing issues the test would surface or be blocked by

None identified from the recon. The form is fully implemented with the §3.11 guard in place, the StubEmailProvider is the default, and sectors are seeded unconditionally. The test should run against the current codebase without encountering pre-existing blockers.

One **observation** (not a blocker): the unique index on `Tenants.Name` has no soft-delete filter. If a future migration is run to soft-delete test tenants for cleanup purposes, re-creates with the same name would still fail at the DB level. The unique-name-per-run strategy is robust against this.
