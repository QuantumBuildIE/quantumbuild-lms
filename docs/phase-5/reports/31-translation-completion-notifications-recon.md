# §31 — Translation Completion Notification Gap: Recon Report

**Date:** 2026-06-17
**Status:** Read-only investigation. No code changed.
**Author:** Claude Code

---

## Summary

The codebase has **zero notification infrastructure** for admin-facing async events. There is no notification center, no in-app inbox, no bell icon, and no user-preference store. The only async notification surface is Sonner toast — used exclusively for synchronous action feedback, not deferred events.

Email infrastructure is fully operational (`IEmailService` → `MailerSendEmailProvider`) and is the correct primary channel for translation/validation completion notifications given job durations of 5–30 minutes.

**The two event hooks that need notification dispatch are precise and narrow:**
- `GenerateContentTranslationsCommandHandler.cs:165–181` — after the per-language foreach loop (one call per successful language)
- `TranslationValidationJob.cs:382–388` — after `RecordValidationCompleted` on job completion

**Critical blocker for "notify the triggering user":** `GenerateContentTranslationsCommand` carries `TriggeredByType` (System/User enum) but not a user ID. The handler has no way to know who initiated the run. Fix options are surfaced in Step 3.

**Recommended direction:** Email-only, per-talk (all languages complete in one email), all Admins for the tenant, no preferences (always on). No in-app surface for now — the existing Notification Settings placeholders confirm product intent for a future build, but they are not a dependency for fixing the gap.

**Biggest open decision:** Recipient strategy — all Admins vs. talk author vs. triggering user.

---

## Step 1 — Notification Infrastructure Inventory

### 1.1 Email Infrastructure

| Component | File | Notes |
|-----------|------|-------|
| `IEmailProvider` | `src/Core/QuantumBuild.Core.Application/Abstractions/Email/IEmailProvider.cs:1` | Provider-agnostic interface: `SendAsync(EmailMessage) → EmailSendResult` |
| `MailerSendEmailProvider` | `src/Core/QuantumBuild.Core.Infrastructure/Services/Email/MailerSendEmailProvider.cs:1` | Posts to `https://api.mailersend.com/v1/email`. Logs and returns `EmailSendResult.Failed` on 429 — **no retry, no backoff (§5.6 open)**. |
| `StubEmailProvider` | `src/Core/QuantumBuild.Core.Infrastructure/Services/Email/StubEmailProvider.cs` | Testing only |
| `IEmailService` | `src/Core/QuantumBuild.Core.Application/Interfaces/IEmailService.cs:1` | Higher-level facade with typed send methods |
| `EmailService` | `src/Core/QuantumBuild.Core.Infrastructure/Services/EmailService.cs:1` | Implements `IEmailService`. All HTML is **hardcoded inline strings** — no template engine, no DB-stored templates. |
| `IToolboxTalkEmailService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/IToolboxTalkEmailService.cs:1` | Employee-facing emails (assignment, reminder, completion, escalation, refresher). **Not admin-facing.** |

**Existing `IEmailService` methods:**
- `SendPasswordSetupEmailAsync` — new user invitation
- `SendEmailAsync` — generic (subject + HTML body)
- `SendPinEmailAsync` — QR PIN delivery
- `SendUserCreatedEmailAsync` — account creation notification
- `SendExternalReviewInvitationEmailAsync` — third-party reviewer invitation

**Template pattern:** All email bodies are built as `$"..."` interpolated string literals in `EmailService.cs`. No template engine. Adding a new notification means adding a new method to `IEmailService` and `EmailService` following this same pattern.

### 1.2 In-App Notification Surfaces

**Finding: none exist.**

