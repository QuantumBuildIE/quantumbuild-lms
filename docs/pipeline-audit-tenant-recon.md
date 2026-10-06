# Pipeline Audit dashboard: tenant header recon

Read-only recon. No code changed. OBSERVED = read directly in source. INFERRED = reasoned from observed code.

## 1. Callers of `getPipelineAuditDashboard`

OBSERVED: exactly one call chain.

| Layer | Location | Detail |
| --- | --- | --- |
| API fn | `web/src/lib/api/toolbox-talks/pipeline-audit.ts:133-140` | Sets `X-Tenant-Id` only if `tenantId` is truthy |
| Hook | `web/src/lib/api/toolbox-talks/use-pipeline-audit.ts:39-45` | `usePipelineAuditDashboard(tenantId?)`, queryKey `['pipeline-audit','dashboard',tenantId]` |
| Component | `web/src/app/(authenticated)/admin/toolbox-talks/pipeline/page.tsx:527` (`DashboardTab`) | Calls `usePipelineAuditDashboard()` with NO argument |

- Where tenantId comes from: nowhere. No picker, route param, prop or active-tenant value is passed, so `tenantId` is always `undefined` and the per-request header branch at `pipeline-audit.ts:137` is dead code in practice. (OBSERVED)
- No other callers of the hook or the function in `web/src`. (OBSERVED, grep)

Access to the page:

- Frontend, `admin/layout.tsx`: requires any of `Core.ManageEmployees`, `Core.ManageUsers`, `Learnings.Manage`, `Learnings.Schedule`, `Learnings.Admin`, `LessonParser.Use` (`corePermissions`, lines 20-27). Operator (`Learnings.View` only) is redirected to `/toolbox-talks`. Supervisor (has `Learnings.Schedule`) passes. (OBSERVED; role-to-permission mapping per CLAUDE.md, INFERRED)
- Frontend, `admin/toolbox-talks/layout.tsx`: requires any of `Learnings.View/Manage/Schedule/Admin`. The "Pipeline Audit" nav item has no extra `permissions` (line 31), so it shows for everyone who passes the layout. (OBSERVED)
- Frontend, page: no gate on the Dashboard tab. `isSuperUser` only adds the "Changes" tab (line 2500). (OBSERVED)
- Backend: `PipelineAuditController` is class-gated `Learnings.View` (`PipelineAuditController.cs:23`). `GET /api/toolbox-talks/pipeline/dashboard` has no stricter action gate. (OBSERVED)

## 2. Can the passed tenantId differ from activeTenantId?

- OBSERVED: the page passes no tenantId and there is no picker on the page. The only tenant switcher is the header `tenant-switcher.tsx`, which writes `activeTenantId` (state plus localStorage). The interceptor (`client.ts:88-94`) then attaches it to every request.
- Result: the per-request override cannot differ from the active tenant today. The only header sent is the interceptor's. The bulk-import bug class (picker vs header switcher) does not exist here.
- INFERRED: it would only become live if a future caller passed a `tenantId`; the interceptor would then overwrite it whenever `activeTenantId` is set.

## 3. Behaviour matrix

The "picked" column is hypothetical, since no caller supplies a picked value.

| Case | Header actually sent (OBSERVED, interceptor overwrites) | Backend tenant (OBSERVED) | UI label |
| --- | --- | --- | --- |
| Active set, picked same | active | active | No tenant label on the page; header switcher shows active tenant name |
| Active set, picked different | active (picked value overwritten) | active | Same: no label on the page |
| No active, picked X | X (interceptor only sets header when active is set, so the per-request value survives) | X | No label on the page |
| No active, nothing picked (what the code does today) | none | SuperUser: `null`, so all tenants aggregated | No label |

- INFERRED: SuperUser with no active tenant should not reach this page. `/admin/toolbox-talks` is `tenantScoped: true` in `admin/layout.tsx:12`, and the layout effect at lines 52-59 redirects SuperUser to `/admin/tenants` when `!activeTenantId`. The page can render for one frame and fire the query before the redirect, which would return the all-tenants aggregate (not displayed since navigation happens, but a request is made).
- Non-SuperUser: the header is ignored (`_currentUser.IsSuperUser` guard, controller line 68). The service falls through to `_currentUser.TenantId` (`PipelineAuditQueryService.cs:230`). (OBSERVED)

