# Learning Active/Inactive Toggle — Recon

**Type:** Read-only investigation (no code changed)
**Date:** 2026-07-15
**Scope:** (1) new learnings should default to Active; (2) Learnings list should get Activate/Deactivate actions with a confirmation dialog

---

## Executive summary

- `ToolboxTalk.IsActive` is **not a learner-visibility gate**. The only thing that controls whether an employee sees a talk is the existence of a `ScheduledTalk` assignment row. This was independently re-confirmed in this recon (see Part 2) and matches an earlier investigation already in the repo at `docs/phase-5/reports/5.23-followup-recon.md`.
- **Root cause of "new learnings need an edit-after-create step to activate" is found and precise, and it depends on which wizard is used:**
  - **Legacy wizard** (`/admin/toolbox-talks/create`, `ContentCreationSessionService`) already defaults reliably to Active (`IsActive = sessionSettings?.IsActiveOnPublish ?? true`, frontend default `isActiveOnPublish: true`). No bug here.
  - **New "Learnings" wizard** (`/admin/toolbox-talks/learnings/**`, `InitialiseToolboxTalkCommandHandler`) creates the draft talk at Step 1 with `IsActive = tenantSettings?.DefaultIsActive ?? false`. For any tenant that has never saved `ToolboxTalkSettings` (no row exists — plausible for most tenants, since a row is only created on first Settings save), this evaluates to **false**. The wizard's own Step 4 "Active on publish" toggle then loads pre-populated from this already-false value and is easy to miss, so the talk is often published inactive and needs a separate edit afterward — exactly the symptom reported.
- **One real, concrete "IsActive gates a write action" case exists**: `CreateToolboxTalkScheduleCommandHandler` throws if the talk is inactive, blocking **new schedule creation**. But **recurring schedules created before deactivation are not stopped** — `ProcessToolboxTalkSchedulesJob` / `ProcessToolboxTalkScheduleCommandHandler` never check `ToolboxTalk.IsActive`, so a Weekly/Monthly/Annually schedule keeps minting new `ScheduledTalk` assignments after the talk is deactivated.
- Reminders (`SendRefresherRemindersJob`, `SendToolboxTalkRemindersJob`) and overdue-marking (`UpdateOverdueToolboxTalksJob`) **never check `IsActive`** — they operate purely on `ScheduledTalk`/`ToolboxTalkCourseAssignment` status and due dates. Deactivating a talk does nothing to reminders/overdue processing for existing assignments.
- No dedicated toggle endpoint exists. Toggling `IsActive` on a **published** talk today requires the full legacy edit form (`ToolboxTalkForm.tsx` → `PUT /api/toolbox-talks/{id}` → `UpdateToolboxTalkCommand`), which submits the entire talk payload (title, sections, questions, all settings). The new wizard's per-field settings endpoint (`PUT /api/toolbox-talks/{id}/settings` → `UpdateToolboxTalkSettingsCommand`) is **Draft-status-only** and its own edit-panel (`SettingsEditPanel.tsx`, used post-publish) doesn't even expose an `isActive` field.
- The frontend Learnings list (`ToolboxTalkList.tsx`) already has an Active/Inactive badge column, an All/Active/Inactive filter (defaulting to "all", i.e. unfiltered), and disables the "Schedule" action with a tooltip when a talk is inactive. It does **not** yet have Activate/Deactivate items in the Actions dropdown, nor any confirmation dialog for them.
- BACKLOG already has an open note (§5.30, "ToolboxTalk.IsActive is functionally decorative") flagging this general area for follow-up.

---

## Part 1 — IsActive field and default

### Entity

