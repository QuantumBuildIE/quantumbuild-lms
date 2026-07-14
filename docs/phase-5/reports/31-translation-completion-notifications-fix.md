# §31 — Translation Completion Notifications: Implementation Report

**Date:** 2026-06-17
**Status:** Done
**Author:** Claude Code

---

## Summary

Implemented email notifications for four translation pipeline events. All four are independently toggleable per-tenant via a new Settings → Notifications tab. Recipients are all active Admin users on the tenant. Notification failures are swallowed so pipeline operations are never blocked.

---

## What Was Built

### 1. EF Migration

**`AddNotificationTogglesToToolboxTalkSettings`** — adds four `bool` columns to `ToolboxTalkSettings` (all `DEFAULT TRUE`):

- `NotifyOnTranslationComplete`
- `NotifyOnValidationComplete`
- `NotifyOnFailure`
- `NotifyOnExternalReviewResponse`

Migration generated via CLI from `src/QuantumBuild.API` targeting `QuantumBuild.Core.Infrastructure` (Note 28 — both `.cs` and `.Designer.cs` present).

### 2. Backend

#### `IToolboxTalkNotificationService` (new interface)
**`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/IToolboxTalkNotificationService.cs`**

Four methods, all `async Task`, all `CancellationToken ct = default`, all swallow exceptions:

| Method | Trigger | Toggle |
|--------|---------|--------|
| `NotifyTranslationCompleteAsync` | `GenerateContentTranslationsCommandHandler` after foreach loop | `NotifyOnTranslationComplete` |
| `NotifyValidationCompleteAsync` | `TranslationValidationJob` after successful completion | `NotifyOnValidationComplete` |
| `NotifyFailureAsync` | `TranslationValidationJob` exception catch | `NotifyOnFailure` |
| `NotifyExternalReviewResponseAsync` | `TranslationWorkflowService.SubmitExternalReview` | `NotifyOnExternalReviewResponse` |

Also defines `TranslationLanguageResult(string LanguageName, string LanguageCode, bool Success, string? ErrorMessage)` record used by the translation-complete method.

#### `ToolboxTalkNotificationService` (new implementation)
**`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkNotificationService.cs`**

- Resolves tenant admins via `UserManager<User>.GetUsersInRoleAsync("Admin")` filtered by `TenantId == tenantId && IsActive && !string.IsNullOrEmpty(Email)`
- Reads toggle settings via `IToolboxTalksDbContext.ToolboxTalkSettings.Where(...).FirstOrDefaultAsync()`
- Null settings → all toggles treated as `true` (safe default)
- `NotifyTranslationCompleteAsync` skips send when `results.Count == 0`
- Builds inline HTML bodies with green CertifiedIQ header branding and a CTA button
- Sends via `IEmailProvider.SendAsync` per admin recipient
- All four public methods wrapped in `try/catch` — exceptions logged via `LogWarning`, never rethrown
- Registered as `AddScoped<IToolboxTalkNotificationService, ToolboxTalkNotificationService>()` in `ServiceCollectionExtensions`

#### `UpdateToolboxTalkNotificationSettingsCommand` + handler + validator (new)
**`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkNotificationSettings/`**

- CQRS command following existing pattern (`UpdateToolboxTalkTenantDefaultsCommandHandler`)
- Create-on-first-save: upserts `ToolboxTalkSettings` row for tenant if not already present
- Returns full `ToolboxTalkSettingsDto`
- Minimal validator: `TenantId.NotEmpty()` only

#### `UpdateToolboxTalkNotificationSettingsDto` + controller action (additions)
**`src/QuantumBuild.API/Controllers/ToolboxTalksController.cs`**

```
PUT /api/toolbox-talks/settings/notifications
Authorization: Learnings.Admin
```

DTO defaults all four toggles to `true`. Returns `Result<ToolboxTalkSettingsDto>` envelope.

#### `ToolboxTalkSettingsDto` (extended)
Added four `bool` properties. `GetToolboxTalkSettingsQueryHandler` projects them from the DB row; the no-row fallback defaults all four to `true`.

### 3. Notification Hooks (four sites)