| What | File | Status |
|------|------|--------|
| Notification center / inbox | — | Does not exist |
| Bell icon or badge in TopNav | `web/src/components/layout/top-nav.tsx:128–135` | Badge exists but shows **employee's pending training count** (red if overdue), not admin notifications |
| Sonner toast | `web/src/lib/providers.tsx:37`, `web/src/components/ui/sonner.tsx` | Present and used in 15+ files. Exclusively synchronous action feedback — no delayed/async toasts |
| Notification Settings tab (admin) | `web/src/app/(authenticated)/admin/toolbox-talks/settings/page.tsx:67–80` | **Placeholder only** — renders "Notification settings coming soon". Tab is wired but empty. |
| Notification Settings tab (user) | `web/src/app/(authenticated)/toolbox-talks/settings/page.tsx:52–63` | Same — empty placeholder |
| Notification components/hooks | — | Zero files matching `*notification*`, `*inbox*`, `*bell*` patterns |

The two "coming soon" placeholders in settings pages are a product commitment signal — the surface was scaffolded before the implementation. They do not unblock the §31 fix; they indicate where preferences UI would eventually live.

### 1.3 SignalR Hubs

| Hub | Route | Group scoping | Sends |
|-----|-------|---------------|-------|
| `TranslationValidationHub` | `/api/hubs/translation-validation` | `validation-{runId}` | `ValidationProgress`, `SectionCompleted`, `ValidationComplete` |
| `SubtitleProcessingHub` | `/api/hubs/subtitle-processing` | per subtitle job | progress events |
| `ContentGenerationHub` | `/api/hubs/content-generation` | per session | generation progress |
| `CorpusRunHub` | `/api/hubs/corpus-run` | per corpus run | run progress |
| `LessonParserHub` | `/api/hubs/lesson-parser` | per session | parser progress |

**Critical finding:** All hubs are **run-scoped** (groups keyed by a specific job/run ID). A client must subscribe to a known run ID to receive events. There is **no user-scoped hub** (a "deliver to user X" channel). Building push-to-user notification via SignalR would require either: (a) a new hub with `user-{userId}` groups, or (b) using SignalR's built-in user ID addressing if the hub can identify users from JWT claims. Neither exists today.

### 1.4 User Preferences Infrastructure

| What | File | Notes |
|------|------|-------|
| `TenantSetting` entity | `src/Core/QuantumBuild.Core.Domain/Entities/TenantSetting.cs` | Key-value store: `TenantId`, `Module`, `Key`, `Value` (all strings) |
| `TenantSettingKeys` | `src/Core/QuantumBuild.Core.Application/Features/TenantSettings/TenantSettingKeys.cs` | 7 keys defined — `EmailTeamName`, `TalkCertificatePrefix`, `CourseCertificatePrefix`, `SkipValidationStep`, `QrLocationTrainingEnabled`, `ExternalParticipantTokenLifetimeDays`, `UseNewWizard`. **No notification keys.** |
| `ICurrentUserService` | `src/Core/QuantumBuild.Core.Application/Interfaces/ICurrentUserService.cs` | Exposes: `UserId` (string), `UserIdGuid`, `UserName`, `TenantId`, `IsSuperUser`, `EmployeeId`. **No email property.** |

The user's email is available in ASP.NET Identity (`ApplicationUser.Email`) but not surfaced through `ICurrentUserService`. Accessing it requires a direct `UserManager<ApplicationUser>` query.

---

## Step 2 — Event Lifecycle Trace

### 2.1 Translation Completion

**Call site:** `GenerateContentTranslationsCommandHandler.cs:165–181`

```csharp
// After SaveChangesAsync succeeds (line 146):
foreach (var successfulResult in results.Where(r => r.Success))
{
    var recordResult = await _workflowService.RecordTranslationCompleted(
        request.ToolboxTalkId,
        successfulResult.LanguageCode,
        request.TriggeredBy,    // ← TriggeredByType enum only, no user ID
        ct: cancellationToken);
    // ...
}
```

- Called **per-language**: a 5-language talk generates 5 sequential calls within one handler invocation.
- `RecordTranslationCompleted` transitions workflow state `Translating → AIGenerated` and logs a `WorkflowEvent`. No side effects beyond workflow state.
- The handler is a synchronous MediatR pipeline step — it runs in the HTTP request context when triggered directly, or in a Hangfire thread when triggered by `ContentGenerationJob` or `MissingTranslationsJob`.