- **Entity:** `ToolboxTalk` (`TenantEntity`)
- **File:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalk.cs:72`
  ```csharp
  public bool IsActive { get; set; } = true;
  ```
- **Type:** plain `bool` (not nullable, not an enum, not computed).
- **C# entity default:** `true`. Unchanged since the file's first commit in this repo (`9e575bf feat: QuantumBuild LMS - standalone LMS extracted from Rascor`) — confirmed via `git log -p --follow` on the file; the only `IsActive` line ever touched is the original `= true` declaration.
- **EF/DB column default:** also `true` — `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Configurations/ToolboxTalkConfiguration.cs:65-67`:
  ```csharp
  builder.Property(t => t.IsActive)
      .IsRequired()
      .HasDefaultValue(true);
  ```
- **Do entity default and DB default agree?** Yes — both `true`. **But neither is actually exercised at creation time**, because both wizard code paths construct the `ToolboxTalk` object with an explicit `IsActive = ...` expression (see below), which overrides the DB column default entirely (the default only matters for a raw INSERT that omits the column, which never happens here since EF always supplies a value from the C# object).

### Tenant-level default-value setting (a second, related "default")

There is also a **tenant-configurable** default that the new wizard consults:

- **Entity:** `ToolboxTalkSettings.DefaultIsActive` — `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkSettings.cs:106-111`:
  ```csharp
  /// Whether new talks should be active (IsActive = true) by default at creation.
  /// IsActive is not a learner-visibility gate — assignment records control visibility.
  /// Consumed by wizard Step 4 via InitialiseToolboxTalkCommandHandler at talk creation.
  public bool DefaultIsActive { get; set; } = true;
  ```
- **EF/DB default:** `true` — `ToolboxTalkSettingsConfiguration.cs:83-85`.
- **In-memory fallback when no `ToolboxTalkSettings` row exists for the tenant** (deliberately different, both by design per the historical fix doc):
  - `GetToolboxTalkSettingsQueryHandler.cs:74`: `DefaultIsActive = false` (this is only the read-model returned to the Settings page GET, not itself a write).
  - `InitialiseToolboxTalkCommandHandler.cs:122`: `IsActive = tenantSettings?.DefaultIsActive ?? false` — **this is the one that actually matters**: it is evaluated every time a new-wizard draft talk is created, and it is `false` whenever the tenant has no `ToolboxTalkSettings` row.

This `?? false` fallback was an **intentional decision**, not an oversight — see `docs/phase-5/reports/5.23-step4-tenant-defaults-fix.md:44`: *"The `GetToolboxTalkSettingsQueryHandler` default-fallback branch uses `DefaultIsActive = false` (the pre-fix behaviour) so existing tenants with no row maintain the current wizard experience."* Before that fix (2026-06-16), `InitialiseToolboxTalkCommandHandler` **hardcoded** `IsActive = false` unconditionally; the fix replaced the hardcode with a tenant-setting read, but preserved `false` as the fallback for "no row" tenants to avoid changing existing tenants' behaviour. The consequence: any tenant that has never saved the Settings → General tab (i.e., never triggered creation of a `ToolboxTalkSettings` row) still gets `IsActive = false` on every new-wizard-created talk, silently, by design.

### How a `ToolboxTalkSettings` row gets created

A row is only created lazily, on first save, by two handlers:
- `UpdateToolboxTalkTenantDefaultsCommandHandler.cs:43-50` (General tab "Save defaults" button)
- `UpdateToolboxTalkNotificationSettingsCommandHandler.cs:43-50` (Notifications tab)

There is **no tenant-provisioning step that creates a `ToolboxTalkSettings` row automatically** (no such call found in tenant-creation code or `DataSeeder`). A brand-new tenant, or any tenant whose admin has never opened `/admin/toolbox-talks/settings` → General tab and clicked Save, has no row — so `InitialiseToolboxTalkCommandHandler` falls back to `false`.

### Creation paths traced

**A. New "Learnings" wizard** (`/admin/toolbox-talks/learnings/new` → `/admin/toolbox-talks/learnings/[talkId]/...`, per CLAUDE.md Note 29):

- Draft `ToolboxTalk` row is created immediately at **Step 1** by `InitialiseToolboxTalkCommandHandler` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/InitialiseToolboxTalk/InitialiseToolboxTalkCommandHandler.cs:75-131`), with:
  ```csharp
  Status = ToolboxTalkStatus.Draft,
  IsActive = tenantSettings?.DefaultIsActive ?? false,   // line 122
  ```
