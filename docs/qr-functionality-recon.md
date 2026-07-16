# QR Functionality Recon — PIN Status, 403 on Create, Address Stub

Date: 2026-07-16
Type: Read-only recon (no code changes made)
Priority: urgent — demo same day

---

## Executive summary (ranked by demo impact)

| # | Issue | Verdict | Blocks demo? |
|---|-------|---------|--------------|
| 3 | `POST /api/qr-locations` → 403 | Backend logic is correct; almost certainly a **stale JWT / wrong test account**, not a code bug. See §3. | **Yes — but likely fixable in seconds (re-login), not a deploy** |
| 1 | PIN "removed"? | **No.** Fully implemented end-to-end, unchanged. The *reveal-plaintext-PIN* UI row is permission-gated and tenant-setting-gated, which likely created the appearance of removal. | No — informational |
| 2 | PIN still requested on scan | **By design, not legacy.** The whole QR-scan flow is anonymous-by-PIN; there is no other identity mechanism in this flow. | No — working as intended |
| 4 | Address field pre-fill | Feasible to auto-fill, but the `Address` field is **not** the thing that builds the QR URL — it's a free-text physical address. The QR URL is already built automatically, server-side, from an environment-aware base URL. The request may be based on a misunderstanding of what `Address` does. | No — polish only, and possibly a non-issue |

---

## 1. PIN status on user/employee accounts

**Verdict: PIN was never removed. It is fully live, end-to-end, unchanged in recent history.**

### Where it lives
`Employee` entity (`src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs:129-166`) carries six PIN-related fields, added under the "QR Location Training PIN fields" region:

- `QrPin` (string?) — hashed PIN (via `IPasswordHasher<Employee>`), never stored plaintext in this column
- `QrPinIsSet` (bool) — false until first PIN generated
- `QrPinGeneratedAt`, `QrPinLastUsedAt` (DateTimeOffset?)
- `QrPinFailedAttempts` (int), `QrPinLockedUntil` (DateTimeOffset?) — 5-attempt / 15-minute lockout
- `QrPinPlain` (string?) — added later (migration `20260528073015_AddQrPinPlainToEmployee`) for SuperUser/Admin audit visibility only; per CLAUDE.md Note 27 this was an explicit product trade-off, not a security oversight

None of these columns have been dropped. No migration removes them. `git log` on `Employee.cs` and the PIN migrations shows only additive commits.

### Full live pipeline (still wired)
1. **Provisioning** — `GenerateEmployeePinsJob` (`src/Core/QuantumBuild.Core.Infrastructure/Jobs/GenerateEmployeePinsJob.cs`) — one-time job, fires when a tenant first sets `QrLocationTrainingEnabled = true`. For every active employee without a PIN, it calls `EmployeePinService.ResetPinAsync`, which hashes+stores the PIN and **emails it** via `IEmailService.SendPinEmailAsync`. The PIN is never returned in any API response at creation time — only emailed.
2. **Ad-hoc reset** — `POST /api/employees/{id}/reset-pin` (`EmployeesController.cs:246`) — same underlying service, callable by an admin (`Learnings.Admin` permission or SuperUser) or by the employee themself.
3. **Verification** — `POST /api/qr/{codeToken}/verify-pin` (`QrScanController.cs:104`) — live, hits the hashed `QrPin` on every active, PIN-set employee in the tenant via `IPasswordHasher.VerifyHashedPassword`, then calls `EmployeePinService.VerifyPinAsync` for lockout bookkeeping.

### Why it might *look* removed
The employee detail page (`web/src/app/(authenticated)/admin/employees/[id]/page.tsx:287-298`) only renders the PIN reveal row when **both**:
```
qrEnabled  → tenant setting QrLocationTrainingEnabled === "true"
canViewPin → usePermission("Learnings.Admin")
```
If either condition is false — QR training not enabled for that tenant, or the viewer lacks `Learnings.Admin` — the entire PIN section is absent from the page, with no placeholder or "hidden" indicator. A viewer without `Learnings.Admin` (e.g. a Supervisor looking at their own team) would see nothing PIN-related at all, which reads exactly like "PIN was removed."