**Trigger sites for the command:**

| Site | File | User context |
|------|------|-------------|
| Direct API (`POST /translations/generate`) | `ToolboxTalksController.cs:~1480` | `ICurrentUserService` available at request time but **not captured in the command** |
| `ContentGenerationJob` | `ContentGenerationJob.cs:~599` | Background job — no user ID available; `TriggeredBy` not explicitly set (defaults to `User`) |
| `MissingTranslationsJob` | `MissingTranslationsJob.cs:~274` | Background job — `TriggeredBy = TriggeredByType.System` explicitly set |
| `EmployeeLanguageChangeHandler` | `EmployeeLanguageChangeHandler.cs:~38` | Event handler — no user ID |

### 2.2 Validation Completion

**Call site:** `TranslationValidationJob.cs:382–388`

```csharp
var wfResult = await _workflowService.RecordValidationCompleted(
    run.ToolboxTalkId.Value,
    run.LanguageCode,
    TriggeredByType.System,       // ← always System; no user ID
    explicitTenantId: tenantId,
    ct: cancellationToken);
```

- Called **per-language** per Hangfire job. Each language's validation run is its own job.
- Always `TriggeredByType.System` — the originating user ID (who clicked "Validate") is not propagated to the job.
- The job receives only `validationRunId` and `tenantId` as parameters.

**Trigger sites:**

| Site | File | User context |
|------|------|-------------|
| Direct API (`POST /validation/validate`) | `TranslationValidationController.cs:~98` | User available at request time; job gets `(run.Id, tenantId)` — no user ID |
| `TranslationJobScheduler` | `TranslationJobScheduler.cs:~17` | Scheduler has no user context |

### 2.3 Failure Paths

**Translation infrastructure failures:** `TranslateForLanguageAsync` (handler:~199) catches exceptions and returns a failed `LanguageTranslationResult`. Failed results are excluded from the `RecordTranslationCompleted` foreach loop (line 165 filter: `.Where(r => r.Success)`). No `RecordTranslationFailed` exists on `ITranslationWorkflowService`. Failures are only logged via `_logger.LogError`.

**Validation job failures:** `TranslationValidationJob.ExecuteAsync` has try/catch that sets `run.Status = ValidationRunStatus.Failed`. No `RecordValidationFailed` on the workflow service. Failures are logged.

**Infrastructure failure vs. outcome failure:** There is no distinction in the current code between:
- **Infrastructure failure** (API call failed, network error, Hangfire job crash) — translation/validation never completes
- **Outcome failure** (translation completed but validation score was Fail) — validation completed, outcome is bad

A complete notification design should distinguish these: infrastructure failures warrant admin notification; outcome failures may be surfaced differently (reviewer workflow, not a raw notification).

---

## Step 3 — Recipient Candidates

### 3.1 The User-ID Gap

`GenerateContentTranslationsCommand` carries `TriggeredByType` (enum: `User` or `System`) but **no user ID, no email, no name**. When the handler calls `RecordTranslationCompleted`, there is no way to know which specific user initiated the run from the command alone.

`TranslationValidationJob` always uses `TriggeredByType.System` — explicitly discarding user context at the job layer.

### 3.2 Candidate Recipient Strategies

| Strategy | Description | Pro | Con |
|----------|-------------|-----|-----|
| **A — All Admins for tenant** | Query all users with Admin role for the tenant; send to all of them | No user-ID propagation needed; no schema changes; always works for system-triggered jobs | Could send to 3-5 people when only 1 cares; no per-user opt-out |
| **B — Triggering user only** | Add `TriggeredByUserId? (Guid?)` to command; controller sets it; background jobs leave null; fallback to talk's `CreatedBy` | Most targeted | Requires schema change to command; null for system jobs; must query User table for email; `CreatedBy` conflates author with notification target |
| **C — Talk's CreatedBy user** | Always notify the talk's author (`ToolboxTalk.CreatedBy` is a string user ID) | Always available; no new fields | Wrong for translations triggered by a different admin than the talk author; conflates creation with ownership |
| **D — Tenant notification email setting** | New `TenantSetting` key `TranslationNotificationEmail`; single designated address per tenant | Zero user-ID complexity; admin controls who gets notified | Requires tenant configuration step; awkward if they want individual-level control |