- The wizard's Step 4 ("Settings", `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx`) exposes an "Active on publish" `Switch`:
  - Form schema: `web/src/features/toolbox-talks/components/learning-wizard/schemas/settingsSchema.ts:18` — `isActiveOnPublish: z.boolean()`.
  - **Blank-form default** (before data loads): `SettingsStep.tsx:84` — `isActiveOnPublish: true`.
  - **Populated-from-draft value** (the actual path taken every time, since the draft talk already exists by Step 1): `SettingsStep.tsx:105` — `isActiveOnPublish: talk.isActive`. Since `talk.isActive` is already `false` from Step 1 creation (no-settings-row tenants), the toggle **loads visibly OFF** and the admin must notice and flip it.
  - Toggle auto-saves per-field via `saveField(...)` (`SettingsStep.tsx:416-419`) which PUTs `UpdateTalkSettingsRequest.isActive` to `PUT /api/toolbox-talks/{id}/settings`.
- **This is the confirmed root cause of the "requires an edit-after-create step" symptom**, for any tenant without a saved `ToolboxTalkSettings` row.

**B. Legacy wizard** (`/admin/toolbox-talks/create`, `web/src/app/(authenticated)/admin/toolbox-talks/create/page.tsx`, backed by `ContentCreationSessionService`):

- The `ToolboxTalk` row is **not** created until the final publish step. `ContentCreationSessionService.cs:1710-1729` (one of several near-identical publish code paths — same pattern also at lines 1448, 1852, 1879, 1950):
  ```csharp
  Status = ToolboxTalkStatus.Published,
  IsActive = sessionSettings?.IsActiveOnPublish ?? true,   // line 1724
  ```
  Fallback is **`?? true`**, not `?? false` — the opposite of the new wizard.
- Frontend default: `web/src/features/toolbox-talks/components/create-wizard/steps/SettingsStep.tsx:38` — `isActiveOnPublish: true` (blank-form default; this wizard's Settings step is reached before the talk row exists, so there's no "already-false" value to load).
- **No bug here** — legacy-wizard-created talks are reliably Active unless the admin explicitly turns the switch off.