## 4. Backend

OBSERVED:

- Endpoint: `GET /api/toolbox-talks/pipeline/dashboard`, `PipelineAuditController.GetDashboard` (lines 61-82).
- Tenant resolution: reads the `X-Tenant-Id` header directly in the controller (not via `CurrentUserService`), only when `IsSuperUser`, and passes it as `tenantOverride` to `GetDashboardSummaryAsync`. `ResolveQueryTenantId` (service lines 222-231): SuperUser + header gives that tenant; SuperUser without header gives `null` (all tenants); others get their JWT tenant.
- Purely read: only `ToListAsync/CountAsync/FirstOrDefaultAsync` in `GetDashboardSummaryAsync` (lines 118-198). No writes, no side effects.
- Scoping within the response:
  - Tenant-scoped by `effectiveTenantId`: deviation counts, top open deviations, locked terms (via tenant glossaries only), module outcomes.
  - System-wide regardless of tenant: `ChangeRecords` count, active pipeline version, most recent change record (`IgnoreQueryFilters`, no tenant predicate).
- Same header pattern is duplicated in `GetModuleOutcomes` (lines 104-109). Other actions (`GetDeviations` etc.) rely on the ambient `ICurrentUserService` tenant filter.

## 5. All `X-Tenant-Id` hits in `web/src`

Production code (OBSERVED, grep on `X-Tenant-Id`, case-insensitive, plus `activeTenantId`):

| File:line | What |
| --- | --- |
| `lib/api/client.ts:88-92` | Interceptor: sets `X-Tenant-Id` from `localStorage.activeTenantId`, overwriting any per-request value |
| `lib/api/toolbox-talks/pipeline-audit.ts:137` | Per-request override in `getPipelineAuditDashboard` (never invoked with a value) |

Comments only: `lib/api/toolbox-talks/bulk-sop-import.ts:67`, `lib/api/admin/bulk-import.ts:77`, `lib/api/branding/branding.ts:13`.

Tests: `components/admin/__tests__/bulk-import-tenant-targeting.test.tsx` (82, 108), `lib/api/branding/__tests__/use-branding.test.tsx:115`.

No header-building helpers found. No other per-request `X-Tenant-Id` overrides exist. Searched for `X-Tenant-Id` and `activeTenantId`; I did not separately search for lowercase `x-tenant-id` string-concatenated or computed header keys beyond the case-insensitive grep, which would have matched them.

Other consumers of `activeTenantId`, none building headers: `tenant-switcher.tsx`, `auth-context.tsx`, `providers.tsx` (clears the query cache on change), `admin/layout.tsx`, `use-branding.ts`, `import-target-tenant.tsx`.

## Notable risks

1. Dead override with the same latent bug shape. `pipeline-audit.ts:136-137` and the `tenantId` hook parameter can never take effect when an active tenant is set. If someone adds a picker later, it will silently show the active tenant's data under the picker's label. Safest fix is to remove the parameter, or add a tenant label showing which tenant is displayed.
2. No tenant label on the page. For a SuperUser the data silently follows the header switcher; the page does not say which tenant it is showing.
3. Mixed scope on one dashboard. Deviations, locked terms and module outcomes are tenant-scoped; Change Records, active pipeline version and latest change record are global. A tenant admin sees the global change-record count next to their own tenant's numbers. Probably intentional (pipeline is system-level), but unlabelled.
4. Locked-terms count semantic. With a tenant, only that tenant's override glossaries are counted (system-default glossaries excluded); with no tenant, all terms. The number can drop sharply when a tenant is chosen vs. all.
5. Brief all-tenants request for SuperUser without an active tenant: the redirect happens in an effect after first render, so the dashboard query may fire once with no header (all-tenants aggregate) before navigation. Low impact, read-only.
6. Backend reads the header in the controller rather than using `CurrentUserService.TenantId`, duplicated in two actions. Consistent with current behaviour but easy to drift.