**Recommended:** Strategy A (all Admins for tenant) for initial implementation. Avoids the user-ID gap entirely, works for both user-triggered and system-triggered paths, and in practice most tenants have 1-3 Admins. If per-user opt-out becomes a need, add preference keys to `TenantSetting` in a follow-up.

**Implementation mechanics for Strategy A:** Query `UserManager.GetUsersInRoleAsync("Admin")` scoped to the tenant (via `ApplicationUser.TenantId`), then send email to each result. The `User.Email` field is available from Identity without any new interface changes.

---

## Step 4 — Frequency and Batching Considerations

### 4.1 How often do events fire

**Translation completion (`RecordTranslationCompleted`):**
- Fires once per successful language per handler invocation
- A talk with 5 target languages generates 5 calls, sequentially, within ~5–30 minutes of total execution time
- The foreach loop at line 165 runs all 5 calls synchronously before the handler returns

**Validation completion (`RecordValidationCompleted`):**
- Fires once per Hangfire job (one job = one language)
- Multiple language jobs may run concurrently if the Hangfire queue is free
- A 5-language validation batch could generate 5 `RecordValidationCompleted` events within a window of minutes to ~1 hour

### 4.2 Batching vs. per-language

Per-language notifications for a 5-language run = 5 emails within a short window = spam.

The natural batching point for translation is **the handler's return** (after the foreach loop). All languages for one talk complete in a single handler execution. One email per talk completion is the right granularity.

Validation is harder: each language is its own Hangfire job, so "all done" for a talk requires coordination across jobs. Options:
- **Per-language validation email**: simple but chatty (5 emails for 5 languages)
- **Polling completion check**: after each job completes, check if all language runs for the talk are now `Completed` — if yes, send one summary email. This requires reading `TranslationValidationRun` rows for the talk.
- **Separate "all runs complete" job**: triggered when the last validation job finishes, sends one summary. More complex.

For an initial implementation, **per-language validation email** is acceptable if the job description makes it clear ("Russian validation complete — 89% score"). The natural mitigation for chattiness is that validation runs don't fire in bursts the way translation jobs do in the handler loop.

### 4.3 Production volume estimate

- Typical tenant: 1-3 talks in translation simultaneously (estimate based on current usage patterns)
- Each translation run: 2-5 languages, 5-30 minutes
- Each validation run: 1 language, 10-30 minutes
- Daily notifications to an Admin: probably 2-6 emails on an active day
- No flood risk identified at this scale

---

## Step 5 — Product Decision Candidates

### Decision A — Channels

**Option A1: Email only**
- Pros: infrastructure exists; works for closed browsers; no frontend work
- Cons: depends on MailerSend; §5.6 (429 drop risk) applies
- Infrastructure needed: 2-3 new methods on `IEmailService` and `EmailService`

**Option A2: In-app only**
- Pros: no external service dependency; real-time feel
- Cons: zero infrastructure exists; requires new backend entity (`Notification`), new API endpoint, new TopNav component, new frontend polling or push mechanism; only surfaces to logged-in users
- Infrastructure needed: backend entity + API + frontend notification center (~3-5 days new work)

**Option A3: Both**
- Best coverage; most work; the right long-term state
- Can be phased: email first (fast), in-app second (proper notification center build)

**Recommendation:** Email first (Option A1), in-app later as a separate chunk informed by §1.3.5.

### Decision B — In-app Surface (if chosen)