| # | File | Location |
|---|------|----------|
| 1 | `GenerateContentTranslationsCommandHandler.cs` | After `RecordTranslationCompleted` foreach loop |
| 2 | `TranslationValidationJob.cs` | After successful `SendCompletionAsync` (talk-scoped runs only) |
| 3 | `TranslationWorkflowService.cs` | After `SaveChangesAsync` in `SubmitExternalReview` |
| 4 | `TranslationValidationJob.cs` | In exception catch, after `UpdateRunStatusAsync` |

All hooks: load talk title via `IgnoreQueryFilters()` then call the notification service. Wrapped in defensive try/catch at hook sites (hook 4 also directly inside catch block).

### 4. Frontend

**`web/src/types/toolbox-talks.ts`**
- Added 4 bool fields to `ToolboxTalkSettings` interface
- Added `UpdateToolboxTalkNotificationSettingsRequest` interface

**`web/src/lib/api/toolbox-talks/toolbox-talks.ts`**
- Added `updateToolboxTalkNotificationSettings` function (PUT to `/toolbox-talks/settings/notifications`, reads `response.data.data` — Result envelope)

**`web/src/lib/api/toolbox-talks/use-toolbox-talks.ts`**
- Added `useUpdateToolboxTalkNotificationSettings` mutation hook

**`web/src/features/toolbox-talks/components/settings/notifications-settings-section.tsx`** (new)
- `'use client'` component
- React Hook Form + Zod + 4 `Switch` toggles
- Reads via `useToolboxTalkSettings`, writes via `useUpdateToolboxTalkNotificationSettings`
- `useEffect` resets form from fetched settings
- Skeleton loading state (4 rows), save button disabled until `isDirty` or `isPending`

**`web/src/app/(authenticated)/admin/toolbox-talks/settings/page.tsx`**
- Replaced "Notifications coming soon" placeholder Card with `<NotificationsSettingsSection />`

### 5. Unit Tests

**`tests/QuantumBuild.Tests.Unit/ToolboxTalks/ToolboxTalkNotificationServiceTests.cs`** (new — 16 tests)

| Test | Verifies |
|------|----------|
| Toggle off → no email (×4 methods) | Toggle guard works for each notification type |
| No settings → email sent | Null settings defaults to `true` |
| Toggle on + admin → email sent | Happy path |
| Multiple admins → multiple emails | Per-admin fanout |
| No admins → no email | Empty admin list guard |
| Empty results → no email | `NotifyTranslationCompleteAsync` result-count guard |
| Email provider throws → swallowed | Resilience |
| UserManager throws → swallowed | Resilience |
| Wrong-tenant admin filtered | Tenant isolation |
| Rejected → subject says "Rejected" | Accepted/rejected text |

All 16 pass. Full suite: 230 of 230 passing.

Two test infrastructure fixes discovered and applied:
1. `TestAsyncQueryProvider.ExecuteAsync` used `GetMethod("Execute")` which throws `AmbiguousMatchException` on `IQueryProvider` (generic + non-generic overloads). Fixed to `GetMethods().Single(m => m.Name == "Execute" && m.IsGenericMethod)`.
2. `using Microsoft.EntityFrameworkCore.Query;` missing (required by `IAsyncQueryProvider`).

---

## Files Changed

### New files
| File | Purpose |
|------|---------|
| `src/Core/QuantumBuild.Core.Infrastructure/Migrations/20260617093041_AddNotificationTogglesToToolboxTalkSettings.cs` | EF migration |
| `src/Core/QuantumBuild.Core.Infrastructure/Migrations/20260617093041_AddNotificationTogglesToToolboxTalkSettings.Designer.cs` | EF migration designer |
| `src/Modules/ToolboxTalks/.../Application/Services/IToolboxTalkNotificationService.cs` | Interface |
| `src/Modules/ToolboxTalks/.../Infrastructure/Services/ToolboxTalkNotificationService.cs` | Implementation |
| `src/Modules/ToolboxTalks/.../Application/Commands/UpdateToolboxTalkNotificationSettings/UpdateToolboxTalkNotificationSettingsCommand.cs` | CQRS command |
| `src/Modules/ToolboxTalks/.../Application/Commands/UpdateToolboxTalkNotificationSettings/UpdateToolboxTalkNotificationSettingsCommandHandler.cs` | Handler |
| `src/Modules/ToolboxTalks/.../Application/Commands/UpdateToolboxTalkNotificationSettings/UpdateToolboxTalkNotificationSettingsCommandValidator.cs` | Validator |
| `web/src/features/toolbox-talks/components/settings/notifications-settings-section.tsx` | Settings UI component |
| `tests/.../ToolboxTalks/ToolboxTalkNotificationServiceTests.cs` | Unit tests |