**C. Direct `POST /api/toolbox-talks`** (`CreateToolboxTalkCommand` / `CreateToolboxTalkCommandHandler`) — a third, simpler creation path (`ToolboxTalks.Create` permission per CLAUDE.md endpoint table):
- `CreateToolboxTalkCommand.cs:24` — `public bool IsActive { get; init; } = true;` (C# record default `true`).
- Handler just assigns `IsActive = request.IsActive` (`CreateToolboxTalkCommandHandler.cs:72`) — no tenant-settings lookup, so whatever the caller supplies (or the record default of `true` if omitted) is used directly. Not identified as reachable from either wizard's primary flow in this recon; noted for completeness since it exists and is the `ToolboxTalks.Create`-gated endpoint in the API surface.

### Summary table — Part 1

| Path | Fallback when no explicit value supplied | Reliable Active-by-default? |
|---|---|---|
| Entity C# default | `true` | N/A (overridden at insert in both wizards) |
| DB column default | `true` | N/A (overridden at insert in both wizards) |
| `ToolboxTalkSettings.DefaultIsActive` entity/DB default | `true` | N/A (only matters if a row exists) |
| New wizard (`InitialiseToolboxTalkCommandHandler.cs:122`) | `tenantSettings?.DefaultIsActive ?? false` | **No**, for tenants without a settings row |
| Legacy wizard (`ContentCreationSessionService.cs:1724` etc.) | `sessionSettings?.IsActiveOnPublish ?? true` | Yes |
| Direct API (`CreateToolboxTalkCommandHandler.cs:72`) | Caller-supplied, record default `true` | Yes, if caller omits the field |

---

## Part 2 — Downstream consumers of IsActive

**Note on the other `IsActive`:** `Employee.IsActive` (Core module — active/inactive employment status) and `User.IsActive` (account-enabled flag) are separate fields on separate entities and were **not** conflated below. Several files matched the bare grep for `IsActive` purely because of these unrelated fields (e.g. `ToolboxTalkReportsService.cs:47,115,443` all filter `Employee.IsActive`, not `ToolboxTalk.IsActive`; `CreateToolboxTalkScheduleCommandHandler.cs` and `ProcessToolboxTalkScheduleCommandHandler.cs` also separately filter `Employee.IsActive` when selecting assignees — distinct from the `ToolboxTalk.IsActive` check discussed below). Also unrelated: `Sector.IsActive`, `SafetyGlossary.IsActive`, `RegulatoryDocument/Profile/Requirement/Criteria.IsActive`, `ToolboxTalkCourse.IsActive` (a **different, course-level** flag — see its own gate below), `QrCode`/`QrLocation.IsActive`, `PipelineVersion.IsActive`, `AuditCorpusEntry.IsActive`.

### Query-time consumers (no effect on write actions)

| Consumer | File:line | Behaviour |
|---|---|---|
| Admin talk list filter | `Queries/GetToolboxTalks/GetToolboxTalksQueryHandler.cs:45-47` | Optional filter (`?isActive=`), not applied unless the admin picks it. Admin-facing only. |
| Dashboard KPI | `Queries/GetToolboxTalkDashboard/GetToolboxTalkDashboardQueryHandler.cs:25,29` | `activeTalks = talks.Count(t => t.IsActive)` — a cosmetic count only. |
| Admin course list filter | `Features/Courses/Queries/GetToolboxTalkCoursesQueryHandler.cs:30-32` | Same optional-filter pattern, but for `ToolboxTalkCourse.IsActive` (course-level, separate flag). |
| Employee "My Toolbox Talks" list | `Queries/GetMyToolboxTalks/GetMyToolboxTalksQueryHandler.cs:23-29` (re-confirmed against current code, matches `docs/phase-5/reports/5.23-followup-recon.md` Gate 1) | Filters only on tenant/employee/`!IsDeleted`/`ScheduledTalkStatus != Cancelled`. **No `ToolboxTalk.IsActive` or `Status` filter.** An operator can see and complete an already-assigned talk even if it is later deactivated. |
| Employee individual talk detail | `Queries/GetMyToolboxTalkById/GetMyToolboxTalkByIdQueryHandler.cs:29-44` | Same — no `IsActive`/`Status` gate. |
| Compliance / overdue / completions / skills-matrix reports | `ToolboxTalkReportsService.cs` (whole file) | No `ToolboxTalk.IsActive` filter anywhere in this service — only `Employee.IsActive` is used (a different field, for scoping to active employees). Deactivating a talk does not remove it from any report; completed records for an inactive talk remain fully visible. |
| Skills matrix grid data | derived from `ScheduledTalks`, per CLAUDE.md's own Skills Matrix section | Same conclusion — no talk-level `IsActive` gate found. |

### Action-time consumers (gate a write operation)

| Consumer | File:line | Behaviour |
|---|---|---|
| **New schedule creation** | `Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs:34-45` | ```if (!toolboxTalk.IsActive) throw new InvalidOperationException($"Learning '{toolboxTalk.Title}' is not active and cannot be scheduled.");``` — **blocks** creating a brand-new `ToolboxTalkSchedule` for an inactive talk. This is the one real "cannot assign to new schedules" effect. |
| **Course assignment** | `Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs:38-47` | Checks `course.IsActive` (the **course's own** `IsActive`, `ToolboxTalkCourse` entity) — `if (!course.IsActive) throw ...`. Does **not** check the `IsActive` flag of the individual talks inside the course. A course could contain a deactivated standalone talk and still be assignable, because only the course-level flag is checked. |
| **Recurring schedule processing (existing schedules)** | `Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs:32-199` and `Jobs/ProcessToolboxTalkSchedulesJob.cs` (whole files, re-confirmed against current code) | **No check on `ToolboxTalk.IsActive` anywhere.** A Weekly/Monthly/Annually schedule created while the talk was active keeps calling this handler daily and **keeps creating new `ScheduledTalk` rows** after the talk is deactivated. This is the most important "conditional effect" finding for the confirmation dialog — see below. |

### Reminders / overdue processing for already-assigned talks

All three re-read directly from source in this recon (not just re-cited from the earlier doc):

| Job | File | IsActive check? |
|---|---|---|
| `SendRefresherRemindersJob` | `Jobs/SendRefresherRemindersJob.cs:54-175` | **No.** Filters only on `ScheduledTalk`/`ToolboxTalkCourseAssignment` status, `IsRefresher`, due-date windows, reminder-sent flags. Refresher reminders keep firing for an inactive talk's existing assignments. |
| `SendToolboxTalkRemindersJob` | `Jobs/SendToolboxTalkRemindersJob.cs:42-174` | **No.** Filters on `ScheduledTalkStatus`, `DueDate`, `RemindersSent`. Overdue reminders keep firing. |
| `UpdateOverdueToolboxTalksJob` | `Jobs/UpdateOverdueToolboxTalksJob.cs:31-70` | **No.** Bulk `ExecuteUpdateAsync` on `Status == Pending || InProgress` and `DueDate < today`, no talk-level filter at all. Existing assignments still get marked Overdue normally. |

### Certificates

- `CertificateGenerationService.cs` has **no** `IsActive` reference (confirmed by grep — the file does not appear among the ~90 `IsActive` hits across the whole `src` tree). Certificate issuance and already-issued certificates are unaffected by deactivation, consistent with CLAUDE.md Note 19's snapshot-at-issuance pattern for certificates generally. Certificate download queries (`GetCertificateDownloadQueryHandler`, `GetAdminCertificateDownloadQueryHandler`, `GetMyCertificatesQueryHandler`, `GetEmployeeCertificatesQueryHandler`) likewise have no `IsActive` dependency.

### Editing an inactive/published talk

- `UpdateToolboxTalkCommandHandler.cs` (the general `PUT /api/toolbox-talks/{id}` handler, full file read) has **no** `IsActive` or `Status` gate anywhere — title/code uniqueness checks, then unconditional field updates. An inactive talk (published or not) can be freely opened and edited via the legacy edit form.
- `UpdateToolboxTalkSettingsCommandHandler.cs:41-44` (the new-wizard per-talk settings endpoint) **does** gate on status, but the opposite way round: `if (talk.Status != ToolboxTalkStatus.Draft) return Result.Fail<ToolboxTalkDto>("Learning must be in Draft status to update settings.", FailureCode.WorkflowInvalidState);`. This means once a new-wizard talk is Published, this endpoint can no longer be used at all — for anything, including flipping `IsActive` — regardless of active/inactive state. (This is a `Status` gate, not an `IsActive` gate, but it directly explains why `SettingsEditPanel.tsx` — the post-publish settings panel — doesn't even attempt to expose an `isActive` field: it would fail server-side if it tried to save through this endpoint.)

### External review workflow

- `SendForReviewCommandHandler.cs` (full file read) and `ExternalReviewController.cs` / the `ITranslationWorkflowService` abstraction have **no** `IsActive` reference anywhere in the module. External review eligibility is governed entirely by translation workflow state (`WorkflowStateEligible`, reviewer email resolution), not by `ToolboxTalk.IsActive`. Deactivating a talk mid-review has no effect on the review workflow.

---

## Part 3 — Existing toggle mechanics

### Is there a dedicated toggle endpoint? No.

`IsActive` can currently only be changed as one field within two different broad commands, depending on wizard/status:

**1. `PUT /api/toolbox-talks/{id}` → `UpdateToolboxTalkCommand`** (route: `ToolboxTalksController.cs:711-716`; handler: `UpdateToolboxTalkCommandHandler.cs`)
- **Permission:** `ToolboxTalks.Edit` (per CLAUDE.md endpoint table).
- **Works regardless of `Status`** (Draft or Published) — this is the only route that can toggle `IsActive` on a Published talk.
- **Full payload required:** title, description, category, frequency, video/PDF URLs, `MinimumVideoWatchPercent`, quiz settings (`RequiresQuiz`, `PassingScore`, shuffle flags, question pool, `AllowRetry`), auto-assign settings, source language, slideshow flag, generate-certificate flag, refresher fields, **and the full `Sections` and `Questions` collections** (the handler diffs these to detect "stalening" changes that mark translations as needing revalidation).
- **Side effects beyond flipping the flag:**
  - Title/code uniqueness re-validation on every save.
  - Section/question diffing (`UpdateSectionsAsync`, `UpdateQuestionsAsync`) — even if content is unchanged, this walks the full collections.
  - If title, description, sections, or questions changed, marks all existing `ToolboxTalkTranslation` rows `NeedsRevalidation = true` and emits `MarkStale` workflow events per affected language (`UpdateToolboxTalkCommandHandler.cs:180-213`).
  - No audit logging (`ISystemAuditLogger`) call found in this handler. No SignalR event. No dedicated cache-invalidation beyond the standard TanStack Query pattern used by the calling hook.
- **Frontend caller:** `ToolboxTalkForm.tsx` (legacy edit form, full talk editor) — has the only currently-live `isActive` `Switch` field (`ToolboxTalkForm.tsx:458-477`, label "Active", description "Only active talks can be scheduled"). Reached via the Learnings list's "Edit" action → `/admin/toolbox-talks/talks/{id}/edit`.
- Note: `SettingsEditPanel.tsx` (the post-publish "Settings" card on the talk detail page) also calls this same `useUpdateToolboxTalk()` mutation, but its own form schema (`settingsEditSchema`, lines 39-50) has **no `isActive` field** — it just passes through the existing `talk.isActive` unchanged (`SettingsEditPanel.tsx:166`). So this panel cannot be used to toggle Active/Inactive today.

**2. `PUT /api/toolbox-talks/{id}/settings` → `UpdateToolboxTalkSettingsCommand`** (route: `ToolboxTalksController.cs:615-623`; handler: `UpdateToolboxTalkSettingsCommandHandler.cs`)
- **Draft-status-only**: fails with `FailureCode.WorkflowInvalidState` if `talk.Status != ToolboxTalkStatus.Draft` (line 41-44). Cannot be used once a talk is Published.
- Narrower payload than #1 (no sections/questions), but still touches title, description, category, certificate flag, video-watch-percent, auto-assign fields, and refresher-frequency mapping — not a single-field toggle.
- Also marks translations stale on title/description change, same as #1.
- **Frontend caller:** `learning-wizard/steps/SettingsStep.tsx` (new wizard Step 4) — the only place with an `isActiveOnPublish` toggle wired to this endpoint, and only reachable while the talk is still a Draft (i.e., pre-publish).

### Conclusion for Part 3

There is genuinely **no lightweight, status-independent way to flip `IsActive` alone**. Any Activate/Deactivate action added to the Learnings list would either need to:
- (a) reuse `UpdateToolboxTalkCommand` by round-tripping the full current talk (sections/questions included) with only `isActive` changed — works today, already reachable, but heavier than necessary and triggers the stalening-check machinery for a no-op content change (harmless since content is unchanged, but wasteful), or
- (b) add a new dedicated, narrow toggle endpoint/command that only ever sets `IsActive` and skips all the uniqueness/stalening logic — cleaner, and the natural target for `ISystemAuditLogger` logging (currently absent from both existing IsActive-writing paths) if that's desired.

### Frontend Learnings list — current Actions dropdown and badge

**File:** `web/src/features/toolbox-talks/components/ToolboxTalkList.tsx`

- **Badge column** (already implemented, line 176-191): green "Active" / gray "Inactive" `Badge`, driven by `item.isActive`.
- **Status filter** (already implemented, line 48-52, 370-385): `Select` with options All / Active / Inactive, using URL param `active`. **Defaults to "All"** (`activeFilter` is `undefined` unless the URL param is set), so both Active and Inactive learnings are shown unfiltered by default — this matches the user's report of seeing both states in a screenshot.
- **Actions dropdown** (line 264-324), current items:
  1. View
  2. Edit (if `canManage`)
  3. Schedule (if `canSchedule`) — **already disabled** when `!item.isActive`, with tooltip "Only active talks can be scheduled" and an inline "(inactive)" label (line 285-297).
  4. Send for Review (conditional on validation-fail stats, if `canManage`)
  5. Delete (if `canManage`) — has its own `DeleteConfirmationDialog`.
- **No Activate/Deactivate items exist yet.** No confirmation dialog exists for this action.
- **Cache invalidation infrastructure already exists and can be reused:** `useUpdateToolboxTalk()` (`web/src/lib/api/toolbox-talks/use-toolbox-talks.ts:97-108`) already invalidates both the list query key (`TOOLBOX_TALKS_KEY`) and the individual-talk key on success — a new Activate/Deactivate mutation (whether it reuses this hook or a new dedicated one) should follow the same `onSuccess` invalidation pattern so the badge updates immediately without a manual refetch.

---

## Part 4 — Confirmation dialog text draft

Based strictly on the Part 2 findings above (not assumptions):

### Deactivate confirmation

> **Deactivate "{talk title}"?**
>
> This learning will no longer be assignable to **new** schedules — admins won't be able to create a new schedule against it while it's inactive.
>
> **This will NOT affect anything already in progress:**
> - Employees with an existing assignment can still see, start, and complete this learning normally.
> - Any **recurring schedule** already set up for this learning will keep creating new assignments on its normal cadence — deactivating does not pause or cancel schedules. Cancel the schedule separately if you want that to stop.
> - Reminder emails and overdue-marking continue as normal for existing assignments.
> - Certificates already issued, and completion/compliance records, are unaffected and remain fully visible in reports.
> - The learning can still be viewed and edited by admins.
>
> You can reactivate this learning at any time.
>
> [Cancel] [Deactivate]

### Activate confirmation (lighter-weight — optional, since activating has essentially no downside; a toast-only success message may be sufficient, but if a confirmation is wanted for symmetry)

> **Activate "{talk title}"?**
>
> This learning becomes assignable to new schedules again. Existing assignments and reminders are unaffected either way.
>
> [Cancel] [Activate]

### Notes on accuracy of the copy above

- The claim "no longer assignable to new schedules" is the one confirmed hard block (`CreateToolboxTalkScheduleCommandHandler.cs:42-45`).
- The claim about course assignment is deliberately **omitted** from the dialog copy above because it's a course-level flag (`ToolboxTalkCourse.IsActive`), not this talk's flag — a deactivated standalone talk **can** still be pulled into an active course assignment if it's a course item, since `AssignCourseCommandHandler` never checks individual item talks' `IsActive`. If this asymmetry is considered worth surfacing to admins, it would need its own callout, e.g.: *"Note: if this learning is used inside a course, deactivating it here does not remove it from that course or block new course assignments."* Recommend including this line if the dialog is meant to be fully honest, since it's a genuine gap an admin could be surprised by.
- The "recurring schedule keeps firing" line is the single most important behavioural surprise found in this recon and should not be softened or omitted — it's the one place where admin intuition ("deactivate = stop everything") is most likely to be wrong.

---

## Part 5 — Chunk breakdown and size estimate

This looks like it fits comfortably in **one chunk**, small-to-medium size for this codebase's conventions (roughly half a day to a day of focused work, in line with similarly-scoped chunks already recorded in `docs/phase-5/reports/`).

### Backend

1. **Default-on-creation fix** — change the fallback in `InitialiseToolboxTalkCommandHandler.cs:122` from `tenantSettings?.DefaultIsActive ?? false` to `?? true` (aligning the new wizard's no-settings-row fallback with the legacy wizard's `?? true` and with the entity/DB defaults). Trivial one-line change. Consider whether `GetToolboxTalkSettingsQueryHandler.cs:74`'s `DefaultIsActive = false` fallback (used only to populate the Settings page's General tab display) should also flip to `true` for consistency, so the tab doesn't show a misleading "off" position for a tenant that hasn't saved yet — small follow-on decision, not strictly required for the creation-time fix to work.
2. **Optional: dedicated toggle endpoint** — a narrow `PATCH`/`PUT /api/toolbox-talks/{id}/active` (or similar) + a small `SetToolboxTalkActiveCommand`/handler that only sets `IsActive`, skips the section/question diffing and translation-stalening logic entirely (there is no content change to stale), and optionally adds `ISystemAuditLogger` logging (currently absent from both existing IsActive-writing paths, per Note 3's pattern used elsewhere). This is optional — reusing `UpdateToolboxTalkCommand` (option (a) in Part 3) also works today with a full-payload round-trip. Recommend doing the dedicated endpoint since the existing broad command already carries known complexity (stalening side effects, heavy payload) that's unnecessary and slightly risky to invoke from a simple list-row action.
3. Permission: reuse `ToolboxTalks.Edit` (matches the existing edit-form gate) unless a narrower policy is wanted.

### Frontend

1. Add "Activate"/"Deactivate" `DropdownMenuItem`s to `ToolboxTalkList.tsx`'s Actions column (conditional on `canManage`, toggling label/icon based on `item.isActive`).
2. Add a confirmation dialog (reuse the existing `AlertDialog`/`DeleteConfirmationDialog`-style pattern already used elsewhere in this list, e.g. the Delete flow) shown only on Deactivate, using copy along the lines drafted in Part 4. Activate can likely skip confirmation (or use a lighter one) since it has no meaningful downside per the findings.
3. Wire a new mutation hook (either a thin wrapper around the new dedicated endpoint, or reusing `useUpdateToolboxTalk()` if going with option (a)) with the same `onSuccess` invalidation pattern already present in `use-toolbox-talks.ts:103-106` so the Active/Inactive badge updates immediately.
4. No changes needed to the existing badge or filter — both already work correctly.

### Out of scope / explicitly not touched by this recon

- The course-level `ToolboxTalkCourse.IsActive` flag and its own toggle UI (separate entity, separate gate in `AssignCourseCommandHandler`) — not part of the stated Learnings-list scope, but flagged in Part 4 as worth a documentation callout in the dialog copy if the team wants full honesty about the course-membership edge case.
- Whether `CreateToolboxTalkScheduleCommandHandler`'s course-assignment gap (deactivated talk still assignable via an active course) should itself be closed — noted as a finding, not a proposed fix.
- Whether `ProcessToolboxTalkScheduleCommandHandler` should start checking `ToolboxTalk.IsActive` and stop recurring schedules on deactivation — this would be a behavioural change beyond "add a toggle UI + fix the default," flagged here only so the team can consciously decide whether the confirmation dialog's current honest disclosure ("recurring schedules keep firing") is acceptable, or whether they'd rather change the behaviour instead.