| Option | Effort | Discoverability |
|--------|--------|----------------|
| Toast on next page load | Medium — needs persistent store for "unseen" notifications | Poor — ephemeral, misses if not on correct page |
| Persistent notification center (bell icon + dropdown) | High — new entity, API, TopNav change, mark-read | Good — always visible |
| Badge on talk row in list | Low — new field on list DTO | Low — only visible on the Learnings list |
| Badge + toast on navigation | Medium-High | Good |

Given the existing "Notification settings coming soon" placeholder, the product expectation is a proper notification center eventually. For §31 scope, this is out of scope.

### Decision C — Email Trigger Granularity

**Option C1: Per-language as it completes**
- Translation: 5 emails for a 5-language run (chatty)
- Validation: 1 email per validation job (acceptable)
- Simple implementation

**Option C2: Per-talk (all languages complete in one email)**
- Translation: 1 email after the handler foreach loop — natural trigger point
- Validation: requires a check "are all other language runs also complete?" — adds complexity
- Best user experience

**Option C3: Per-job summary (languages + failures table)**
- Same as C2 but includes failure summary in the email body
- Best information density; same complexity as C2

**Recommendation:** C3 for translation (handler already has the full results list). C1 for validation (per-language is simpler and the timing is spread out enough to be acceptable).

### Decision D — Failure Notifications

**Option D1: Same channel as completion** — failures appear in the same email alongside successes in the summary; a "0 of 3 languages succeeded" email is itself the failure notification.

**Option D2: Separate email for infrastructure failure** — if all languages fail (exception, API down), send a distinct "Translation failed — action required" email. On partial failure (2 of 5 succeeded), include failure detail in the summary.

**Option D3: Failures only** — silent on success, email only on failure. Misses the "fire-and-notify" UX goal.

**Recommendation:** D1 with failure detail in the summary email body. A "3 of 5 languages translated successfully — 2 failed (Romanian, Polish)" email covers both completion and failure in one notification.

For validation outcome failures (low scores): these are already surfaced via the existing ReviewerDecision workflow — the reviewer sees them in the product. An additional "validation completed with some Fail sections" notification may be desirable but is a separate decision (out of scope for §31).

### Decision E — Preferences

**Option E1: Tenant-level opt-out (always-on by default)**
- New `TenantSetting` key: `TranslationCompletionNotificationsEnabled = "true"` (default)
- Admin can disable in Settings → Notifications tab (which already has a placeholder)
- Infrastructure needed: 1 new `TenantSettingKeys` constant + read it in the notification dispatch

**Option E2: User-level preferences**
- Would require a new `UserSetting` entity or new User columns
- Higher cost; more flexible; no infrastructure exists today

**Option E3: No preferences (always on)**
- Simplest; no new schema
- Acceptable for initial implementation

**Recommendation:** E3 (always on) for initial build. Add E1 (tenant opt-out) when the Notifications settings tab is properly built.

---

## Step 6 — Adjacent Observations and Dependencies

### 6.1 Relationship to §1.3.5 (broader fire-and-notify)

§1.3.5 is the umbrella — it covers bulk import, content generation, validation, corpus runs, and translation completion. §31 is the translation-specific instance.

If §1.3.5 were to build a generic notification framework (backend entity + API + frontend center), §31 would compose with it rather than duplicate it. However, §1.3.5 is currently P2, Open, and has no scoped implementation plan. Proceeding with §31 as email-only does not create significant duplication: adding 2-3 methods to `IEmailService` is not a "framework" — a future notification center would add a dispatch channel alongside email, not replace it.

**Verdict:** §31 can proceed independently without waiting for §1.3.5. If and when a notification center lands, it adds a second channel to the same dispatch hooks.

### 6.2 §5.6 (MailerSend 429 handling)

`MailerSendEmailProvider.cs:70–75` logs and returns `EmailSendResult.Failed` on any non-success status code, including 429. There is no retry or backoff. The `EmailService.SendEmailAsync` wrapper logs the failure (`LogWarning`) and returns — the caller never knows a notification was dropped.

For §31's email notifications: at typical translation volume (2-6 emails/day), hitting MailerSend's rate limit is unlikely. The risk is real for burst scenarios (a tenant running 10 translation jobs simultaneously), but that's an edge case today.