### Modified files
| File | Change |
|------|--------|
| `src/Modules/ToolboxTalks/.../Application/DTOs/ToolboxTalkSettingsDto.cs` | +4 notification bool properties |
| `src/Modules/ToolboxTalks/.../Application/Queries/GetToolboxTalkSettings/GetToolboxTalkSettingsQueryHandler.cs` | Projects 4 new fields; defaults to `true` in fallback |
| `src/Modules/ToolboxTalks/.../Infrastructure/ServiceCollectionExtensions.cs` | Registers `IToolboxTalkNotificationService` |
| `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs` | +`PUT /settings/notifications` action + DTO |
| `src/Modules/ToolboxTalks/.../Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs` | Hook 1 |
| `src/Modules/ToolboxTalks/.../Infrastructure/Jobs/TranslationValidationJob.cs` | Hooks 2 + 4 |
| `src/Modules/ToolboxTalks/.../Infrastructure/Services/Workflows/TranslationWorkflowService.cs` | Hook 3 |
| `web/src/types/toolbox-talks.ts` | +4 bool fields + `UpdateToolboxTalkNotificationSettingsRequest` type |
| `web/src/lib/api/toolbox-talks/toolbox-talks.ts` | +`updateToolboxTalkNotificationSettings` fn |
| `web/src/lib/api/toolbox-talks/use-toolbox-talks.ts` | +`useUpdateToolboxTalkNotificationSettings` hook |
| `web/src/app/(authenticated)/admin/toolbox-talks/settings/page.tsx` | Replaces Notifications placeholder with real section |
| `BACKLOG.md` | §31 → Done; +§7.4 user-ID gap; +§7.5 email template extraction |

---

## Design Decisions

### Email-only (no in-app notification center)
`IEmailProvider` / `MailerSend` is fully operational. An in-app notification center requires a new entity, API, and TopNav component (~3-5 days). Proceeding with email first follows the phased approach recommended in the recon report. The two Settings tab placeholders ("coming soon") remain as product intent markers for a future build.

### All Admins as recipients
`GenerateContentTranslationsCommand` carries no user ID — there is no reliable way to identify who triggered a system-initiated job. All-Admins eliminates the user-ID gap entirely and is correct for most tenants (1-3 Admins). See §7.4 for the path to per-user targeting if needed.

### Four independent toggles
One master toggle conflates "I don't want to know about failures" with "I don't want to know when things succeed." Separate toggles give tenants the precision they need.

### `NotifyTranslationCompleteAsync` skips on empty results
An empty results list has nothing to report. This is also needed for correctness: the `GenerateContentTranslationsCommandHandler` calls the method after the foreach loop even when no languages were translated (edge case in the system-triggered path).

---

## Known Limitations

### MailerSend 429 silent drop (§5.6)
`MailerSendEmailProvider` returns `EmailSendResult.Failed` on HTTP 429 with no retry. At typical translation volume (2-6 notifications/day) the rate limit is unlikely to be hit. Mitigated (but not fixed) when §5.6 ships.

### Hook 2 only fires for talk-scoped validation runs
`TranslationValidationJob` hook 2 guards on `run.ToolboxTalkId.HasValue`. Course-level validation runs (where only `CourseId` is set) do not trigger `NotifyValidationCompleteAsync`. Acceptable for §31 scope; course validation email can be added when course-level validation becomes a more common workflow.

### External review hook fires for all accepted/rejected responses
`SubmitExternalReview` in `TranslationWorkflowService` is the single hook for external review response. The notification fires for both `accepted = true` and `accepted = false`. Email subject reflects the outcome ("Accepted" / "Rejected").