Separately: the **"Reset Workstation PIN" button** at the top of the same page (`page.tsx:236-245`) is gated only on `qrEnabled` — **not** on `canViewPin`. Any authenticated viewer who can reach that page while the tenant has QR training enabled sees an active Reset button, even without `Learnings.Admin`. The backend does enforce authorization correctly on the actual endpoint (`isAdmin || isSelf` check, `EmployeesController.cs:249-257`), so this is a UI-only inconsistency (button visibly enabled, then silently 403s for non-self, non-admin viewers) — not a security hole, but worth tightening for a clean demo (hide the button, don't just let the backend reject it).

### Note on the Users tab
The employee PIN fields live on the **Employee** entity, surfaced only on `/admin/employees/{id}`. The **Users** tab (`/admin/users`) manages ASP.NET Identity `User` records — a related but distinct entity — and never has, and does not now, show any PIN field. If a recent screenshot showed the Users tab with no PIN column, that is expected/correct; it is not evidence of removal.

---

## 2. QR scan flow and PIN requirement

**Verdict: the PIN prompt is a live, intentional design — not a legacy leftover.**

### Flow
1. User scans QR → lands on `web/src/app/qr/[codeToken]/page.tsx` (public route, outside `(authenticated)` group, no JWT anywhere in this flow)
2. Page calls `GET /api/qr-locations/codes/{codeToken}` (`[AllowAnonymous]`) to fetch location/talk metadata for display
3. Page shows a 6-digit `PinInput` component (`page.tsx:138-207`)
4. On submit → `POST /api/qr/{codeToken}/verify-pin` (`[AllowAnonymous]`, `QrScanController.cs:104`) — matches the entered PIN against all `QrPinIsSet` employees for that QR code's tenant, returns a `sessionToken` on success
5. `GET /api/qr/session/{sessionToken}` loads the talk/course content (sections, quiz, video) scoped to that employee's `PreferredLanguage`
6. Employee works through content → `POST /api/qr/session/{sessionToken}/complete` writes a `ScheduledTalk` + `ScheduledTalkCompletion` record tied to that employee

### Why PIN is required at all
This flow has **no login, no JWT, no session cookie** — it is designed for a shared kiosk/tablet at a physical location where individual employees are not separately authenticated by any other means. The PIN is the *only* mechanism that identifies which employee is completing the training at that station. There is no alternate/legacy auth path here to compare against — this is the sole design, current since Phase 3 of the original build (`9b1cf57 feat: QR Location Training Phase 3 — scan auth + PIN verification`).

Cross-referencing with §1: since PIN provisioning, storage, and verification are all fully intact, there is no scenario in the current code where the scan page would prompt for a PIN that no employee actually has a chance to receive — **provided** `QrLocationTrainingEnabled` was turned on for the tenant (which triggers `GenerateEmployeePinsJob`) and employees have valid email addresses on file.

---

## 3. `POST /api/qr-locations` → 403 Forbidden

### The gate, precisely
`QrLocationController` (`src/QuantumBuild.API/Controllers/QrLocationController.cs`):
- **Class-level:** `[Authorize(Policy = "Learnings.View")]` (line 103)
- **`CreateLocation` (POST) action-level:** `[Authorize(Policy = "Learnings.Admin")]` (line 175)
- Same stacked pattern on `UpdateLocation`, `DeleteLocation`, `CreateCode`, `UpdateCode`, `DeleteCode`.

ASP.NET Core requires **both** the class-level and action-level `[Authorize]` to pass (per CLAUDE.md Note 24). Here that means: caller must hold **both** `Learnings.View` and `Learnings.Admin`.

This is *not* the same shape of bug as the Note 24 regulatory-browse incident (commit `c26b2a5`), where the class-level policy was **more** restrictive than the action needed (`Tenant.Manage` blocking a `Learnings.Admin`-only action). Here the class gate (`Learnings.View`) is **less** restrictive than the action gate (`Learnings.Admin`) — every role that holds `Learnings.Admin` (Admin, SuperUser) also implicitly satisfies `Learnings.View` in this codebase's seed data. So the stacking itself isn't structurally broken the way it was for regulatory-browse.

### Permission plumbing verified end-to-end (all correct)
- `Permissions.cs` defines `Learnings.Admin` as a real, registered permission constant, included in `Permissions.GetAll()`.
- `Program.cs:87` — `AddPermissionPolicies(Permissions.GetAll())` registers a policy per permission, including `Learnings.Admin`. Confirmed present, not a typo/missing-policy situation.
- `DataSeeder.GetPermissionsForRole()` (`DataSeeder.cs:379-397`) — `"Admin" => allPermissions.Where(p => p.Name != Permissions.Tenant.Manage)`, i.e. Admin gets every permission including `Learnings.Admin`. `"SuperUser" => allPermissions` — all of them.
- `SeedPermissionsAsync` / `SeedRolePermissionsAsync` run **unconditionally in every environment including Production** (`DataSeeder.cs:41-44`, before the Dev/Demo-only credential-seeding gate) — so this is not a case of Production/Demo missing a permission row that only Development got.
- `PermissionAuthorizationHandler` (`PermissionAuthorizationHandler.cs`) — SuperUser (`is_super_user` claim = "true") **always** succeeds every policy, regardless of the `permission` claims list. If the account testing today is genuinely logged in as SuperUser, this 403 should be structurally impossible.
- JWT claims are built fresh from the DB at token-issue time (`AuthService.GenerateJwtTokenAsync`, calls `GetUserPermissionsAsync(user.Id)` live, no caching).

**Conclusion: the authorization code itself is correctly wired.** The bug, if there is one, is not in this controller's attributes or the permission-seeding pipeline.

### Ranked likely causes

**1. (Most likely, and the most demo-relevant) Stale access token that predates a role/permission change, with no self-healing path.**
`web/src/lib/api/client.ts:108-109` — the axios response interceptor only auto-refreshes the token on **401**. A permission failure returns **403**, which the interceptor does not treat as retryable. `RefreshTokenAsync` *does* rebuild permission claims live from the DB when it runs — but it only runs on 401 or on manual re-login, never automatically on a 403.
Practically: if the account being used for the demo was assigned the Admin role (or had `Learnings.Admin` become relevant) *after* their last login, or if a "Remember me" persisted token from an earlier session is still active, they will keep getting 403 on every attempt until either (a) the 60-minute access token naturally expires and a refresh is triggered by some other 401 first, or (b) they explicitly log out and log back in.
**→ First thing to try before the demo: log out and log back in on the account that will run the demo, then retry the QR location create.**

**2. The test account genuinely lacks `Learnings.Admin`.**
If the account is a Supervisor or Operator (seeded with `Learnings.View`/`Learnings.Schedule` only, or `Learnings.View` only — `DataSeeder.cs:388-393`), the class-level gate (`Learnings.View`) passes, letting them reach the QR Locations page and its GET endpoints, but the action-level `Learnings.Admin` gate correctly rejects the POST. This would be **correct, intended behavior**, not a bug — but worth ruling out by checking which role the demo account actually holds in the `AspNetUserRoles` table.
Also worth checking: `GetPermissionsForRole` only has switch cases for `"SuperUser"`, `"Admin"`, `"Supervisor"`, `"Operator"` — any other `Role.Name` (e.g. a stray leftover from the legacy roles CLAUDE.md says were "deleted" in migration `20260218125524_UpdateRolesPermissionsAndAddSuperUser` — Finance/OfficeStaff/SiteManager/WarehouseStaff/Operative) falls through to `Enumerable.Empty<Permission>()`. If any tenant's admin-equivalent user still references one of those role rows (e.g. the migration deleted the Role row but a demo/production tenant has a UserRole pointing at a role that was recreated with a legacy name, or a custom role was created through a path that bypassed the intended four), that account would have **zero** permissions, not just a missing `Learnings.Admin`.

**3. Not CORS, not anti-forgery.** This API uses bearer-JWT auth throughout, no cookies, no `[ValidateAntiForgeryToken]` on this controller — CSRF/anti-forgery is not in play. A CORS preflight failure would surface as a browser network error, not a same-origin 403 with an RFC9110 ProblemDetails body from the API itself. The generic RFC9110 body reported is exactly ASP.NET Core's default `ForbidResult` output for a failed `[Authorize(Policy=...)]` check — consistent with causes 1 or 2, not with routing/proxy/CORS issues.

**4. Frontend gap worth noting but unlikely to be the actual cause today:** the QR Locations page itself (`web/src/app/(authenticated)/admin/toolbox-talks/qr-locations/page.tsx`) has no client-side permission check on the "New Location" / "New QR Code" buttons — they render unconditionally whenever the page is reached. The only gate keeping a `Learnings.View`-only user off this page in normal navigation is that the **nav tab itself** is hidden unless `Learnings.Admin` is present (`web/src/components/layout/top-nav.tsx:30`). A user who knows the direct URL and holds only `Learnings.View` could reach the page, see enabled Create buttons, and 403 on submit. Low likelihood of being today's actual cause (would require the demo user to have typed the URL directly), but a legitimate latent gap to fix alongside the Reset PIN button gap noted in §1.

### Recommended fix scope
- **Immediate (today):** confirm the demo account's actual role in the DB, and have them log out/log back in regardless, to force a fresh token with current permission claims. This is a session/data issue, not a code deploy.
- **If cause 2 is confirmed** (account genuinely on wrong/legacy role): reassign to `Admin` via the Users admin UI (`PUT /api/users/{id}` with the correct `RoleIds`), not a code change.
- **Only if neither 1 nor 2 explains it** should this become a code investigation — at that point, add temporary logging of `context.User.Claims` in `PermissionAuthorizationHandler` for the `Learnings.Admin` policy to see exactly what claims the rejected request actually carried, which would immediately disambiguate 1 vs 2 vs something unforeseen.
- **Separately, lower priority, not blocking:** gate the "New Location"/"New QR Code" buttons and the "Reset Workstation PIN" button behind `usePermission("Learnings.Admin")` client-side, matching what the backend already enforces, so the UI doesn't show actionable buttons that will silently 403.

---

## 4. Address field pre-population feasibility

### Current implementation
- `CreateQrLocationRequest.Address` / `UpdateQrLocationRequest.Address` — plain nullable `string`, **no validation** (`QrLocationController.cs:17-26`)
- Frontend: a single free-text `Input` in `LocationDialog` (`qr-locations/page.tsx:99-101`), no formatting, no URL parsing, no default value
- Rendered back as a subtitle under the location name in the location list (`page.tsx:733`)

### Important: `Address` is not what builds the QR code's URL
The actual URL encoded into the generated QR PNG is built entirely server-side, independent of the `Address` field, in `QrLocationController.CreateCode` (lines 318-320):
```csharp
var appBaseUrl = _configuration["AppSettings:BaseUrl"]?.TrimEnd('/')
    ?? "https://rascorweb-production.up.railway.app";
var qrUrl = $"{appBaseUrl}/qr/{codeToken}";
```
This already reads an environment-specific config value (`AppSettings:BaseUrl`, referenced elsewhere in CLAUDE.md for the Production custom-domain note) and generates a `codeToken`-keyed URL automatically — no user ever types a URL anywhere in the current QR-code-creation flow. Talk/course selection is done via a dropdown (`QrCodeDialog`, `page.tsx:190-226`), not by constructing or editing a URL.

### What this means for the request as described
The ask was: pre-populate "address" with `https://.../toolbox-talks/` so the user only adds a talk ID. But:
- There is no field in the current UI where a user manually builds a URL to link a QR code to a talk — that's already solved via the Talk/Course selector.
- `Address` is a separate, purely descriptive field for the **physical location** (e.g. "Warehouse 3, Loading Bay B") — unrelated to routing.
- Auto-filling `Address` with a base URL would overwrite/repurpose a field that's meant to hold a street address, and would have **zero effect** on the QR code's actual destination URL, since that's generated independently server-side.

**Feasibility of the literal ask, if still wanted:** trivial. `window.location.origin` (client-side, in the Next.js app) or an existing public env var could pre-fill any text input in under an hour of work; no backend change needed since `Address` has no validation to conflict with. But this would be cosmetic — it would not change what the QR code actually does, and could confuse admins into thinking `Address` controls the link.

**Recommendation:** clarify with the requester what problem they're trying to solve before touching this field — most likely candidates are (a) genuinely wanting a friendlier/clearer physical-address label (trivial rename/placeholder text change), or (b) a misunderstanding that's already resolved by the existing Talk/Course dropdown in `QrCodeDialog`, in which case no change is needed at all.

---

## 5. Recent history relevant to items 1–3

Commits touching QR/PIN/permissions (`git log --oneline -i --grep`, newest first among relevant hits):

| Commit | Date-adjacent context | Relevance |
|---|---|---|
| `24c19b2` feat(reviewers): tenant reviewer configuration foundation | latest on branch | unrelated |
| `e6d3919` fix(migrations): rewrite AddQrCodeCourseId to add only CourseId column | — | schema hygiene, not auth |
| `fe57fe0` fix: restore missing Designer.cs files for three earlier migrations (EmployeePin, QrLocation/Code, QrSession) | — | confirms PIN/QR migrations are intact and were being actively maintained, not deprecated (Note 28 pattern) |
| `d83a33a` / `effb8e9` / `bb494d8` / `62c3fba` | — | QR video/subtitle playback fixes, unrelated to PIN or 403 |
| `feb0bdd` feat: store QR PIN plaintext, visible to SuperUsers and tenant Admins | — | **introduced `QrPinPlain` + the `Learnings.Admin`-gated reveal row** — the likely source of "PIN looks removed" for non-Learnings.Admin viewers (§1) |
| `98f14c1` fix: QR locations edit dialog not pre-populating form fields | — | same `LocationDialog` component discussed in §4; prior bug there was about edit-mode pre-fill, not create-mode address content |
| `918ff9f` fix: remove non-functional language selector from QR scan page | — | unrelated |
| `0daffe0` feat: QR codes support Course assignment alongside Talk | — | added the Talk/Course dropdown referenced in §4 |
| `a0be76f` fix: QR Locations page — empty SelectItem values and broken filter | — | unrelated to today's 403 |
| `c26b2a5` fix: regulatory browse 403 for tenant admins | 2026-05-20 | **structurally similar 403 pattern** (class-vs-action `[Authorize]` stacking) but on a *different* controller (`RegulatoryIngestionController`); confirms this exact class of bug has happened before in this codebase and the team's fix pattern (split into a dedicated lower-privilege controller) — useful precedent, but the QrLocationController stacking is not the same shape of bug (see §3 discussion) |
| `9b1cf57` / `c836b82` / `3b97f3e` | original build | Phase 1–3 of QR Location Training — establishes PIN + scan-auth design as original intent, not a later addition |

No commit in this history modifies `QrLocationController`'s `[Authorize]` attributes after their original introduction — the `Learnings.View`/`Learnings.Admin` split on that controller is original design, not a recent regression.

**Note on documentation drift (context, not a bug):** CLAUDE.md's permission table still lists `ToolboxTalks.View/Create/Edit/Delete/Schedule/Admin` and only `Learnings.View`/`Learnings.Schedule` — but the actual `Permissions.cs` in this codebase has fully moved to a `Learnings.*` namespace (`View`, `Manage`, `Schedule`, `Admin`), with no `ToolboxTalks.*` permissions remaining anywhere in code. This is pre-existing CLAUDE.md staleness unrelated to today's issues, surfaced here only because it was necessary to verify permission names while investigating §3.

---

## Overall diagnosis, ranked by confidence

1. **§3 (403) — high confidence it's an account/session state issue, not a code defect.** Every layer of the permission pipeline (constants → policy registration → seeding → JWT claim generation → authorization handler) was traced and is internally consistent. Fastest unblock: re-login on the demo account; if that doesn't resolve it, check the account's actual DB role next, before touching code.
2. **§1 (PIN) — high confidence nothing was removed.** All six PIN fields, the hashing/lockout service, the provisioning job, and the verify endpoint are all present, wired, and unmodified in recent history. The only real gap is a UI-visibility one (permission+setting-gated reveal row, ungated reset button) — cosmetic/consistency issues, not functional regressions.
3. **§2 (PIN prompt on scan) — high confidence it's intentional, current design**, not a legacy leftover, since it's the sole identity mechanism in an anonymous kiosk flow.
4. **§4 (address stub) — likely a misunderstanding of what the field does**, or at minimum a low-value cosmetic change; recommend clarifying intent before implementing.

## Recommended fix scope per issue (for a future prompt, not executed here)

- **§3:** No code change until account/session cause is confirmed. If confirmed as cause 2 (legacy/wrong role), the fix is a role reassignment via existing UI, not new code. If somehow neither 1 nor 2 explains it, smallest diagnostic step is temporary claims logging in `PermissionAuthorizationHandler`, not a structural change to `QrLocationController`.
- **§1:** Optional polish — gate "Reset Workstation PIN" button behind `canViewPin` (or a separate, more permissive check matching the backend's `isAdmin || isSelf`) so the button doesn't appear for users who will just get a 403.
- **§2:** No action needed.
- **§4:** Needs a clarifying conversation with the requester before scoping anything. If still wanted after that, it's a small frontend-only change (default value from `window.location.origin`, or leave the field alone and just rename its label) — but confirm the actual friction point first, since the current dropdown-based Talk/Course assignment already avoids manual URL entry.