**Recommendation:** Proceed with §31 without waiting for §5.6. Disclose the dropped-on-429 risk in the fix report. If §5.6 is resolved before §31 lands, the notification send automatically inherits the retry behaviour.

### 6.3 User email not on ICurrentUserService

`ICurrentUserService` exposes `UserId` (string) and `UserIdGuid` but not email. Any notification that needs to send to a specific user must query `UserManager<ApplicationUser>` or the `Users` DbSet. This is a minor friction but not a blocker — the existing `EmailService` already receives email addresses as string parameters, and looking up users by ID from UserManager is a one-liner.

### 6.4 `TriggeredByType.User` default on background jobs

`ContentGenerationJob.cs:~599` dispatches `GenerateContentTranslationsCommand` without setting `TriggeredBy`, so it defaults to `TriggeredByType.User`. This is misleading — the ContentGenerationJob is a Hangfire background job. If Strategy B (notify triggering user) is chosen, this default would incorrectly signal "a user triggered this" when it was a system process. Any §31 implementation should be aware that `TriggeredByType.User` on the command does not reliably mean a human user exists to notify.

---

## Step 7 — Sized Candidate Combinations

### Combination 1 — Email-only, per-talk summary, all Admins, no preferences

**Description:** After the translation handler foreach loop and after each validation job completes, query all Admin users for the tenant and send one summary email.

**Backend changes:**
- Add `SendTranslationBatchCompletedAsync(Guid tenantId, string talkTitle, List<LanguageTranslationResult> results)` to `IEmailService` and `EmailService`
- Add `SendValidationCompletedAsync(Guid tenantId, string talkTitle, string languageName, int score, ValidationOutcome outcome)` to `IEmailService` and `EmailService`
- In `GenerateContentTranslationsCommandHandler.cs`: after line 181 (post-foreach), dispatch `SendTranslationBatchCompletedAsync`
- In `TranslationValidationJob.cs`: after line 388 (post-`RecordValidationCompleted`), dispatch `SendValidationCompletedAsync`
- Both dispatch sites need to query Admin users for the tenant via `UserManager` — requires injecting it into the handler/job or delegating to a new `IAdminNotificationService`
- Look up `ToolboxTalk.Title` for the talk ID (already loaded in the handler; needs loading in the job)
- HTML email templates (inline strings, following existing pattern in `EmailService`)

**Frontend changes:** None.

**Schema changes:** None.

**Estimated effort:** 0.5–1 day backend only.

**Risk:** §5.6 (silent drop on MailerSend 429). `UserManager` injection into Hangfire jobs follows Note 22 patterns (explicit TenantId, scoped services).

---

### Combination 2 — Email-only, per-talk summary, triggering user, no preferences

**Description:** Same as Combination 1, but add `TriggeredByUserId? Guid?` to `GenerateContentTranslationsCommand`. Controller sets it from `ICurrentUserService.UserIdGuid`. Background jobs leave it null; fallback to all Admins when null.

**Backend changes:** Same as Combination 1, plus:
- Add `TriggeredByUserId?` to `GenerateContentTranslationsCommand`
- `ToolboxTalksController` sets it from `ICurrentUserService.UserIdGuid` when dispatching
- `ContentGenerationJob`, `MissingTranslationsJob`, `EmployeeLanguageChangeHandler` leave it null
- `GenerateContentTranslationsCommandHandler` passes it to the notification dispatch
- Notification dispatch: if `TriggeredByUserId` is non-null, query that user's email and send to them; if null, send to all Admins

**No changes needed for validation** — validation jobs always use System trigger; fallback to all Admins is the only path.

**Estimated effort:** 1 day. The extra work is the command change and conditional recipient logic.

---

### Combination 3 — In-app notification center + email, per-talk, all Admins, tenant opt-out

**Description:** Full notification surface: bell icon in TopNav, notification center dropdown, persisted `Notification` entity, API endpoint, email as a second channel. Tenant-level opt-out setting.

**Backend changes:**
- New `Notification` entity (TenantEntity): `TenantId`, `RecipientUserId`, `Type` (enum), `Title`, `Body`, `IsRead`, `RelatedEntityId`, `RelatedEntityType`, `CreatedAt`
- EF config, migration (CLI-generated with Designer.cs per Note 28)
- New `INotificationService` with `DispatchAsync(Guid tenantId, NotificationType type, string title, string body, Guid? relatedEntityId)`
- `GET /api/notifications` — current user's unread notifications (paginated)
- `PUT /api/notifications/{id}/mark-read` — mark read
- New `TenantSettingKeys.TranslationCompletionNotificationsEnabled`
- Hook into the two completion points as in Combination 1

**Frontend changes:**
- Bell icon in `TopNav` with unread count badge
- `NotificationCenter` dropdown component
- `useNotifications` hook (TanStack Query polling every 30s or SignalR user-channel push)
- Mark-read on open/click
- Settings → Notifications tab: toggle for opt-out (replaces placeholder)

**Estimated effort:** 3–5 days (dominated by frontend notification center).

---

## Recommended Direction

**Combination 1** (email-only, per-talk summary, all Admins, no preferences) with one addition: disclose failures in the summary email body.

**Rationale:**

1. **Duration mismatch favours email.** Translation runs take 5–30 minutes. The user has almost certainly navigated away or closed the browser. SignalR (already wired for the wizard's live view) only helps users who stay on the screen. Email is the correct primary channel for async durations at this scale.

2. **Infrastructure is ready.** `IEmailService.SendEmailAsync` exists and works. Adding 2 methods follows an established pattern (`EmailService.cs` hardcoded HTML templates). No framework needed.

3. **All-Admins eliminates the user-ID gap** entirely. Strategy B (triggering user) is strictly more precise but adds friction: a new command field, a conditional recipient query, and a null-fallback for system-triggered jobs. The practical gain is small — most tenants have 1-3 Admins and all of them want to know when a translation is done. Revisit if tenants with many Admins report noise.

4. **In-app notification center is a separate build.** The "coming soon" placeholder confirms product intent. But it's 3-5 days of frontend + backend entity work, and it blocks nothing in the email path. Build it when §1.3.5 gets proper scope and priority.

5. **No preference infrastructure needed initially.** Always-on email to all Admins is acceptable for launch. Add the tenant opt-out (`TenantSetting` key) when the Notifications settings tab gets properly built.

---

## Coverage Table

| Step | Investigated | Key finding |
|------|-------------|-------------|
| Email infrastructure | ✅ | `IEmailService` / `MailerSendEmailProvider` fully operational; no 429 retry (§5.6) |
| In-app notification surfaces | ✅ | None exist; two empty placeholder tabs in settings pages |
| SignalR hubs | ✅ | 5 hubs, all run-scoped; no user-scoped delivery channel |
| User preferences | ✅ | `TenantSetting` key-value store exists; no notification keys defined |
| Translation completion call site | ✅ | `GenerateContentTranslationsCommandHandler.cs:165–181`; per-language; no user ID on command |
| Validation completion call site | ✅ | `TranslationValidationJob.cs:382–388`; per-language; always TriggeredByType.System |
| Translation trigger sites | ✅ | 4 sites; user ID available in controller path only; not captured in command |
| Validation trigger sites | ✅ | 2 sites; no user ID propagated to job |
| Failure paths | ✅ | Logged only; no workflow failure recording; no notification |
| Recipient candidates | ✅ | 4 strategies; all-Admins recommended |
| Frequency / batching | ✅ | Per-talk email (natural); per-language validation email (acceptable) |
| §1.3.5 relationship | ✅ | §31 can proceed without waiting; email path doesn't duplicate a future framework |
| §5.6 dependency | ✅ | Known risk; acceptable for expected volume |
| Sizing | ✅ | 3 combinations sized: 0.5–1d / 1d / 3–5d |
